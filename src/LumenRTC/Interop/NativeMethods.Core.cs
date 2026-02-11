using System.Reflection;
using System.Runtime.InteropServices;

namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
    private const string LibName = "lumenrtc";
    private const uint ExpectedAbiMajor = 1;

    private static readonly string[] InitializeExports = OperatingSystem.IsWindows()
        ? new[] { "lrtc_initialize", "_lrtc_initialize", "_lrtc_initialize@0" }
        : new[] { "lrtc_initialize" };

    private static readonly string[] AbiMajorExports = OperatingSystem.IsWindows()
        ? new[] { "lrtc_abi_version_major", "_lrtc_abi_version_major", "_lrtc_abi_version_major@0" }
        : new[] { "lrtc_abi_version_major" };

    private static readonly string[] AbiMinorExports = OperatingSystem.IsWindows()
        ? new[] { "lrtc_abi_version_minor", "_lrtc_abi_version_minor", "_lrtc_abi_version_minor@0" }
        : new[] { "lrtc_abi_version_minor" };

    private static readonly string[] AbiPatchExports = OperatingSystem.IsWindows()
        ? new[] { "lrtc_abi_version_patch", "_lrtc_abi_version_patch", "_lrtc_abi_version_patch@0" }
        : new[] { "lrtc_abi_version_patch" };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint AbiVersionGetter();

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

        string? lastValidationError = null;
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

            if (ValidateLibraryHandle(handle, candidate, out var validationError))
            {
                return handle;
            }

            lastValidationError = validationError;
            NativeLibrary.Free(handle);
        }

        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var fallbackHandle))
        {
            if (ValidateLibraryHandle(fallbackHandle, libraryName, out var validationError))
            {
                return fallbackHandle;
            }

            lastValidationError = validationError;
            NativeLibrary.Free(fallbackHandle);
        }

        if (!string.IsNullOrWhiteSpace(lastValidationError))
        {
            throw new DllNotFoundException(lastValidationError);
        }

        return IntPtr.Zero;
    }

    private static bool ValidateLibraryHandle(nint handle, string source, out string? error)
    {
        if (!TryGetExport(handle, InitializeExports, out _))
        {
            error = $"Native library '{source}' is missing required entry point '{InitializeExports[0]}'.";
            return false;
        }

        if (!TryGetExport(handle, AbiMajorExports, out var majorPtr))
        {
            error = $"Native library '{source}' is missing ABI version function '{AbiMajorExports[0]}'.";
            return false;
        }

        if (!TryGetExport(handle, AbiMinorExports, out var minorPtr))
        {
            error = $"Native library '{source}' is missing ABI version function '{AbiMinorExports[0]}'.";
            return false;
        }

        if (!TryGetExport(handle, AbiPatchExports, out var patchPtr))
        {
            error = $"Native library '{source}' is missing ABI version function '{AbiPatchExports[0]}'.";
            return false;
        }

        uint major;
        uint minor;
        uint patch;
        try
        {
            var majorGetter = Marshal.GetDelegateForFunctionPointer<AbiVersionGetter>(majorPtr);
            var minorGetter = Marshal.GetDelegateForFunctionPointer<AbiVersionGetter>(minorPtr);
            var patchGetter = Marshal.GetDelegateForFunctionPointer<AbiVersionGetter>(patchPtr);
            major = majorGetter();
            minor = minorGetter();
            patch = patchGetter();
        }
        catch (Exception ex)
        {
            error = $"Native library '{source}' ABI version probe failed: {ex.Message}";
            return false;
        }

        if (major != ExpectedAbiMajor)
        {
            error = $"Native library '{source}' ABI {major}.{minor}.{patch} is incompatible with managed ABI {ExpectedAbiMajor}.x.x.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryGetExport(nint handle, string[] candidateNames, out nint symbol)
    {
        foreach (var name in candidateNames)
        {
            if (NativeLibrary.TryGetExport(handle, name, out symbol))
            {
                return true;
            }
        }

        symbol = IntPtr.Zero;
        return false;
    }

    private static IEnumerable<string> EnumerateNativeCandidates()
    {
        // On Windows, prefer a non-colliding filename for NuGet publish (LumenRTC.dll vs lumenrtc.dll on case-insensitive paths).
        var fileNames = OperatingSystem.IsWindows()
            ? new[] { "lumenrtc_native.dll", "lumenrtc.dll" }
            : OperatingSystem.IsMacOS() ? new[] { "liblumenrtc.dylib" } : new[] { "liblumenrtc.so" };
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var candidates = new List<string>();

        AddNativeCandidate(candidates, seen, Environment.GetEnvironmentVariable("LumenRtcNativeDir"), fileNames);
        AddNativeCandidate(candidates, seen, Environment.GetEnvironmentVariable("LUMENRTC_NATIVE_DIR"), fileNames);
        AddNativeCandidate(candidates, seen, Path.Combine(AppContext.BaseDirectory, "native"), fileNames);
        AddNativeCandidate(candidates, seen, Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native"), fileNames);
        AddNativeCandidate(candidates, seen, AppContext.BaseDirectory, fileNames);
        AddNativeCandidate(candidates, seen, Path.Combine(Path.GetDirectoryName(typeof(NativeMethods).Assembly.Location) ?? string.Empty, "native"), fileNames);
        AddNativeCandidate(candidates, seen, Path.GetDirectoryName(typeof(NativeMethods).Assembly.Location), fileNames);
        AddNativeCandidate(candidates, seen, Path.Combine(Environment.CurrentDirectory, "native", "build"), fileNames);
        AddNativeCandidate(candidates, seen, Path.Combine(Environment.CurrentDirectory, "native", "build", "Release"), fileNames);

        foreach (var path in candidates)
        {
            yield return path;
        }
    }

    private static void AddNativeCandidate(List<string> candidates, HashSet<string> seen, string? directory, string[] fileNames)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        foreach (var fileName in fileNames)
        {
            var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));
            if (seen.Add(fullPath))
            {
                candidates.Add(fullPath);
            }
        }
    }

}
