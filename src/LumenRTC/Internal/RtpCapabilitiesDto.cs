namespace LumenRTC.Internal;

internal sealed class RtpCapabilitiesDto
{
    public List<RtpCodecCapabilityDto>? Codecs { get; set; }
    public List<RtpHeaderExtensionCapabilityDto>? HeaderExtensions { get; set; }
}
