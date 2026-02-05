using System;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.ScreenShare;

internal static class Program
{
    public static void Main()
    {
        LumenRtc.Initialize();

        try
        {
            using var factory = PeerConnectionFactory.Create();
            factory.Initialize();

            using var desktop = factory.GetDesktopDevice();
            using var list = desktop.GetMediaList(DesktopType.Screen);

            var updateResult = list.UpdateSourceList(forceReload: true, getThumbnail: false);
            if (updateResult != 0)
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
            using var renderer = new SdlVideoRenderer("LumenRTC Screen Share", 1280, 720);

            track.AddSink(renderer.Sink);
            Console.WriteLine("Close the SDL window to stop.");

            renderer.Run();

            capturer.Stop();
            factory.Terminate();
        }
        finally
        {
            LumenRtc.Terminate();
        }
    }
}
