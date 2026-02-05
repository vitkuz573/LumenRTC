using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LumenRTC.Interop;

namespace LumenRTC;

public static class LumenRtc
{
    public static void Initialize()
    {
        var result = NativeMethods.lrtc_initialize();
        if (result != LrtcResult.Ok)
        {
            throw new InvalidOperationException($"LumenRTC init failed: {result}");
        }
    }

    public static void Terminate()
    {
        NativeMethods.lrtc_terminate();
    }
}

public enum PeerConnectionState
{
    New = 0,
    Connecting = 1,
    Connected = 2,
    Disconnected = 3,
    Failed = 4,
    Closed = 5,
}

public enum SignalingState
{
    Stable = 0,
    HaveLocalOffer = 1,
    HaveRemoteOffer = 2,
    HaveLocalPrAnswer = 3,
    HaveRemotePrAnswer = 4,
    Closed = 5,
}

public enum IceGatheringState
{
    New = 0,
    Gathering = 1,
    Complete = 2,
}

public enum IceConnectionState
{
    New = 0,
    Checking = 1,
    Completed = 2,
    Connected = 3,
    Failed = 4,
    Disconnected = 5,
    Closed = 6,
    Max = 7,
}

public enum DataChannelState
{
    Connecting = 0,
    Open = 1,
    Closing = 2,
    Closed = 3,
}

public enum MediaType
{
    Audio = 0,
    Video = 1,
    Data = 2,
}

public enum IceTransportsType
{
    None = 0,
    Relay = 1,
    NoHost = 2,
    All = 3,
}

public enum BundlePolicy
{
    Balanced = 0,
    MaxBundle = 1,
    MaxCompat = 2,
}

public enum RtcpMuxPolicy
{
    Negotiate = 0,
    Require = 1,
}

public enum CandidateNetworkPolicy
{
    All = 0,
    LowCost = 1,
}

public enum TcpCandidatePolicy
{
    Enabled = 0,
    Disabled = 1,
}

public enum MediaSecurityType
{
    SrtpNone = 0,
    SdesSrtp = 1,
    DtlsSrtp = 2,
}

public enum SdpSemantics
{
    PlanB = 0,
    UnifiedPlan = 1,
}

public enum AudioSourceType
{
    Microphone = 0,
    Custom = 1,
}

public enum DesktopType
{
    Screen = 0,
    Window = 1,
}

public enum DesktopCaptureState
{
    Running = 0,
    Stopped = 1,
    Failed = 2,
}

public enum DegradationPreference
{
    Disabled = 0,
    MaintainFramerate = 1,
    MaintainResolution = 2,
    Balanced = 3,
}

public enum RtpTransceiverDirection
{
    SendRecv = 0,
    SendOnly = 1,
    RecvOnly = 2,
    Inactive = 3,
    Stopped = 4,
}

public enum VideoFrameFormat
{
    Argb = 0,
    Bgra = 1,
    Abgr = 2,
    Rgba = 3,
}

public sealed class IceServer
{
    public IceServer(string uri, string? username = null, string? password = null)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Username = username;
        Password = password;
    }

    public string Uri { get; }
    public string? Username { get; }
    public string? Password { get; }
}

public sealed class RtcConfiguration
{
    public List<IceServer> IceServers { get; } = new();

    public IceTransportsType IceTransportsType { get; set; } = IceTransportsType.All;
    public BundlePolicy BundlePolicy { get; set; } = BundlePolicy.Balanced;
    public RtcpMuxPolicy RtcpMuxPolicy { get; set; } = RtcpMuxPolicy.Require;
    public CandidateNetworkPolicy CandidateNetworkPolicy { get; set; } = CandidateNetworkPolicy.All;
    public TcpCandidatePolicy TcpCandidatePolicy { get; set; } = TcpCandidatePolicy.Enabled;
    public int IceCandidatePoolSize { get; set; } = 0;
    public MediaSecurityType SrtpType { get; set; } = MediaSecurityType.DtlsSrtp;
    public SdpSemantics SdpSemantics { get; set; } = SdpSemantics.UnifiedPlan;
    public bool OfferToReceiveAudio { get; set; } = true;
    public bool OfferToReceiveVideo { get; set; } = true;
    public bool DisableIpv6 { get; set; } = false;
    public bool DisableIpv6OnWifi { get; set; } = false;
    public int MaxIpv6Networks { get; set; } = 5;
    public bool DisableLinkLocalNetworks { get; set; } = false;
    public int ScreencastMinBitrate { get; set; } = -1;
    public bool EnableDscp { get; set; } = false;
    public bool UseRtpMux { get; set; } = true;
    public uint LocalAudioBandwidth { get; set; } = 128;
    public uint LocalVideoBandwidth { get; set; } = 512;
}

public sealed class AudioOptions
{
    public bool EchoCancellation { get; set; } = true;
    public bool AutoGainControl { get; set; } = true;
    public bool NoiseSuppression { get; set; } = true;
    public bool HighpassFilter { get; set; } = false;
}

public sealed class RtpEncodingSettings
{
    public int? MaxBitrateBps { get; set; }
    public int? MinBitrateBps { get; set; }
    public double? MaxFramerate { get; set; }
    public double? ScaleResolutionDownBy { get; set; }
    public bool? Active { get; set; }
    public DegradationPreference? DegradationPreference { get; set; }

