namespace LumenRTC;

/// <summary>
/// Options for creating a managed WebRTC runtime context.
/// </summary>
public sealed class RtcContextOptions
{
    public bool InitializeRuntime { get; set; } = true;

    public bool InitializeFactory { get; set; } = true;

    public bool TerminateFactoryOnDispose { get; set; } = true;

    public bool TerminateRuntimeOnDispose { get; set; } = true;

    public RtcConfiguration? DefaultConfiguration { get; set; }

    public MediaConstraints? DefaultConstraints { get; set; }
}
