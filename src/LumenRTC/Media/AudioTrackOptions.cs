namespace LumenRTC;

/// <summary>
/// Options for creating a local audio track.
/// </summary>
public sealed class AudioTrackOptions
{
    public string? TrackId { get; set; }

    public string? SourceLabel { get; set; } = "audio";

    public AudioSourceType SourceType { get; set; } = AudioSourceType.Microphone;

    public AudioOptions? AudioOptions { get; set; }
}