    internal LrtcRtpEncodingSettings ToNative()
    {
        return new LrtcRtpEncodingSettings
        {
            max_bitrate_bps = MaxBitrateBps ?? -1,
            min_bitrate_bps = MinBitrateBps ?? -1,
            max_framerate = MaxFramerate ?? -1,
            scale_resolution_down_by = ScaleResolutionDownBy ?? -1,
            active = Active.HasValue ? (Active.Value ? 1 : 0) : -1,
            degradation_preference = DegradationPreference.HasValue ? (int)DegradationPreference.Value : -1,
        };
    }
}

public readonly struct AudioFrame
{
    public AudioFrame(ReadOnlyMemory<byte> data, int bitsPerSample, int sampleRate, int channels, int frames)
    {
        Data = data;
        BitsPerSample = bitsPerSample;
        SampleRate = sampleRate;
        Channels = channels;
        Frames = frames;
    }

    public ReadOnlyMemory<byte> Data { get; }
    public int BitsPerSample { get; }
    public int SampleRate { get; }
    public int Channels { get; }
    public int Frames { get; }
}

public sealed class MediaConstraints : SafeHandle
{
    private MediaConstraints() : base(IntPtr.Zero, true) { }

    public static MediaConstraints Create()
    {
        var handle = NativeMethods.lrtc_media_constraints_create();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create media constraints.");
        }
        var constraints = new MediaConstraints();
        constraints.SetHandle(handle);
        return constraints;
    }

    public void AddMandatory(string key, string value)
    {
        using var keyUtf8 = new Utf8String(key);
        using var valueUtf8 = new Utf8String(value);
        NativeMethods.lrtc_media_constraints_add_mandatory(handle, keyUtf8.Pointer, valueUtf8.Pointer);
    }

    public void AddOptional(string key, string value)
    {
        using var keyUtf8 = new Utf8String(key);
        using var valueUtf8 = new Utf8String(value);
        NativeMethods.lrtc_media_constraints_add_optional(handle, keyUtf8.Pointer, valueUtf8.Pointer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_media_constraints_release(handle);
        return true;
    }
}

internal sealed class RtcConfigurationMarshaler : IDisposable
{
    private readonly List<Utf8String> _strings = new();
    private IntPtr _ptr;

    public IntPtr Pointer => _ptr;

    public RtcConfigurationMarshaler(RtcConfiguration config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        var native = new LrtcRtcConfig
        {
            ice_servers = new LrtcIceServer[LrtcConstants.MaxIceServers],
            ice_server_count = 0,
            ice_transports_type = (int)config.IceTransportsType,
            bundle_policy = (int)config.BundlePolicy,
            rtcp_mux_policy = (int)config.RtcpMuxPolicy,
            candidate_network_policy = (int)config.CandidateNetworkPolicy,
            tcp_candidate_policy = (int)config.TcpCandidatePolicy,
            ice_candidate_pool_size = config.IceCandidatePoolSize,
            srtp_type = (int)config.SrtpType,
            sdp_semantics = (int)config.SdpSemantics,
            offer_to_receive_audio = config.OfferToReceiveAudio,
            offer_to_receive_video = config.OfferToReceiveVideo,
            disable_ipv6 = config.DisableIpv6,
            disable_ipv6_on_wifi = config.DisableIpv6OnWifi,
            max_ipv6_networks = config.MaxIpv6Networks,
            disable_link_local_networks = config.DisableLinkLocalNetworks,
            screencast_min_bitrate = config.ScreencastMinBitrate,
            enable_dscp = config.EnableDscp,
            use_rtp_mux = config.UseRtpMux,
            local_audio_bandwidth = config.LocalAudioBandwidth,
            local_video_bandwidth = config.LocalVideoBandwidth,
        };

        var max = Math.Min(config.IceServers.Count, LrtcConstants.MaxIceServers);
        for (var i = 0; i < max; i++)
        {
            var server = config.IceServers[i];
            var uri = new Utf8String(server.Uri);
            var username = new Utf8String(server.Username);
            var password = new Utf8String(server.Password);
            _strings.Add(uri);
            _strings.Add(username);
            _strings.Add(password);
            native.ice_servers[i] = new LrtcIceServer
            {
                uri = uri.Pointer,
                username = username.Pointer,
                password = password.Pointer,
            };
        }
        native.ice_server_count = (uint)max;

        _ptr = Marshal.AllocHGlobal(Marshal.SizeOf<LrtcRtcConfig>());
        Marshal.StructureToPtr(native, _ptr, false);
    }

    public void Dispose()
    {
        foreach (var str in _strings)
        {
            str.Dispose();
        }
        if (_ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_ptr);
            _ptr = IntPtr.Zero;
        }
    }
}

internal sealed class Utf8StringArray : IDisposable
{
    private readonly Utf8String[] _strings;
    private IntPtr _buffer;

    public Utf8StringArray(IReadOnlyList<string>? values)
    {
        if (values == null || values.Count == 0)
        {
            _strings = Array.Empty<Utf8String>();
            _buffer = IntPtr.Zero;
            Count = 0;
            return;
        }

        _strings = new Utf8String[values.Count];
        _buffer = Marshal.AllocHGlobal(IntPtr.Size * values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            _strings[i] = new Utf8String(values[i]);
            Marshal.WriteIntPtr(_buffer, i * IntPtr.Size, _strings[i].Pointer);
        }
        Count = values.Count;
    }

    public int Count { get; }
    public IntPtr Pointer => _buffer;

    public void Dispose()
    {
        foreach (var str in _strings)
        {
            str.Dispose();
        }
        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }
    }
}

