namespace LumenRTC;

/// <summary>
/// Options for creating a Core API runtime session.
/// </summary>
public sealed class CoreRtcOptions
{
    public bool InitializeRuntime { get; set; } = true;

    public bool InitializeFactory { get; set; } = true;

    public bool TerminateFactoryOnDispose { get; set; } = true;

    public bool TerminateRuntimeOnDispose { get; set; } = true;
}
