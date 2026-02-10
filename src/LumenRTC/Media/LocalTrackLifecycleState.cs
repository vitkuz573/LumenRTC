namespace LumenRTC;

/// <summary>
/// Lifecycle state of a local track wrapper.
/// </summary>
public enum LocalTrackLifecycleState
{
    Created = 0,
    Running = 1,
    Stopped = 2,
    Disposed = 3,
}