internal static class NativeString
{
    public static string GetString(IntPtr handle, Func<IntPtr, IntPtr, uint, int> getter)
    {
        var required = getter(handle, IntPtr.Zero, 0);
        if (required <= 0)
        {
            return string.Empty;
        }

        var buffer = Marshal.AllocHGlobal(required);
        try
        {
            var result = getter(handle, buffer, (uint)required);
            if (result < 0)
            {
                return string.Empty;
            }
            return Utf8String.Read(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}

public sealed class PeerConnectionFactory : SafeHandle
{
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
        var native = callbacks.BuildNative();
        using var configMarshaler = config != null ? new RtcConfigurationMarshaler(config) : null;
        var configPtr = configMarshaler?.Pointer ?? IntPtr.Zero;
        var constraintsPtr = constraints?.DangerousGetHandle() ?? IntPtr.Zero;
        var pcHandle = NativeMethods.lrtc_peer_connection_create(
            handle, configPtr, constraintsPtr, ref native, IntPtr.Zero);
        if (pcHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create peer connection.");
        }
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
        return new MediaStream(stream, streamId);
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

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_factory_release(handle);
        return true;
    }
}

public readonly record struct AudioDeviceInfo(string Name, string Guid);

public sealed class AudioDevice : SafeHandle
{
    private const int MaxDeviceNameSize = 128;
    private const int MaxGuidSize = 128;

    internal AudioDevice(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public short PlayoutDevices() => NativeMethods.lrtc_audio_device_playout_devices(handle);

    public short RecordingDevices() => NativeMethods.lrtc_audio_device_recording_devices(handle);

    public AudioDeviceInfo GetPlayoutDeviceName(ushort index)
    {
        return GetDeviceName(isPlayout: true, index);
    }

    public AudioDeviceInfo GetRecordingDeviceName(ushort index)
    {
        return GetDeviceName(isPlayout: false, index);
    }

    private AudioDeviceInfo GetDeviceName(bool isPlayout, ushort index)
    {
        var nameBuf = Marshal.AllocHGlobal(MaxDeviceNameSize);
        var guidBuf = Marshal.AllocHGlobal(MaxGuidSize);
        try
        {
            int result = isPlayout
                ? NativeMethods.lrtc_audio_device_playout_device_name(handle, index, nameBuf, MaxDeviceNameSize, guidBuf, MaxGuidSize)
                : NativeMethods.lrtc_audio_device_recording_device_name(handle, index, nameBuf, MaxDeviceNameSize, guidBuf, MaxGuidSize);
            if (result != 0)
            {
                throw new InvalidOperationException("Failed to query audio device name.");
            }
            return new AudioDeviceInfo(Utf8String.Read(nameBuf), Utf8String.Read(guidBuf));
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuf);
            Marshal.FreeHGlobal(guidBuf);
        }
    }

    public void SetPlayoutDevice(ushort index)
    {
        var result = NativeMethods.lrtc_audio_device_set_playout_device(handle, index);
        if (result != 0) throw new InvalidOperationException("Failed to set playout device.");
    }

    public void SetRecordingDevice(ushort index)
    {
        var result = NativeMethods.lrtc_audio_device_set_recording_device(handle, index);
        if (result != 0) throw new InvalidOperationException("Failed to set recording device.");
    }

    public void SetMicrophoneVolume(uint volume)
    {
        var result = NativeMethods.lrtc_audio_device_set_microphone_volume(handle, volume);
        if (result != 0) throw new InvalidOperationException("Failed to set microphone volume.");
    }

    public uint GetMicrophoneVolume()
    {
        var result = NativeMethods.lrtc_audio_device_microphone_volume(handle, out var volume);
        if (result != 0) throw new InvalidOperationException("Failed to get microphone volume.");
        return volume;
    }

    public void SetSpeakerVolume(uint volume)
    {
        var result = NativeMethods.lrtc_audio_device_set_speaker_volume(handle, volume);
        if (result != 0) throw new InvalidOperationException("Failed to set speaker volume.");
    }

    public uint GetSpeakerVolume()
    {
        var result = NativeMethods.lrtc_audio_device_speaker_volume(handle, out var volume);
        if (result != 0) throw new InvalidOperationException("Failed to get speaker volume.");
        return volume;
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_audio_device_release(handle);
        return true;
    }
}

public readonly record struct VideoDeviceInfo(string Name, string UniqueId);

public sealed class VideoDevice : SafeHandle
{
    private const int MaxNameSize = 256;
    private const int MaxUniqueIdSize = 256;

    internal VideoDevice(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public uint NumberOfDevices() => NativeMethods.lrtc_video_device_number_of_devices(handle);

    public VideoDeviceInfo GetDeviceName(uint index)
    {
        var nameBuf = Marshal.AllocHGlobal(MaxNameSize);
        var idBuf = Marshal.AllocHGlobal(MaxUniqueIdSize);
        try
        {
            var result = NativeMethods.lrtc_video_device_get_device_name(handle, index, nameBuf, MaxNameSize, idBuf, MaxUniqueIdSize);
            if (result != 0)
            {
                throw new InvalidOperationException("Failed to query video device name.");
            }
            return new VideoDeviceInfo(Utf8String.Read(nameBuf), Utf8String.Read(idBuf));
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuf);
            Marshal.FreeHGlobal(idBuf);
        }
    }

    public VideoCapturer CreateCapturer(string name, uint index, uint width, uint height, uint fps)
    {
        using var nameUtf8 = new Utf8String(name);
        var capturer = NativeMethods.lrtc_video_device_create_capturer(handle, nameUtf8.Pointer, index, width, height, fps);
        if (capturer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create video capturer.");
        }
        return new VideoCapturer(capturer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_device_release(handle);
        return true;
    }
}

public sealed class DesktopDevice : SafeHandle
{
    internal DesktopDevice(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public DesktopMediaList GetMediaList(DesktopType type)
    {
        var list = NativeMethods.lrtc_desktop_device_get_media_list(handle, (LrtcDesktopType)type);
        if (list == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get desktop media list.");
        }
        return new DesktopMediaList(list, type);
    }

    public DesktopCapturer CreateCapturer(MediaSource source, bool showCursor = true)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var capturer = NativeMethods.lrtc_desktop_device_create_capturer(handle, source.DangerousGetHandle(), showCursor);
        if (capturer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create desktop capturer.");
        }
        return new DesktopCapturer(capturer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_desktop_device_release(handle);
        return true;
    }
}

public sealed class DesktopMediaList : SafeHandle
{
    public DesktopType Type { get; }

    internal DesktopMediaList(IntPtr handle, DesktopType type) : base(IntPtr.Zero, true)
    {
        Type = type;
        SetHandle(handle);
    }

    public int UpdateSourceList(bool forceReload = false, bool getThumbnail = true)
    {
        return NativeMethods.lrtc_desktop_media_list_update(handle, forceReload, getThumbnail);
    }

    public int SourceCount => NativeMethods.lrtc_desktop_media_list_get_source_count(handle);

    public MediaSource GetSource(int index)
    {
        var source = NativeMethods.lrtc_desktop_media_list_get_source(handle, index);
        if (source == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get desktop media source.");
        }
        return new MediaSource(source);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_desktop_media_list_release(handle);
        return true;
    }
}

public sealed class MediaSource : SafeHandle
{
    internal MediaSource(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
        Id = NativeString.GetString(handle, NativeMethods.lrtc_media_source_get_id);
        Name = NativeString.GetString(handle, NativeMethods.lrtc_media_source_get_name);
        var typeValue = NativeMethods.lrtc_media_source_get_type(handle);
        Type = typeValue < 0 ? DesktopType.Screen : (DesktopType)typeValue;
    }

    public string Id { get; }
    public string Name { get; }
    public DesktopType Type { get; }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_media_source_release(handle);
        return true;
    }
}

public sealed class DesktopCapturer : SafeHandle
{
    internal DesktopCapturer(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public DesktopCaptureState Start(uint fps)
    {
        return (DesktopCaptureState)NativeMethods.lrtc_desktop_capturer_start(handle, fps);
    }

    public DesktopCaptureState Start(uint fps, uint x, uint y, uint width, uint height)
    {
        return (DesktopCaptureState)NativeMethods.lrtc_desktop_capturer_start_region(handle, fps, x, y, width, height);
    }

    public void Stop()
    {
        NativeMethods.lrtc_desktop_capturer_stop(handle);
    }

    public bool IsRunning => NativeMethods.lrtc_desktop_capturer_is_running(handle);

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_desktop_capturer_release(handle);
        return true;
    }
}

public sealed class VideoCapturer : SafeHandle
{
    internal VideoCapturer(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public bool Start() => NativeMethods.lrtc_video_capturer_start(handle);

    public bool CaptureStarted() => NativeMethods.lrtc_video_capturer_capture_started(handle);

    public void Stop() => NativeMethods.lrtc_video_capturer_stop(handle);

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_capturer_release(handle);
        return true;
    }
}

public sealed class AudioSource : SafeHandle
{
    internal AudioSource(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public void CaptureFrame(ReadOnlySpan<byte> data, int bitsPerSample, int sampleRate, int channels, int frames)
    {
        if (data.IsEmpty) return;
        unsafe
        {
            fixed (byte* ptr = data)
            {
                NativeMethods.lrtc_audio_source_capture_frame(handle, (IntPtr)ptr, bitsPerSample, sampleRate, (nuint)channels, (nuint)frames);
            }
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_audio_source_release(handle);
        return true;
    }
}

public sealed class VideoSource : SafeHandle
{
    internal VideoSource(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_source_release(handle);
        return true;
    }
}

public sealed class MediaStream : SafeHandle
{
    public string Id { get; }

    internal MediaStream(IntPtr handle, string id) : base(IntPtr.Zero, true)
    {
        Id = id;
        SetHandle(handle);
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

public sealed class RtpSender : SafeHandle
{
    internal RtpSender(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public MediaType MediaType
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_sender_get_media_type(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get sender media type.");
            }
            return (MediaType)value;
        }
    }

    public string Id => NativeString.GetString(handle, NativeMethods.lrtc_rtp_sender_get_id);

    public AudioTrack? AudioTrack
    {
        get
        {
            var track = NativeMethods.lrtc_rtp_sender_get_audio_track(handle);
            return track == IntPtr.Zero ? null : new AudioTrack(track);
        }
    }

    public VideoTrack? VideoTrack
    {
        get
        {
            var track = NativeMethods.lrtc_rtp_sender_get_video_track(handle);
            return track == IntPtr.Zero ? null : new VideoTrack(track);
        }
    }

    public bool SetEncodingParameters(RtpEncodingSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        var native = settings.ToNative();
        var result = NativeMethods.lrtc_rtp_sender_set_encoding_parameters(handle, ref native);
        return result != 0;
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_rtp_sender_release(handle);
        return true;
    }
}

public sealed class RtpReceiver : SafeHandle
{
    internal RtpReceiver(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public MediaType MediaType
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_receiver_get_media_type(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get receiver media type.");
            }
            return (MediaType)value;
        }
    }

    public string Id => NativeString.GetString(handle, NativeMethods.lrtc_rtp_receiver_get_id);

    public AudioTrack? AudioTrack
    {
        get
        {
            var track = NativeMethods.lrtc_rtp_receiver_get_audio_track(handle);
            return track == IntPtr.Zero ? null : new AudioTrack(track);
        }
    }

    public VideoTrack? VideoTrack
    {
        get
        {
            var track = NativeMethods.lrtc_rtp_receiver_get_video_track(handle);
            return track == IntPtr.Zero ? null : new VideoTrack(track);
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_rtp_receiver_release(handle);
        return true;
    }
}

public sealed class RtpTransceiver : SafeHandle
{
    private delegate int TransceiverErrorInvoker(IntPtr transceiver, IntPtr error, uint errorLen);

    internal RtpTransceiver(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public MediaType MediaType
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_transceiver_get_media_type(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get transceiver media type.");
            }
            return (MediaType)value;
        }
    }

    public string Mid => NativeString.GetString(handle, NativeMethods.lrtc_rtp_transceiver_get_mid);

    public RtpTransceiverDirection Direction
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_transceiver_get_direction(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get transceiver direction.");
            }
            return (RtpTransceiverDirection)value;
        }
    }

    public RtpTransceiverDirection CurrentDirection
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_transceiver_get_current_direction(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get transceiver current direction.");
            }
            return (RtpTransceiverDirection)value;
        }
    }

    public bool Stopped => NativeMethods.lrtc_rtp_transceiver_get_stopped(handle) != 0;

    public bool Stopping => NativeMethods.lrtc_rtp_transceiver_get_stopping(handle) != 0;

    public RtpSender? Sender
    {
        get
        {
            var sender = NativeMethods.lrtc_rtp_transceiver_get_sender(handle);
            return sender == IntPtr.Zero ? null : new RtpSender(sender);
        }
    }

    public RtpReceiver? Receiver
    {
        get
        {
            var receiver = NativeMethods.lrtc_rtp_transceiver_get_receiver(handle);
            return receiver == IntPtr.Zero ? null : new RtpReceiver(receiver);
        }
    }

    public bool TrySetDirection(RtpTransceiverDirection direction, out string? error)
    {
        return InvokeWithError(
            (transceiver, err, len) => NativeMethods.lrtc_rtp_transceiver_set_direction(
                transceiver,
                (LrtcRtpTransceiverDirection)direction,
                err,
                len),
            out error);
    }

    public void SetDirection(RtpTransceiverDirection direction)
    {
        if (!TrySetDirection(direction, out var error))
        {
            throw new InvalidOperationException(error ?? "Failed to set transceiver direction.");
        }
    }

    public bool TryStop(out string? error)
    {
        return InvokeWithError(
            (transceiver, err, len) => NativeMethods.lrtc_rtp_transceiver_stop(transceiver, err, len),
            out error);
    }

    public void Stop()
    {
        if (!TryStop(out var error))
        {
            throw new InvalidOperationException(error ?? "Failed to stop transceiver.");
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_rtp_transceiver_release(handle);
        return true;
    }

    private bool InvokeWithError(TransceiverErrorInvoker invoker, out string? error)
    {
        const int BufferSize = 512;
        var buffer = Marshal.AllocHGlobal(BufferSize);
        try
        {
            unsafe
            {
                new Span<byte>((void*)buffer, BufferSize).Clear();
            }
            var result = invoker(handle, buffer, (uint)BufferSize);
            if (result != 0)
            {
                error = null;
                return true;
            }
            var message = Utf8String.Read(buffer);
            error = string.IsNullOrWhiteSpace(message) ? null : message;
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}

public sealed class PeerConnection : SafeHandle
{
    private readonly PeerConnectionCallbacks _callbacks;
    private readonly List<Delegate> _keepAlive = new();

    internal PeerConnection(IntPtr handle, PeerConnectionCallbacks callbacks)
        : base(IntPtr.Zero, true)
    {
        _callbacks = callbacks;
        SetHandle(handle);
    }

    public new void Close()
    {
        NativeMethods.lrtc_peer_connection_close(handle);
    }

    public DataChannel CreateDataChannel(string label, DataChannelInit? init = null)
    {
        var settings = init ?? DataChannelInit.Default;
        using var labelUtf8 = new Utf8String(label);
        using var protocolUtf8 = new Utf8String(settings.Protocol);
        var channel = NativeMethods.lrtc_peer_connection_create_data_channel(
            handle,
            labelUtf8.Pointer,
            settings.Ordered ? 1 : 0,
            settings.Reliable ? 1 : 0,
            settings.MaxRetransmitTime,
            settings.MaxRetransmits,
            protocolUtf8.Pointer,
            settings.Negotiated ? 1 : 0,
            settings.Id);
        if (channel == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create data channel.");
        }
        return new DataChannel(channel);
    }

    public void CreateOffer(Action<string, string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcSdpSuccessCb successCb = (_, sdpPtr, typePtr) =>
            onSuccess(Utf8String.Read(sdpPtr), Utf8String.Read(typePtr));
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_create_offer(handle, successCb, errorCb, IntPtr.Zero, IntPtr.Zero);
    }

    public void CreateAnswer(Action<string, string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcSdpSuccessCb successCb = (_, sdpPtr, typePtr) =>
            onSuccess(Utf8String.Read(sdpPtr), Utf8String.Read(typePtr));
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_create_answer(handle, successCb, errorCb, IntPtr.Zero, IntPtr.Zero);
    }

    public void RestartIce()
    {
        NativeMethods.lrtc_peer_connection_restart_ice(handle);
    }

    public void SetLocalDescription(string sdp, string type, Action onSuccess, Action<string> onFailure)
    {
        using var sdpUtf8 = new Utf8String(sdp);
        using var typeUtf8 = new Utf8String(type);

        LrtcVoidCb successCb = _ => onSuccess?.Invoke();
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure?.Invoke(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_set_local_description(
            handle, sdpUtf8.Pointer, typeUtf8.Pointer, successCb, errorCb, IntPtr.Zero);
    }

    public void SetRemoteDescription(string sdp, string type, Action onSuccess, Action<string> onFailure)
    {
        using var sdpUtf8 = new Utf8String(sdp);
        using var typeUtf8 = new Utf8String(type);

        LrtcVoidCb successCb = _ => onSuccess?.Invoke();
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure?.Invoke(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_set_remote_description(
            handle, sdpUtf8.Pointer, typeUtf8.Pointer, successCb, errorCb, IntPtr.Zero);
    }

    public void GetLocalDescription(Action<string, string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcSdpSuccessCb successCb = (_, sdpPtr, typePtr) =>
            onSuccess(Utf8String.Read(sdpPtr), Utf8String.Read(typePtr));
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_get_local_description(handle, successCb, errorCb, IntPtr.Zero);
    }

    public void GetRemoteDescription(Action<string, string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcSdpSuccessCb successCb = (_, sdpPtr, typePtr) =>
            onSuccess(Utf8String.Read(sdpPtr), Utf8String.Read(typePtr));
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_get_remote_description(handle, successCb, errorCb, IntPtr.Zero);
    }

    public void AddIceCandidate(string sdpMid, int sdpMlineIndex, string candidate)
    {
        using var midUtf8 = new Utf8String(sdpMid);
        using var candUtf8 = new Utf8String(candidate);
        NativeMethods.lrtc_peer_connection_add_ice_candidate(handle, midUtf8.Pointer, sdpMlineIndex, candUtf8.Pointer);
    }

    public void GetStats(Action<string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcStatsSuccessCb successCb = (_, jsonPtr) => onSuccess(Utf8String.Read(jsonPtr));
        LrtcStatsErrorCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_get_stats(handle, successCb, errorCb, IntPtr.Zero);
    }

    public Task<string> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        if (cancellationToken.CanBeCanceled)
        {
            registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        GetStats(
            json =>
            {
                registration.Dispose();
                tcs.TrySetResult(json);
            },
            error =>
            {
                registration.Dispose();
                tcs.TrySetException(new InvalidOperationException(error));
            });

        return tcs.Task;
    }

    public bool AddStream(MediaStream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        return NativeMethods.lrtc_peer_connection_add_stream(handle, stream.DangerousGetHandle());
    }

    public bool RemoveStream(MediaStream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        return NativeMethods.lrtc_peer_connection_remove_stream(handle, stream.DangerousGetHandle());
    }

    public bool AddAudioTrack(AudioTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        using var ids = new Utf8StringArray(streamIds);
        var result = NativeMethods.lrtc_peer_connection_add_audio_track(handle, track.DangerousGetHandle(), ids.Pointer, (uint)ids.Count);
        return result != 0;
    }

    public bool AddVideoTrack(VideoTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        using var ids = new Utf8StringArray(streamIds);
        var result = NativeMethods.lrtc_peer_connection_add_video_track(handle, track.DangerousGetHandle(), ids.Pointer, (uint)ids.Count);
        return result != 0;
    }

    public RtpSender AddAudioTrackSender(AudioTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        using var ids = new Utf8StringArray(streamIds);
        var sender = NativeMethods.lrtc_peer_connection_add_audio_track_sender(handle, track.DangerousGetHandle(), ids.Pointer, (uint)ids.Count);
        if (sender == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to add audio track sender.");
        }
        return new RtpSender(sender);
    }

    public RtpSender AddVideoTrackSender(VideoTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        using var ids = new Utf8StringArray(streamIds);
        var sender = NativeMethods.lrtc_peer_connection_add_video_track_sender(handle, track.DangerousGetHandle(), ids.Pointer, (uint)ids.Count);
        if (sender == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to add video track sender.");
        }
        return new RtpSender(sender);
    }

    public bool RemoveTrack(RtpSender sender)
    {
        if (sender == null) throw new ArgumentNullException(nameof(sender));
        var result = NativeMethods.lrtc_peer_connection_remove_track(handle, sender.DangerousGetHandle());
        return result != 0;
    }

    public IReadOnlyList<RtpSender> GetSenders()
    {
        var count = NativeMethods.lrtc_peer_connection_sender_count(handle);
        if (count == 0)
        {
            return Array.Empty<RtpSender>();
        }

        var list = new List<RtpSender>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var sender = NativeMethods.lrtc_peer_connection_get_sender(handle, i);
            if (sender != IntPtr.Zero)
            {
                list.Add(new RtpSender(sender));
            }
        }
        return list;
    }

    public IReadOnlyList<RtpReceiver> GetReceivers()
    {
        var count = NativeMethods.lrtc_peer_connection_receiver_count(handle);
        if (count == 0)
        {
            return Array.Empty<RtpReceiver>();
        }

        var list = new List<RtpReceiver>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var receiver = NativeMethods.lrtc_peer_connection_get_receiver(handle, i);
            if (receiver != IntPtr.Zero)
            {
                list.Add(new RtpReceiver(receiver));
            }
        }
        return list;
    }

    public IReadOnlyList<RtpTransceiver> GetTransceivers()
    {
        var count = NativeMethods.lrtc_peer_connection_transceiver_count(handle);
        if (count == 0)
        {
            return Array.Empty<RtpTransceiver>();
        }

        var list = new List<RtpTransceiver>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var transceiver = NativeMethods.lrtc_peer_connection_get_transceiver(handle, i);
            if (transceiver != IntPtr.Zero)
            {
                list.Add(new RtpTransceiver(transceiver));
            }
        }
        return list;
    }

    public bool SetCodecPreferences(MediaType mediaType, IReadOnlyList<string> mimeTypes)
    {
        if (mimeTypes == null) throw new ArgumentNullException(nameof(mimeTypes));
        using var mimes = new Utf8StringArray(mimeTypes);
        var result = NativeMethods.lrtc_peer_connection_set_codec_preferences(
            handle,
            (LrtcMediaType)mediaType,
            mimes.Pointer,
            (uint)mimes.Count);
        return result != 0;
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_peer_connection_release(handle);
        return true;
    }
}

public sealed class DataChannel : SafeHandle
{
    private DataChannelCallbacks? _callbacks;

    internal DataChannel(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public void SetCallbacks(DataChannelCallbacks callbacks)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        NativeMethods.lrtc_data_channel_set_callbacks(handle, ref native, IntPtr.Zero);
    }

    public void Send(ReadOnlySpan<byte> data, bool binary = true)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                NativeMethods.lrtc_data_channel_send(handle, (IntPtr)ptr, (uint)data.Length, binary ? 1 : 0);
            }
        }
    }

