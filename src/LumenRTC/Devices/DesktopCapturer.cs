namespace LumenRTC;

/// <summary>
/// Captures frames from desktop sources.
/// </summary>
public sealed partial class DesktopCapturer : SafeHandle
{
    internal DesktopCapturer(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public DesktopCaptureState Start(uint fps)
    {
        return (DesktopCaptureState)NativeMethods.lrtc_desktop_capturer_start(handle, fps);
    }

    public DesktopCaptureState Start(uint fps, uint x, uint y, uint width, uint height)
    {
        return (DesktopCaptureState)NativeMethods.lrtc_desktop_capturer_start_region(handle, fps, x, y, width, height);
    }

    public void Stop()
    {
        NativeMethods.lrtc_desktop_capturer_stop(handle);
    }

    public bool IsRunning => NativeMethods.lrtc_desktop_capturer_is_running(handle);
}
