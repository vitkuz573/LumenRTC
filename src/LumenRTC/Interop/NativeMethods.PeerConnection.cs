namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
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
    internal static extern void lrtc_peer_connection_get_sender_stats(
        IntPtr pc,
        IntPtr sender,
        LrtcStatsSuccessCb success,
        LrtcStatsErrorCb failure,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_peer_connection_get_receiver_stats(
        IntPtr pc,
        IntPtr receiver,
        LrtcStatsSuccessCb success,
        LrtcStatsErrorCb failure,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_peer_connection_set_codec_preferences(
        IntPtr pc,
        LrtcMediaType mediaType,
        IntPtr mimeTypes,
        uint mimeTypeCount);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_peer_connection_set_transceiver_codec_preferences(
        IntPtr pc,
        IntPtr transceiver,
        IntPtr mimeTypes,
        uint mimeTypeCount);

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
    internal static extern IntPtr lrtc_peer_connection_add_audio_track_sender(
        IntPtr pc,
        IntPtr track,
        IntPtr streamIds,
        uint streamIdCount);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_add_video_track_sender(
        IntPtr pc,
        IntPtr track,
        IntPtr streamIds,
        uint streamIdCount);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_add_transceiver(
        IntPtr pc,
        LrtcMediaType mediaType);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_add_audio_track_transceiver(
        IntPtr pc,
        IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_add_video_track_transceiver(
        IntPtr pc,
        IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_add_transceiver_with_init(
        IntPtr pc,
        LrtcMediaType mediaType,
        ref LrtcRtpTransceiverInit init);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_add_audio_track_transceiver_with_init(
        IntPtr pc,
        IntPtr track,
        ref LrtcRtpTransceiverInit init);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_add_video_track_transceiver_with_init(
        IntPtr pc,
        IntPtr track,
        ref LrtcRtpTransceiverInit init);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_peer_connection_remove_track(
        IntPtr pc,
        IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_peer_connection_sender_count(IntPtr pc);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_get_sender(
        IntPtr pc,
        uint index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_peer_connection_receiver_count(IntPtr pc);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_get_receiver(
        IntPtr pc,
        uint index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_peer_connection_transceiver_count(IntPtr pc);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_peer_connection_get_transceiver(
        IntPtr pc,
        uint index);

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
}
