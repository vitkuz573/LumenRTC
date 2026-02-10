namespace LumenRTC;

/// <summary>
/// Captures frames from a camera device.
/// </summary>
public sealed partial class VideoCapturer : SafeHandle
{
    internal VideoCapturer(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }
}
