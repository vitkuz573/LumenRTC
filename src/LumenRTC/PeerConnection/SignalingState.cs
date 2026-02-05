namespace LumenRTC;

/// <summary>
/// Signaling state as defined by WebRTC.
/// </summary>
public enum SignalingState
{
    Stable = 0,
    HaveLocalOffer = 1,
    HaveRemoteOffer = 2,
    HaveLocalPrAnswer = 3,
    HaveRemotePrAnswer = 4,
    Closed = 5,
}
