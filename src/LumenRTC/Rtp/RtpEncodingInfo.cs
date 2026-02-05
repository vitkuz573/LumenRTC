namespace LumenRTC;

/// <summary>
/// Effective encoding settings reported by RTP sender or receiver.
/// </summary>
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
