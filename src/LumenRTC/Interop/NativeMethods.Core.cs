namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
    private const string LibName = "lumenrtc";

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern LrtcResult lrtc_initialize();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_terminate();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_logging_set_min_level(int severity);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_logging_set_callback(
        int severity,
        LrtcLogMessageCb callback,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_logging_remove_callback();
}
