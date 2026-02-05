namespace LumenRTC;

/// <summary>
/// State of ICE candidate gathering.
/// </summary>
public enum IceGatheringState
{
    New = 0,
    Gathering = 1,
    Complete = 2,
}
