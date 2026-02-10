namespace LumenRTC;

/// <summary>
/// Collection of audio and video tracks.
/// </summary>
public sealed partial class MediaStream : SafeHandle
{
    public string Id { get; }
    public string Label { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(Label) ? Id : Label;

    internal MediaStream(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
        Id = NativeString.GetString(handle, NativeMethods.lrtc_media_stream_get_id);
        Label = NativeString.GetString(handle, NativeMethods.lrtc_media_stream_get_label);
    }

    public bool TryAddTrack(AudioTrack track, out string? error)
    {
        if (track == null)
        {
            error = "Track cannot be null.";
            return false;
        }

        try
        {
            var added = AddAudioTrack(track);
            error = added ? null : "Failed to add audio track to media stream.";
            return added;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryAddTrack(VideoTrack track, out string? error)
    {
        if (track == null)
        {
            error = "Track cannot be null.";
            return false;
        }

        try
        {
            var added = AddVideoTrack(track);
            error = added ? null : "Failed to add video track to media stream.";
            return added;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryRemoveTrack(AudioTrack track, out string? error)
    {
        if (track == null)
        {
            error = "Track cannot be null.";
            return false;
        }

        try
        {
            var removed = RemoveAudioTrack(track);
            error = removed ? null : "Failed to remove audio track from media stream.";
            return removed;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryRemoveTrack(VideoTrack track, out string? error)
    {
        if (track == null)
        {
            error = "Track cannot be null.";
            return false;
        }

        try
        {
            var removed = RemoveVideoTrack(track);
            error = removed ? null : "Failed to remove video track from media stream.";
            return removed;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
