using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.ScreenShareLoopback;

internal static class Program
{
    private enum IceApplyMode
    {
        Auto,
        Direct,
        HostOnly,
    }

    private readonly record struct CandidateForwardItem(
        string SourcePc,
        string TargetPc,
        string Mid,
        int Mline,
        string Candidate,
        bool EndOfCandidates);

    private readonly record struct ParsedCandidate(string Protocol, string Address, string Type);

    private sealed class CandidateDispatchMetrics
    {
        public long Enqueued;
        public long Dequeued;
        public long Applied;
        public long Failed;
        public long Dropped;
        public long DirectFallbackApplied;
        public long WorkerFaults;
    }

    public static async Task Main(string[] args)
    {
        var sourceIndex = Math.Max(0, GetIntArg(args, "--source", 0));
        var fps = Math.Max(1, GetIntArg(args, "--fps", 30));
        var showCursor = GetBoolArg(args, "--cursor", true);
        var traceSignaling = GetBoolArg(args, "--trace-signaling", false);
        var statsIntervalMs = Math.Max(0, GetIntArg(args, "--stats-interval-ms", 2000));
        var disableIpv6 = GetBoolArg(args, "--disable-ipv6", true);
        var stun = GetArg(args, "--stun", string.Empty);
        var forceLocalLoopback = GetBoolArg(args, "--force-local-loopback", false);
        var dumpSdp = GetBoolArg(args, "--dump-sdp", false);
        var iceModeRaw = GetArg(args, "--ice-mode", "auto");
        var hasExplicitIceMode = HasArg(args, "--ice-mode");
        var iceMode = ParseIceApplyMode(iceModeRaw);
        if (forceLocalLoopback && !hasExplicitIceMode)
        {
            iceMode = IceApplyMode.HostOnly;
        }
        if (forceLocalLoopback && hasExplicitIceMode && iceMode != IceApplyMode.HostOnly)
        {
            Console.WriteLine("Warning: --force-local-loopback=true ignored because --ice-mode is explicitly set.");
        }

        void Trace(string text)
        {
            if (!traceSignaling)
            {
                return;
            }

            Console.WriteLine($"[trace:loopback] {text}");
        }

        Console.WriteLine($"ICE apply mode: {ToIceModeArg(iceMode)}");
        LumenRtc.Initialize();
        try
        {
            using var senderFactory = PeerConnectionFactory.Create();
            senderFactory.Initialize();

            using var receiverFactory = PeerConnectionFactory.Create();
            receiverFactory.Initialize();

            var firstFrameLogged = 0;
            using var renderer = new SdlVideoRenderer("LumenRTC Screen Share (Loopback)", 1280, 720);
            using var firstFrameProbeSink = new VideoSink(new VideoSinkCallbacks
            {
                OnFrame = frame =>
                {
                    if (Interlocked.Exchange(ref firstFrameLogged, 1) == 0)
                    {
                        Console.WriteLine($"First frame received: {frame.Width}x{frame.Height}");
                    }
                }
            });

            VideoTrack? remoteVideoTrack = null;
            var iceSync = new object();
            var pendingForPc1 = new List<CandidateForwardItem>();
            var pendingForPc2 = new List<CandidateForwardItem>();
            var pc1CanApplyRemoteCandidates = false;
            var pc2CanApplyRemoteCandidates = false;
            var pc1LastMid = "0";
            var pc2LastMid = "0";
            var pc1LastMline = 0;
            var pc2LastMline = 0;
            var startedAt = DateTime.UtcNow;

            PeerConnection? pc1 = null;
            PeerConnection? pc2 = null;
            RtpSender? sender = null;
            var dispatchMetrics = new CandidateDispatchMetrics();
            var candidatePumpFaulted = 0;
            var candidatePumpFallbackLogged = 0;
            using var candidatePumpCts = new CancellationTokenSource();
            var candidateChannel = Channel.CreateUnbounded<CandidateForwardItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });

