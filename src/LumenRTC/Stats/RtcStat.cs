namespace LumenRTC;

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

    public bool TryGetString(string name, out string? value)
    {
        if (Data.ValueKind == JsonValueKind.Object &&
            Data.TryGetProperty(name, out var prop) &&
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
        if (Data.ValueKind == JsonValueKind.Object &&
            Data.TryGetProperty(name, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            return prop.TryGetDouble(out value);
        }
        value = 0;
        return false;
    }

    public bool TryGetInt64(string name, out long value)
    {
        if (Data.ValueKind == JsonValueKind.Object &&
            Data.TryGetProperty(name, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            return prop.TryGetInt64(out value);
        }
        value = 0;
        return false;
    }

    public bool TryGetBool(string name, out bool value)
    {
        if (Data.ValueKind == JsonValueKind.Object &&
            Data.TryGetProperty(name, out var prop) &&
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
        if (Data.ValueKind == JsonValueKind.Object &&
            Data.TryGetProperty(name, out var prop) &&
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
}
