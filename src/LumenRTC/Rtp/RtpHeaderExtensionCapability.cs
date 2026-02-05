namespace LumenRTC;

public readonly record struct RtpHeaderExtensionCapability(
    string Uri,
    int PreferredId,
    bool PreferredEncrypt);
