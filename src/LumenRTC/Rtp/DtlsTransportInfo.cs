namespace LumenRTC;

public readonly record struct DtlsTransportInfo(
    DtlsTransportState State,
    int SslCipherSuite,
    int SrtpCipherSuite);
