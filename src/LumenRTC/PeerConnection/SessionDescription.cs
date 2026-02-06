namespace LumenRTC;

/// <summary>
/// Represents an SDP session description.
/// </summary>
public readonly record struct SessionDescription(string Sdp, string Type)
{
    public SdpType ParsedType => SdpTypeExtensions.Parse(Type);

    public static SessionDescription From(SdpType type, string sdp)
    {
        return new SessionDescription(sdp, type.ToSdpString());
    }
}
