namespace LumenRTC;

/// <summary>
/// Audio processing options for microphone or custom audio sources.
/// </summary>
public sealed class AudioOptions
{
    public bool EchoCancellation { get; set; } = true;
    public bool AutoGainControl { get; set; } = true;
    public bool NoiseSuppression { get; set; } = true;
    public bool HighpassFilter { get; set; } = false;
}
