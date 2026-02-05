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
internal delegate void LrtcVideoFrameCb(IntPtr userData, IntPtr frame);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcDataChannelCreatedCb(IntPtr userData, IntPtr channel);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LrtcVideoTrackCb(IntPtr userData, IntPtr track);

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
    public LrtcVideoTrackCb? on_audio_track;
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
    internal static extern void lrtc_peer_connection_add_ice_candidate(
        IntPtr pc,
        IntPtr sdpMid,
        int sdpMlineIndex,
        IntPtr candidate);

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
