namespace LumenRTC;

public sealed class AudioSource : SafeHandle
{
    internal AudioSource(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public void CaptureFrame(ReadOnlySpan<byte> data, int bitsPerSample, int sampleRate, int channels, int frames)
    {
        if (data.IsEmpty) return;
        unsafe
        {
            fixed (byte* ptr = data)
            {
                NativeMethods.lrtc_audio_source_capture_frame(handle, (IntPtr)ptr, bitsPerSample, sampleRate, (nuint)channels, (nuint)frames);
            }
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_audio_source_release(handle);
        return true;
    }
}
