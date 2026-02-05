namespace LumenRTC;

public sealed record VideoQualityPreset(
    string Name,
    int MaxBitrateBps,
    double MaxFramerate,
    double ScaleResolutionDownBy,
    DegradationPreference DegradationPreference,
    RtpPriority NetworkPriority,
    double BitratePriority,
    int? NumTemporalLayers = null,
    string? ScalabilityMode = null)
{
    internal RtpEncodingSettings ToEncodingSettings()
    {
        return new RtpEncodingSettings
        {
            MaxBitrateBps = MaxBitrateBps,
            MaxFramerate = MaxFramerate,
            ScaleResolutionDownBy = ScaleResolutionDownBy,
            DegradationPreference = DegradationPreference,
            NetworkPriority = NetworkPriority,
            BitratePriority = BitratePriority,
            NumTemporalLayers = NumTemporalLayers,
            ScalabilityMode = ScalabilityMode,
            Active = true,
        };
    }
}