            bool DispatchCandidate(CandidateForwardItem item)
            {
                var useDirectApply = iceMode == IceApplyMode.Direct || Volatile.Read(ref candidatePumpFaulted) != 0;
                if (useDirectApply)
                {
                    if (iceMode != IceApplyMode.Direct && Interlocked.CompareExchange(ref candidatePumpFallbackLogged, 1, 0) == 0)
                    {
                        Console.WriteLine("Candidate worker faulted. Falling back to direct candidate apply.");
                    }

                    Interlocked.Increment(ref dispatchMetrics.DirectFallbackApplied);
                    return TryApplyCandidate(
                        item,
                        () => pc1,
                        () => pc2,
                        iceMode,
                        dispatchMetrics,
                        Trace,
                        countDequeued: false);
                }

                if (candidateChannel.Writer.TryWrite(item))
                {
                    Interlocked.Increment(ref dispatchMetrics.Enqueued);
                    return true;
                }

                Console.WriteLine($"Failed to enqueue candidate {item.SourcePc}->{item.TargetPc} mid={item.Mid} mline={item.Mline}.");
                Interlocked.Increment(ref dispatchMetrics.Failed);
                return false;
            }

            void AttachRemoteTrack(VideoTrack track)
            {
                if (remoteVideoTrack != null)
                {
                    try
                    {
                        remoteVideoTrack.RemoveSink(renderer.Sink);
                    }
                    catch
                    {
                        // Ignore cleanup failures from stale track wrappers.
                    }

                    try
                    {
                        remoteVideoTrack.RemoveSink(firstFrameProbeSink);
                    }
                    catch
                    {
                        // Ignore cleanup failures from stale track wrappers.
                    }

                    remoteVideoTrack.Dispose();
                }

                remoteVideoTrack = track;
                remoteVideoTrack.AddSink(renderer.Sink);
                remoteVideoTrack.AddSink(firstFrameProbeSink);

                string trackId;
                try
                {
                    trackId = remoteVideoTrack.Id;
                }
                catch
                {
                    trackId = "<unknown>";
                }

                Console.WriteLine($"Attached remote video track: {trackId}");
            }

            void QueueOrForwardToPc1(string sourcePc, string mid, int mline, string candidate, bool endOfCandidates = false)
            {
                var normalizedMid = string.IsNullOrWhiteSpace(mid) ? "0" : mid;
                var item = new CandidateForwardItem(sourcePc, "pc1", normalizedMid, mline, candidate ?? string.Empty, endOfCandidates);
                var forwardNow = false;
                lock (iceSync)
                {
                    if (pc1CanApplyRemoteCandidates)
                    {
                        forwardNow = true;
                    }
                    else
                    {
                        pendingForPc1.Add(item);
                        Trace($"pc1 queue {(endOfCandidates ? "eoc" : "candidate")} from {sourcePc} mid={normalizedMid} mline={mline}");
                    }
                }

                if (forwardNow)
                {
                    DispatchCandidate(item);
                }
            }

            void QueueOrForwardToPc2(string sourcePc, string mid, int mline, string candidate, bool endOfCandidates = false)
            {
                var normalizedMid = string.IsNullOrWhiteSpace(mid) ? "0" : mid;
                var item = new CandidateForwardItem(sourcePc, "pc2", normalizedMid, mline, candidate ?? string.Empty, endOfCandidates);
                var forwardNow = false;
                lock (iceSync)
                {
                    if (pc2CanApplyRemoteCandidates)
                    {
                        forwardNow = true;
                    }
                    else
                    {
                        pendingForPc2.Add(item);
                        Trace($"pc2 queue {(endOfCandidates ? "eoc" : "candidate")} from {sourcePc} mid={normalizedMid} mline={mline}");
                    }
                }

                if (forwardNow)
                {
                    DispatchCandidate(item);
                }
            }

            void MarkPc1ReadyForRemoteCandidatesAndFlush()
            {
                List<CandidateForwardItem> pending;
                lock (iceSync)
                {
                    pc1CanApplyRemoteCandidates = true;
                    pending = new List<CandidateForwardItem>(pendingForPc1);
                    pendingForPc1.Clear();
                }

                if (pending.Count > 0)
                {
                    Trace($"pc1 flush queued candidates: {pending.Count}");
                }

                foreach (var candidate in pending)
                {
                    DispatchCandidate(candidate);
                }
            }

