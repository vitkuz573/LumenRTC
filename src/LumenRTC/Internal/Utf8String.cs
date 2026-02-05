namespace LumenRTC.Internal;

internal sealed class Utf8String : IDisposable
{
    public IntPtr Pointer { get; private set; }

    public Utf8String(string? value)
    {
        Pointer = value == null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(value);
    }

    public static string Read(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    public void Dispose()
    {
        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(Pointer);
            Pointer = IntPtr.Zero;
        }
    }
}
