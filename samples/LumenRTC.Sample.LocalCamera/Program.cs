using System;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.LocalCamera;

internal static class Program
{
    public static void Main()
    {
        using var rtc = RtcContext.Create();

        const uint width = 1280;
        const uint height = 720;
        const uint fps = 30;

        using var camera = rtc.CreateCameraTrack(new CameraTrackOptions
        {
            Width = width,
            Height = height,
            Fps = fps,
        });

        using var renderer = new SdlVideoRenderer("LumenRTC Camera", (int)width, (int)height);
        camera.Track.AddSink(renderer.Sink);

        Console.WriteLine("Close the SDL window to stop.");
        renderer.Run();
    }
}
