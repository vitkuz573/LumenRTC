namespace LumenRTC;

/// <summary>
/// Direction of media flow for a transceiver.
/// </summary>
public enum RtpTransceiverDirection
{
    SendRecv = 0,
    SendOnly = 1,
    RecvOnly = 2,
    Inactive = 3,
    Stopped = 4,
}
