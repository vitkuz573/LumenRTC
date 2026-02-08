namespace LumenRTC;

/// <summary>
/// Entry point for the high-level Convenience API.
/// </summary>
public static class ConvenienceApi
{
    public static RtcContext CreateContext(RtcContextOptions? options = null)
    {
        return RtcContext.Create(options);
    }
}
