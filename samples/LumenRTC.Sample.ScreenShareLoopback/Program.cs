using System;
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

            PeerConnection? pc1 = null;
            PeerConnection? pc2 = null;

            pc1 = factory.CreatePeerConnection(new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    pc2?.AddIceCandidate(mid, mline, cand);
                }
            });

            pc2 = factory.CreatePeerConnection(new PeerConnectionCallbacks
            {
                OnIceCandidate = (mid, mline, cand) =>
                {
                    pc1?.AddIceCandidate(mid, mline, cand);
                },
                OnVideoTrack = track =>
                {
                    track.AddSink(renderer.Sink);
                }
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
                    pc1.SetLocalDescription(sdp, type, () => { }, err => Console.WriteLine(err));
                    pc2.SetRemoteDescription(sdp, type, () =>
                    {
                        pc2.CreateAnswer(
                            (answerSdp, answerType) =>
                            {
                                pc2.SetLocalDescription(answerSdp, answerType, () => { }, err => Console.WriteLine(err));
                                pc1.SetRemoteDescription(answerSdp, answerType, () => { }, err => Console.WriteLine(err));
                            },
                            err => Console.WriteLine(err));
                    }, err => Console.WriteLine(err));
                },
                err => Console.WriteLine(err));

            Console.WriteLine("Loopback running. Close the SDL window to stop.");
            renderer.Run();

            capturer.Stop();
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
