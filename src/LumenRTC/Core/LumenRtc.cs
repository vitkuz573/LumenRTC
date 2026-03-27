namespace LumenRTC;

/// <summary>
/// Static entry point for initializing and terminating the native WebRTC runtime.
/// </summary>
public static class LumenRtc
{
    private static readonly Lazy<uint> _abiVersionMajor = new(NativeMethods.lrtc_abi_version_major);
    private static readonly Lazy<uint> _abiVersionMinor = new(NativeMethods.lrtc_abi_version_minor);
    private static readonly Lazy<uint> _abiVersionPatch = new(NativeMethods.lrtc_abi_version_patch);
    private static readonly Lazy<Version> _abiVersion = new(() =>
        new(checked((int)AbiVersionMajor), checked((int)AbiVersionMinor), checked((int)AbiVersionPatch)));
    private static readonly Lazy<string> _abiVersionString = new(ReadAbiVersionString);

    public static uint AbiVersionMajor => _abiVersionMajor.Value;
    public static uint AbiVersionMinor => _abiVersionMinor.Value;
    public static uint AbiVersionPatch => _abiVersionPatch.Value;
    public static Version AbiVersion => _abiVersion.Value;
    public static string AbiVersionString => _abiVersionString.Value;

    private static string ReadAbiVersionString()
    {
        const int BufferSize = 128;
        Span<byte> buffer = stackalloc byte[BufferSize];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                var written = NativeMethods.lrtc_abi_version_string((IntPtr)ptr, BufferSize);
                if (written > 0 && written < BufferSize)
                {
                    var span = buffer[..written];
                    var zeroIndex = span.IndexOf((byte)0);
                    var textLength = zeroIndex >= 0 ? zeroIndex : written;
                    return System.Text.Encoding.UTF8.GetString(buffer[..textLength]);
                }
            }
        }
        return $"{AbiVersionMajor}.{AbiVersionMinor}.{AbiVersionPatch}";
    }

    public static void Initialize()
    {
        var result = NativeMethods.lrtc_initialize();
        if (result != LrtcResult.Ok)
        {
            throw new InvalidOperationException($"LumenRTC init failed: {result}");
        }
    }

    public static void Terminate()
    {
        NativeMethods.lrtc_terminate();
    }
}
