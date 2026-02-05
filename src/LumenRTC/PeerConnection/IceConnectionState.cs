namespace LumenRTC;

/// <summary>
/// State of the ICE connection.
/// </summary>
public enum IceConnectionState
{
    New = 0,
    Checking = 1,
    Completed = 2,
    Connected = 3,
    Failed = 4,
    Disconnected = 5,
    Closed = 6,
    Max = 7,
}
