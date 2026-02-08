namespace LumenRTC;

/// <summary>
/// Entry point for the low-level Core API.
/// </summary>
public static class CoreApi
{
    public static CoreRtcSession CreateSession(CoreRtcOptions? options = null)
    {
        return CoreRtcSession.Create(options);
    }
}
