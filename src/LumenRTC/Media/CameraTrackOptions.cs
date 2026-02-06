namespace LumenRTC;

/// <summary>
/// Options for creating a local camera track.
/// </summary>
public sealed class CameraTrackOptions
{
    public string? TrackId { get; set; }

    public string? SourceLabel { get; set; } = "camera";

    public uint Width { get; set; } = 1280;

    public uint Height { get; set; } = 720;

    public uint Fps { get; set; } = 30;

    public int? DeviceIndex { get; set; }

    public string? DeviceName { get; set; }

    public MediaConstraints? Constraints { get; set; }

    public bool AutoStart { get; set; } = true;
}
