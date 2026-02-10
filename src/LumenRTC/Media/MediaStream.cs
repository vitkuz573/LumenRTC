namespace LumenRTC;

/// <summary>
/// Collection of audio and video tracks.
/// </summary>
public sealed partial class MediaStream : SafeHandle
{
    public string Id { get; }
    public string Label { get; }

    internal MediaStream(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
        Id = NativeString.GetString(handle, NativeMethods.lrtc_media_stream_get_id);
        Label = NativeString.GetString(handle, NativeMethods.lrtc_media_stream_get_label);
    }
}
