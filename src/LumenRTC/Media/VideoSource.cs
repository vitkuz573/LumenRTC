namespace LumenRTC;

/// <summary>
/// Video source used to create local video tracks.
/// </summary>
public sealed partial class VideoSource : SafeHandle
{
    internal VideoSource(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }
}
