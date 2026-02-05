namespace LumenRTC;

public sealed class VideoSource : SafeHandle
{
    internal VideoSource(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_source_release(handle);
        return true;
    }
}
