namespace LumenRTC;

/// <summary>
/// DTLS transport information for an RTP sender or receiver.
/// </summary>
public readonly record struct DtlsTransportInfo(
    DtlsTransportState State,
    int SslCipherSuite,
    int SrtpCipherSuite);
