namespace LumenRTC;

/// <summary>
/// Initialization parameters for creating a transceiver.
/// </summary>
public sealed class RtpTransceiverInit
{
    public RtpTransceiverDirection Direction { get; set; } = RtpTransceiverDirection.SendRecv;
    public List<string> StreamIds { get; } = new();
    public List<RtpEncodingSettings> SendEncodings { get; } = new();
}

public readonly record struct SimulcastLayer(
    string Rid,
    double ScaleResolutionDownBy,
    int? MaxBitrateBps = null,
    double? MaxFramerate = null,
    int? NumTemporalLayers = null,
    string? ScalabilityMode = null);
