namespace LumenRTC;

/// <summary>
/// Creates peer connections, tracks, and device sources.
/// </summary>
public sealed class PeerConnectionFactory : SafeHandle
{
    private static readonly JsonSerializerOptions s_rtpCapabilitiesJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private PeerConnectionFactory() : base(IntPtr.Zero, true) { }

    public static PeerConnectionFactory Create()
    {
        var handle = NativeMethods.lrtc_factory_create();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create peer connection factory.");
        }
        var factory = new PeerConnectionFactory();
        factory.SetHandle(handle);
        return factory;
    }

    public void Initialize()
    {
        var result = NativeMethods.lrtc_factory_initialize(handle);
        if (result != LrtcResult.Ok)
        {
            throw new InvalidOperationException($"Factory initialize failed: {result}");
        }
    }

    public void Terminate()
    {
        NativeMethods.lrtc_factory_terminate(handle);
    }

    public PeerConnection CreatePeerConnection(PeerConnectionCallbacks callbacks, RtcConfiguration? config = null, MediaConstraints? constraints = null)
    {
        if (callbacks == null)
        {
            throw new ArgumentNullException(nameof(callbacks));
        }
        // Create with empty callbacks first to avoid reverse-P/Invoke during native
        // registration. We apply managed callbacks in a second step.
        var emptyCallbacks = default(LrtcPeerConnectionCallbacks);
        using var configMarshaler = config != null ? new RtcConfigurationMarshaler(config) : null;
        var configPtr = configMarshaler?.Pointer ?? IntPtr.Zero;
        var constraintsPtr = constraints?.DangerousGetHandle() ?? IntPtr.Zero;
        var pcHandle = NativeMethods.lrtc_peer_connection_create(
            handle, configPtr, constraintsPtr, ref emptyCallbacks, IntPtr.Zero);
        if (pcHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create peer connection.");
        }

        var native = callbacks.BuildNative();
        NativeMethods.lrtc_peer_connection_set_callbacks(pcHandle, ref native, IntPtr.Zero);
        return new PeerConnection(pcHandle, callbacks);
    }

    public AudioDevice GetAudioDevice()
    {
        var device = NativeMethods.lrtc_factory_get_audio_device(handle);
        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get audio device.");
        }
        return new AudioDevice(device);
    }

    public VideoDevice GetVideoDevice()
    {
        var device = NativeMethods.lrtc_factory_get_video_device(handle);
        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get video device.");
        }
        return new VideoDevice(device);
    }

    public DesktopDevice GetDesktopDevice()
    {
        var device = NativeMethods.lrtc_factory_get_desktop_device(handle);
        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Desktop capture is not available.");
        }
        return new DesktopDevice(device);
    }

    public AudioSource CreateAudioSource(string label, AudioSourceType sourceType = AudioSourceType.Microphone, AudioOptions? options = null)
    {
        using var labelUtf8 = new Utf8String(label);
        var nativeOptions = new LrtcAudioOptions
        {
            echo_cancellation = options?.EchoCancellation ?? true,
            auto_gain_control = options?.AutoGainControl ?? true,
            noise_suppression = options?.NoiseSuppression ?? true,
            highpass_filter = options?.HighpassFilter ?? false,
        };
        var source = NativeMethods.lrtc_factory_create_audio_source(handle, labelUtf8.Pointer, (LrtcAudioSourceType)sourceType, ref nativeOptions);
        if (source == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create audio source.");
        }
        return new AudioSource(source);
    }

    public VideoSource CreateVideoSource(VideoCapturer capturer, string label, MediaConstraints? constraints = null)
    {
        if (capturer == null) throw new ArgumentNullException(nameof(capturer));
        using var labelUtf8 = new Utf8String(label);
        var source = NativeMethods.lrtc_factory_create_video_source(handle, capturer.DangerousGetHandle(), labelUtf8.Pointer, constraints?.DangerousGetHandle() ?? IntPtr.Zero);
        if (source == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create video source.");
        }
        return new VideoSource(source);
    }

    public VideoSource CreateDesktopSource(DesktopCapturer capturer, string label, MediaConstraints? constraints = null)
    {
        if (capturer == null) throw new ArgumentNullException(nameof(capturer));
        using var labelUtf8 = new Utf8String(label);
        var source = NativeMethods.lrtc_factory_create_desktop_source(handle, capturer.DangerousGetHandle(), labelUtf8.Pointer, constraints?.DangerousGetHandle() ?? IntPtr.Zero);
        if (source == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create desktop source.");
        }
        return new VideoSource(source);
    }

    public AudioTrack CreateAudioTrack(AudioSource source, string trackId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        using var trackUtf8 = new Utf8String(trackId);
        var track = NativeMethods.lrtc_factory_create_audio_track(handle, source.DangerousGetHandle(), trackUtf8.Pointer);
        if (track == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create audio track.");
        }
        return new AudioTrack(track);
    }

    public VideoTrack CreateVideoTrack(VideoSource source, string trackId)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        using var trackUtf8 = new Utf8String(trackId);
        var track = NativeMethods.lrtc_factory_create_video_track(handle, source.DangerousGetHandle(), trackUtf8.Pointer);
        if (track == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create video track.");
        }
        return new VideoTrack(track);
    }

    public MediaStream CreateStream(string streamId)
    {
        using var streamUtf8 = new Utf8String(streamId);
        var stream = NativeMethods.lrtc_factory_create_stream(handle, streamUtf8.Pointer);
        if (stream == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create media stream.");
        }
        return new MediaStream(stream);
    }

    public IReadOnlyList<string> GetRtpSenderCodecMimeTypes(MediaType mediaType)
    {
        string? json = null;
        string? error = null;
        LrtcStatsSuccessCb success = (_, jsonPtr) => json = Utf8String.Read(jsonPtr);
        LrtcStatsErrorCb failure = (_, errPtr) => error = Utf8String.Read(errPtr);

        NativeMethods.lrtc_factory_get_rtp_sender_codec_mime_types(handle, (LrtcMediaType)mediaType, success, failure, IntPtr.Zero);

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
    }

    public RtpCapabilities GetRtpSenderCapabilities(MediaType mediaType)
    {
        string? json = null;
        string? error = null;
        LrtcStatsSuccessCb success = (_, jsonPtr) => json = Utf8String.Read(jsonPtr);
        LrtcStatsErrorCb failure = (_, errPtr) => error = Utf8String.Read(errPtr);

        NativeMethods.lrtc_factory_get_rtp_sender_capabilities(
            handle,
            (LrtcMediaType)mediaType,
            success,
            failure,
            IntPtr.Zero);

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }

        return ParseRtpCapabilities(json);
    }

    public RtpCapabilities GetRtpReceiverCapabilities(MediaType mediaType)
    {
        string? json = null;
        string? error = null;
        LrtcStatsSuccessCb success = (_, jsonPtr) => json = Utf8String.Read(jsonPtr);
        LrtcStatsErrorCb failure = (_, errPtr) => error = Utf8String.Read(errPtr);

        NativeMethods.lrtc_factory_get_rtp_receiver_capabilities(
            handle,
            (LrtcMediaType)mediaType,
            success,
            failure,
            IntPtr.Zero);

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }

        return ParseRtpCapabilities(json);
    }

    private static RtpCapabilities ParseRtpCapabilities(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return RtpCapabilities.Empty;
        }

        var dto = JsonSerializer.Deserialize<RtpCapabilitiesDto>(json, s_rtpCapabilitiesJsonOptions);
        if (dto == null)
        {
            return RtpCapabilities.Empty;
        }

        var codecs = new List<RtpCodecCapability>();
        if (dto.Codecs != null)
        {
            foreach (var codec in dto.Codecs)
            {
                if (codec == null)
                {
                    continue;
                }
                codecs.Add(new RtpCodecCapability(
                    codec.MimeType ?? string.Empty,
                    codec.ClockRate,
                    codec.Channels,
                    codec.SdpFmtpLine ?? string.Empty));
            }
        }

        var extensions = new List<RtpHeaderExtensionCapability>();
        if (dto.HeaderExtensions != null)
        {
            foreach (var ext in dto.HeaderExtensions)
            {
                if (ext == null)
                {
                    continue;
                }
                extensions.Add(new RtpHeaderExtensionCapability(
                    ext.Uri ?? string.Empty,
                    ext.PreferredId,
                    ext.PreferredEncrypt));
            }
        }

        return new RtpCapabilities(codecs, extensions);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_factory_release(handle);
        return true;
    }
}

public readonly record struct AudioDeviceInfo(string Name, string Guid);
