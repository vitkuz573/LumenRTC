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
    }

    public VideoSource Source { get; }

    public VideoTrack Track { get; }

    public VideoCapturer? CameraCapturer { get; }

    public DesktopCapturer? DesktopCapturer { get; }

    public DesktopCaptureState? LastDesktopCaptureState { get; private set; }

    public string TrackId { get; }

    public string SourceLabel { get; }

    public bool IsCamera => CameraCapturer != null;

    public bool IsScreenShare => DesktopCapturer != null;

    public bool Start()
    {
        if (CameraCapturer != null)
        {
            return CameraCapturer.Start();
        }

        if (DesktopCapturer != null)
        {
            var state = DesktopCapturer.Start(_fps);
            LastDesktopCaptureState = state;
            return state == DesktopCaptureState.Running;
        }

        return false;
    }

    public void Stop()
    {
        if (CameraCapturer != null)
        {
            if (CameraCapturer.CaptureStarted())
            {
                CameraCapturer.Stop();
            }
            return;
        }

        if (DesktopCapturer != null && DesktopCapturer.IsRunning)
        {
            DesktopCapturer.Stop();
        }
    }

    public bool AddTo(PeerConnection peerConnection, IReadOnlyList<string>? streamIds = null)
    {
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        return peerConnection.AddVideoTrack(Track, streamIds);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        Track.Dispose();
        Source.Dispose();
        CameraCapturer?.Dispose();
        DesktopCapturer?.Dispose();
        _desktopSource?.Dispose();
        _desktopList?.Dispose();
        _desktopDevice?.Dispose();
        _videoDevice?.Dispose();
    }
}
