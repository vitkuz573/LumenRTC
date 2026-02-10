namespace LumenRTC;

/// <summary>
/// Parsed WebRTC stats report.
/// </summary>
public sealed class RtcStatsReport : IDisposable
{
    private readonly JsonDocument _document;
    private readonly Dictionary<string, RtcStat> _statsById;
    private readonly Dictionary<string, List<RtcStat>> _statsByType;

    private RtcStatsReport(JsonDocument document, List<RtcStat> stats, string rawJson)
    {
        _document = document;
        Stats = stats;
        RawJson = rawJson;
        _statsById = new Dictionary<string, RtcStat>(StringComparer.Ordinal);
        _statsByType = new Dictionary<string, List<RtcStat>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < stats.Count; i++)
        {
            var stat = stats[i];

            if (!string.IsNullOrWhiteSpace(stat.Id) && !_statsById.ContainsKey(stat.Id))
            {
                _statsById.Add(stat.Id, stat);
            }

            if (string.IsNullOrWhiteSpace(stat.Type))
            {
                continue;
            }

            if (!_statsByType.TryGetValue(stat.Type, out var byType))
            {
                byType = new List<RtcStat>();
                _statsByType[stat.Type] = byType;
            }
            byType.Add(stat);
        }
    }

    public string RawJson { get; }
    public IReadOnlyList<RtcStat> Stats { get; }
    public int Count => Stats.Count;
    public IReadOnlyCollection<string> Types => _statsByType.Keys;

    public IEnumerable<RtcStat> GetByType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            yield break;
        }

        if (!_statsByType.TryGetValue(type, out var byType))
        {
            yield break;
        }

        for (var i = 0; i < byType.Count; i++)
        {
            yield return byType[i];
        }
    }

    public IEnumerable<RtcStat> InboundRtp => GetByType(RtcStatTypes.InboundRtp);
    public IEnumerable<RtcStat> OutboundRtp => GetByType(RtcStatTypes.OutboundRtp);
    public IEnumerable<RtcStat> RemoteInboundRtp => GetByType(RtcStatTypes.RemoteInboundRtp);
    public IEnumerable<RtcStat> RemoteOutboundRtp => GetByType(RtcStatTypes.RemoteOutboundRtp);
    public IEnumerable<RtcStat> CandidatePairs => GetByType(RtcStatTypes.CandidatePair);
    public IEnumerable<RtcStat> Transport => GetByType(RtcStatTypes.Transport);
    public IEnumerable<RtcStat> Track => GetByType(RtcStatTypes.Track);

    public bool TryGetById(string id, out RtcStat? stat)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            stat = null;
            return false;
        }

        return _statsById.TryGetValue(id, out stat);
    }

    public RtcStat? GetByIdOrDefault(string id)
    {
        return TryGetById(id, out var stat) ? stat : null;
    }

    public RtcStat? GetFirstByType(string type)
    {
        foreach (var stat in GetByType(type))
        {
            return stat;
        }

        return null;
    }

    public IEnumerable<RtcStat> GetByTrackId(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            yield break;
        }

        foreach (var stat in Stats)
        {
            if (stat.TryGetString("trackId", out var value) &&
                string.Equals(value, trackId, StringComparison.Ordinal))
            {
                yield return stat;
            }
        }
    }

    public IEnumerable<RtcStat> GetBySsrc(uint ssrc)
    {
        foreach (var stat in Stats)
        {
            if (stat.TryGetUInt32("ssrc", out var value) && value == ssrc)
            {
                yield return stat;
                continue;
            }
            if (stat.TryGetInt64("ssrc", out var value64) && value64 == ssrc)
            {
                yield return stat;
            }
        }
    }

    public IEnumerable<RtcStat> Query(RtcStatQuery query)
    {
        IEnumerable<RtcStat> source = string.IsNullOrWhiteSpace(query.Type)
            ? Stats
            : GetByType(query.Type);

        foreach (var stat in source)
        {
            if (!string.IsNullOrWhiteSpace(query.Id) &&
                !string.Equals(stat.Id, query.Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(query.TrackId))
            {
                if (!stat.TryGetString("trackId", out var trackId) ||
                    !string.Equals(trackId, query.TrackId, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            if (query.Ssrc.HasValue)
            {
                var targetSsrc = query.Ssrc.Value;
                var matches = stat.TryGetUInt32("ssrc", out var ssrcValue) && ssrcValue == targetSsrc;
                if (!matches)
                {
                    matches = stat.TryGetInt64("ssrc", out var ssrcValue64) && ssrcValue64 == targetSsrc;
                }
                if (!matches)
                {
                    continue;
                }
            }

            if (query.Predicate != null && !query.Predicate(stat))
            {
                continue;
            }

            yield return stat;
        }
    }

    public RtcStat? GetFirst(in RtcStatQuery query)
    {
        foreach (var stat in Query(query))
        {
            return stat;
        }

        return null;
    }

    public IReadOnlyList<RtcStat> ToList(in RtcStatQuery query)
    {
        var result = new List<RtcStat>();
        foreach (var stat in Query(query))
        {
            result.Add(stat);
        }
        return result;
    }

    public bool TryGetSelectedCandidatePair(out RtcStat? selected)
    {
        foreach (var candidatePair in CandidatePairs)
        {
            if (candidatePair.TryGetBool("selected", out var isSelected) && isSelected)
            {
                selected = candidatePair;
                return true;
            }
        }

        selected = null;
        return false;
    }

    public static RtcStatsReport Parse(string? json)
    {
        var raw = string.IsNullOrWhiteSpace(json) ? "[]" : json!;
        var document = JsonDocument.Parse(raw);
        var stats = new List<RtcStat>();
        ParseStatsContainer(document.RootElement, stats);

        return new RtcStatsReport(document, stats, raw);
    }

    public void Dispose()
    {
        _document.Dispose();
    }

    private static void ParseStatsContainer(JsonElement root, List<RtcStat> stats)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                {
                    ParseStatsContainer(item, stats);
                }
                break;
            case JsonValueKind.Object:
                if (LooksLikeStat(root))
                {
                    TryAddStat(root, stats, fallbackId: null);
                    break;
                }

                if (root.TryGetProperty("stats", out var nestedStats))
                {
                    ParseStatsContainer(nestedStats, stats);
                }

                foreach (var property in root.EnumerateObject())
                {
                    if (property.NameEquals("stats"))
                    {
                        continue;
                    }

                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (LooksLikeStat(property.Value))
                        {
                            TryAddStat(property.Value, stats, property.Name);
                            continue;
                        }

                        ParseStatsContainer(property.Value, stats);
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        ParseStatsContainer(property.Value, stats);
                    }
                }
                break;
        }
    }

    private static bool LooksLikeStat(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty("type", out var typeProp) &&
               typeProp.ValueKind == JsonValueKind.String;
    }

    private static void TryAddStat(JsonElement element, List<RtcStat> stats, string? fallbackId)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var type = ReadString(element, "type");
        if (string.IsNullOrWhiteSpace(type))
        {
            return;
        }

        var id = ReadString(element, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = fallbackId ?? string.Empty;
        }

        var timestampUs = ReadTimestampUs(element);
        stats.Add(new RtcStat(id, type, timestampUs, element));
    }

    private static string ReadString(JsonElement element, string key)
    {
        if (element.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static double ReadTimestampUs(JsonElement element)
    {
        if (element.TryGetProperty("timestampUs", out var timestampUs) &&
            timestampUs.ValueKind == JsonValueKind.Number &&
            timestampUs.TryGetDouble(out var valueUs))
        {
            return valueUs;
        }

        if (element.TryGetProperty("timestampMs", out var timestampMs) &&
            timestampMs.ValueKind == JsonValueKind.Number &&
            timestampMs.TryGetDouble(out var valueMs))
        {
            return valueMs * 1000.0;
        }

        if (element.TryGetProperty("timestamp", out var timestamp) &&
            timestamp.ValueKind == JsonValueKind.Number &&
            timestamp.TryGetDouble(out var value))
        {
            return value;
        }

        return 0;
    }
}
