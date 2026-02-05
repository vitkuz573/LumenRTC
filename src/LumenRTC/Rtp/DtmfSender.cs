namespace LumenRTC;

public sealed class DtmfSender : SafeHandle
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

    public bool CanInsert => NativeMethods.lrtc_dtmf_sender_can_insert(handle) != 0;

    public bool InsertDtmf(string tones, int duration = 100, int interToneGap = 70, int commaDelay = -1)
    {
        if (tones == null) throw new ArgumentNullException(nameof(tones));
        using var utf8 = new Utf8String(tones);
        return NativeMethods.lrtc_dtmf_sender_insert(
            handle,
            utf8.Pointer,
            duration,
            interToneGap,
            commaDelay) != 0;
    }

    public string Tones => NativeString.GetString(handle, NativeMethods.lrtc_dtmf_sender_tones);

    public int Duration => NativeMethods.lrtc_dtmf_sender_duration(handle);

    public int InterToneGap => NativeMethods.lrtc_dtmf_sender_inter_tone_gap(handle);

    public int CommaDelay => NativeMethods.lrtc_dtmf_sender_comma_delay(handle);

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_dtmf_sender_release(handle);
        return true;
    }
}
