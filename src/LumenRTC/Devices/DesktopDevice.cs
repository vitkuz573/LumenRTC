namespace LumenRTC;

/// <summary>
/// Enumerates desktop sources for screen/window capture.
/// </summary>
public sealed class DesktopDevice : SafeHandle
{
    internal DesktopDevice(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public DesktopMediaList GetMediaList(DesktopType type)
    {
        var list = NativeMethods.lrtc_desktop_device_get_media_list(handle, (LrtcDesktopType)type);
        if (list == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get desktop media list.");
        }
        return new DesktopMediaList(list, type);
    }

    public DesktopCapturer CreateCapturer(MediaSource source, bool showCursor = true)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var capturer = NativeMethods.lrtc_desktop_device_create_capturer(handle, source.DangerousGetHandle(), showCursor);
        if (capturer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create desktop capturer.");
        }
        return new DesktopCapturer(capturer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_desktop_device_release(handle);
        return true;
    }
}
