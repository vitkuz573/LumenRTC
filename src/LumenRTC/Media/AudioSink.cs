namespace LumenRTC;

/// <summary>
/// Receives audio frames from a track.
/// </summary>
public sealed class AudioSink : SafeHandle
{
    private readonly AudioSinkCallbacks _callbacks;

    public AudioSink(AudioSinkCallbacks callbacks)
        : base(IntPtr.Zero, true)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        var handle = NativeMethods.lrtc_audio_sink_create(ref native, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create audio sink.");
        }
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_audio_sink_release(handle);
        return true;
    }
}
