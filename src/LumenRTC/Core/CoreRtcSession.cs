namespace LumenRTC;

/// <summary>
/// Core API session that exposes native runtime and factory primitives directly.
/// </summary>
public sealed class CoreRtcSession : IDisposable
{
    private readonly bool _terminateFactory;
    private readonly bool _terminateRuntime;
    private bool _disposed;

    private CoreRtcSession(
        PeerConnectionFactory factory,
        bool terminateFactory,
        bool terminateRuntime)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _terminateFactory = terminateFactory;
        _terminateRuntime = terminateRuntime;
    }

    public PeerConnectionFactory Factory { get; }

    public static CoreRtcSession Create(CoreRtcOptions? options = null)
    {
        options ??= new CoreRtcOptions();

        if (options.InitializeRuntime)
        {
            LumenRtc.Initialize();
        }

        var factory = PeerConnectionFactory.Create();
        if (options.InitializeFactory)
        {
            factory.Initialize();
        }

        var terminateFactory = options.TerminateFactoryOnDispose && options.InitializeFactory;
        var terminateRuntime = options.TerminateRuntimeOnDispose && options.InitializeRuntime;
        return new CoreRtcSession(factory, terminateFactory, terminateRuntime);
    }

    public PeerConnection CreatePeerConnection(
        PeerConnectionCallbacks callbacks,
        RtcConfiguration? config = null,
        MediaConstraints? constraints = null)
    {
        return Factory.CreatePeerConnection(callbacks, config, constraints);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_terminateFactory)
        {
            Factory.Terminate();
        }

        Factory.Dispose();

        if (_terminateRuntime)
        {
            LumenRtc.Terminate();
        }
    }
}
