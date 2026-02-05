namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_desktop_device_get_media_list(
        IntPtr device,
        LrtcDesktopType type);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_desktop_device_create_capturer(
        IntPtr device,
        IntPtr source,
        [MarshalAs(UnmanagedType.I1)] bool showCursor);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_desktop_device_release(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_desktop_media_list_update(
        IntPtr list,
        [MarshalAs(UnmanagedType.I1)] bool forceReload,
        [MarshalAs(UnmanagedType.I1)] bool getThumbnail);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_desktop_media_list_get_source_count(IntPtr list);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_desktop_media_list_get_source(
        IntPtr list,
        int index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_desktop_media_list_release(IntPtr list);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_media_source_get_id(
        IntPtr source,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_media_source_get_name(
        IntPtr source,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_media_source_get_type(IntPtr source);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_media_source_release(IntPtr source);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern LrtcDesktopCaptureState lrtc_desktop_capturer_start(
        IntPtr capturer,
        uint fps);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern LrtcDesktopCaptureState lrtc_desktop_capturer_start_region(
        IntPtr capturer,
        uint fps,
        uint x,
        uint y,
        uint w,
        uint h);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_desktop_capturer_stop(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_desktop_capturer_is_running(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_desktop_capturer_release(IntPtr capturer);
}
