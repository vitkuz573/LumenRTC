namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_set_encoding_parameters(
        IntPtr sender,
        ref LrtcRtpEncodingSettings settings);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_set_encoding_parameters_at(
        IntPtr sender,
        uint index,
        ref LrtcRtpEncodingSettings settings);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_rtp_sender_encoding_count(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_get_encoding_info(
        IntPtr sender,
        uint index,
        out LrtcRtpEncodingInfo info);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_get_encoding_rid(
        IntPtr sender,
        uint index,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_get_encoding_scalability_mode(
        IntPtr sender,
        uint index,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_get_degradation_preference(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_get_parameters_mid(
        IntPtr sender,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_get_dtls_info(
        IntPtr sender,
        out LrtcDtlsTransportInfo info);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_rtp_sender_get_ssrc(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_replace_audio_track(
        IntPtr sender,
        IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_replace_video_track(
        IntPtr sender,
        IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_get_media_type(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_get_id(
        IntPtr sender,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_rtp_sender_stream_id_count(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_get_stream_id(
        IntPtr sender,
        uint index,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_sender_set_stream_ids(
        IntPtr sender,
        IntPtr streamIds,
        uint streamIdCount);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_rtp_sender_get_audio_track(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_rtp_sender_get_video_track(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_rtp_sender_get_dtmf_sender(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_dtmf_sender_set_callbacks(
        IntPtr sender,
        ref LrtcDtmfSenderCallbacks callbacks,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_dtmf_sender_can_insert(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_dtmf_sender_insert(
        IntPtr sender,
        IntPtr tones,
        int duration,
        int interToneGap,
        int commaDelay);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_dtmf_sender_tones(
        IntPtr sender,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_dtmf_sender_duration(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_dtmf_sender_inter_tone_gap(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_dtmf_sender_comma_delay(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_dtmf_sender_release(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_rtp_sender_release(IntPtr sender);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_get_media_type(IntPtr receiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_get_id(
        IntPtr receiver,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_rtp_receiver_encoding_count(IntPtr receiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_get_encoding_info(
        IntPtr receiver,
        uint index,
        out LrtcRtpEncodingInfo info);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_get_encoding_rid(
        IntPtr receiver,
        uint index,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_get_encoding_scalability_mode(
        IntPtr receiver,
        uint index,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_get_degradation_preference(IntPtr receiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_get_parameters_mid(
        IntPtr receiver,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_get_dtls_info(
        IntPtr receiver,
        out LrtcDtlsTransportInfo info);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_rtp_receiver_stream_id_count(IntPtr receiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_get_stream_id(
        IntPtr receiver,
        uint index,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_rtp_receiver_stream_count(IntPtr receiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_rtp_receiver_get_stream(
        IntPtr receiver,
        uint index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_rtp_receiver_get_audio_track(IntPtr receiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_rtp_receiver_get_video_track(IntPtr receiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_receiver_set_jitter_buffer_min_delay(
        IntPtr receiver,
        double delaySeconds);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_rtp_receiver_release(IntPtr receiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_get_media_type(IntPtr transceiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_get_mid(
        IntPtr transceiver,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_get_direction(IntPtr transceiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_get_current_direction(IntPtr transceiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_get_fired_direction(IntPtr transceiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_get_id(
        IntPtr transceiver,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_get_stopped(IntPtr transceiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_get_stopping(IntPtr transceiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_set_direction(
        IntPtr transceiver,
        LrtcRtpTransceiverDirection direction,
        IntPtr error,
        uint errorLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_rtp_transceiver_stop(
        IntPtr transceiver,
        IntPtr error,
        uint errorLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_rtp_transceiver_get_sender(IntPtr transceiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_rtp_transceiver_get_receiver(IntPtr transceiver);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_rtp_transceiver_release(IntPtr transceiver);
}
