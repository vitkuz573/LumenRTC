namespace LumenRTC;

public readonly record struct RtpEncodingInfo(
    uint Ssrc,
    int MaxBitrateBps,
    int MinBitrateBps,
    double MaxFramerate,
    double ScaleResolutionDownBy,
    bool Active,
    double BitratePriority,
    RtpPriority NetworkPriority,
    int NumTemporalLayers,
    bool AdaptivePtime,
    string Rid,
    string ScalabilityMode);
