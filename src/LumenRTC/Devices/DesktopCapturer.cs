namespace LumenRTC;

/// <summary>
/// Captures frames from desktop sources.
/// </summary>
public sealed partial class DesktopCapturer : SafeHandle
{
    internal DesktopCapturer(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }
}
