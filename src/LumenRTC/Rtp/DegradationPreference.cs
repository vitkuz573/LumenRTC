namespace LumenRTC;

/// <summary>
/// Preference for video quality degradation behavior.
/// </summary>
public enum DegradationPreference
{
    Disabled = 0,
    MaintainFramerate = 1,
    MaintainResolution = 2,
    Balanced = 3,
}