    public new void Close()
    {
        NativeMethods.lrtc_data_channel_close(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_data_channel_release(handle);
        return true;
    }
}

public sealed class AudioTrack : SafeHandle
{
    internal AudioTrack(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public void SetVolume(double volume)
    {
        NativeMethods.lrtc_audio_track_set_volume(handle, volume);
    }

    public void AddSink(AudioSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        NativeMethods.lrtc_audio_track_add_sink(handle, sink.DangerousGetHandle());
    }

    public void RemoveSink(AudioSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        NativeMethods.lrtc_audio_track_remove_sink(handle, sink.DangerousGetHandle());
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_audio_track_release(handle);
        return true;
    }
}

public sealed class AudioSink : SafeHandle
{
    private readonly AudioSinkCallbacks _callbacks;

    public AudioSink(AudioSinkCallbacks callbacks)
        : base(IntPtr.Zero, true)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        var handle = NativeMethods.lrtc_audio_sink_create(ref native, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create audio sink.");
        }
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_audio_sink_release(handle);
        return true;
    }
}

public sealed class VideoTrack : SafeHandle
{
    internal VideoTrack(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
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

public sealed class VideoSink : SafeHandle
{
    private readonly VideoSinkCallbacks _callbacks;

    public VideoSink(VideoSinkCallbacks callbacks)
        : base(IntPtr.Zero, true)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        var handle = NativeMethods.lrtc_video_sink_create(ref native, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create video sink.");
        }
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_sink_release(handle);
        return true;
    }
}

public sealed class VideoFrame : SafeHandle
{
    internal VideoFrame(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public int Width => NativeMethods.lrtc_video_frame_width(handle);
    public int Height => NativeMethods.lrtc_video_frame_height(handle);

    public void CopyToI420(Span<byte> y, int strideY, Span<byte> u, int strideU, Span<byte> v, int strideV)
    {
        unsafe
        {
            fixed (byte* yPtr = y)
            fixed (byte* uPtr = u)
            fixed (byte* vPtr = v)
            {
                NativeMethods.lrtc_video_frame_copy_i420(handle, (IntPtr)yPtr, strideY, (IntPtr)uPtr, strideU, (IntPtr)vPtr, strideV);
            }
        }
    }

    public void CopyToArgb(Span<byte> argb, int strideArgb, int width, int height, int format)
    {
        unsafe
        {
            fixed (byte* argbPtr = argb)
            {
                NativeMethods.lrtc_video_frame_to_argb(handle, (IntPtr)argbPtr, strideArgb, width, height, format);
            }
        }
    }

    public void CopyToArgb(Span<byte> argb, int strideArgb, int width, int height, VideoFrameFormat format)
    {
        CopyToArgb(argb, strideArgb, width, height, (int)format);
    }

    public VideoFrame Retain()
    {
        var retained = NativeMethods.lrtc_video_frame_retain(handle);
        if (retained == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to retain video frame.");
        }
        return new VideoFrame(retained);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_frame_release(handle);
        return true;
    }
}

public sealed class PeerConnectionCallbacks
{
    public Action<SignalingState>? OnSignalingState;
    public Action<PeerConnectionState>? OnPeerConnectionState;
    public Action<IceGatheringState>? OnIceGatheringState;
    public Action<IceConnectionState>? OnIceConnectionState;
    public Action<string, int, string>? OnIceCandidate;
    public Action<DataChannel>? OnDataChannel;
    public Action<VideoTrack>? OnVideoTrack;
    public Action<AudioTrack>? OnAudioTrack;
    public Action? OnRenegotiationNeeded;

