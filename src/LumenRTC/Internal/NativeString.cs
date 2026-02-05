namespace LumenRTC;

internal static class NativeString
{
    public static string GetString(IntPtr handle, Func<IntPtr, IntPtr, uint, int> getter)
    {
        var required = getter(handle, IntPtr.Zero, 0);
        if (required <= 0)
        {
            return string.Empty;
        }

        var buffer = Marshal.AllocHGlobal(required);
        try
        {
            var result = getter(handle, buffer, (uint)required);
            if (result < 0)
            {
                return string.Empty;
            }
            return Utf8String.Read(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static string GetIndexedString(
        IntPtr handle,
        uint index,
        Func<IntPtr, uint, IntPtr, uint, int> getter)
    {
        var required = getter(handle, index, IntPtr.Zero, 0);
        if (required <= 0)
        {
            return string.Empty;
        }

        var buffer = Marshal.AllocHGlobal(required);
        try
        {
            var result = getter(handle, index, buffer, (uint)required);
            if (result < 0)
            {
                return string.Empty;
            }
            return Utf8String.Read(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
