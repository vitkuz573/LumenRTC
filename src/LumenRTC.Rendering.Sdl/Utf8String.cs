using System;
using System.Runtime.InteropServices;

namespace LumenRTC.Rendering.Sdl;

internal sealed class Utf8String : IDisposable
{
    public IntPtr Pointer { get; private set; }

    public Utf8String(string? value)
    {
        Pointer = value == null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(value);
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
