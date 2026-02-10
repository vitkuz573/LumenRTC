namespace LumenRTC;

/// <summary>
/// Static entry point for initializing and terminating the native WebRTC runtime.
/// </summary>
public static class LumenRtc
{
    public static uint AbiVersionMajor => NativeMethods.lrtc_abi_version_major();
    public static uint AbiVersionMinor => NativeMethods.lrtc_abi_version_minor();
    public static uint AbiVersionPatch => NativeMethods.lrtc_abi_version_patch();

    public static Version AbiVersion =>
        new(checked((int)AbiVersionMajor), checked((int)AbiVersionMinor), checked((int)AbiVersionPatch));

    public static string AbiVersionString
    {
        get
        {
            const int maxAttempts = 4;
            var bufferSize = 64;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var buffer = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    var written = NativeMethods.lrtc_abi_version_string(buffer, (uint)bufferSize);
                    if (written <= 0)
                    {
                        return $"{AbiVersionMajor}.{AbiVersionMinor}.{AbiVersionPatch}";
                    }

                    if (written >= bufferSize)
                    {
                        bufferSize = checked(bufferSize * 2);
                        continue;
                    }

                    var bytes = new byte[written];
                    Marshal.Copy(buffer, bytes, 0, written);
                    var zeroIndex = Array.IndexOf(bytes, (byte)0);
                    var textLength = zeroIndex >= 0 ? zeroIndex : bytes.Length;
                    return System.Text.Encoding.UTF8.GetString(bytes, 0, textLength);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return $"{AbiVersionMajor}.{AbiVersionMinor}.{AbiVersionPatch}";
        }
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
