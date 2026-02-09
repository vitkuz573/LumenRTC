namespace LumenRTC;

/// <summary>
/// Captures frames from a camera device.
/// </summary>
public sealed partial class VideoCapturer : SafeHandle
{
    internal VideoCapturer(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public bool Start() => NativeMethods.lrtc_video_capturer_start(handle);

    public bool CaptureStarted() => NativeMethods.lrtc_video_capturer_capture_started(handle);

    public void Stop() => NativeMethods.lrtc_video_capturer_stop(handle);
}
