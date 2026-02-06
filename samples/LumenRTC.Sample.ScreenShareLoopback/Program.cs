using System;
using System.Collections.Generic;
using System.Linq;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.ScreenShareLoopback;

internal static class Program
{
    public static void Main()
    {
        LumenRtc.Initialize();

        try
        {
            using var factory = PeerConnectionFactory.Create();
            factory.Initialize();

            using var renderer = new SdlVideoRenderer("LumenRTC Screen Share (Loopback)", 1280, 720);
            VideoTrack? remoteVideoTrack = null;
            var iceSync = new object();
            var pendingForPc1 = new List<(string Mid, int Mline, string Candidate)>();
            var pendingForPc2 = new List<(string Mid, int Mline, string Candidate)>();
            var pc1CanApplyRemoteCandidates = false;
            var pc2CanApplyRemoteCandidates = false;

            PeerConnection? pc1 = null;
            PeerConnection? pc2 = null;

            void AttachRemoteTrack(VideoTrack track)
            {
                try
                {
                    if (remoteVideoTrack != null && remoteVideoTrack.Id == track.Id)
                    {
                        track.Dispose();
                        return;
                    }
                }
                catch
                {
                    // Ignore id lookup failures and continue with latest track.
                }

                remoteVideoTrack?.Dispose();
                remoteVideoTrack = track;
                remoteVideoTrack.AddSink(renderer.Sink);

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
                    }
                }

                if (forwardNow)
                {
                    pc1?.AddIceCandidate(mid, mline, candidate);
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
                    }
                }

                if (forwardNow)
                {
                    pc2?.AddIceCandidate(mid, mline, candidate);
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

                foreach (var candidate in pending)
                {
                    pc1?.AddIceCandidate(candidate.Mid, candidate.Mline, candidate.Candidate);
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

                foreach (var candidate in pending)
                {
                    pc2?.AddIceCandidate(candidate.Mid, candidate.Mline, candidate.Candidate);
                }
            }

            pc1 = factory.CreatePeerConnection(new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    QueueOrForwardToPc2(mid, mline, cand);
                },
                OnPeerConnectionState = state => Console.WriteLine($"pc1 state: {state}"),
                OnIceConnectionState = state => Console.WriteLine($"pc1 ice: {state}"),
            });

            pc2 = factory.CreatePeerConnection(new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    QueueOrForwardToPc1(mid, mline, cand);
                },
                OnVideoTrack = track =>
                {
                    AttachRemoteTrack(track);
                },
                OnTrack = (transceiver, receiver) =>
                {
                    try
                    {
                        if (receiver.MediaType == MediaType.Video)
                        {
                            var track = receiver.VideoTrack;
                            if (track != null)
                            {
                                AttachRemoteTrack(track);
                            }
                        }
                    }
                    finally
                    {
                        transceiver.Dispose();
                        receiver.Dispose();
                    }
                },
                OnPeerConnectionState = state => Console.WriteLine($"pc2 state: {state}"),
                OnIceConnectionState = state => Console.WriteLine($"pc2 ice: {state}"),
            });

            // Desktop capture
            using var desktop = factory.GetDesktopDevice();
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

            using var source = list.GetSource(0);
            Console.WriteLine($"Capturing screen: {source.Name}");

            using var capturer = desktop.CreateCapturer(source, showCursor: true);
            var state = capturer.Start(30);
            if (state != DesktopCaptureState.Running)
            {
                Console.WriteLine($"Failed to start desktop capture: {state}");
                return;
            }

            using var videoSource = factory.CreateDesktopSource(capturer, "screen");
            using var track = factory.CreateVideoTrack(videoSource, "screen0");

            var sender = pc1.AddVideoTrackSender(track, new[] { "stream0" });

            var codecs = factory.GetRtpSenderCodecMimeTypes(MediaType.Video);
            var preferred = new[] { "video/AV1", "video/VP9", "video/VP8", "video/H264" }
                .Where(codecs.Contains)
                .ToArray();
            if (preferred.Length > 0)
            {
                pc1.SetCodecPreferences(MediaType.Video, preferred);
            }

            sender.SetEncodingParameters(new RtpEncodingSettings
            {
                MaxBitrateBps = 3_000_000,
                MaxFramerate = 30,
                DegradationPreference = DegradationPreference.MaintainResolution,
            });

            pc1.CreateOffer(
                (sdp, type) =>
                {
                    pc1.SetLocalDescription(
                        sdp,
                        type,
                        () =>
                        {
                            pc2.SetRemoteDescription(
                                sdp,
                                type,
                                () =>
                                {
                                    pc2.CreateAnswer(
                                        (answerSdp, answerType) =>
                                        {
                                            pc2.SetLocalDescription(
                                                answerSdp,
                                                answerType,
                                                () =>
                                                {
                                                    // Apply queued candidates only after pc2 has both local
                                                    // and remote descriptions, otherwise some candidates may
                                                    // be rejected and lost.
                                                    MarkPc2ReadyForRemoteCandidatesAndFlush();
                                                    pc1.SetRemoteDescription(
                                                        answerSdp,
                                                        answerType,
                                                        () =>
                                                        {
                                                            MarkPc1ReadyForRemoteCandidatesAndFlush();
                                                        },
                                                        err => Console.WriteLine(err));
                                                },
                                                err => Console.WriteLine(err));
                                        },
                                        err => Console.WriteLine(err));
                                },
                                err => Console.WriteLine(err));
                        },
                        err => Console.WriteLine(err));
                },
                err => Console.WriteLine(err));

            Console.WriteLine("Loopback running. Close the SDL window to stop.");
            renderer.Run();

            capturer.Stop();
            remoteVideoTrack?.Dispose();
            pc1?.Close();
            pc2?.Close();
            pc1?.Dispose();
            pc2?.Dispose();
            factory.Terminate();
        }
        finally
        {
            LumenRtc.Terminate();
        }
    }
}
