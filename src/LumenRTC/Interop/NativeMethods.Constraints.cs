namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
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
}
