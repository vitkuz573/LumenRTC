namespace LumenRTC;

public sealed record RtpCapabilities(
    IReadOnlyList<RtpCodecCapability> Codecs,
    IReadOnlyList<RtpHeaderExtensionCapability> HeaderExtensions)
{
    public static RtpCapabilities Empty { get; } =
        new(Array.Empty<RtpCodecCapability>(), Array.Empty<RtpHeaderExtensionCapability>());
}
