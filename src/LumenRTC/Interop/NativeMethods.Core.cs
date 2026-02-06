using System.Reflection;
using System.Runtime.InteropServices;

namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
    private const string LibName = "lumenrtc";
    private static readonly string[] RequiredExports = OperatingSystem.IsWindows()
        ? new[] { "lrtc_initialize", "_lrtc_initialize", "_lrtc_initialize@0" }
        : new[] { "lrtc_initialize" };

    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, ResolveNative);
    }

    private static IntPtr ResolveNative(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibName, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in EnumerateNativeCandidates())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            if (!NativeLibrary.TryLoad(candidate, out var handle))
            {
                continue;
            }

            if (HasRequiredExport(handle))
            {
                return handle;
            }

            NativeLibrary.Free(handle);
        }

        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var fallbackHandle))
        {
            if (HasRequiredExport(fallbackHandle))
            {
                return fallbackHandle;
            }

            NativeLibrary.Free(fallbackHandle);
        }

        return IntPtr.Zero;
    }

    private static bool HasRequiredExport(nint handle)
    {
        foreach (var exportName in RequiredExports)
        {
            if (NativeLibrary.TryGetExport(handle, exportName, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateNativeCandidates()
    {
        var fileName = OperatingSystem.IsWindows()
            ? "lumenrtc.dll"
            : OperatingSystem.IsMacOS() ? "liblumenrtc.dylib" : "liblumenrtc.so";
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var candidates = new List<string>();

        AddNativeCandidate(candidates, seen, Environment.GetEnvironmentVariable("LumenRtcNativeDir"), fileName);
        AddNativeCandidate(candidates, seen, Environment.GetEnvironmentVariable("LUMENRTC_NATIVE_DIR"), fileName);
        AddNativeCandidate(candidates, seen, AppContext.BaseDirectory, fileName);
        AddNativeCandidate(candidates, seen, Path.GetDirectoryName(typeof(NativeMethods).Assembly.Location), fileName);
        AddNativeCandidate(candidates, seen, Path.Combine(Environment.CurrentDirectory, "native", "build"), fileName);
        AddNativeCandidate(candidates, seen, Path.Combine(Environment.CurrentDirectory, "native", "build", "Release"), fileName);

        foreach (var path in candidates)
        {
            yield return path;
        }
    }

    private static void AddNativeCandidate(List<string> candidates, HashSet<string> seen, string? directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));
        if (seen.Add(fullPath))
        {
            candidates.Add(fullPath);
        }
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern LrtcResult lrtc_initialize();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_terminate();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_logging_set_min_level(int severity);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_logging_set_callback(
        int severity,
        LrtcLogMessageCb callback,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_logging_remove_callback();
}
