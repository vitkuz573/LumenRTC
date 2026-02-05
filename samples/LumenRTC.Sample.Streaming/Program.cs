using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.Streaming;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var role = GetArg(args, "--role", "sender").ToLowerInvariant();
        var server = GetArg(args, "--server", "ws://localhost:8080/ws/");
        var room = GetArg(args, "--room", "demo");
        var stun = GetArg(args, "--stun", string.Empty);
        var captureType = GetArg(args, "--capture", "screen").ToLowerInvariant();
        var sourceIndex = GetIntArg(args, "--source", 0);
        var fps = GetIntArg(args, "--fps", 30);
        var showCursor = GetBoolArg(args, "--cursor", true);
        var maxBitrateKbps = GetIntArg(args, "--max-bitrate-kbps", 3000);

        var uri = BuildUri(server, room);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(uri, cts.Token).ConfigureAwait(false);
        Console.WriteLine($"Connected to signaling server {uri}");

        using var sendLock = new SemaphoreSlim(1, 1);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        async Task SendAsync(SignalMessage message)
        {
            var json = JsonSerializer.Serialize(message, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await sendLock.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                await ws.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token).ConfigureAwait(false);
            }
            finally
            {
                sendLock.Release();
            }
        }

        LumenRtc.Initialize();
        try
        {
            using var factory = PeerConnectionFactory.Create();
            factory.Initialize();

            var config = new RtcConfiguration();
            if (!string.IsNullOrWhiteSpace(stun))
            {
                config.IceServers.Add(new IceServer(stun));
            }

            PeerConnection? pc = null;
            DataChannel? dataChannel = null;
            SdlVideoRenderer? renderer = null;
            var offerSent = false;

            var callbacks = new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    _ = SendAsync(new SignalMessage
                    {
                        Type = "candidate",
                        SdpMid = mid,
                        SdpMLineIndex = mline,
                        Candidate = cand
                    });
                },
                OnDataChannel = channel =>
                {
                    if (role == "sender")
                    {
                        dataChannel = channel;
                        dataChannel.SetCallbacks(new DataChannelCallbacks
                        {
                            OnStateChange = state => Console.WriteLine($"DataChannel state: {state}"),
                            OnMessage = (data, _) =>
                            {
                                var text = Encoding.UTF8.GetString(data.Span);
                                Console.WriteLine($"Data message: {text}");
                            }
                        });
                    }
                },
                OnVideoTrack = track =>
                {
                    if (role == "viewer" && renderer != null)
                    {
                        track.AddSink(renderer.Sink);
                    }
                },
                OnPeerConnectionState = state => Console.WriteLine($"PC state: {state}"),
                OnIceConnectionState = state => Console.WriteLine($"ICE state: {state}"),
            };

            pc = factory.CreatePeerConnection(callbacks, config);

            DesktopDevice? desktopDevice = null;
            DesktopMediaList? mediaList = null;
            MediaSource? mediaSource = null;
            DesktopCapturer? capturer = null;
            VideoSource? videoSource = null;
            VideoTrack? videoTrack = null;
            RtpSender? rtpSender = null;

            if (role == "sender")
            {
                desktopDevice = factory.GetDesktopDevice();
                var type = captureType == "window" ? DesktopType.Window : DesktopType.Screen;
                mediaList = desktopDevice.GetMediaList(type);

                var updateResult = mediaList.UpdateSourceList(forceReload: true, getThumbnail: false);
                if (updateResult != 0)
                {
                    Console.WriteLine("Failed to update desktop sources.");
                    return;
                }

                if (mediaList.SourceCount == 0 || sourceIndex >= mediaList.SourceCount)
                {
                    Console.WriteLine("No desktop sources available for capture.");
                    return;
                }

                mediaSource = mediaList.GetSource(sourceIndex);
                Console.WriteLine($"Capturing {mediaSource.Type}: {mediaSource.Name}");

                capturer = desktopDevice.CreateCapturer(mediaSource, showCursor);
                var state = capturer.Start((uint)Math.Max(1, fps));
                if (state != DesktopCaptureState.Running)
                {
                    Console.WriteLine($"Failed to start capture: {state}");
                    return;
                }

                videoSource = factory.CreateDesktopSource(capturer, "screen");
                videoTrack = factory.CreateVideoTrack(videoSource, "screen0");
                rtpSender = pc.AddVideoTrackSender(videoTrack, new[] { "stream0" });

                var codecs = factory.GetRtpSenderCodecMimeTypes(MediaType.Video);
                var preferred = new[] { "video/AV1", "video/VP9", "video/VP8", "video/H264" }
                    .Where(codecs.Contains)
                    .ToArray();
                if (preferred.Length > 0)
                {
                    pc.SetCodecPreferences(MediaType.Video, preferred);
                }

                rtpSender.SetEncodingParameters(new RtpEncodingSettings
                {
                    MaxBitrateBps = maxBitrateKbps * 1000,
                    MaxFramerate = fps,
                    DegradationPreference = DegradationPreference.MaintainResolution,
                });
            }
            else
            {
                renderer = new SdlVideoRenderer("LumenRTC Remote Viewer", 1280, 720);
                renderer.Start();

                controlChannel = pc.CreateDataChannel("control");
                controlChannel.SetCallbacks(new DataChannelCallbacks
                {
                    OnStateChange = state =>
                    {
                        Console.WriteLine($"DataChannel state: {state}");
                        if (state == DataChannelState.Open)
                        {
                            var payload = Encoding.UTF8.GetBytes("hello from viewer");
                            controlChannel.Send(payload, true);
                        }
                    },
                    OnMessage = (data, _) =>
                    {
                        var text = Encoding.UTF8.GetString(data.Span);
                        Console.WriteLine($"Control message: {text}");
                    }
                });
            }

            async Task StartOfferAsync()
            {
                if (offerSent || pc == null)
                {
                    return;
                }

                offerSent = true;
                pc.CreateOffer(
                    (sdp, type) =>
                    {
                        pc.SetLocalDescription(sdp, type,
                            () => _ = SendAsync(new SignalMessage
                            {
                                Type = "offer",
                                Sdp = sdp,
                                SdpType = type
                            }),
                            err => Console.WriteLine(err));
                    },
                    err => Console.WriteLine(err));
            }

            async Task HandleMessageAsync(SignalMessage message)
            {
                if (pc == null)
                {
                    return;
                }

                switch (message.Type)
                {
                    case "ready":
                        if (role == "sender" && message.Role == "viewer")
                        {
                            await StartOfferAsync().ConfigureAwait(false);
                        }
                        break;
                    case "offer":
                        if (role == "viewer" && message.Sdp != null && message.SdpType != null)
                        {
                            pc.SetRemoteDescription(message.Sdp, message.SdpType,
                                () =>
                                {
                                    pc.CreateAnswer(
                                        (sdp, type) =>
                                        {
                                            pc.SetLocalDescription(sdp, type,
                                                () => _ = SendAsync(new SignalMessage
                                                {
                                                    Type = "answer",
                                                    Sdp = sdp,
                                                    SdpType = type
                                                }),
                                                err => Console.WriteLine(err));
                                        },
                                        err => Console.WriteLine(err));
                                },
                                err => Console.WriteLine(err));
                        }
                        break;
                    case "answer":
                        if (role == "sender" && message.Sdp != null && message.SdpType != null)
                        {
                            pc.SetRemoteDescription(message.Sdp, message.SdpType, () => { }, err => Console.WriteLine(err));
                        }
                        break;
                    case "candidate":
                        if (!string.IsNullOrWhiteSpace(message.Candidate) && !string.IsNullOrWhiteSpace(message.SdpMid))
                        {
                            pc.AddIceCandidate(message.SdpMid, message.SdpMLineIndex ?? 0, message.Candidate);
                        }
                        break;
                }
            }

            _ = SendAsync(new SignalMessage
            {
                Type = "ready",
                Role = role
            });

            var receiveTask = ReceiveLoopAsync(ws, message => HandleMessageAsync(message), cts.Token);

            Console.WriteLine("Press Ctrl+C to exit.");
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // exit
            }

            if (role == "sender")
            {
                capturer?.Stop();
            }

            rtpSender?.Dispose();
            videoTrack?.Dispose();
            videoSource?.Dispose();
            capturer?.Dispose();
            mediaSource?.Dispose();
            mediaList?.Dispose();
            desktopDevice?.Dispose();
            controlChannel?.Dispose();
            renderer?.Dispose();
            pc.Close();
            pc.Dispose();
            factory.Terminate();

            await receiveTask.ConfigureAwait(false);
        }
        finally
        {
            LumenRtc.Terminate();
        }
    }

    private static async Task ReceiveLoopAsync(
        ClientWebSocket socket,
        Func<SignalMessage, Task> onMessage,
        CancellationToken token)
    {
        var buffer = new byte[64 * 1024];
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var message = await ReceiveMessageAsync(socket, buffer, token).ConfigureAwait(false);
            if (message == null)
            {
                break;
            }

            SignalMessage? payload = null;
            try
            {
                payload = JsonSerializer.Deserialize<SignalMessage>(message, options);
            }
            catch
            {
                continue;
            }

            if (payload?.Type == null)
            {
                continue;
            }

            await onMessage(payload).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReceiveMessageAsync(ClientWebSocket socket, byte[] buffer, CancellationToken token)
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

    private static Uri BuildUri(string server, string room)
    {
        if (!server.StartsWith("ws", StringComparison.OrdinalIgnoreCase))
        {
            server = server.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
                           .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase);
        }
        if (!server.EndsWith("/"))
        {
            server += "/";
        }

        return new Uri($"{server}?room={Uri.EscapeDataString(room)}");
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
        return value.Equals("1") || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
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
    }
}
