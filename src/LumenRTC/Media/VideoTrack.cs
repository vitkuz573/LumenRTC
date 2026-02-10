namespace LumenRTC;

/// <summary>
/// Local or remote video track.
/// </summary>
public sealed partial class VideoTrack : SafeHandle
{
    internal VideoTrack(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }
}
