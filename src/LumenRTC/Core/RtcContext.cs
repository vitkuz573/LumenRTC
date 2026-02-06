namespace LumenRTC;

/// <summary>
/// Manages the native runtime and a peer connection factory lifetime.
/// </summary>
public sealed class RtcContext : IDisposable
{
    private readonly bool _terminateFactory;
    private readonly bool _terminateRuntime;
    private bool _disposed;

    private RtcContext(
        PeerConnectionFactory factory,
        RtcConfiguration? defaultConfiguration,
        MediaConstraints? defaultConstraints,
        bool terminateFactory,
        bool terminateRuntime)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        DefaultConfiguration = defaultConfiguration;
        DefaultConstraints = defaultConstraints;
        _terminateFactory = terminateFactory;
        _terminateRuntime = terminateRuntime;
    }

    public PeerConnectionFactory Factory { get; }

    public RtcConfiguration? DefaultConfiguration { get; }

    public MediaConstraints? DefaultConstraints { get; }

    public static RtcContext Create(RtcContextOptions? options = null)
    {
        options ??= new RtcContextOptions();

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

        return new RtcContext(factory, options.DefaultConfiguration, options.DefaultConstraints, terminateFactory, terminateRuntime);
    }

    public PeerConnection CreatePeerConnection(PeerConnectionCallbacks callbacks, RtcConfiguration? config = null, MediaConstraints? constraints = null)
    {
        return Factory.CreatePeerConnection(
            callbacks,
            config ?? DefaultConfiguration,
            constraints ?? DefaultConstraints);
    }

    public PeerConnectionBuilder CreatePeerConnectionBuilder()
    {
        return new PeerConnectionBuilder(Factory, DefaultConfiguration, DefaultConstraints);
    }

    public PeerConnection CreatePeerConnection(Action<PeerConnectionBuilder> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        var builder = CreatePeerConnectionBuilder();
        configure(builder);
        return builder.Build();
    }

    public LocalAudioTrack CreateLocalAudioTrack(AudioTrackOptions? options = null)
    {
        return Factory.CreateLocalAudioTrack(options);
    }

    public LocalVideoTrack CreateCameraTrack(CameraTrackOptions? options = null)
    {
        return Factory.CreateCameraTrack(options);
    }

    public LocalVideoTrack CreateScreenTrack(ScreenTrackOptions? options = null)
    {
        return Factory.CreateScreenTrack(options);
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
