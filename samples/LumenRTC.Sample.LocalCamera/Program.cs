using System;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.LocalCamera;

internal static class Program
{
    public static void Main()
    {
        LumenRtc.Initialize();

        try
        {
            using var factory = PeerConnectionFactory.Create();
            factory.Initialize();

            using var device = factory.GetVideoDevice();
            var count = device.NumberOfDevices();
            if (count == 0)
            {
                Console.WriteLine("No video capture devices found.");
                return;
            }

            var info = device.GetDeviceName(0);
            Console.WriteLine($"Using device: {info.Name} ({info.UniqueId})");

            const uint width = 1280;
            const uint height = 720;
            const uint fps = 30;

            using var capturer = device.CreateCapturer(info.Name, 0, width, height, fps);
            if (!capturer.Start())
            {
                Console.WriteLine("Failed to start capture.");
                return;
            }

            using var source = factory.CreateVideoSource(capturer, "camera");
            using var track = factory.CreateVideoTrack(source, "video0");
            using var renderer = new SdlVideoRenderer("LumenRTC Camera", (int)width, (int)height);

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