            void MarkPc2ReadyForRemoteCandidatesAndFlush()
            {
                List<CandidateForwardItem> pending;
                lock (iceSync)
                {
                    pc2CanApplyRemoteCandidates = true;
                    pending = new List<CandidateForwardItem>(pendingForPc2);
                    pendingForPc2.Clear();
                }

                if (pending.Count > 0)
                {
                    Trace($"pc2 flush queued candidates: {pending.Count}");
                }

                foreach (var candidate in pending)
                {
                    DispatchCandidate(candidate);
                }
            }

            var config = new RtcConfiguration
            {
                DisableIpv6 = disableIpv6,
                DisableIpv6OnWifi = disableIpv6,
            };
            if (!string.IsNullOrWhiteSpace(stun))
            {
                config.IceServers.Add(new IceServer(stun));
            }

            pc1 = senderFactory.CreatePeerConnection(new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    var candidateType = ExtractCandidateType(cand);
                    var candidateAddress = ExtractCandidateAddress(cand);
                    Trace($"pc1 local candidate type={candidateType} addr={candidateAddress} mid={mid} mline={mline}");
                    lock (iceSync)
                    {
                        pc1LastMid = string.IsNullOrWhiteSpace(mid) ? "0" : mid;
                        pc1LastMline = mline;
                    }
                    QueueOrForwardToPc2("pc1", mid, mline, cand);
                },
                OnPeerConnectionState = state => Console.WriteLine($"pc1 state: {state}"),
                OnIceConnectionState = state => Console.WriteLine($"pc1 ice: {state}"),
                OnIceGatheringState = state =>
                {
                    Console.WriteLine($"pc1 gathering: {state}");
                    if (state == IceGatheringState.Complete)
                    {
                        string mid;
                        int mline;
                        lock (iceSync)
                        {
                            mid = pc1LastMid;
                            mline = pc1LastMline;
                        }
                        Trace($"pc1 local eoc mid={mid} mline={mline}");
                        QueueOrForwardToPc2("pc1", mid, mline, string.Empty, endOfCandidates: true);
                    }
                },
            }, config);

            pc2 = receiverFactory.CreatePeerConnection(new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    var candidateType = ExtractCandidateType(cand);
                    var candidateAddress = ExtractCandidateAddress(cand);
                    Trace($"pc2 local candidate type={candidateType} addr={candidateAddress} mid={mid} mline={mline}");
                    lock (iceSync)
                    {
                        pc2LastMid = string.IsNullOrWhiteSpace(mid) ? "0" : mid;
                        pc2LastMline = mline;
                    }
                    QueueOrForwardToPc1("pc2", mid, mline, cand);
                },
                OnVideoTrack = AttachRemoteTrack,
                OnPeerConnectionState = state => Console.WriteLine($"pc2 state: {state}"),
                OnIceConnectionState = state => Console.WriteLine($"pc2 ice: {state}"),
                OnIceGatheringState = state =>
                {
                    Console.WriteLine($"pc2 gathering: {state}");
                    if (state == IceGatheringState.Complete)
                    {
                        string mid;
                        int mline;
                        lock (iceSync)
                        {
                            mid = pc2LastMid;
                            mline = pc2LastMline;
                        }
                        Trace($"pc2 local eoc mid={mid} mline={mline}");
                        QueueOrForwardToPc1("pc2", mid, mline, string.Empty, endOfCandidates: true);
                    }
                },
            }, config);

            using var desktop = senderFactory.GetDesktopDevice();
            using var list = desktop.GetMediaList(DesktopType.Screen);

            var updateResult = list.UpdateSourceList(forceReload: true, getThumbnail: false);
            if (updateResult < 0)
            {
                Console.WriteLine("Failed to update screen sources.");
                return;
            }

            if (list.SourceCount == 0)
            {
                Console.WriteLine("No screens available for capture.");
                return;
            }

            if (sourceIndex >= list.SourceCount)
            {
                Console.WriteLine($"Requested source index {sourceIndex} is out of range. Available: {list.SourceCount}.");
                return;
            }

            using var mediaSource = list.GetSource(sourceIndex);
            Console.WriteLine($"Capturing screen: {mediaSource.Name}");

            using var capturer = desktop.CreateCapturer(mediaSource, showCursor: showCursor);
            var captureState = capturer.Start((uint)fps);
            if (captureState != DesktopCaptureState.Running)
            {
                Console.WriteLine($"Failed to start desktop capture: {captureState}");
                return;
            }

            using var videoSource = senderFactory.CreateDesktopSource(capturer, "screen");
            using var track = senderFactory.CreateVideoTrack(videoSource, "screen0");
            sender = pc1.AddVideoTrackSender(track, new[] { "stream0" });
            if (!sender.SetEncodingParameters(new RtpEncodingSettings
                {
                    MaxBitrateBps = 3_000_000,
                    MaxFramerate = fps,
                    DegradationPreference = DegradationPreference.MaintainResolution,
                }))
            {
                Console.WriteLine("Warning: failed to set sender encoding parameters.");
            }

            Task candidatePumpTask = Task.CompletedTask;
            if (iceMode != IceApplyMode.Direct)
            {
                candidatePumpTask = CandidateApplyLoopAsync(
                    candidateChannel.Reader,
                    () => pc1,
                    () => pc2,
                    iceMode,
                    dispatchMetrics,
                    Trace,
                    ex =>
                    {
                        Interlocked.Exchange(ref candidatePumpFaulted, 1);
                        Interlocked.Increment(ref dispatchMetrics.WorkerFaults);
                        Console.WriteLine($"Candidate worker faulted: {ex.GetType().Name}: {ex.Message}");
                    },
                    candidatePumpCts.Token);
            }

            await NegotiateAsync(
                    pc1,
                    pc2,
                    MarkPc2ReadyForRemoteCandidatesAndFlush,
                    MarkPc1ReadyForRemoteCandidatesAndFlush,
                    Trace,
                    dumpSdp)
                .ConfigureAwait(false);

            using var statsCts = new CancellationTokenSource();
            var statsTask = Task.CompletedTask;
            if (statsIntervalMs > 0)
            {
                statsTask = Task.Run(
                    () => StatsLoopAsync(
                        pc1,
                        pc2,
                        statsIntervalMs,
                        () => Volatile.Read(ref firstFrameLogged) != 0,
                        () => DateTime.UtcNow - startedAt,
                        dispatchMetrics,
                        iceMode,
                        statsCts.Token),
                    statsCts.Token);
            }

            try
            {
                Console.WriteLine("Loopback running. Close the SDL window to stop.");
                renderer.Run();
            }
            finally
            {
                statsCts.Cancel();
                candidatePumpCts.Cancel();
                candidateChannel.Writer.TryComplete();
                try
                {
                    await statsTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown.
                }
                try
                {
                    await candidatePumpTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown.
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Candidate worker stopped with error: {ex.GetType().Name}: {ex.Message}");
                }
            }

            capturer.Stop();

            if (remoteVideoTrack != null)
            {
                try
                {
                    remoteVideoTrack.RemoveSink(renderer.Sink);
                }
                catch
                {
                    // Ignore cleanup failures.
                }
                try
                {
                    remoteVideoTrack.RemoveSink(firstFrameProbeSink);
                }
                catch
                {
                    // Ignore cleanup failures.
                }
                remoteVideoTrack.Dispose();
            }

            sender.Dispose();
            pc1.Close();
            pc2.Close();
            pc1.Dispose();
            pc2.Dispose();
            senderFactory.Terminate();
            receiverFactory.Terminate();
        }
        finally
        {
            LumenRtc.Terminate();
        }
    }

    private static async Task NegotiateAsync(
        PeerConnection pc1,
        PeerConnection pc2,
        Action onPc2ReadyForRemoteCandidates,
        Action onPc1ReadyForRemoteCandidates,
        Action<string> trace,
        bool dumpSdp)
    {
        trace("CreateOffer");
        var (offerSdp, offerType) = await CreateOfferAsync(pc1).ConfigureAwait(false);
        if (dumpSdp)
        {
            DumpSdpSummary("pc1 offer", offerSdp);
        }

        trace("SetLocalDescription(offer) on pc1");
        await SetLocalDescriptionAsync(pc1, offerSdp, offerType).ConfigureAwait(false);

        trace("SetRemoteDescription(offer) on pc2");
        await SetRemoteDescriptionAsync(pc2, offerSdp, offerType).ConfigureAwait(false);

        trace("CreateAnswer");
        var (answerSdp, answerType) = await CreateAnswerAsync(pc2).ConfigureAwait(false);
        if (dumpSdp)
        {
            DumpSdpSummary("pc2 answer", answerSdp);
        }

        trace("SetLocalDescription(answer) on pc2");
        await SetLocalDescriptionAsync(pc2, answerSdp, answerType).ConfigureAwait(false);
        onPc2ReadyForRemoteCandidates();

        trace("SetRemoteDescription(answer) on pc1");
        await SetRemoteDescriptionAsync(pc1, answerSdp, answerType).ConfigureAwait(false);
        onPc1ReadyForRemoteCandidates();
    }

    private static Task<(string Sdp, string Type)> CreateOfferAsync(PeerConnection pc)
    {
        var tcs = new TaskCompletionSource<(string Sdp, string Type)>(TaskCreationOptions.RunContinuationsAsynchronously);
        pc.CreateOffer(
            (sdp, type) => tcs.TrySetResult((sdp, type)),
            error => tcs.TrySetException(new InvalidOperationException($"CreateOffer failed: {error}")));
        return tcs.Task;
    }

    private static Task<(string Sdp, string Type)> CreateAnswerAsync(PeerConnection pc)
    {
        var tcs = new TaskCompletionSource<(string Sdp, string Type)>(TaskCreationOptions.RunContinuationsAsynchronously);
        pc.CreateAnswer(
            (sdp, type) => tcs.TrySetResult((sdp, type)),
            error => tcs.TrySetException(new InvalidOperationException($"CreateAnswer failed: {error}")));
        return tcs.Task;
    }

    private static Task SetLocalDescriptionAsync(PeerConnection pc, string sdp, string type)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pc.SetLocalDescription(
            sdp,
            type,
            () => tcs.TrySetResult(),
            error => tcs.TrySetException(new InvalidOperationException($"SetLocalDescription failed: {error}")));
        return tcs.Task;
    }

    private static Task SetRemoteDescriptionAsync(PeerConnection pc, string sdp, string type)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pc.SetRemoteDescription(
            sdp,
            type,
            () => tcs.TrySetResult(),
            error => tcs.TrySetException(new InvalidOperationException($"SetRemoteDescription failed: {error}")));
        return tcs.Task;
    }

    private static async Task StatsLoopAsync(
        PeerConnection pc1,
        PeerConnection pc2,
        int intervalMs,
        Func<bool> firstFrameReceived,
        Func<TimeSpan> elapsed,
        CandidateDispatchMetrics dispatchMetrics,
        IceApplyMode iceMode,
        CancellationToken token)
    {
        var timeoutPrinted = false;

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(intervalMs, token).ConfigureAwait(false);
            var pc1HasSucceededPair = await PrintStatsAsync("pc1", pc1, token).ConfigureAwait(false);
            var pc2HasSucceededPair = await PrintStatsAsync("pc2", pc2, token).ConfigureAwait(false);
            PrintDispatchHeartbeat(dispatchMetrics, iceMode);

            if (!timeoutPrinted &&
                !firstFrameReceived() &&
                elapsed() > TimeSpan.FromSeconds(12))
            {
                timeoutPrinted = true;
                Console.WriteLine(
                    $"No video frame received after 12s. mode={ToIceModeArg(iceMode)} " +
                    $"dispatch={GetDispatchSummary(dispatchMetrics)} " +
                    $"pairSelected={(pc1HasSucceededPair || pc2HasSucceededPair)}");
                Console.WriteLine("Next diagnostics:");
                Console.WriteLine("  dotnet run --project .\\samples\\LumenRTC.Sample.ScreenShareLoopback\\LumenRTC.Sample.ScreenShareLoopback.csproj -- --trace-signaling=true --stun=stun:stun.l.google.com:19302 --stats-interval-ms=2000 --ice-mode=direct");
                Console.WriteLine("  dotnet run --project .\\samples\\LumenRTC.Sample.ScreenShareLoopback\\LumenRTC.Sample.ScreenShareLoopback.csproj -- --trace-signaling=true --stun=stun:stun.l.google.com:19302 --stats-interval-ms=2000 --ice-mode=host-only");
            }
        }
    }

    private static async Task<bool> PrintStatsAsync(string label, PeerConnection pc, CancellationToken token)
    {
        try
        {
            using var report = await pc.GetStatsReportAsync(token).ConfigureAwait(false);

            var pairCount = 0;
            var hasSucceededPair = false;
            var stateCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var candidateSummary = "none";
            foreach (var pair in report.CandidatePairs)
            {
                pairCount++;
                if (pair.TryGetString("state", out var state) &&
                    !string.IsNullOrWhiteSpace(state))
                {
                    stateCounts.TryGetValue(state, out var current);
                    stateCounts[state] = current + 1;

                    if (string.Equals(state, "succeeded", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSucceededPair = true;
                        pair.TryGetBool("nominated", out var nominated);
                        var sent = ReadInt64(pair, "bytesSent");
                        var recv = ReadInt64(pair, "bytesReceived");
                        candidateSummary = $"succeeded,nominated={nominated},pairBytes={sent}/{recv}";
                        break;
                    }
                }
            }
            if (candidateSummary == "none" && pairCount > 0)
            {
                var parts = new List<string>();
                foreach (var kv in stateCounts)
                {
                    parts.Add($"{kv.Key}:{kv.Value}");
                }
                candidateSummary = $"pairs={pairCount},states={string.Join(",", parts)}";
            }

            var localCandidateCount = CountStats(report.GetByType("local-candidate"));
            var remoteCandidateCount = CountStats(report.GetByType("remote-candidate"));

            var transportSummary = "none";
            foreach (var transport in report.Transport)
            {
                transport.TryGetString("selectedCandidatePairId", out var selectedPairId);
                var sent = ReadInt64(transport, "bytesSent");
                var recv = ReadInt64(transport, "bytesReceived");
                transportSummary = $"selectedPair={(string.IsNullOrWhiteSpace(selectedPairId) ? "-" : selectedPairId)},bytes={sent}/{recv}";
                break;
            }

            long outboundVideoBytes = 0;
            foreach (var outbound in report.OutboundRtp)
            {
                if (IsVideoRtpStat(outbound))
                {
                    outboundVideoBytes = Math.Max(outboundVideoBytes, ReadInt64(outbound, "bytesSent"));
                }
            }

            long inboundVideoBytes = 0;
            foreach (var inbound in report.InboundRtp)
            {
                if (IsVideoRtpStat(inbound))
                {
                    inboundVideoBytes = Math.Max(inboundVideoBytes, ReadInt64(inbound, "bytesReceived"));
                }
            }

            Console.WriteLine(
                $"[{label} stats] candidate={candidateSummary} " +
                $"localCand={localCandidateCount} remoteCand={remoteCandidateCount} " +
                $"transport={transportSummary} " +
                $"outboundVideoBytes={outboundVideoBytes} inboundVideoBytes={inboundVideoBytes}");
            return hasSucceededPair;
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{label} stats] failed: {ex.Message}");
            return false;
        }
    }

    private static async Task CandidateApplyLoopAsync(
        ChannelReader<CandidateForwardItem> reader,
        Func<PeerConnection?> getPc1,
        Func<PeerConnection?> getPc2,
        IceApplyMode iceMode,
        CandidateDispatchMetrics metrics,
        Action<string> trace,
        Action<Exception> onWorkerFault,
        CancellationToken token)
    {
        try
        {
            while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var item))
                {
                    TryApplyCandidate(
                        item,
                        getPc1,
                        getPc2,
                        iceMode,
                        metrics,
                        trace,
                        countDequeued: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            onWorkerFault(ex);
        }
    }

    private static bool TryApplyCandidate(
        CandidateForwardItem item,
        Func<PeerConnection?> getPc1,
        Func<PeerConnection?> getPc2,
        IceApplyMode iceMode,
        CandidateDispatchMetrics metrics,
        Action<string> trace,
        bool countDequeued)
    {
        if (countDequeued)
        {
            Interlocked.Increment(ref metrics.Dequeued);
        }

        var target = string.Equals(item.TargetPc, "pc1", StringComparison.OrdinalIgnoreCase)
            ? getPc1()
            : getPc2();
        if (target == null)
        {
            Interlocked.Increment(ref metrics.Dropped);
            trace($"drop candidate: target {item.TargetPc} is not available");
            return false;
        }

        if (!ShouldForwardCandidate(item, iceMode))
        {
            Interlocked.Increment(ref metrics.Dropped);
            trace(
                $"drop {item.SourcePc}->{item.TargetPc} candidate type={ExtractCandidateType(item.Candidate)} " +
                $"addr={ExtractCandidateAddress(item.Candidate)} (mode={ToIceModeArg(iceMode)})");
            return true;
        }

        var candidateValue = item.EndOfCandidates ? string.Empty : item.Candidate;
        var applied = target.TryAddIceCandidate(item.Mid, item.Mline, candidateValue);
        var candidateKind = item.EndOfCandidates
            ? "eoc"
            : $"candidate type={ExtractCandidateType(item.Candidate)} addr={ExtractCandidateAddress(item.Candidate)}";

        if (applied)
        {
            Interlocked.Increment(ref metrics.Applied);
            trace($"{item.TargetPc} apply {candidateKind} from {item.SourcePc} mid={item.Mid} mline={item.Mline}");
            return true;
        }

        Interlocked.Increment(ref metrics.Failed);
        Console.WriteLine($"{item.TargetPc} failed to apply {candidateKind} from {item.SourcePc} mid={item.Mid} mline={item.Mline}");
        return false;
    }

    private static bool ShouldForwardCandidate(CandidateForwardItem item, IceApplyMode iceMode)
    {
        if (item.EndOfCandidates)
        {
            return true;
        }

        return iceMode switch
        {
            IceApplyMode.HostOnly => IsLoopbackFriendlyCandidate(item.Candidate),
            _ => true,
        };
    }

    private static void PrintDispatchHeartbeat(CandidateDispatchMetrics metrics, IceApplyMode mode)
    {
        Console.WriteLine($"[ice-dispatch] mode={ToIceModeArg(mode)} {GetDispatchSummary(metrics)}");
    }

    private static string GetDispatchSummary(CandidateDispatchMetrics metrics)
    {
        var enqueued = Interlocked.Read(ref metrics.Enqueued);
        var dequeued = Interlocked.Read(ref metrics.Dequeued);
        var applied = Interlocked.Read(ref metrics.Applied);
        var failed = Interlocked.Read(ref metrics.Failed);
        var dropped = Interlocked.Read(ref metrics.Dropped);
        var direct = Interlocked.Read(ref metrics.DirectFallbackApplied);
        var faults = Interlocked.Read(ref metrics.WorkerFaults);
        return $"enq={enqueued} deq={dequeued} applied={applied} failed={failed} dropped={dropped} direct={direct} faults={faults}";
    }

    private static int CountStats(IEnumerable<RtcStat> stats)
    {
        var count = 0;
        foreach (var _ in stats)
        {
            count++;
        }

        return count;
    }

    private static void DumpSdpSummary(string label, string sdp)
    {
        if (string.IsNullOrWhiteSpace(sdp))
        {
            Console.WriteLine($"[{label} sdp] <empty>");
            return;
        }

        var lines = sdp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var candidateLines = new List<string>();
        var totalCandidateLines = 0;
        var sb = new StringBuilder();
        sb.AppendLine($"[{label} sdp]");

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("a=candidate:", StringComparison.Ordinal))
            {
                totalCandidateLines++;
                if (candidateLines.Count < 6)
                {
                    candidateLines.Add(line);
                }
                continue;
            }

            if (line.StartsWith("m=video ", StringComparison.Ordinal) ||
                line.StartsWith("a=ice-ufrag:", StringComparison.Ordinal) ||
                line.StartsWith("a=ice-pwd:", StringComparison.Ordinal) ||
                line.StartsWith("a=fingerprint:", StringComparison.Ordinal) ||
                line.StartsWith("a=setup:", StringComparison.Ordinal) ||
                line.StartsWith("a=rtcp-mux", StringComparison.Ordinal))
            {
                sb.AppendLine($"  {line}");
            }
        }

        sb.AppendLine($"  candidateLines={totalCandidateLines}");
        for (var i = 0; i < candidateLines.Count; i++)
        {
            sb.AppendLine($"  cand[{i}] {candidateLines[i]}");
        }

        Console.Write(sb.ToString());
    }

    private static bool IsLoopbackFriendlyCandidate(string candidate)
    {
        var parsed = TryParseCandidate(candidate);
        if (parsed == null)
        {
            return false;
        }

        if (!string.Equals(parsed.Value.Type, "host", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(parsed.Value.Protocol, "udp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var address = parsed.Value.Address;
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        if (address.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(address, out var ip))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return IsPrivateIpv4(ip);
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || IsUniqueLocalIpv6(ip);
        }

        return false;
    }

    private static bool IsPrivateIpv4(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        if (bytes[0] == 10)
        {
            return true;
        }

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        return false;
    }

    private static bool IsUniqueLocalIpv6(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 16)
        {
            return false;
        }

        return (bytes[0] & 0xFE) == 0xFC;
    }

    private static ParsedCandidate? TryParseCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var tokens = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 8)
        {
            return null;
        }

        var protocol = tokens[2];
        var address = tokens[4];
        var type = string.Empty;

        for (var i = 6; i < tokens.Length - 1; i++)
        {
            if (string.Equals(tokens[i], "typ", StringComparison.OrdinalIgnoreCase))
            {
                type = tokens[i + 1];
                break;
            }
        }

        return new ParsedCandidate(protocol, address, type);
    }

    private static string ExtractCandidateType(string candidate)
    {
        var parsed = TryParseCandidate(candidate);
        return parsed?.Type ?? "<unknown>";
    }

    private static string ExtractCandidateAddress(string candidate)
    {
        var parsed = TryParseCandidate(candidate);
        return parsed?.Address ?? "<unknown>";
    }

    private static bool IsVideoRtpStat(RtcStat stat)
    {
        if (stat.TryGetString("kind", out var kind) &&
            string.Equals(kind, "video", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (stat.TryGetString("mediaType", out var mediaType) &&
            string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static long ReadInt64(RtcStat stat, string field)
    {
        if (stat.TryGetInt64(field, out var value))
        {
            return value;
        }

        if (stat.TryGetUInt32(field, out var value32))
        {
            return value32;
        }

        if (stat.TryGetDouble(field, out var valueDouble))
        {
            return (long)valueDouble;
        }

        return 0;
    }

    private static IceApplyMode ParseIceApplyMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return IceApplyMode.Auto;
        }

        if (raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return IceApplyMode.Auto;
        }

        if (raw.Equals("direct", StringComparison.OrdinalIgnoreCase))
        {
            return IceApplyMode.Direct;
        }

        if (raw.Equals("host-only", StringComparison.OrdinalIgnoreCase))
        {
            return IceApplyMode.HostOnly;
        }

        Console.WriteLine($"Unknown --ice-mode '{raw}', using auto.");
        return IceApplyMode.Auto;
    }

    private static string ToIceModeArg(IceApplyMode mode)
    {
        return mode switch
        {
            IceApplyMode.Auto => "auto",
            IceApplyMode.Direct => "direct",
            IceApplyMode.HostOnly => "host-only",
            _ => "auto",
        };
    }

    private static bool HasArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetArg(string[] args, string name, string fallback)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring(name.Length + 1);
            }

            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return fallback;
    }

    private static int GetIntArg(string[] args, string name, int fallback)
    {
        var value = GetArg(args, name, string.Empty);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool GetBoolArg(string[] args, string name, bool fallback)
    {
        var value = GetArg(args, name, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Equals("1", StringComparison.Ordinal) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
