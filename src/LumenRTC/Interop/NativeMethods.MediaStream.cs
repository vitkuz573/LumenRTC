namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_media_stream_add_audio_track(IntPtr stream, IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_media_stream_add_video_track(IntPtr stream, IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_media_stream_remove_audio_track(IntPtr stream, IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_media_stream_remove_video_track(IntPtr stream, IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_media_stream_get_id(IntPtr stream, IntPtr buffer, uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_media_stream_get_label(IntPtr stream, IntPtr buffer, uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_media_stream_release(IntPtr stream);
}
