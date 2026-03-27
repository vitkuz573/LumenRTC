namespace LumenRTC.Internal;

internal static class NativeString
{
    // Strings shorter than this threshold are decoded via stackalloc to avoid
    // unmanaged heap round-trips. 512 bytes covers the vast majority of IDs,
    // codec names, SDP mid values, and similar short native strings.
    private const int StackAllocThreshold = 512;

    public static string GetString(IntPtr handle, Func<IntPtr, IntPtr, uint, int> getter)
    {
        var required = getter(handle, IntPtr.Zero, 0);
        if (required <= 0)
        {
            return string.Empty;
        }

        if (required <= StackAllocThreshold)
        {
            Span<byte> stack = stackalloc byte[required];
            unsafe
            {
                fixed (byte* ptr = stack)
                {
                    var result = getter(handle, (IntPtr)ptr, (uint)required);
                    if (result < 0)
                    {
                        return string.Empty;
                    }
                    // Exclude the null terminator if present.
                    var length = stack[required - 1] == 0 ? required - 1 : required;
                    return System.Text.Encoding.UTF8.GetString(stack[..length]);
                }
            }
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

        if (required <= StackAllocThreshold)
        {
            Span<byte> stack = stackalloc byte[required];
            unsafe
            {
                fixed (byte* ptr = stack)
                {
                    var result = getter(handle, index, (IntPtr)ptr, (uint)required);
                    if (result < 0)
                    {
                        return string.Empty;
                    }
                    var length = stack[required - 1] == 0 ? required - 1 : required;
                    return System.Text.Encoding.UTF8.GetString(stack[..length]);
                }
            }
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
