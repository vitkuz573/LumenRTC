using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.ScreenShareLoopback.Core;

internal static class Program
{
    private enum IceApplyMode
    {
        Auto,
        Direct,
        HostOnly,
    }

    private enum SignalingMode
    {
        Inproc,
        Ws,
    }

    private enum IceExchangeMode
    {
        NonTrickle,
        Trickle,
    }

    private sealed class CandidateDispatchMetrics
    {
        public long Enqueued;
        public long Applied;
        public long Dropped;
        public long Failed;
    }

    private sealed class SignalMessage
    {
        public string Type { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? Sdp { get; set; }
        public string? SdpType { get; set; }
        public string? SdpMid { get; set; }
        public int? SdpMLineIndex { get; set; }
        public string? Candidate { get; set; }
        public string? PeerId { get; set; }
        public int? PeerCount { get; set; }
    }

    private interface ISignalingChannel : IAsyncDisposable
    {
        Task StartAsync(Func<string, SignalMessage, Task> onMessage, CancellationToken token);
        Task SendAsync(string role, SignalMessage message, CancellationToken token);
    }

    private sealed class InProcessSignalingChannel : ISignalingChannel
    {
        private readonly Channel<(string TargetRole, SignalMessage Message)> _outbound =
            Channel.CreateUnbounded<(string TargetRole, SignalMessage Message)>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });

        private Func<string, SignalMessage, Task>? _onMessage;
        private CancellationTokenSource? _pumpCts;
        private Task? _pumpTask;

        public Task StartAsync(Func<string, SignalMessage, Task> onMessage, CancellationToken token)
        {
            _onMessage = onMessage;
            _pumpCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _pumpTask = Task.Run(() => PumpAsync(_pumpCts.Token));
            return Task.CompletedTask;
        }

        public async Task SendAsync(string role, SignalMessage message, CancellationToken token)
        {
            var targetRole = string.Equals(role, "sender", StringComparison.OrdinalIgnoreCase)
                ? "viewer"
                : "sender";
            var item = (targetRole, CloneSignalMessage(message));

            await _outbound.Writer.WriteAsync(item, token).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _outbound.Writer.TryComplete();
            if (_pumpCts != null)
            {
                _pumpCts.Cancel();
            }

            if (_pumpTask != null)
            {
                try
                {
                    await _pumpTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown.
                }
            }

            _pumpCts?.Dispose();
        }

