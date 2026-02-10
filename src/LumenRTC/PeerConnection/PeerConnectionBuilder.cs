namespace LumenRTC;

/// <summary>
/// Fluent builder for creating peer connections with callbacks and defaults.
/// </summary>
public sealed partial class PeerConnectionBuilder
{
    private readonly PeerConnectionFactory _factory;
    private readonly PeerConnectionCallbacks _callbacks = new();

    public PeerConnectionBuilder(
        PeerConnectionFactory factory,
        RtcConfiguration? defaultConfiguration = null,
        MediaConstraints? defaultConstraints = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Configuration = defaultConfiguration;
        Constraints = defaultConstraints;
    }

    public PeerConnectionCallbacks Callbacks => _callbacks;

    public RtcConfiguration? Configuration { get; private set; }

    public MediaConstraints? Constraints { get; private set; }

    public PeerConnectionBuilder WithConfiguration(RtcConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        return this;
    }

    public PeerConnectionBuilder WithConstraints(MediaConstraints constraints)
    {
        Constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
        return this;
    }

    public PeerConnection Build()
    {
        return _factory.CreatePeerConnection(_callbacks, Configuration, Constraints);
    }
}
