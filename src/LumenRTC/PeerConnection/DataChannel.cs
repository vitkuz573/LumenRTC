namespace LumenRTC;

/// <summary>
/// RTC data channel for arbitrary message transport.
/// </summary>
public sealed class DataChannel : SafeHandle
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

    public void Send(ReadOnlySpan<byte> data, bool binary = true)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                NativeMethods.lrtc_data_channel_send(handle, (IntPtr)ptr, (uint)data.Length, binary ? 1 : 0);
            }
        }
    }

    public new void Close()
    {
        NativeMethods.lrtc_data_channel_close(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_data_channel_release(handle);
        return true;
    }
}
