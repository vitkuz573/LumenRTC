namespace LumenRTC;

/// <summary>
/// Supported RTP codecs and header extensions.
/// </summary>
public sealed record RtpCapabilities(
    IReadOnlyList<RtpCodecCapability> Codecs,
    IReadOnlyList<RtpHeaderExtensionCapability> HeaderExtensions)
{
    public static RtpCapabilities Empty { get; } =
        new(Array.Empty<RtpCodecCapability>(), Array.Empty<RtpHeaderExtensionCapability>());
}
