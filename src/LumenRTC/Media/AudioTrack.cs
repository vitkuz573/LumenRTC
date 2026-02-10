namespace LumenRTC;

/// <summary>
/// Local or remote audio track.
/// </summary>
public sealed partial class AudioTrack : SafeHandle
{
    internal AudioTrack(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }
}
