namespace LumenRTC;

/// <summary>
/// RTC data channel for arbitrary message transport.
/// </summary>
public sealed partial class DataChannel : SafeHandle
{
    private DataChannelCallbacks? _callbacks;

    internal DataChannel(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public void SetCallbacks(DataChannelCallbacks callbacks)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        NativeMethods.lrtc_data_channel_set_callbacks(handle, ref native, IntPtr.Zero);
    }
}
