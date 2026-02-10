namespace LumenRTC;

/// <summary>
/// Local or remote video track.
/// </summary>
public sealed partial class VideoTrack : SafeHandle
{
    internal VideoTrack(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public bool IsLive => State == TrackState.Live;

    public bool IsEnded => State == TrackState.Ended;

    public void Mute()
    {
        Enabled = false;
    }

    public void Unmute()
    {
        Enabled = true;
    }

    public bool TrySetEnabled(bool enabled, out string? error)
    {
        try
        {
            Enabled = enabled;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryAddSink(VideoSink sink, out string? error)
    {
        if (sink == null)
        {
            error = "Sink cannot be null.";
            return false;
        }

        try
        {
            AddSink(sink);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryRemoveSink(VideoSink sink, out string? error)
    {
        if (sink == null)
        {
            error = "Sink cannot be null.";
            return false;
        }

        try
        {
            RemoveSink(sink);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
