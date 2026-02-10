namespace LumenRTC;

/// <summary>
/// Local or remote audio track.
/// </summary>
public sealed partial class AudioTrack : SafeHandle
{
    internal AudioTrack(IntPtr handle) : base(IntPtr.Zero, true)
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

    public bool TrySetVolume(double volume, out string? error)
    {
        if (double.IsNaN(volume) || double.IsInfinity(volume) || volume < 0)
        {
            error = "Volume must be a finite non-negative number.";
            return false;
        }

        try
        {
            SetVolume(volume);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void SetNormalizedVolume(double normalizedVolume)
    {
        if (double.IsNaN(normalizedVolume) || double.IsInfinity(normalizedVolume))
        {
            throw new ArgumentOutOfRangeException(nameof(normalizedVolume), "Volume must be a finite number.");
        }

        var clamped = Math.Clamp(normalizedVolume, 0.0, 1.0);
        SetVolume(clamped);
    }

    public bool TrySetNormalizedVolume(double normalizedVolume, out string? error)
    {
        if (double.IsNaN(normalizedVolume) || double.IsInfinity(normalizedVolume))
        {
            error = "Normalized volume must be a finite number.";
            return false;
        }

        return TrySetVolume(Math.Clamp(normalizedVolume, 0.0, 1.0), out error);
    }

    public bool TryAddSink(AudioSink sink, out string? error)
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

    public bool TryRemoveSink(AudioSink sink, out string? error)
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
