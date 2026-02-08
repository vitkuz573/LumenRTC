using System;
using LumenRTC;
using LumenRTC.Rendering.Sdl;

namespace LumenRTC.Sample.LocalCamera.Core;

internal static class Program
{
    public static void Main()
    {
        using var session = CoreApi.CreateSession();

        const uint width = 1280;
        const uint height = 720;
        const uint fps = 30;

        using var videoDevice = session.Factory.GetVideoDevice();
        var deviceCount = videoDevice.NumberOfDevices();
        if (deviceCount == 0)
        {
            Console.WriteLine("No camera devices available.");
            return;
        }

        using var renderer = new SdlVideoRenderer("LumenRTC Camera (Core API)", (int)width, (int)height);

        var deviceInfo = videoDevice.GetDeviceName(0);
        using var capturer = videoDevice.CreateCapturer(deviceInfo.Name, 0, width, height, fps);
        if (!capturer.Start())
        {
            Console.WriteLine("Failed to start camera capture.");
            return;
        }

        using var source = session.Factory.CreateVideoSource(capturer, "camera");
        using var track = session.Factory.CreateVideoTrack(source, "camera0");

        track.AddSink(renderer.Sink);
        Console.WriteLine($"Capturing camera: {deviceInfo.Name}");
        Console.WriteLine("Close the SDL window to stop.");

        renderer.Run();
        capturer.Stop();
    }
}
