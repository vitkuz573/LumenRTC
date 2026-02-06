namespace LumenRTC;

/// <summary>
/// SDP description types.
/// </summary>
public enum SdpType
{
    Unknown = 0,
    Offer,
    Answer,
    Pranswer,
    Rollback,
}

/// <summary>
/// Helpers for converting SDP types to and from strings.
/// </summary>
public static class SdpTypeExtensions
{
    public static string ToSdpString(this SdpType type)
    {
        return type switch
        {
            SdpType.Offer => "offer",
            SdpType.Answer => "answer",
            SdpType.Pranswer => "pranswer",
            SdpType.Rollback => "rollback",
            _ => "",
        };
    }

    public static SdpType Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SdpType.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "offer" => SdpType.Offer,
            "answer" => SdpType.Answer,
            "pranswer" => SdpType.Pranswer,
            "rollback" => SdpType.Rollback,
            _ => SdpType.Unknown,
        };
    }
}