        private async Task PumpAsync(CancellationToken token)
        {
            var onMessage = _onMessage;
            if (onMessage == null)
            {
                return;
            }

            while (await _outbound.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (_outbound.Reader.TryRead(out var item))
                {
                    await onMessage(item.TargetRole, item.Message).ConfigureAwait(false);
                }
            }
        }
    }

    private sealed class WebSocketSignalingChannel : ISignalingChannel
    {
        private readonly Uri _uri;
        private readonly JsonSerializerOptions _sendJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly JsonSerializerOptions _receiveJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly SemaphoreSlim _senderSendLock = new(1, 1);
        private readonly SemaphoreSlim _viewerSendLock = new(1, 1);

        private ClientWebSocket? _senderSocket;
        private ClientWebSocket? _viewerSocket;
        private CancellationTokenSource? _receiveCts;
        private Task? _senderReceiveTask;
        private Task? _viewerReceiveTask;

        public WebSocketSignalingChannel(Uri uri)
        {
            _uri = uri;
        }

        public async Task StartAsync(Func<string, SignalMessage, Task> onMessage, CancellationToken token)
        {
            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            _senderSocket = new ClientWebSocket();
            _viewerSocket = new ClientWebSocket();

            await _senderSocket.ConnectAsync(_uri, token).ConfigureAwait(false);
            await _viewerSocket.ConnectAsync(_uri, token).ConfigureAwait(false);

            _senderReceiveTask = Task.Run(() => ReceiveLoopAsync("sender", _senderSocket, onMessage, _receiveCts.Token));
            _viewerReceiveTask = Task.Run(() => ReceiveLoopAsync("viewer", _viewerSocket, onMessage, _receiveCts.Token));
        }

        public async Task SendAsync(string role, SignalMessage message, CancellationToken token)
        {
            var socket = string.Equals(role, "sender", StringComparison.OrdinalIgnoreCase)
                ? _senderSocket
                : _viewerSocket;
            var sendLock = string.Equals(role, "sender", StringComparison.OrdinalIgnoreCase)
                ? _senderSendLock
                : _viewerSendLock;

            if (socket == null || socket.State != WebSocketState.Open)
            {
                return;
            }

            var json = JsonSerializer.Serialize(message, _sendJsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await sendLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            }
            finally
            {
                sendLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_receiveCts != null)
            {
                _receiveCts.Cancel();
            }

            await CloseSocketAsync(_senderSocket).ConfigureAwait(false);
            await CloseSocketAsync(_viewerSocket).ConfigureAwait(false);

            if (_senderReceiveTask != null)
            {
                try
                {
                    await _senderReceiveTask.ConfigureAwait(false);
                }
                catch
                {
                    // Ignore shutdown race exceptions.
                }
            }

            if (_viewerReceiveTask != null)
            {
                try
                {
                    await _viewerReceiveTask.ConfigureAwait(false);
                }
                catch
                {
                    // Ignore shutdown race exceptions.
                }
            }

            _senderSendLock.Dispose();
            _viewerSendLock.Dispose();
            _receiveCts?.Dispose();
        }

        private async Task ReceiveLoopAsync(
            string role,
            ClientWebSocket socket,
            Func<string, SignalMessage, Task> onMessage,
            CancellationToken token)
        {
            var buffer = new byte[64 * 1024];

            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var json = await ReceiveMessageAsync(socket, buffer, token).ConfigureAwait(false);
                if (json == null)
                {
                    break;
                }

                SignalMessage? payload;
                try
                {
                    payload = JsonSerializer.Deserialize<SignalMessage>(json, _receiveJsonOptions);
                }
                catch
                {
                    continue;
                }

                if (payload?.Type == null)
                {
                    continue;
                }

                await onMessage(role, payload).ConfigureAwait(false);
            }
        }

        private static async Task<string?> ReceiveMessageAsync(
            ClientWebSocket socket,
            byte[] buffer,
            CancellationToken token)
        {
            using var stream = new MemoryStream();
            while (true)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
                }
                catch
                {
                    return null;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (result.Count > 0)
                {
                    stream.Write(buffer, 0, result.Count);
                }

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static async Task CloseSocketAsync(ClientWebSocket? socket)
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignore close failures.
            }
            finally
            {
                socket.Dispose();
            }
        }
    }

    public static async Task Main(string[] args)
    {
        var sourceIndex = Math.Max(0, GetIntArg(args, "--source", 0));
        var fps = Math.Max(1, GetIntArg(args, "--fps", 30));
        var showCursor = GetBoolArg(args, "--cursor", true);
        var traceSignaling = GetBoolArg(args, "--trace-signaling", false);
        var traceIceNative = GetBoolArg(args, "--trace-ice-native", false);
        var statsIntervalMs = Math.Max(0, GetIntArg(args, "--stats-interval-ms", 2000));
        var disableIpv6 = GetBoolArg(args, "--disable-ipv6", false);
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

        var signalingMode = ParseSignalingMode(GetArg(args, "--signaling-mode", "inproc"));
        var iceExchangeMode = ParseIceExchangeMode(GetArg(args, "--ice-exchange", "nontrickle"));
        var signalingServer = GetArg(args, "--server", "ws://localhost:8080/ws/");
        var signalingRoom = GetArg(args, "--room", "demo");

        Console.WriteLine($"ICE apply mode: {ToIceModeArg(iceMode)}");
        Console.WriteLine($"ICE exchange: {ToIceExchangeModeArg(iceExchangeMode)}");
        Console.WriteLine($"Signaling mode: {ToSignalingModeArg(signalingMode)}");
        Console.WriteLine($"Native ICE trace: {(traceIceNative ? "on" : "off")}");

        void Trace(string role, string text)
        {
            if (!traceSignaling)
            {
                return;
            }

            Console.WriteLine($"[trace:{role}] {text}");
        }

        Environment.SetEnvironmentVariable("LUMENRTC_TRACE_ICE_NATIVE", traceIceNative ? "1" : "0");
        LumenRtc.Initialize();
        try
        {
            using var senderFactory = PeerConnectionFactory.Create();
            using var viewerFactory = PeerConnectionFactory.Create();
            senderFactory.Initialize();
            viewerFactory.Initialize();

            await using var signaling = CreateSignalingChannel(signalingMode, signalingServer, signalingRoom);

            var startedAt = DateTime.UtcNow;
            var firstFrameLogged = 0;
            var offerSent = false;
            var offerSync = new object();
            var senderLocalDescriptionSet = false;
            var senderRemoteDescriptionSet = false;
            var viewerLocalDescriptionSet = false;
            var viewerRemoteDescriptionSet = false;
            var candidateSync = new object();
            var senderQueuedCandidates = new List<(string Mid, int MlineIndex, string Candidate)>();
            var viewerQueuedCandidates = new List<(string Mid, int MlineIndex, string Candidate)>();
            var dispatchMetrics = new CandidateDispatchMetrics();
            var senderGatheringCompleteTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var viewerGatheringCompleteTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            PeerConnection? senderPc = null;
            PeerConnection? viewerPc = null;
            RtpSender? senderRtp = null;
            VideoTrack? remoteVideoTrack = null;
            var remoteTracks = new List<VideoTrack>();

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
                        // Ignore stale sink cleanup issues.
                    }

                    try
                    {
                        remoteVideoTrack.RemoveSink(firstFrameProbeSink);
                    }
                    catch
                    {
                        // Ignore stale sink cleanup issues.
                    }
                }

                remoteVideoTrack = track;
                remoteTracks.Add(track);
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

            void QueueOrApplyOnSender(string mid, int mline, string candidate)
            {
                var applyNow = false;
                lock (candidateSync)
                {
                    if (senderLocalDescriptionSet && senderRemoteDescriptionSet)
                    {
                        applyNow = true;
                    }
                    else
                    {
                        senderQueuedCandidates.Add((mid, mline, candidate));
                    }
                }

                if (!applyNow)
                {
                    Trace("sender", $"queue candidate mid={mid} mline={mline}");
                    return;
                }

                ApplyCandidate("sender", senderPc, mid, mline, candidate, iceMode, dispatchMetrics, traceSignaling, Trace);
            }

            void QueueOrApplyOnViewer(string mid, int mline, string candidate)
            {
                var applyNow = false;
                lock (candidateSync)
                {
                    if (viewerLocalDescriptionSet && viewerRemoteDescriptionSet)
                    {
                        applyNow = true;
                    }
                    else
                    {
                        viewerQueuedCandidates.Add((mid, mline, candidate));
                    }
                }

                if (!applyNow)
                {
                    Trace("viewer", $"queue candidate mid={mid} mline={mline}");
                    return;
                }

                ApplyCandidate("viewer", viewerPc, mid, mline, candidate, iceMode, dispatchMetrics, traceSignaling, Trace);
            }

            void FlushSenderQueuedCandidatesIfReady()
            {
                List<(string Mid, int MlineIndex, string Candidate)> pending;
                lock (candidateSync)
                {
                    if (!senderLocalDescriptionSet || !senderRemoteDescriptionSet || senderQueuedCandidates.Count == 0)
                    {
                        return;
                    }

                    pending = new List<(string Mid, int MlineIndex, string Candidate)>(senderQueuedCandidates);
                    senderQueuedCandidates.Clear();
                }

                if (pending.Count > 0)
                {
                    Trace("sender", $"flush queued candidates: {pending.Count}");
                }

                foreach (var candidate in pending)
                {
                    ApplyCandidate("sender", senderPc, candidate.Mid, candidate.MlineIndex, candidate.Candidate, iceMode, dispatchMetrics, traceSignaling, Trace);
                }
            }

            void FlushViewerQueuedCandidatesIfReady()
            {
                List<(string Mid, int MlineIndex, string Candidate)> pending;
                lock (candidateSync)
                {
                    if (!viewerLocalDescriptionSet || !viewerRemoteDescriptionSet || viewerQueuedCandidates.Count == 0)
                    {
                        return;
                    }

                    pending = new List<(string Mid, int MlineIndex, string Candidate)>(viewerQueuedCandidates);
                    viewerQueuedCandidates.Clear();
                }

                if (pending.Count > 0)
                {
                    Trace("viewer", $"flush queued candidates: {pending.Count}");
                }

                foreach (var candidate in pending)
                {
                    ApplyCandidate("viewer", viewerPc, candidate.Mid, candidate.MlineIndex, candidate.Candidate, iceMode, dispatchMetrics, traceSignaling, Trace);
                }
            }

            void MarkSenderLocalDescriptionSetAndFlush()
            {
                lock (candidateSync)
                {
                    senderLocalDescriptionSet = true;
                }

                FlushSenderQueuedCandidatesIfReady();
            }

            void MarkSenderRemoteDescriptionSetAndFlush()
            {
                lock (candidateSync)
                {
                    senderRemoteDescriptionSet = true;
                }

                FlushSenderQueuedCandidatesIfReady();
            }

            void MarkViewerLocalDescriptionSetAndFlush()
            {
                lock (candidateSync)
                {
                    viewerLocalDescriptionSet = true;
                }

                FlushViewerQueuedCandidatesIfReady();
            }

            void MarkViewerRemoteDescriptionSetAndFlush()
            {
                lock (candidateSync)
                {
                    viewerRemoteDescriptionSet = true;
                }

                FlushViewerQueuedCandidatesIfReady();
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

            senderPc = senderFactory.CreatePeerConnection(new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    if (iceExchangeMode == IceExchangeMode.NonTrickle)
                    {
                        return;
                    }

                    Trace("sender", $"local candidate type={ExtractCandidateType(cand)} addr={ExtractCandidateAddress(cand)} mid={mid} mline={mline}");
                    _ = signaling.SendAsync("sender", new SignalMessage
                    {
                        Type = "candidate",
                        SdpMid = mid,
                        SdpMLineIndex = mline,
                        Candidate = cand,
                    }, CancellationToken.None);
                },
                OnPeerConnectionState = state => Console.WriteLine($"sender state: {state}"),
                OnIceConnectionState = state => Console.WriteLine($"sender ice: {state}"),
                OnIceGatheringState = state =>
                {
                    Console.WriteLine($"sender gathering: {state}");
                    if (state == IceGatheringState.Complete)
                    {
                        senderGatheringCompleteTcs.TrySetResult(true);
                    }
                },
            }, config);

            viewerPc = viewerFactory.CreatePeerConnection(new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    if (iceExchangeMode == IceExchangeMode.NonTrickle)
                    {
                        return;
                    }

                    Trace("viewer", $"local candidate type={ExtractCandidateType(cand)} addr={ExtractCandidateAddress(cand)} mid={mid} mline={mline}");
                    _ = signaling.SendAsync("viewer", new SignalMessage
                    {
                        Type = "candidate",
                        SdpMid = mid,
                        SdpMLineIndex = mline,
                        Candidate = cand,
                    }, CancellationToken.None);
                },
                OnVideoTrack = AttachRemoteTrack,
                OnPeerConnectionState = state => Console.WriteLine($"viewer state: {state}"),
                OnIceConnectionState = state => Console.WriteLine($"viewer ice: {state}"),
                OnIceGatheringState = state =>
                {
                    Console.WriteLine($"viewer gathering: {state}");
                    if (state == IceGatheringState.Complete)
                    {
                        viewerGatheringCompleteTcs.TrySetResult(true);
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
            using var videoTrack = senderFactory.CreateVideoTrack(videoSource, "screen0");
            senderRtp = senderPc.AddVideoTrackSender(videoTrack, new[] { "stream0" });
            if (!senderRtp.SetEncodingParameters(new RtpEncodingSettings
                {
                    MaxBitrateBps = 3_000_000,
                    MaxFramerate = fps,
                    DegradationPreference = DegradationPreference.MaintainResolution,
                }))
            {
                Console.WriteLine("Warning: failed to set sender encoding parameters.");
            }

            async Task StartOfferAsync()
            {
                lock (offerSync)
                {
                    if (offerSent)
                    {
                        Trace("sender", "start offer skipped: already sent");
                        return;
                    }
                    offerSent = true;
                }

                Trace("sender", "CreateOffer");
                senderPc.CreateOffer(
                    (sdp, type) =>
                    {
                        if (dumpSdp)
                        {
                            DumpSdpSummary("sender offer", sdp);
                        }

                        Trace("sender", "SetLocalDescription(offer)");
                        senderPc.SetLocalDescription(
                            sdp,
                            type,
                            () =>
                            {
                                MarkSenderLocalDescriptionSetAndFlush();
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        if (iceExchangeMode == IceExchangeMode.NonTrickle)
                                        {
                                            await WaitForGatheringCompleteAsync(
                                                "sender",
                                                senderGatheringCompleteTcs.Task,
                                                timeoutMs: 8000,
                                                traceEnabled: traceSignaling,
                                                trace: Trace).ConfigureAwait(false);

                                            var localOffer = await senderPc.GetLocalDescriptionAsync().ConfigureAwait(false);
                                            if (dumpSdp)
                                            {
                                                DumpSdpSummary("sender offer", localOffer.Sdp);
                                            }

                                            await signaling.SendAsync("sender", new SignalMessage
                                            {
                                                Type = "offer",
                                                Sdp = localOffer.Sdp,
                                                SdpType = localOffer.Type,
                                            }, CancellationToken.None).ConfigureAwait(false);
                                            return;
                                        }

                                        await signaling.SendAsync("sender", new SignalMessage
                                        {
                                            Type = "offer",
                                            Sdp = sdp,
                                            SdpType = type,
                                        }, CancellationToken.None).ConfigureAwait(false);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Failed to send offer: {ex.Message}");
                                        lock (offerSync)
                                        {
                                            offerSent = false;
                                        }
                                    }
                                });
                            },
                            error =>
                            {
                                Console.WriteLine($"SetLocalDescription(offer) failed: {error}");
                                lock (offerSync)
                                {
                                    offerSent = false;
                                }
                            });
                    },
                    error =>
                    {
                        Console.WriteLine($"CreateOffer failed: {error}");
                        lock (offerSync)
                        {
                            offerSent = false;
                        }
                    });
            }

            async Task HandleSignalAsync(string role, SignalMessage message)
            {
                Trace(role, $"rx {message.Type}");
                switch (message.Type)
                {
                    case "room_state":
                        if (string.Equals(role, "sender", StringComparison.OrdinalIgnoreCase) &&
                            (message.PeerCount ?? 0) > 1)
                        {
                            await StartOfferAsync().ConfigureAwait(false);
                        }
                        break;
                    case "peer_joined":
                        if (string.Equals(role, "sender", StringComparison.OrdinalIgnoreCase) &&
                            (message.PeerCount ?? 0) > 1)
                        {
                            await StartOfferAsync().ConfigureAwait(false);
                        }
                        break;
                    case "ready":
                        if (string.Equals(role, "sender", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(message.Role, "viewer", StringComparison.OrdinalIgnoreCase))
                        {
                            await StartOfferAsync().ConfigureAwait(false);
                        }
                        break;
                    case "offer":
                        if (!string.Equals(role, "viewer", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(message.Sdp) ||
                            string.IsNullOrWhiteSpace(message.SdpType))
                        {
                            break;
                        }

                        if (dumpSdp)
                        {
                            DumpSdpSummary("viewer offer", message.Sdp);
                        }

                        viewerPc.SetRemoteDescription(
                            message.Sdp,
                            message.SdpType,
                            () =>
                            {
                                MarkViewerRemoteDescriptionSetAndFlush();
                                viewerPc.CreateAnswer(
                                    (answerSdp, answerType) =>
                                    {
                                        if (dumpSdp)
                                        {
                                            DumpSdpSummary("viewer answer", answerSdp);
                                        }

                                        viewerPc.SetLocalDescription(
                                            answerSdp,
                                            answerType,
                                            () =>
                                            {
                                                MarkViewerLocalDescriptionSetAndFlush();
                                                _ = Task.Run(async () =>
                                                {
                                                    try
                                                    {
                                                        if (iceExchangeMode == IceExchangeMode.NonTrickle)
                                                        {
                                                            await WaitForGatheringCompleteAsync(
                                                                "viewer",
                                                                viewerGatheringCompleteTcs.Task,
                                                                timeoutMs: 8000,
                                                                traceEnabled: traceSignaling,
                                                                trace: Trace).ConfigureAwait(false);

                                                            var localAnswer = await viewerPc.GetLocalDescriptionAsync().ConfigureAwait(false);
                                                            if (dumpSdp)
                                                            {
                                                                DumpSdpSummary("viewer answer", localAnswer.Sdp);
                                                            }

                                                            await signaling.SendAsync("viewer", new SignalMessage
                                                            {
                                                                Type = "answer",
                                                                Sdp = localAnswer.Sdp,
                                                                SdpType = localAnswer.Type,
                                                            }, CancellationToken.None).ConfigureAwait(false);
                                                            return;
                                                        }

                                                        await signaling.SendAsync("viewer", new SignalMessage
                                                        {
                                                            Type = "answer",
                                                            Sdp = answerSdp,
                                                            SdpType = answerType,
                                                        }, CancellationToken.None).ConfigureAwait(false);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Console.WriteLine($"Failed to send answer: {ex.Message}");
                                                    }
                                                });
                                            },
                                            error => Console.WriteLine($"SetLocalDescription(answer) failed: {error}"));
                                    },
                                    error => Console.WriteLine($"CreateAnswer failed: {error}"));
                            },
                            error => Console.WriteLine($"SetRemoteDescription(offer) failed: {error}"));
                        break;
                    case "answer":
                        if (!string.Equals(role, "sender", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(message.Sdp) ||
                            string.IsNullOrWhiteSpace(message.SdpType))
                        {
                            break;
                        }

                        senderPc.SetRemoteDescription(
                            message.Sdp,
                            message.SdpType,
                            () =>
                            {
                                MarkSenderRemoteDescriptionSetAndFlush();
                            },
                            error => Console.WriteLine($"SetRemoteDescription(answer) failed: {error}"));
                        break;
                    case "candidate":
                        if (iceExchangeMode == IceExchangeMode.NonTrickle)
                        {
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(message.Candidate) || string.IsNullOrWhiteSpace(message.SdpMid))
                        {
                            break;
                        }

                        if (string.Equals(role, "sender", StringComparison.OrdinalIgnoreCase))
                        {
                            QueueOrApplyOnSender(message.SdpMid, message.SdpMLineIndex ?? 0, message.Candidate);
                        }
                        else
                        {
                            QueueOrApplyOnViewer(message.SdpMid, message.SdpMLineIndex ?? 0, message.Candidate);
                        }
                        break;
                }
            }

            using var runCts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                runCts.Cancel();
            };

            await signaling.StartAsync(HandleSignalAsync, runCts.Token).ConfigureAwait(false);

            await signaling.SendAsync("sender", new SignalMessage
            {
                Type = "ready",
                Role = "sender",
            }, runCts.Token).ConfigureAwait(false);

            await signaling.SendAsync("viewer", new SignalMessage
            {
                Type = "ready",
                Role = "viewer",
            }, runCts.Token).ConfigureAwait(false);

            using var statsCts = new CancellationTokenSource();
            var statsTask = Task.CompletedTask;
            if (statsIntervalMs > 0)
            {
                statsTask = Task.Run(
                    () => StatsLoopAsync(
                        senderPc,
                        viewerPc,
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
                try
                {
                    await statsTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown.
                }

                runCts.Cancel();
            }

            capturer.Stop();

            foreach (var track in remoteTracks)
            {
                try
                {
                    track.RemoveSink(renderer.Sink);
                }
                catch
                {
                    // Ignore cleanup errors.
                }
                try
                {
                    track.RemoveSink(firstFrameProbeSink);
                }
                catch
                {
                    // Ignore cleanup errors.
                }
                track.Dispose();
            }

            senderRtp?.Dispose();
            senderPc.Close();
            viewerPc.Close();
            senderPc.Dispose();
            viewerPc.Dispose();
            senderFactory.Terminate();
            viewerFactory.Terminate();
        }
        finally
        {
            LumenRtc.Terminate();
        }
    }

    private static void ApplyCandidate(
        string role,
        PeerConnection? pc,
        string mid,
        int mline,
        string candidate,
        IceApplyMode iceMode,
        CandidateDispatchMetrics metrics,
        bool traceEnabled,
        Action<string, string> trace)
    {
        Interlocked.Increment(ref metrics.Enqueued);

        if (pc == null)
        {
            Interlocked.Increment(ref metrics.Failed);
            return;
        }

        if (iceMode == IceApplyMode.HostOnly && !IsLoopbackFriendlyCandidate(candidate))
        {
            Interlocked.Increment(ref metrics.Dropped);
            if (traceEnabled)
            {
                trace(role, $"drop candidate type={ExtractCandidateType(candidate)} addr={ExtractCandidateAddress(candidate)} (mode={ToIceModeArg(iceMode)})");
            }
            return;
        }

        _ = ApplyCandidateWithRetriesAsync(role, pc, mid, mline, candidate, metrics, traceEnabled, trace);
    }

    private static async Task ApplyCandidateWithRetriesAsync(
        string role,
        PeerConnection pc,
        string mid,
        int mline,
        string candidate,
        CandidateDispatchMetrics metrics,
        bool traceEnabled,
        Action<string, string> trace)
    {
        const int maxAttempts = 40;
        const int retryDelayMs = 25;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (pc.TryAddIceCandidate(mid, mline, candidate))
                {
                    Interlocked.Increment(ref metrics.Applied);
                    if (traceEnabled)
                    {
                        var attemptSuffix = attempt > 1 ? $" attempt={attempt}" : string.Empty;
                        trace(role, $"apply candidate type={ExtractCandidateType(candidate)} addr={ExtractCandidateAddress(candidate)} mid={mid} mline={mline}{attemptSuffix}");
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(retryDelayMs).ConfigureAwait(false);
            }
        }

        Interlocked.Increment(ref metrics.Failed);
        if (lastException != null)
        {
            Console.WriteLine(
                $"{role} failed to apply candidate mid={mid} mline={mline} after {maxAttempts} attempts: " +
                $"{lastException.Message}");
            return;
        }

        Console.WriteLine(
            $"{role} rejected candidate mid={mid} mline={mline} " +
            $"type={ExtractCandidateType(candidate)} addr={ExtractCandidateAddress(candidate)} " +
            $"after {maxAttempts} attempts");
    }

    private static async Task WaitForGatheringCompleteAsync(
        string role,
        Task completion,
        int timeoutMs,
        bool traceEnabled,
        Action<string, string> trace)
    {
        using var timeoutCts = new CancellationTokenSource();
        timeoutCts.CancelAfter(timeoutMs);
        try
        {
            await completion.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            if (traceEnabled)
            {
                trace(role, "gathering complete for nontrickle exchange");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"{role} gathering did not complete within {timeoutMs}ms; sending current SDP.");
        }
    }

    private static async Task StatsLoopAsync(
        PeerConnection senderPc,
        PeerConnection viewerPc,
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
            var senderHasPair = await PrintStatsAsync("sender", senderPc, token).ConfigureAwait(false);
            var viewerHasPair = await PrintStatsAsync("viewer", viewerPc, token).ConfigureAwait(false);
            PrintDispatchHeartbeat(dispatchMetrics, iceMode);

            if (!timeoutPrinted && !firstFrameReceived() && elapsed() > TimeSpan.FromSeconds(12))
            {
                timeoutPrinted = true;
                Console.WriteLine(
                    $"No video frame received after 12s. mode={ToIceModeArg(iceMode)} " +
                    $"dispatch={GetDispatchSummary(dispatchMetrics)} " +
                    $"pairSelected={(senderHasPair || viewerHasPair)}");
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
                if (!pair.TryGetString("state", out var state) || string.IsNullOrWhiteSpace(state))
                {
                    continue;
                }

                stateCounts.TryGetValue(state, out var current);
                stateCounts[state] = current + 1;

                if (!string.Equals(state, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                hasSucceededPair = true;
                pair.TryGetBool("nominated", out var nominated);
                var sent = ReadInt64(pair, "bytesSent");
                var recv = ReadInt64(pair, "bytesReceived");
                candidateSummary = $"succeeded,nominated={nominated},pairBytes={sent}/{recv}";
                break;
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
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{label} stats] failed: {ex.Message}");
            return false;
        }
    }

    private static ISignalingChannel CreateSignalingChannel(SignalingMode mode, string server, string room)
    {
        if (mode == SignalingMode.Inproc)
        {
            return new InProcessSignalingChannel();
        }

        var uri = BuildWsUri(server, room);
        return new WebSocketSignalingChannel(uri);
    }

    private static Uri BuildWsUri(string server, string room)
    {
        var value = server.Trim();
        if (!value.StartsWith("ws", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
                .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase);
        }

        if (!value.EndsWith("/", StringComparison.Ordinal))
        {
            value += "/";
        }

        return new Uri($"{value}?room={Uri.EscapeDataString(room)}");
    }

    private static SignalMessage CloneSignalMessage(SignalMessage source)
    {
        return new SignalMessage
        {
            Type = source.Type,
            Role = source.Role,
            Sdp = source.Sdp,
            SdpType = source.SdpType,
            SdpMid = source.SdpMid,
            SdpMLineIndex = source.SdpMLineIndex,
            Candidate = source.Candidate,
            PeerId = source.PeerId,
            PeerCount = source.PeerCount,
        };
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

    private readonly record struct ParsedCandidate(string Protocol, string Address, string Type);

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
        if (stat.TryGetString("kind", out var kind) && string.Equals(kind, "video", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (stat.TryGetString("mediaType", out var mediaType) && string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase))
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

    private static int CountStats(IEnumerable<RtcStat> stats)
    {
        var count = 0;
        foreach (var _ in stats)
        {
            count++;
        }

        return count;
    }

    private static void PrintDispatchHeartbeat(CandidateDispatchMetrics metrics, IceApplyMode mode)
    {
        Console.WriteLine($"[ice-dispatch] mode={ToIceModeArg(mode)} {GetDispatchSummary(metrics)}");
    }

    private static string GetDispatchSummary(CandidateDispatchMetrics metrics)
    {
        var enqueued = Interlocked.Read(ref metrics.Enqueued);
        var applied = Interlocked.Read(ref metrics.Applied);
        var dropped = Interlocked.Read(ref metrics.Dropped);
        var failed = Interlocked.Read(ref metrics.Failed);
        return $"enq={enqueued} applied={applied} dropped={dropped} failed={failed}";
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

    private static IceExchangeMode ParseIceExchangeMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return IceExchangeMode.NonTrickle;
        }

        if (raw.Equals("nontrickle", StringComparison.OrdinalIgnoreCase))
        {
            return IceExchangeMode.NonTrickle;
        }

        if (raw.Equals("trickle", StringComparison.OrdinalIgnoreCase))
        {
            return IceExchangeMode.Trickle;
        }

        Console.WriteLine($"Unknown --ice-exchange '{raw}', using nontrickle.");
        return IceExchangeMode.NonTrickle;
    }

    private static string ToIceExchangeModeArg(IceExchangeMode mode)
    {
        return mode switch
        {
            IceExchangeMode.NonTrickle => "nontrickle",
            IceExchangeMode.Trickle => "trickle",
            _ => "nontrickle",
        };
    }

    private static SignalingMode ParseSignalingMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return SignalingMode.Inproc;
        }

        if (raw.Equals("inproc", StringComparison.OrdinalIgnoreCase))
        {
            return SignalingMode.Inproc;
        }

        if (raw.Equals("ws", StringComparison.OrdinalIgnoreCase))
        {
            return SignalingMode.Ws;
        }

        Console.WriteLine($"Unknown --signaling-mode '{raw}', using inproc.");
        return SignalingMode.Inproc;
    }

    private static string ToSignalingModeArg(SignalingMode mode)
    {
        return mode switch
        {
            SignalingMode.Inproc => "inproc",
            SignalingMode.Ws => "ws",
            _ => "inproc",
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
