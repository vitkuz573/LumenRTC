namespace LumenRTC;

internal sealed class RtpCodecCapabilityDto
{
    public string? MimeType { get; set; }
    public int ClockRate { get; set; }
    public int Channels { get; set; }
    public string? SdpFmtpLine { get; set; }
}
