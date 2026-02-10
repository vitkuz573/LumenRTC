namespace LumenRTC;

/// <summary>
/// Common WebRTC RTCStats type names.
/// </summary>
public static class RtcStatTypes
{
    public const string InboundRtp = "inbound-rtp";
    public const string OutboundRtp = "outbound-rtp";
    public const string RemoteInboundRtp = "remote-inbound-rtp";
    public const string RemoteOutboundRtp = "remote-outbound-rtp";
    public const string CandidatePair = "candidate-pair";
    public const string LocalCandidate = "local-candidate";
    public const string RemoteCandidate = "remote-candidate";
    public const string Transport = "transport";
    public const string Track = "track";
    public const string Codec = "codec";
}
