namespace LumenRTC;

/// <summary>
/// Supported RTP codec capability.
/// </summary>
public readonly record struct RtpCodecCapability(
    string MimeType,
    int ClockRate,
    int Channels,
    string SdpFmtpLine);
