namespace LumenRTC;

public sealed class DesktopMediaList : SafeHandle
{
    public DesktopType Type { get; }

    internal DesktopMediaList(IntPtr handle, DesktopType type) : base(IntPtr.Zero, true)
    {
        Type = type;
        SetHandle(handle);
    }

    public int UpdateSourceList(bool forceReload = false, bool getThumbnail = true)
    {
        return NativeMethods.lrtc_desktop_media_list_update(handle, forceReload, getThumbnail);
    }

    public int SourceCount => NativeMethods.lrtc_desktop_media_list_get_source_count(handle);

    public MediaSource GetSource(int index)
    {
        var source = NativeMethods.lrtc_desktop_media_list_get_source(handle, index);
        if (source == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get desktop media source.");
        }
        return new MediaSource(source);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_desktop_media_list_release(handle);
        return true;
    }
}
