namespace LumenRTC;

/// <summary>
/// Controls SRTP/DTLS media security mode.
/// </summary>
public enum MediaSecurityType
{
    SrtpNone = 0,
    SdesSrtp = 1,
    DtlsSrtp = 2,
}
