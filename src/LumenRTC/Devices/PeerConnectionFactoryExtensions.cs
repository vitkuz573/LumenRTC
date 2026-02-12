namespace LumenRTC;

/// <summary>
/// Convenience helpers for creating peer connections and local tracks.
/// </summary>
public static class PeerConnectionFactoryExtensions
{
    public static PeerConnectionBuilder CreatePeerConnectionBuilder(
        this PeerConnectionFactory factory,
        RtcConfiguration? defaultConfiguration = null,
        MediaConstraints? defaultConstraints = null)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        return new PeerConnectionBuilder(factory, defaultConfiguration, defaultConstraints);
    }

    public static PeerConnection CreatePeerConnection(
        this PeerConnectionFactory factory,
        Action<PeerConnectionBuilder> configure,
        RtcConfiguration? defaultConfiguration = null,
        MediaConstraints? defaultConstraints = null)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        var builder = factory.CreatePeerConnectionBuilder(defaultConfiguration, defaultConstraints);
        configure(builder);
        return builder.Build();
    }

    public static LocalAudioTrack CreateLocalAudioTrack(this PeerConnectionFactory factory, AudioTrackOptions? options = null)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        options ??= new AudioTrackOptions();

        var trackId = EnsureId(options.TrackId, "audio");
        var sourceLabel = string.IsNullOrWhiteSpace(options.SourceLabel) ? "audio" : options.SourceLabel;

        var source = factory.CreateAudioSource(sourceLabel, options.SourceType, options.AudioOptions);
        var track = factory.CreateAudioTrack(source, trackId);
        return new LocalAudioTrack(source, track, trackId, sourceLabel);
    }

    public static LocalVideoTrack CreateCameraTrack(this PeerConnectionFactory factory, CameraTrackOptions? options = null)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        options ??= new CameraTrackOptions();

        var trackId = EnsureId(options.TrackId, "video");
        var sourceLabel = string.IsNullOrWhiteSpace(options.SourceLabel) ? "camera" : options.SourceLabel;

        VideoDevice? device = null;
        VideoCapturer? capturer = null;
        VideoSource? source = null;
        VideoTrack? track = null;

        try
        {
            device = factory.GetVideoDevice();
            var (index, info) = ResolveVideoDevice(device, options);

            capturer = device.CreateCapturer(info.Name, index, options.Width, options.Height, options.Fps);
            source = factory.CreateVideoSource(capturer, sourceLabel, options.Constraints);
            track = factory.CreateVideoTrack(source, trackId);

            var localTrack = new LocalVideoTrack(device, capturer, source, track, options.Fps, trackId, sourceLabel);
            device = null;
            capturer = null;
            source = null;
            track = null;

            if (options.AutoStart && !localTrack.Start())
            {
                localTrack.Dispose();
                throw new InvalidOperationException("Failed to start camera capture.");
            }

            return localTrack;
        }
        catch
        {
            track?.Dispose();
            source?.Dispose();
            capturer?.Dispose();
            device?.Dispose();
            throw;
        }
    }

    public static LocalVideoTrack CreateScreenTrack(this PeerConnectionFactory factory, ScreenTrackOptions? options = null)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        options ??= new ScreenTrackOptions();

        var trackId = EnsureId(options.TrackId, "screen");
        var sourceLabel = string.IsNullOrWhiteSpace(options.SourceLabel) ? "screen" : options.SourceLabel;

        DesktopDevice? device = null;
        DesktopMediaList? list = null;
        MediaSource? mediaSource = null;
        DesktopCapturer? capturer = null;
        VideoSource? source = null;
        VideoTrack? track = null;

        try
        {
            device = factory.GetDesktopDevice();
            list = device.GetMediaList(options.Type);
            list.UpdateSourceList(options.ForceReload, options.GetThumbnail);

            if (list.SourceCount == 0)
            {
                throw new InvalidOperationException("No desktop capture sources found.");
            }

            if (options.SourceIndex < 0 || options.SourceIndex >= list.SourceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(options.SourceIndex),
                    $"Desktop source index {options.SourceIndex} is out of range. Available sources: {list.SourceCount}.");
            }

            mediaSource = list.GetSource(options.SourceIndex);
            capturer = device.CreateCapturer(mediaSource, options.ShowCursor);

            // Keep the desktop capture startup order aligned with the known-good low-level path:
            // CreateCapturer -> Start -> CreateDesktopSource -> CreateVideoTrack.
            // This avoids sessions where the capturer reports Running but no frames are produced.
            if (options.AutoStart)
            {
                var state = capturer.Start(options.Fps);
                if (state != DesktopCaptureState.Running)
                {
                    throw new InvalidOperationException($"Failed to start desktop capture ({state}).");
                }
            }

            source = factory.CreateDesktopSource(capturer, sourceLabel, options.Constraints);
            track = factory.CreateVideoTrack(source, trackId);

            var localTrack = new LocalVideoTrack(device, list, mediaSource, capturer, source, track, options.Fps, trackId, sourceLabel);
            device = null;
            list = null;
            mediaSource = null;
            capturer = null;
            source = null;
            track = null;

            return localTrack;
        }
        catch
        {
            track?.Dispose();
            source?.Dispose();
            capturer?.Dispose();
            mediaSource?.Dispose();
            list?.Dispose();
            device?.Dispose();
            throw;
        }
    }

    private static (uint Index, VideoDeviceInfo Info) ResolveVideoDevice(VideoDevice device, CameraTrackOptions options)
    {
        var count = device.NumberOfDevices();
        if (count == 0)
        {
            throw new InvalidOperationException("No video capture devices found.");
        }

        if (!string.IsNullOrWhiteSpace(options.DeviceName))
        {
            for (uint i = 0; i < count; i++)
            {
                var info = device.GetDeviceName(i);
                if (Matches(info, options.DeviceName))
                {
                    return (i, info);
                }
            }

            throw new InvalidOperationException($"Video device '{options.DeviceName}' not found.");
        }

        var index = options.DeviceIndex.HasValue ? (uint)options.DeviceIndex.Value : 0;
        if (index >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(options.DeviceIndex),
                $"Video device index {index} is out of range. Available devices: {count}.");
        }

        return (index, device.GetDeviceName(index));
    }

    private static bool Matches(VideoDeviceInfo info, string name)
    {
        return string.Equals(info.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(info.UniqueId, name, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureId(string? id, string prefix)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        return $"{prefix}-{Guid.NewGuid():N}";
    }
}
