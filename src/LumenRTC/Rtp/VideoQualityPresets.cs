namespace LumenRTC;

public static class VideoQualityPresets
{
    public static VideoQualityPreset LowLatency { get; } =
        new("LowLatency",
            MaxBitrateBps: 1_500_000,
            MaxFramerate: 60,
            ScaleResolutionDownBy: 1.0,
            DegradationPreference: DegradationPreference.MaintainFramerate,
            NetworkPriority: RtpPriority.High,
            BitratePriority: 1.2,
            NumTemporalLayers: 2);

    public static VideoQualityPreset Balanced { get; } =
        new("Balanced",
            MaxBitrateBps: 2_500_000,
            MaxFramerate: 60,
            ScaleResolutionDownBy: 1.0,
            DegradationPreference: DegradationPreference.Balanced,
            NetworkPriority: RtpPriority.High,
            BitratePriority: 1.0,
            NumTemporalLayers: 2);

    public static VideoQualityPreset HighQuality { get; } =
        new("HighQuality",
            MaxBitrateBps: 4_000_000,
            MaxFramerate: 60,
            ScaleResolutionDownBy: 1.0,
            DegradationPreference: DegradationPreference.Balanced,
            NetworkPriority: RtpPriority.High,
            BitratePriority: 1.1,
            NumTemporalLayers: 2);

    public static VideoQualityPreset HighResolution { get; } =
        new("HighResolution",
            MaxBitrateBps: 2_000_000,
            MaxFramerate: 30,
            ScaleResolutionDownBy: 1.0,
            DegradationPreference: DegradationPreference.MaintainResolution,
            NetworkPriority: RtpPriority.Medium,
            BitratePriority: 1.0);

    public static VideoQualityPreset LowBandwidth { get; } =
        new("LowBandwidth",
            MaxBitrateBps: 900_000,
            MaxFramerate: 20,
            ScaleResolutionDownBy: 1.5,
            DegradationPreference: DegradationPreference.MaintainResolution,
            NetworkPriority: RtpPriority.Low,
            BitratePriority: 0.8);
}
