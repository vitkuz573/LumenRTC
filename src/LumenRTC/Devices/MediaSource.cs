namespace LumenRTC;

/// <summary>
/// Represents a desktop capture source (screen or window).
/// </summary>
public sealed partial class MediaSource : SafeHandle
{
    internal MediaSource(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
        Id = NativeString.GetString(handle, NativeMethods.lrtc_media_source_get_id);
        Name = NativeString.GetString(handle, NativeMethods.lrtc_media_source_get_name);
        var typeValue = NativeMethods.lrtc_media_source_get_type(handle);
        Type = typeValue < 0 ? DesktopType.Screen : (DesktopType)typeValue;
    }

    public string Id { get; }
    public string Name { get; }
    public DesktopType Type { get; }
}