    private LrtcPeerConnectionStateCb? _signalingStateCb;
    private LrtcPeerConnectionStateCb? _pcStateCb;
    private LrtcPeerConnectionStateCb? _iceGatheringCb;
    private LrtcPeerConnectionStateCb? _iceConnectionCb;
    private LrtcIceCandidateCb? _iceCandidateCb;
    private LrtcDataChannelCreatedCb? _dataChannelCb;
    private LrtcVideoTrackCb? _videoTrackCb;
    private LrtcAudioTrackCb? _audioTrackCb;
    private LrtcVoidCb? _renegotiationCb;

    internal LrtcPeerConnectionCallbacks BuildNative()
    {
        _signalingStateCb = (ud, state) => OnSignalingState?.Invoke((SignalingState)state);
        _pcStateCb = (ud, state) => OnPeerConnectionState?.Invoke((PeerConnectionState)state);
        _iceGatheringCb = (ud, state) => OnIceGatheringState?.Invoke((IceGatheringState)state);
        _iceConnectionCb = (ud, state) => OnIceConnectionState?.Invoke((IceConnectionState)state);
        _iceCandidateCb = (ud, mid, mline, cand) =>
            OnIceCandidate?.Invoke(Utf8String.Read(mid), mline, Utf8String.Read(cand));
        _dataChannelCb = (ud, channelPtr) => OnDataChannel?.Invoke(new DataChannel(channelPtr));
        _videoTrackCb = (ud, trackPtr) => OnVideoTrack?.Invoke(new VideoTrack(trackPtr));
        _audioTrackCb = (ud, trackPtr) => OnAudioTrack?.Invoke(new AudioTrack(trackPtr));
        _renegotiationCb = ud => OnRenegotiationNeeded?.Invoke();

        return new LrtcPeerConnectionCallbacks
        {
            on_signaling_state = _signalingStateCb,
            on_peer_connection_state = _pcStateCb,
            on_ice_gathering_state = _iceGatheringCb,
            on_ice_connection_state = _iceConnectionCb,
            on_ice_candidate = _iceCandidateCb,
            on_data_channel = _dataChannelCb,
            on_video_track = _videoTrackCb,
            on_audio_track = _audioTrackCb,
            on_renegotiation_needed = _renegotiationCb,
        };
    }
}

public sealed class DataChannelCallbacks
{
    public Action<DataChannelState>? OnStateChange;
    public Action<ReadOnlyMemory<byte>, bool>? OnMessage;

