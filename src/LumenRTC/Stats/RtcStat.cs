namespace LumenRTC;

/// <summary>
/// Single stats entry from a WebRTC stats report.
/// </summary>
public sealed class RtcStat
{
    internal RtcStat(string id, string type, double timestampUs, JsonElement data)
    {
        Id = id;
        Type = type;
        TimestampUs = timestampUs;
        Data = data;
    }

    public string Id { get; }
    public string Type { get; }
    public double TimestampUs { get; }
    public JsonElement Data { get; }

    public bool Contains(string name)
    {
        return Data.ValueKind == JsonValueKind.Object &&
               !string.IsNullOrWhiteSpace(name) &&
               Data.TryGetProperty(name, out _);
    }

    public bool TryGetProperty(string name, out JsonElement value)
    {
        if (Data.ValueKind == JsonValueKind.Object &&
            !string.IsNullOrWhiteSpace(name) &&
            Data.TryGetProperty(name, out var prop))
        {
            value = prop;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetString(string name, out string? value)
    {
        if (TryGetProperty(name, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return true;
        }
        value = null;
        return false;
    }

    public bool TryGetDouble(string name, out double value)
    {
        if (TryGetProperty(name, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            return prop.TryGetDouble(out value);
        }
        value = 0;
        return false;
    }

    public bool TryGetInt64(string name, out long value)
    {
        if (TryGetProperty(name, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            return prop.TryGetInt64(out value);
        }
        value = 0;
        return false;
    }

    public bool TryGetBool(string name, out bool value)
    {
        if (TryGetProperty(name, out var prop) &&
            (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False))
        {
            value = prop.GetBoolean();
            return true;
        }
        value = false;
        return false;
    }

    public bool TryGetUInt32(string name, out uint value)
    {
        if (TryGetProperty(name, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            if (prop.TryGetUInt32(out value))
            {
                return true;
            }
            if (prop.TryGetInt64(out var intValue) && intValue >= 0 && intValue <= uint.MaxValue)
            {
                value = (uint)intValue;
                return true;
            }
            if (prop.TryGetDouble(out var doubleValue) &&
                doubleValue >= 0 && doubleValue <= uint.MaxValue)
            {
                value = (uint)doubleValue;
                return true;
            }
        }
        value = 0;
        return false;
    }

    public bool TryGetStringArray(string name, out IReadOnlyList<string> values)
    {
        if (!TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            values = Array.Empty<string>();
            return false;
        }

        var list = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    list.Add(value);
                }
            }
        }

        values = list;
        return true;
    }

    public string? GetStringOrDefault(string name, string? fallback = null)
    {
        return TryGetString(name, out var value) ? value : fallback;
    }

    public double? GetDoubleOrNull(string name)
    {
        return TryGetDouble(name, out var value) ? value : null;
    }

    public long? GetInt64OrNull(string name)
    {
        return TryGetInt64(name, out var value) ? value : null;
    }

    public uint? GetUInt32OrNull(string name)
    {
        return TryGetUInt32(name, out var value) ? value : null;
    }

    public bool? GetBoolOrNull(string name)
    {
        return TryGetBool(name, out var value) ? value : null;
    }

    public bool IsType(string type)
    {
        return !string.IsNullOrWhiteSpace(type) &&
               string.Equals(Type, type, StringComparison.OrdinalIgnoreCase);
    }
}
