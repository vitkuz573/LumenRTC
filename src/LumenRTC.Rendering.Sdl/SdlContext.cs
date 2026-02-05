using System;
using System.Runtime.InteropServices;

namespace LumenRTC.Rendering.Sdl;

internal sealed class SdlContext : IDisposable
{
    private static readonly object Sync = new();
    private static int _refCount;
    private bool _disposed;

    public static SdlContext Acquire()
    {
        lock (Sync)
        {
            if (_refCount == 0)
            {
                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        SdlNative.SDL_SetMainReady();
                    }

                    var result = SdlNative.SDL_Init(SdlNative.SDL_INIT_VIDEO);
                    if (result != 0)
                    {
                        throw new InvalidOperationException($"SDL_Init failed: {SdlNative.GetError()}");
                    }
                }
                catch (DllNotFoundException ex)
                {
                    throw new InvalidOperationException(
                        "SDL2 runtime not found. Install SDL2 and make sure it is on PATH/LD_LIBRARY_PATH.",
                        ex);
                }
                catch (EntryPointNotFoundException ex)
                {
                    throw new InvalidOperationException(
                        "SDL2 entry points not found. Verify the SDL2 runtime matches the expected version.",
                        ex);
                }
            }

            _refCount++;
        }

        return new SdlContext();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (Sync)
        {
            if (_refCount > 0 && --_refCount == 0)
            {
                SdlNative.SDL_Quit();
            }
        }

        _disposed = true;
    }
}
