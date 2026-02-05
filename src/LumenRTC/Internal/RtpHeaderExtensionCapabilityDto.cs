namespace LumenRTC;

internal sealed class RtpHeaderExtensionCapabilityDto
{
    public string? Uri { get; set; }
    public int PreferredId { get; set; }
    public bool PreferredEncrypt { get; set; }
}
