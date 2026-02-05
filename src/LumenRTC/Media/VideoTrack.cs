namespace LumenRTC;

public sealed class VideoTrack : SafeHandle
{
    internal VideoTrack(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public string Id => NativeString.GetString(handle, NativeMethods.lrtc_video_track_get_id);

    public TrackState State
    {
        get
        {
            var value = NativeMethods.lrtc_video_track_get_state(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get video track state.");
            }
            return (TrackState)value;
        }
    }

    public bool Enabled
    {
        get => NativeMethods.lrtc_video_track_get_enabled(handle) != 0;
        set
        {
            var result = NativeMethods.lrtc_video_track_set_enabled(handle, value ? 1 : 0);
            if (result == 0)
            {
                throw new InvalidOperationException("Failed to set video track enabled state.");
            }
        }
    }

    public void AddSink(VideoSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        NativeMethods.lrtc_video_track_add_sink(handle, sink.DangerousGetHandle());
    }

    public void RemoveSink(VideoSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        NativeMethods.lrtc_video_track_remove_sink(handle, sink.DangerousGetHandle());
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_track_release(handle);
        return true;
    }
}
