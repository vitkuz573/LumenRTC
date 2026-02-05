namespace LumenRTC;

/// <summary>
/// Supported RTP header extension capability.
/// </summary>
public readonly record struct RtpHeaderExtensionCapability(
    string Uri,
    int PreferredId,
    bool PreferredEncrypt);
