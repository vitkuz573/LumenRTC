namespace LumenRTC;

/// <summary>
/// Helpers for common simulcast encoding configurations.
/// </summary>
public static class Simulcast
{
    public static RtpTransceiverInit CreateVideoSimulcast(
        string streamId,
        IReadOnlyList<SimulcastLayer> layers,
        RtpTransceiverDirection direction = RtpTransceiverDirection.SendRecv)
    {
        if (layers == null) throw new ArgumentNullException(nameof(layers));

        var init = new RtpTransceiverInit { Direction = direction };
        if (!string.IsNullOrWhiteSpace(streamId))
        {
            init.StreamIds.Add(streamId);
        }

        foreach (var layer in layers)
        {
            if (string.IsNullOrWhiteSpace(layer.Rid))
            {
                continue;
            }
            init.SendEncodings.Add(new RtpEncodingSettings
            {
                Rid = layer.Rid,
                ScaleResolutionDownBy = layer.ScaleResolutionDownBy,
                MaxBitrateBps = layer.MaxBitrateBps,
                MaxFramerate = layer.MaxFramerate,
                NumTemporalLayers = layer.NumTemporalLayers,
                ScalabilityMode = layer.ScalabilityMode,
                Active = true,
            });
        }

        return init;
    }
}
