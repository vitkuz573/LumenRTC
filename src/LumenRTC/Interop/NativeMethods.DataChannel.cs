namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
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
}
