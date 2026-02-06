namespace LumenRTC;

/// <summary>
/// Options for creating a local screen share track.
/// </summary>
public sealed class ScreenTrackOptions
{
    public string? TrackId { get; set; }

    public string? SourceLabel { get; set; } = "screen";

    public DesktopType Type { get; set; } = DesktopType.Screen;

    public int SourceIndex { get; set; } = 0;

    public uint Fps { get; set; } = 30;

    public bool ShowCursor { get; set; } = true;

    public bool ForceReload { get; set; } = true;

    public bool GetThumbnail { get; set; } = false;

    public MediaConstraints? Constraints { get; set; }

    public bool AutoStart { get; set; } = true;
}
