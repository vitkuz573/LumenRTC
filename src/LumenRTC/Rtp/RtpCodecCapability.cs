namespace LumenRTC;

public readonly record struct RtpCodecCapability(
    string MimeType,
    int ClockRate,
    int Channels,
    string SdpFmtpLine);
