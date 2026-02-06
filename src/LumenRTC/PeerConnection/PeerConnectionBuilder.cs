namespace LumenRTC;

/// <summary>
/// Fluent builder for creating peer connections with callbacks and defaults.
/// </summary>
public sealed class PeerConnectionBuilder
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

    public PeerConnectionBuilder OnSignalingState(Action<SignalingState> handler)
    {
        _callbacks.OnSignalingState = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnPeerConnectionState(Action<PeerConnectionState> handler)
    {
        _callbacks.OnPeerConnectionState = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnIceGatheringState(Action<IceGatheringState> handler)
    {
        _callbacks.OnIceGatheringState = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnIceConnectionState(Action<IceConnectionState> handler)
    {
        _callbacks.OnIceConnectionState = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnIceCandidate(Action<IceCandidate> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        _callbacks.OnIceCandidate = (mid, index, cand) => handler(new IceCandidate(mid, index, cand));
        return this;
    }

    public PeerConnectionBuilder OnIceCandidate(Action<string, int, string> handler)
    {
        _callbacks.OnIceCandidate = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnDataChannel(Action<DataChannel> handler)
    {
        _callbacks.OnDataChannel = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnVideoTrack(Action<VideoTrack> handler)
    {
        _callbacks.OnVideoTrack = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnAudioTrack(Action<AudioTrack> handler)
    {
        _callbacks.OnAudioTrack = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnTrack(Action<RtpTransceiver, RtpReceiver> handler)
    {
        _callbacks.OnTrack = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnRemoveTrack(Action<RtpReceiver> handler)
    {
        _callbacks.OnRemoveTrack = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnectionBuilder OnRenegotiationNeeded(Action handler)
    {
        _callbacks.OnRenegotiationNeeded = handler ?? throw new ArgumentNullException(nameof(handler));
        return this;
    }

    public PeerConnection Build()
    {
        return _factory.CreatePeerConnection(_callbacks, Configuration, Constraints);
    }
}