    private LrtcDataChannelStateCb? _stateCb;
    private LrtcDataChannelMessageCb? _messageCb;

    internal LrtcDataChannelCallbacks BuildNative()
    {
        _stateCb = (ud, state) => OnStateChange?.Invoke((DataChannelState)state);
        _messageCb = (ud, dataPtr, length, binary) =>
        {
            if (dataPtr == IntPtr.Zero || length <= 0)
            {
                OnMessage?.Invoke(ReadOnlyMemory<byte>.Empty, binary != 0);
                return;
            }
            var managed = new byte[length];
            Marshal.Copy(dataPtr, managed, 0, length);
            OnMessage?.Invoke(managed, binary != 0);
        };

        return new LrtcDataChannelCallbacks
        {
            on_state_change = _stateCb,
            on_message = _messageCb,
        };
    }
}

public sealed class AudioSinkCallbacks
{
    public Action<AudioFrame>? OnData;
    private LrtcAudioFrameCb? _frameCb;

    internal LrtcAudioSinkCallbacks BuildNative()
    {
        _frameCb = (ud, audioPtr, bitsPerSample, sampleRate, channels, frames) =>
        {
            var channelsInt = (int)channels;
            var framesInt = (int)frames;
            if (audioPtr == IntPtr.Zero || bitsPerSample <= 0 || channelsInt <= 0 || framesInt <= 0)
            {
                OnData?.Invoke(new AudioFrame(ReadOnlyMemory<byte>.Empty, bitsPerSample, sampleRate, channelsInt, framesInt));
                return;
            }

            var bytesPerSample = Math.Max(1, (bitsPerSample + 7) / 8);
            var totalBytes = (long)channelsInt * framesInt * bytesPerSample;
            if (totalBytes <= 0 || totalBytes > int.MaxValue)
            {
                OnData?.Invoke(new AudioFrame(ReadOnlyMemory<byte>.Empty, bitsPerSample, sampleRate, channelsInt, framesInt));
                return;
            }

            var data = new byte[(int)totalBytes];
            Marshal.Copy(audioPtr, data, 0, (int)totalBytes);
            OnData?.Invoke(new AudioFrame(data, bitsPerSample, sampleRate, channelsInt, framesInt));
        };

        return new LrtcAudioSinkCallbacks
        {
            on_data = _frameCb,
        };
    }
}

public sealed class VideoSinkCallbacks
{
    public Action<VideoFrame>? OnFrame;
    private LrtcVideoFrameCb? _frameCb;

