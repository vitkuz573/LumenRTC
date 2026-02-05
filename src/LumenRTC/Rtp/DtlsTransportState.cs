namespace LumenRTC;

/// <summary>
/// DTLS transport state.
/// </summary>
public enum DtlsTransportState
{
    New = 0,
    Connecting = 1,
    Connected = 2,
    Closed = 3,
    Failed = 4,
}
