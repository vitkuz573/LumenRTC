namespace LumenRTC;

/// <summary>
/// Convenience wrapper for a local audio track and its source.
/// </summary>
public sealed class LocalAudioTrack : IDisposable
{
    private bool _disposed;
    private LocalTrackLifecycleState _lifecycleState;

    internal LocalAudioTrack(AudioSource source, AudioTrack track, string trackId, string sourceLabel)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Track = track ?? throw new ArgumentNullException(nameof(track));
        TrackId = trackId;
        SourceLabel = sourceLabel;
        _lifecycleState = track.Enabled
            ? LocalTrackLifecycleState.Running
            : LocalTrackLifecycleState.Stopped;
    }

    public AudioSource Source { get; }

    public AudioTrack Track { get; }

    public string TrackId { get; }

    public string SourceLabel { get; }

    public LocalTrackLifecycleState LifecycleState => _lifecycleState;

    public bool IsDisposed => _disposed;

    public bool IsRunning => _lifecycleState == LocalTrackLifecycleState.Running;

    public bool IsStopped => _lifecycleState == LocalTrackLifecycleState.Stopped;

    public bool IsEnabled
    {
        get
        {
            ThrowIfDisposed();
            return Track.Enabled;
        }
        set
        {
            ThrowIfDisposed();
            Track.Enabled = value;
            _lifecycleState = value ? LocalTrackLifecycleState.Running : LocalTrackLifecycleState.Stopped;
        }
    }

    public bool Start()
    {
        ThrowIfDisposed();
        if (_lifecycleState == LocalTrackLifecycleState.Running)
        {
            return true;
        }

        if (Track.IsEnded)
        {
            return false;
        }

        Track.Unmute();
        _lifecycleState = LocalTrackLifecycleState.Running;
        return true;
    }

    public bool TryStart(out string? error)
    {
        try
        {
            var started = Start();
            if (started)
            {
                error = null;
                return true;
            }

            error = "Audio track is ended and cannot be restarted.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Stop()
    {
        ThrowIfDisposed();
        Track.Mute();
        _lifecycleState = LocalTrackLifecycleState.Stopped;
    }

    public bool TryStop(out string? error)
    {
        try
        {
            Stop();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void Mute()
    {
        ThrowIfDisposed();
        Track.Mute();
        _lifecycleState = LocalTrackLifecycleState.Stopped;
    }

    public void Unmute()
    {
        ThrowIfDisposed();
        if (!Start())
        {
            throw new InvalidOperationException("Audio track is ended and cannot be unmuted.");
        }
    }

    public bool TrySetEnabled(bool enabled, out string? error)
    {
        ThrowIfDisposed();
        var ok = Track.TrySetEnabled(enabled, out error);
        if (ok)
        {
            _lifecycleState = enabled ? LocalTrackLifecycleState.Running : LocalTrackLifecycleState.Stopped;
        }

        return ok;
    }

    public bool TrySetVolume(double volume, out string? error)
    {
        ThrowIfDisposed();
        return Track.TrySetVolume(volume, out error);
    }

    public void SetNormalizedVolume(double normalizedVolume)
    {
        ThrowIfDisposed();
        Track.SetNormalizedVolume(normalizedVolume);
    }

    public bool TrySetNormalizedVolume(double normalizedVolume, out string? error)
    {
        ThrowIfDisposed();
        return Track.TrySetNormalizedVolume(normalizedVolume, out error);
    }

    public bool AddTo(PeerConnection peerConnection, IReadOnlyList<string>? streamIds = null)
    {
        ThrowIfDisposed();
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        return peerConnection.AddAudioTrack(Track, streamIds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifecycleState = LocalTrackLifecycleState.Disposed;
        Track.Dispose();
        Source.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
