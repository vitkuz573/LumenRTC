namespace LumenRTC;

/// <summary>
/// Represents an ICE candidate.
/// </summary>
public readonly record struct IceCandidate(string SdpMid, int SdpMlineIndex, string Candidate);
