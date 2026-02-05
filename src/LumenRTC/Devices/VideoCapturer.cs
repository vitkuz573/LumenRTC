namespace LumenRTC;

public sealed class VideoCapturer : SafeHandle
{
    internal VideoCapturer(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public bool Start() => NativeMethods.lrtc_video_capturer_start(handle);

    public bool CaptureStarted() => NativeMethods.lrtc_video_capturer_capture_started(handle);

    public void Stop() => NativeMethods.lrtc_video_capturer_stop(handle);

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_capturer_release(handle);
        return true;
    }
}
