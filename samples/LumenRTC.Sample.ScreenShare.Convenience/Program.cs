using System;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.ScreenShare.Convenience;

internal static class Program
{
    public static void Main()
    {
        try
        {
            using var rtc = ConvenienceApi.CreateContext();
            using var track = rtc.CreateScreenTrack(new ScreenTrackOptions
            {
                Type = DesktopType.Screen,
                SourceIndex = 0,
                Fps = 30,
                ShowCursor = true,
            });

            using var renderer = new SdlVideoRenderer("LumenRTC Screen Share (Convenience API)", 1280, 720);
            track.Track.AddSink(renderer.Sink);

            Console.WriteLine("Capturing screen index 0.");
            Console.WriteLine("Close the SDL window to stop.");
            renderer.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Screen capture failed: {ex.Message}");
        }
    }
}
