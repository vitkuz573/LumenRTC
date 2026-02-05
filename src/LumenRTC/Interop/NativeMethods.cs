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

internal enum LrtcAudioSourceType : int
{
    Microphone = 0,
    Custom = 1,
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
internal delegate void LrtcStatsSuccessCb(IntPtr userData, IntPtr json);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcStatsErrorCb(IntPtr userData, IntPtr error);

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
    public LrtcVoidCb? on_renegotiation_needed;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LrtcDataChannelCallbacks
{
    public LrtcDataChannelStateCb? on_state_change;
    public LrtcDataChannelMessageCb? on_message;
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

internal static class NativeMethods
{
    private const string LibName = "lumenrtc";

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern LrtcResult lrtc_initialize();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_terminate();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_factory_create();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern LrtcResult lrtc_factory_initialize(IntPtr factory);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_factory_terminate(IntPtr factory);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_factory_release(IntPtr factory);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_factory_get_audio_device(IntPtr factory);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_factory_get_video_device(IntPtr factory);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_factory_create_audio_source(
        IntPtr factory,
        IntPtr label,
        LrtcAudioSourceType sourceType,
        ref LrtcAudioOptions options);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_factory_create_video_source(
        IntPtr factory,
        IntPtr capturer,
        IntPtr label,
        IntPtr constraints);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_factory_create_audio_track(
        IntPtr factory,
        IntPtr source,
        IntPtr trackId);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_factory_create_video_track(
        IntPtr factory,
        IntPtr source,
        IntPtr trackId);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_factory_create_stream(
        IntPtr factory,
        IntPtr streamId);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_media_constraints_create();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_media_constraints_add_mandatory(
        IntPtr constraints,
        IntPtr key,
        IntPtr value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_media_constraints_add_optional(
        IntPtr constraints,
        IntPtr key,
        IntPtr value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_media_constraints_release(IntPtr constraints);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern short lrtc_audio_device_playout_devices(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern short lrtc_audio_device_recording_devices(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_playout_device_name(
        IntPtr device,
        ushort index,
        IntPtr name,
        uint nameLen,
        IntPtr guid,
        uint guidLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_recording_device_name(
        IntPtr device,
        ushort index,
        IntPtr name,
        uint nameLen,
        IntPtr guid,
        uint guidLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_set_playout_device(IntPtr device, ushort index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_set_recording_device(IntPtr device, ushort index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_set_microphone_volume(IntPtr device, uint volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_microphone_volume(IntPtr device, out uint volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_set_speaker_volume(IntPtr device, uint volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_speaker_volume(IntPtr device, out uint volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_device_release(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_video_device_number_of_devices(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_device_get_device_name(
        IntPtr device,
        uint index,
        IntPtr name,
        uint nameLen,
        IntPtr uniqueId,
        uint uniqueIdLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_device_create_capturer(
        IntPtr device,
        IntPtr name,
        uint index,
        nuint width,
        nuint height,
        nuint targetFps);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_device_release(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_video_capturer_start(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_video_capturer_capture_started(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_capturer_stop(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_capturer_release(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_source_capture_frame(
        IntPtr source,
        IntPtr audioData,
        int bitsPerSample,
        int sampleRate,
        nuint channels,
        nuint frames);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_source_release(IntPtr source);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_source_release(IntPtr source);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_track_set_volume(IntPtr track, double volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_track_add_sink(IntPtr track, IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_track_remove_sink(IntPtr track, IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_track_release(IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_audio_sink_create(ref LrtcAudioSinkCallbacks callbacks, IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_sink_release(IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_media_stream_add_audio_track(IntPtr stream, IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_media_stream_add_video_track(IntPtr stream, IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_media_stream_remove_audio_track(IntPtr stream, IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_media_stream_remove_video_track(IntPtr stream, IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_media_stream_release(IntPtr stream);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_create(
        IntPtr factory,
        IntPtr config,
        IntPtr constraints,
        ref LrtcPeerConnectionCallbacks callbacks,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_set_callbacks(
        IntPtr pc,
        ref LrtcPeerConnectionCallbacks callbacks,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_close(IntPtr pc);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_release(IntPtr pc);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_create_offer(
        IntPtr pc,
        LrtcSdpSuccessCb success,
        LrtcSdpErrorCb failure,
        IntPtr userData,
        IntPtr constraints);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_create_answer(
        IntPtr pc,
        LrtcSdpSuccessCb success,
        LrtcSdpErrorCb failure,
        IntPtr userData,
        IntPtr constraints);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_restart_ice(IntPtr pc);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_set_local_description(
        IntPtr pc,
        IntPtr sdp,
        IntPtr type,
        LrtcVoidCb success,
        LrtcSdpErrorCb failure,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_set_remote_description(
        IntPtr pc,
        IntPtr sdp,
        IntPtr type,
        LrtcVoidCb success,
        LrtcSdpErrorCb failure,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_get_local_description(
        IntPtr pc,
        LrtcSdpSuccessCb success,
        LrtcSdpErrorCb failure,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_get_remote_description(
        IntPtr pc,
        LrtcSdpSuccessCb success,
        LrtcSdpErrorCb failure,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_get_stats(
        IntPtr pc,
        LrtcStatsSuccessCb success,
        LrtcStatsErrorCb failure,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_add_ice_candidate(
        IntPtr pc,
        IntPtr sdpMid,
        int sdpMlineIndex,
        IntPtr candidate);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_peer_connection_add_stream(IntPtr pc, IntPtr stream);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_peer_connection_remove_stream(IntPtr pc, IntPtr stream);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_peer_connection_add_audio_track(
        IntPtr pc,
        IntPtr track,
        IntPtr streamIds,
        uint streamIdCount);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_peer_connection_add_video_track(
        IntPtr pc,
        IntPtr track,
        IntPtr streamIds,
        uint streamIdCount);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_create_data_channel(
        IntPtr pc,
        IntPtr label,
        int ordered,
        int reliable,
        int maxRetransmitTime,
        int maxRetransmits,
        IntPtr protocol,
        int negotiated,
        int id);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_data_channel_set_callbacks(
        IntPtr channel,
        ref LrtcDataChannelCallbacks callbacks,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_data_channel_send(
        IntPtr channel,
        IntPtr data,
        uint size,
        int binary);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_data_channel_close(IntPtr channel);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_data_channel_release(IntPtr channel);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_sink_create(
        ref LrtcVideoSinkCallbacks callbacks,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_sink_release(IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_track_add_sink(IntPtr track, IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_track_remove_sink(IntPtr track, IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_track_release(IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_width(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_height(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_stride_y(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_stride_u(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_stride_v(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_frame_data_y(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_frame_data_u(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_frame_data_v(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_copy_i420(
        IntPtr frame,
        IntPtr dstY,
        int dstStrideY,
        IntPtr dstU,
        int dstStrideU,
        IntPtr dstV,
        int dstStrideV);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_to_argb(
        IntPtr frame,
        IntPtr dstArgb,
        int dstStrideArgb,
        int destWidth,
        int destHeight,
        int format);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_frame_retain(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_frame_release(IntPtr frame);
}
