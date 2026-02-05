namespace LumenRTC;

/// <summary>
/// Receives video frames from a track.
/// </summary>
public sealed class VideoSink : SafeHandle
{
    private readonly VideoSinkCallbacks _callbacks;

    public VideoSink(VideoSinkCallbacks callbacks)
        : base(IntPtr.Zero, true)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        var handle = NativeMethods.lrtc_video_sink_create(ref native, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create video sink.");
        }
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_sink_release(handle);
        return true;
    }
}