    internal LrtcVideoSinkCallbacks BuildNative()
    {
        _frameCb = (ud, framePtr) =>
        {
            if (framePtr == IntPtr.Zero) return;
            using var frame = new VideoFrame(framePtr);
            OnFrame?.Invoke(frame);
        };

        return new LrtcVideoSinkCallbacks
        {
            on_frame = _frameCb,
        };
    }
}

public readonly struct DataChannelInit
{
    public static DataChannelInit Default => new(
        ordered: true,
        reliable: true,
        maxRetransmitTime: -1,
        maxRetransmits: -1,
        protocol: "sctp",
        negotiated: false,
        id: 0);

    public DataChannelInit(
        bool ordered,
        bool reliable,
        int maxRetransmitTime,
        int maxRetransmits,
        string protocol,
        bool negotiated,
        int id)
    {
        Ordered = ordered;
        Reliable = reliable;
        MaxRetransmitTime = maxRetransmitTime;
        MaxRetransmits = maxRetransmits;
        Protocol = protocol ?? "sctp";
        Negotiated = negotiated;
        Id = id;
    }

    public bool Ordered { get; }
    public bool Reliable { get; }
    public int MaxRetransmitTime { get; }
    public int MaxRetransmits { get; }
    public string Protocol { get; }
    public bool Negotiated { get; }
    public int Id { get; }
}

internal sealed class Utf8String : IDisposable
{
    public IntPtr Pointer { get; private set; }

    public Utf8String(string? value)
    {
        Pointer = value == null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(value);
    }

    public static string Read(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    public void Dispose()
    {
        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(Pointer);
            Pointer = IntPtr.Zero;
        }
    }
}
