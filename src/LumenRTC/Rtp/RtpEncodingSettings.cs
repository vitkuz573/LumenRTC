namespace LumenRTC;

public sealed class RtpEncodingSettings
{
    public int? MaxBitrateBps { get; set; }
    public int? MinBitrateBps { get; set; }
    public double? MaxFramerate { get; set; }
    public double? ScaleResolutionDownBy { get; set; }
    public bool? Active { get; set; }
    public DegradationPreference? DegradationPreference { get; set; }
    public double? BitratePriority { get; set; }
    public RtpPriority? NetworkPriority { get; set; }
    public int? NumTemporalLayers { get; set; }
    public string? ScalabilityMode { get; set; }
    public string? Rid { get; set; }
    public bool? AdaptivePtime { get; set; }

    internal LrtcRtpEncodingSettings ToNative(
        out Utf8String? rid,
        out Utf8String? scalabilityMode)
    {
        rid = string.IsNullOrWhiteSpace(Rid) ? null : new Utf8String(Rid);
        scalabilityMode = string.IsNullOrWhiteSpace(ScalabilityMode)
            ? null
            : new Utf8String(ScalabilityMode);

        return new LrtcRtpEncodingSettings
        {
            max_bitrate_bps = MaxBitrateBps ?? -1,
            min_bitrate_bps = MinBitrateBps ?? -1,
            max_framerate = MaxFramerate ?? -1,
            scale_resolution_down_by = ScaleResolutionDownBy ?? -1,
            active = Active.HasValue ? (Active.Value ? 1 : 0) : -1,
            degradation_preference = DegradationPreference.HasValue ? (int)DegradationPreference.Value : -1,
            bitrate_priority = BitratePriority ?? -1,
            network_priority = NetworkPriority.HasValue ? (int)NetworkPriority.Value : -1,
            num_temporal_layers = NumTemporalLayers ?? -1,
            scalability_mode = scalabilityMode?.Pointer ?? IntPtr.Zero,
            rid = rid?.Pointer ?? IntPtr.Zero,
            adaptive_ptime = AdaptivePtime.HasValue ? (AdaptivePtime.Value ? 1 : 0) : -1,
        };
    }
}
