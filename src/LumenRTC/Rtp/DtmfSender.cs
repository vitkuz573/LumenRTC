namespace LumenRTC;

/// <summary>
/// Sends DTMF tones over an audio RTP sender.
/// </summary>
public sealed partial class DtmfSender : SafeHandle
{
    private DtmfSenderCallbacks? _callbacks;

    internal DtmfSender(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public void SetCallbacks(DtmfSenderCallbacks callbacks)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        NativeMethods.lrtc_dtmf_sender_set_callbacks(handle, ref native, IntPtr.Zero);
    }
}
