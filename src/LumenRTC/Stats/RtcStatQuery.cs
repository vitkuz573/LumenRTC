namespace LumenRTC;

/// <summary>
/// Query options for filtering stats report entries.
/// </summary>
public readonly record struct RtcStatQuery(
    string? Type = null,
    string? Id = null,
    string? TrackId = null,
    uint? Ssrc = null,
    Func<RtcStat, bool>? Predicate = null);
