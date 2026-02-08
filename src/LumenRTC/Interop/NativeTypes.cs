using System;
using System.Runtime.InteropServices;

namespace LumenRTC.Interop;

internal enum LrtcResult : int
{
    Ok = 0,
    Error = 1,
    InvalidArg = 2,
    NotImplemented = 3,
}

internal enum LrtcLogSeverity : int
{
    Verbose = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    None = 4,
}

internal enum LrtcPeerConnectionState : int
{
    New = 0,
    Connecting = 1,
    Connected = 2,
    Disconnected = 3,
    Failed = 4,
    Closed = 5,
}

internal enum LrtcSignalingState : int
{
    Stable = 0,
    HaveLocalOffer = 1,
    HaveRemoteOffer = 2,
    HaveLocalPrAnswer = 3,
    HaveRemotePrAnswer = 4,
    Closed = 5,
}

internal enum LrtcIceGatheringState : int
{
    New = 0,
    Gathering = 1,
    Complete = 2,
}

internal enum LrtcIceConnectionState : int
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

internal enum LrtcDataChannelState : int
{
    Connecting = 0,
    Open = 1,
    Closing = 2,
    Closed = 3,
}

internal enum LrtcMediaType : int
{
    Audio = 0,
    Video = 1,
    Data = 2,
}

internal enum LrtcRtpTransceiverDirection : int
{
    SendRecv = 0,
    SendOnly = 1,
    RecvOnly = 2,
    Inactive = 3,
    Stopped = 4,
}

internal enum LrtcDtlsTransportState : int
{
    New = 0,
    Connecting = 1,
    Connected = 2,
    Closed = 3,
    Failed = 4,
}

internal enum LrtcAudioSourceType : int
{
    Microphone = 0,
    Custom = 1,
}

internal enum LrtcDesktopType : int
{
    Screen = 0,
    Window = 1,
}

internal enum LrtcDesktopCaptureState : int
{
    Running = 0,
    Stopped = 1,
    Failed = 2,
}

internal enum LrtcTrackState : int
{
    Live = 0,
    Ended = 1,
}

internal static class LrtcConstants
{
    public const int MaxIceServers = 8;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcIceServer
{
    public IntPtr uri;
    public IntPtr username;
    public IntPtr password;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct LrtcRtcConfig
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = LrtcConstants.MaxIceServers)]
    public LrtcIceServer[] ice_servers;

    public uint ice_server_count;
    public int ice_transports_type;
    public int bundle_policy;
    public int rtcp_mux_policy;
    public int candidate_network_policy;
    public int tcp_candidate_policy;
    public int ice_candidate_pool_size;
    public int srtp_type;
    public int sdp_semantics;

    [MarshalAs(UnmanagedType.I1)] public bool offer_to_receive_audio;
    [MarshalAs(UnmanagedType.I1)] public bool offer_to_receive_video;
    [MarshalAs(UnmanagedType.I1)] public bool disable_ipv6;
    [MarshalAs(UnmanagedType.I1)] public bool disable_ipv6_on_wifi;

    public int max_ipv6_networks;

    [MarshalAs(UnmanagedType.I1)] public bool disable_link_local_networks;

    public int screencast_min_bitrate;

    [MarshalAs(UnmanagedType.I1)] public bool enable_dscp;
    [MarshalAs(UnmanagedType.I1)] public bool use_rtp_mux;

    public uint local_audio_bandwidth;
    public uint local_video_bandwidth;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcAudioOptions
{
    [MarshalAs(UnmanagedType.I1)] public bool echo_cancellation;
    [MarshalAs(UnmanagedType.I1)] public bool auto_gain_control;
    [MarshalAs(UnmanagedType.I1)] public bool noise_suppression;
    [MarshalAs(UnmanagedType.I1)] public bool highpass_filter;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcRtpEncodingSettings
{
    public int max_bitrate_bps;
    public int min_bitrate_bps;
    public double max_framerate;
    public double scale_resolution_down_by;
    public int active;
    public int degradation_preference;
    public double bitrate_priority;
    public int network_priority;
    public int num_temporal_layers;
    public IntPtr scalability_mode;
    public IntPtr rid;
    public int adaptive_ptime;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcRtpEncodingInfo
{
    public uint ssrc;
    public int max_bitrate_bps;
    public int min_bitrate_bps;
    public double max_framerate;
    public double scale_resolution_down_by;
    public int active;
    public double bitrate_priority;
    public int network_priority;
    public int num_temporal_layers;
    public int adaptive_ptime;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcDtlsTransportInfo
{
    public int state;
    public int ssl_cipher_suite;
    public int srtp_cipher_suite;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcRtpTransceiverInit
{
    public LrtcRtpTransceiverDirection direction;
    public IntPtr stream_ids;
    public uint stream_id_count;
    public IntPtr send_encodings;
    public uint send_encoding_count;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcSdpSuccessCb(IntPtr userData, IntPtr sdp, IntPtr type);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcSdpErrorCb(IntPtr userData, IntPtr error);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcVoidCb(IntPtr userData);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcPeerConnectionStateCb(IntPtr userData, int state);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcIceCandidateCb(IntPtr userData, IntPtr sdpMid, int sdpMlineIndex, IntPtr candidate);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcDataChannelStateCb(IntPtr userData, int state);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcDataChannelMessageCb(IntPtr userData, IntPtr data, int length, int binary);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcAudioFrameCb(IntPtr userData, IntPtr audioData, int bitsPerSample, int sampleRate, nuint channels, nuint frames);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcVideoFrameCb(IntPtr userData, IntPtr frame);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcDataChannelCreatedCb(IntPtr userData, IntPtr channel);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcVideoTrackCb(IntPtr userData, IntPtr track);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcAudioTrackCb(IntPtr userData, IntPtr track);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcTrackCb(IntPtr userData, IntPtr transceiver, IntPtr receiver);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcStatsSuccessCb(IntPtr userData, IntPtr json);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcStatsFailureCb(IntPtr userData, IntPtr error);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcLogMessageCb(IntPtr userData, IntPtr message);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcDtmfToneCb(IntPtr userData, IntPtr tone, IntPtr toneBuffer);

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcPeerConnectionCallbacks
{
    public LrtcPeerConnectionStateCb? on_signaling_state;
    public LrtcPeerConnectionStateCb? on_peer_connection_state;
    public LrtcPeerConnectionStateCb? on_ice_gathering_state;
    public LrtcPeerConnectionStateCb? on_ice_connection_state;
    public LrtcIceCandidateCb? on_ice_candidate;
    public LrtcDataChannelCreatedCb? on_data_channel;
    public LrtcVideoTrackCb? on_video_track;
    public LrtcAudioTrackCb? on_audio_track;
    public LrtcTrackCb? on_track;
    public LrtcTrackCb? on_remove_track;
    public LrtcVoidCb? on_renegotiation_needed;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcDataChannelCallbacks
{
    public LrtcDataChannelStateCb? on_state_change;
    public LrtcDataChannelMessageCb? on_message;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcDtmfSenderCallbacks
{
    public LrtcDtmfToneCb? on_tone_change;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcVideoSinkCallbacks
{
    public LrtcVideoFrameCb? on_frame;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcAudioSinkCallbacks
{
    public LrtcAudioFrameCb? on_data;
}
