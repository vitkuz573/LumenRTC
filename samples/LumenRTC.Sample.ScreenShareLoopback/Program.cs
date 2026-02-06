using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.ScreenShareLoopback;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var sourceIndex = Math.Max(0, GetIntArg(args, "--source", 0));
        var fps = Math.Max(1, GetIntArg(args, "--fps", 30));
        var showCursor = GetBoolArg(args, "--cursor", true);
        var traceSignaling = GetBoolArg(args, "--trace-signaling", false);
        var statsIntervalMs = Math.Max(0, GetIntArg(args, "--stats-interval-ms", 2000));
        var disableIpv6 = GetBoolArg(args, "--disable-ipv6", true);
        var stun = GetArg(args, "--stun", string.Empty);

        void Trace(string text)
        {
            if (!traceSignaling)
            {
                return;
            }

            Console.WriteLine($"[trace:loopback] {text}");
        }

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
            var pendingForPc1 = new List<(string Mid, int Mline, string Candidate)>();
            var pendingForPc2 = new List<(string Mid, int Mline, string Candidate)>();
            var pc1CanApplyRemoteCandidates = false;
            var pc2CanApplyRemoteCandidates = false;
            var startedAt = DateTime.UtcNow;

            PeerConnection? pc1 = null;
            PeerConnection? pc2 = null;
            RtpSender? sender = null;

            void SafeApplyRemoteCandidate(PeerConnection? pc, string who, string mid, int mline, string candidate)
            {
                if (pc == null)
                {
                    return;
                }

                try
                {
                    pc.AddIceCandidate(mid, mline, candidate);
                    Trace($"{who} apply candidate mid={mid} mline={mline}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{who} failed to apply ICE candidate: {ex.Message}");
                }
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

            void QueueOrForwardToPc1(string mid, int mline, string candidate)
            {
                var forwardNow = false;
                lock (iceSync)
                {
                    if (pc1CanApplyRemoteCandidates)
                    {
                        forwardNow = true;
                    }
                    else
                    {
                        pendingForPc1.Add((mid, mline, candidate));
                        Trace($"pc1 queue candidate mid={mid} mline={mline}");
                    }
                }

                if (forwardNow)
                {
                    SafeApplyRemoteCandidate(pc1, "pc1", mid, mline, candidate);
                }
            }

            void QueueOrForwardToPc2(string mid, int mline, string candidate)
            {
                var forwardNow = false;
                lock (iceSync)
                {
                    if (pc2CanApplyRemoteCandidates)
                    {
                        forwardNow = true;
                    }
                    else
                    {
                        pendingForPc2.Add((mid, mline, candidate));
                        Trace($"pc2 queue candidate mid={mid} mline={mline}");
                    }
                }

                if (forwardNow)
                {
                    SafeApplyRemoteCandidate(pc2, "pc2", mid, mline, candidate);
                }
            }

            void MarkPc1ReadyForRemoteCandidatesAndFlush()
            {
                List<(string Mid, int Mline, string Candidate)> pending;
                lock (iceSync)
                {
                    pc1CanApplyRemoteCandidates = true;
                    pending = new List<(string Mid, int Mline, string Candidate)>(pendingForPc1);
                    pendingForPc1.Clear();
                }

                if (pending.Count > 0)
                {
                    Trace($"pc1 flush queued candidates: {pending.Count}");
                }

                foreach (var candidate in pending)
                {
                    SafeApplyRemoteCandidate(pc1, "pc1", candidate.Mid, candidate.Mline, candidate.Candidate);
                }
            }

            void MarkPc2ReadyForRemoteCandidatesAndFlush()
            {
                List<(string Mid, int Mline, string Candidate)> pending;
                lock (iceSync)
                {
                    pc2CanApplyRemoteCandidates = true;
                    pending = new List<(string Mid, int Mline, string Candidate)>(pendingForPc2);
                    pendingForPc2.Clear();
                }

                if (pending.Count > 0)
                {
                    Trace($"pc2 flush queued candidates: {pending.Count}");
                }

                foreach (var candidate in pending)
                {
                    SafeApplyRemoteCandidate(pc2, "pc2", candidate.Mid, candidate.Mline, candidate.Candidate);
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
                    Trace($"pc1 local candidate mid={mid} mline={mline}");
                    QueueOrForwardToPc2(mid, mline, cand);
                },
                OnPeerConnectionState = state => Console.WriteLine($"pc1 state: {state}"),
                OnIceConnectionState = state => Console.WriteLine($"pc1 ice: {state}"),
            }, config);

            pc2 = receiverFactory.CreatePeerConnection(new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    Trace($"pc2 local candidate mid={mid} mline={mline}");
                    QueueOrForwardToPc1(mid, mline, cand);
                },
                OnVideoTrack = AttachRemoteTrack,
                OnPeerConnectionState = state => Console.WriteLine($"pc2 state: {state}"),
                OnIceConnectionState = state => Console.WriteLine($"pc2 ice: {state}"),
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

            await NegotiateAsync(pc1, pc2, MarkPc2ReadyForRemoteCandidatesAndFlush, MarkPc1ReadyForRemoteCandidatesAndFlush, Trace)
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
                try
                {
                    await statsTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown.
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
        Action<string> trace)
    {
        trace("CreateOffer");
        var (offerSdp, offerType) = await CreateOfferAsync(pc1).ConfigureAwait(false);

        trace("SetLocalDescription(offer) on pc1");
        await SetLocalDescriptionAsync(pc1, offerSdp, offerType).ConfigureAwait(false);

        trace("SetRemoteDescription(offer) on pc2");
        await SetRemoteDescriptionAsync(pc2, offerSdp, offerType).ConfigureAwait(false);

        trace("CreateAnswer");
        var (answerSdp, answerType) = await CreateAnswerAsync(pc2).ConfigureAwait(false);

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
        CancellationToken token)
    {
        var firstFrameWarningPrinted = false;

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(intervalMs, token).ConfigureAwait(false);
            await PrintStatsAsync("pc1", pc1, token).ConfigureAwait(false);
            await PrintStatsAsync("pc2", pc2, token).ConfigureAwait(false);

            if (!firstFrameWarningPrinted &&
                !firstFrameReceived() &&
                elapsed() > TimeSpan.FromSeconds(8))
            {
                firstFrameWarningPrinted = true;
                Console.WriteLine("No video frame received after 8s. Try --trace-signaling=true and --stun=stun:stun.l.google.com:19302.");
            }
        }
    }

    private static async Task PrintStatsAsync(string label, PeerConnection pc, CancellationToken token)
    {
        try
        {
            using var report = await pc.GetStatsReportAsync(token).ConfigureAwait(false);

            var candidateSummary = "none";
            foreach (var pair in report.CandidatePairs)
            {
                if (pair.TryGetString("state", out var state) &&
                    string.Equals(state, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    pair.TryGetBool("nominated", out var nominated);
                    var sent = ReadInt64(pair, "bytesSent");
                    var recv = ReadInt64(pair, "bytesReceived");
                    candidateSummary = $"succeeded,nominated={nominated},pairBytes={sent}/{recv}";
                    break;
                }
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

            Console.WriteLine($"[{label} stats] candidate={candidateSummary} outboundVideoBytes={outboundVideoBytes} inboundVideoBytes={inboundVideoBytes}");
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{label} stats] failed: {ex.Message}");
        }
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
