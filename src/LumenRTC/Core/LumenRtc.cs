namespace LumenRTC;

/// <summary>
/// Static entry point for initializing and terminating the native WebRTC runtime.
/// </summary>
public static class LumenRtc
{
    public static void Initialize()
    {
        var result = NativeMethods.lrtc_initialize();
        if (result != LrtcResult.Ok)
        {
            throw new InvalidOperationException($"LumenRTC init failed: {result}");
        }
    }

    public static void Terminate()
    {
        NativeMethods.lrtc_terminate();
    }
}
