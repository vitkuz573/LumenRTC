namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
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
    internal static extern IntPtr lrtc_factory_get_desktop_device(IntPtr factory);

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
    internal static extern IntPtr lrtc_factory_create_desktop_source(
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
    internal static extern void lrtc_factory_get_rtp_sender_codec_mime_types(
        IntPtr factory,
        LrtcMediaType mediaType,
        LrtcStatsSuccessCb success,
        LrtcStatsErrorCb failure,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_factory_get_rtp_sender_capabilities(
        IntPtr factory,
        LrtcMediaType mediaType,
        LrtcStatsSuccessCb success,
        LrtcStatsErrorCb failure,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_factory_get_rtp_receiver_capabilities(
        IntPtr factory,
        LrtcMediaType mediaType,
        LrtcStatsSuccessCb success,
        LrtcStatsErrorCb failure,
        IntPtr userData);
}
