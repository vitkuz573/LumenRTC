using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LumenRTC.Rendering.Sdl;

internal static class SdlNative
{
    private const string LibName = "SDL2";

    static SdlNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(SdlNative).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        string[] candidates;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            candidates = new[] { "SDL2.dll" };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            candidates = new[] { "libSDL2-2.0.0.dylib", "libSDL2.dylib" };
        }
        else
        {
            candidates = new[] { "libSDL2-2.0.so.0", "libSDL2.so" };
        }

        foreach (var name in candidates)
        {
            if (NativeLibrary.TryLoad(name, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    internal const uint SDL_INIT_VIDEO = 0x00000020;
    internal const uint SDL_WINDOW_SHOWN = 0x00000004;
    internal const uint SDL_WINDOW_RESIZABLE = 0x00000020;
    internal const uint SDL_RENDERER_ACCELERATED = 0x00000002;
    internal const uint SDL_RENDERER_PRESENTVSYNC = 0x00000004;
    internal const int SDL_TEXTUREACCESS_STREAMING = 1;
    internal const uint SDL_WINDOWPOS_UNDEFINED = 0x1FFF0000;
    internal const uint SDL_WINDOWPOS_CENTERED = 0x2FFF0000;
    internal const uint SDL_QUIT = 0x100;

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct SDL_Event
    {
        public uint type;
        public fixed byte padding[56];
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_SetMainReady();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_Init(uint flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_Quit();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_CreateWindow(
        IntPtr title,
        int x,
        int y,
        int w,
        int h,
        uint flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_DestroyWindow(IntPtr window);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_CreateRenderer(IntPtr window, int index, uint flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_DestroyRenderer(IntPtr renderer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_CreateTexture(IntPtr renderer, uint format, int access, int w, int h);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_DestroyTexture(IntPtr texture);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_UpdateTexture(IntPtr texture, IntPtr rect, IntPtr pixels, int pitch);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_RenderClear(IntPtr renderer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_RenderCopy(IntPtr renderer, IntPtr texture, IntPtr srcRect, IntPtr dstRect);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_RenderPresent(IntPtr renderer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_PollEvent(out SDL_Event sdlEvent);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_Delay(uint ms);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint SDL_MasksToPixelFormatEnum(int bpp, uint rmask, uint gmask, uint bmask, uint amask);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SDL_GetError();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SDL_SetWindowSize(IntPtr window, int w, int h);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SDL_SetRenderDrawColor(IntPtr renderer, byte r, byte g, byte b, byte a);

    internal static string GetError()
    {
        var ptr = SDL_GetError();
        return ptr == IntPtr.Zero ? "unknown" : Marshal.PtrToStringUTF8(ptr) ?? "unknown";
    }
}
