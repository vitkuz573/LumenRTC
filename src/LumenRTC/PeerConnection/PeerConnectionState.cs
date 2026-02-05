namespace LumenRTC;

/// <summary>
/// Overall state of a peer connection.
/// </summary>
public enum PeerConnectionState
{
    New = 0,
    Connecting = 1,
    Connected = 2,
    Disconnected = 3,
    Failed = 4,
    Closed = 5,
}
