namespace LumenRTC;

public sealed class RtcStatsReport : IDisposable
{
    private readonly JsonDocument _document;

    private RtcStatsReport(JsonDocument document, List<RtcStat> stats, string rawJson)
    {
        _document = document;
        Stats = stats;
        RawJson = rawJson;
    }

    public string RawJson { get; }
    public IReadOnlyList<RtcStat> Stats { get; }

    public IEnumerable<RtcStat> GetByType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            yield break;
        }

        for (var i = 0; i < Stats.Count; i++)
        {
            var stat = Stats[i];
            if (string.Equals(stat.Type, type, StringComparison.OrdinalIgnoreCase))
            {
                yield return stat;
            }
        }
    }

    public IEnumerable<RtcStat> InboundRtp => GetByType("inbound-rtp");
    public IEnumerable<RtcStat> OutboundRtp => GetByType("outbound-rtp");
    public IEnumerable<RtcStat> RemoteInboundRtp => GetByType("remote-inbound-rtp");
    public IEnumerable<RtcStat> RemoteOutboundRtp => GetByType("remote-outbound-rtp");
    public IEnumerable<RtcStat> CandidatePairs => GetByType("candidate-pair");
    public IEnumerable<RtcStat> Transport => GetByType("transport");
    public IEnumerable<RtcStat> Track => GetByType("track");

    public IEnumerable<RtcStat> GetByTrackId(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            yield break;
        }

        for (var i = 0; i < Stats.Count; i++)
        {
            var stat = Stats[i];
            if (stat.TryGetString("trackId", out var value) &&
                string.Equals(value, trackId, StringComparison.Ordinal))
            {
                yield return stat;
            }
        }
    }

    public IEnumerable<RtcStat> GetBySsrc(uint ssrc)
    {
        for (var i = 0; i < Stats.Count; i++)
        {
            var stat = Stats[i];
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

    public static RtcStatsReport Parse(string? json)
    {
        var raw = string.IsNullOrWhiteSpace(json) ? "[]" : json!;
        var document = JsonDocument.Parse(raw);
        var stats = new List<RtcStat>();

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = element.TryGetProperty("id", out var idProp)
                    ? idProp.GetString() ?? string.Empty
                    : string.Empty;
                var type = element.TryGetProperty("type", out var typeProp)
                    ? typeProp.GetString() ?? string.Empty
                    : string.Empty;
                double timestamp = 0;
                if (element.TryGetProperty("timestampUs", out var tsProp) &&
                    tsProp.ValueKind == JsonValueKind.Number)
                {
                    tsProp.TryGetDouble(out timestamp);
                }

                stats.Add(new RtcStat(id, type, timestamp, element));
            }
        }

        return new RtcStatsReport(document, stats, raw);
    }

    public void Dispose()
    {
        _document.Dispose();
    }
}
