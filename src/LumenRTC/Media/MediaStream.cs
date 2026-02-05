namespace LumenRTC;

/// <summary>
/// Collection of audio and video tracks.
/// </summary>
public sealed class MediaStream : SafeHandle
{
    public string Id { get; }
    public string Label { get; }

    internal MediaStream(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
        Id = NativeString.GetString(handle, NativeMethods.lrtc_media_stream_get_id);
        Label = NativeString.GetString(handle, NativeMethods.lrtc_media_stream_get_label);
    }

    public bool AddAudioTrack(AudioTrack track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        return NativeMethods.lrtc_media_stream_add_audio_track(handle, track.DangerousGetHandle());
    }

    public bool AddVideoTrack(VideoTrack track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        return NativeMethods.lrtc_media_stream_add_video_track(handle, track.DangerousGetHandle());
    }

    public bool RemoveAudioTrack(AudioTrack track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        return NativeMethods.lrtc_media_stream_remove_audio_track(handle, track.DangerousGetHandle());
    }

    public bool RemoveVideoTrack(VideoTrack track)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        return NativeMethods.lrtc_media_stream_remove_video_track(handle, track.DangerousGetHandle());
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_media_stream_release(handle);
        return true;
    }
}
