namespace LumenRTC;

/// <summary>
/// Convenience wrapper for a local video track and its capture pipeline.
/// </summary>
public sealed class LocalVideoTrack : IDisposable
{
    private readonly VideoDevice? _videoDevice;
    private readonly DesktopDevice? _desktopDevice;
    private readonly DesktopMediaList? _desktopList;
    private readonly MediaSource? _desktopSource;
    private readonly uint _fps;
    private bool _disposed;
    private LocalTrackLifecycleState _lifecycleState;

    internal LocalVideoTrack(
        VideoDevice device,
        VideoCapturer capturer,
        VideoSource source,
        VideoTrack track,
        uint fps,
        string trackId,
        string sourceLabel)
    {
        _videoDevice = device ?? throw new ArgumentNullException(nameof(device));
        CameraCapturer = capturer ?? throw new ArgumentNullException(nameof(capturer));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Track = track ?? throw new ArgumentNullException(nameof(track));
        _fps = fps;
        TrackId = trackId;
        SourceLabel = sourceLabel;
        _lifecycleState = capturer.CaptureStarted()
            ? LocalTrackLifecycleState.Running
            : LocalTrackLifecycleState.Created;
    }

    internal LocalVideoTrack(
        DesktopDevice device,
        DesktopMediaList list,
        MediaSource mediaSource,
        DesktopCapturer capturer,
        VideoSource source,
        VideoTrack track,
        uint fps,
        string trackId,
        string sourceLabel)
    {
        _desktopDevice = device ?? throw new ArgumentNullException(nameof(device));
        _desktopList = list ?? throw new ArgumentNullException(nameof(list));
        _desktopSource = mediaSource ?? throw new ArgumentNullException(nameof(mediaSource));
        DesktopCapturer = capturer ?? throw new ArgumentNullException(nameof(capturer));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Track = track ?? throw new ArgumentNullException(nameof(track));
        _fps = fps;
        TrackId = trackId;
        SourceLabel = sourceLabel;
        _lifecycleState = capturer.IsRunning
            ? LocalTrackLifecycleState.Running
            : LocalTrackLifecycleState.Created;
        LastDesktopCaptureState = capturer.IsRunning
            ? DesktopCaptureState.Running
            : DesktopCaptureState.Stopped;
    }

    public VideoSource Source { get; }

    public VideoTrack Track { get; }

    public VideoCapturer? CameraCapturer { get; }

    public DesktopCapturer? DesktopCapturer { get; }

    public DesktopCaptureState? LastDesktopCaptureState { get; private set; }

    public string TrackId { get; }

    public string SourceLabel { get; }

    public LocalTrackLifecycleState LifecycleState => _lifecycleState;

    public bool IsDisposed => _disposed;

    public bool IsRunning => _lifecycleState == LocalTrackLifecycleState.Running;

    public bool IsStopped => _lifecycleState == LocalTrackLifecycleState.Stopped;

    public bool IsCamera => CameraCapturer != null;

    public bool IsScreenShare => DesktopCapturer != null;

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
        }
    }

    public void Mute()
    {
        ThrowIfDisposed();
        Track.Mute();
    }

    public void Unmute()
    {
        ThrowIfDisposed();
        Track.Unmute();
    }

    public bool TrySetEnabled(bool enabled, out string? error)
    {
        ThrowIfDisposed();
        return Track.TrySetEnabled(enabled, out error);
    }

    public bool Start()
    {
        ThrowIfDisposed();
        if (_lifecycleState == LocalTrackLifecycleState.Running)
        {
            return true;
        }

        var started = StartCore();
        _lifecycleState = started
            ? LocalTrackLifecycleState.Running
            : LocalTrackLifecycleState.Stopped;
        return started;
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

            error = "Failed to start local video capture.";
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
        StopCore();
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

    public bool AddTo(PeerConnection peerConnection, IReadOnlyList<string>? streamIds = null)
    {
        ThrowIfDisposed();
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        return peerConnection.AddVideoTrack(Track, streamIds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            StopCore();
        }
        catch
        {
        }

        _disposed = true;
        _lifecycleState = LocalTrackLifecycleState.Disposed;

        Track.Dispose();
        Source.Dispose();
        CameraCapturer?.Dispose();
        DesktopCapturer?.Dispose();
        _desktopSource?.Dispose();
        _desktopList?.Dispose();
        _desktopDevice?.Dispose();
        _videoDevice?.Dispose();
    }

    private bool StartCore()
    {
        if (CameraCapturer != null)
        {
            if (CameraCapturer.CaptureStarted())
            {
                return true;
            }

            return CameraCapturer.Start();
        }

        if (DesktopCapturer != null)
        {
            if (DesktopCapturer.IsRunning)
            {
                LastDesktopCaptureState = DesktopCaptureState.Running;
                return true;
            }

            var state = DesktopCapturer.Start(_fps);
            LastDesktopCaptureState = state;
            return state == DesktopCaptureState.Running;
        }

        return false;
    }

    private void StopCore()
    {
        if (CameraCapturer != null)
        {
            if (CameraCapturer.CaptureStarted())
            {
                CameraCapturer.Stop();
            }

            _lifecycleState = LocalTrackLifecycleState.Stopped;
            return;
        }

        if (DesktopCapturer != null)
        {
            if (DesktopCapturer.IsRunning)
            {
                DesktopCapturer.Stop();
            }

            LastDesktopCaptureState = DesktopCaptureState.Stopped;
            _lifecycleState = LocalTrackLifecycleState.Stopped;
            return;
        }

        _lifecycleState = LocalTrackLifecycleState.Stopped;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
