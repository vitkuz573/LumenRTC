namespace LumenRTC;

public sealed class PeerConnectionCallbacks
{
    public Action<SignalingState>? OnSignalingState;
    public Action<PeerConnectionState>? OnPeerConnectionState;
    public Action<IceGatheringState>? OnIceGatheringState;
    public Action<IceConnectionState>? OnIceConnectionState;
    public Action<string, int, string>? OnIceCandidate;
    public Action<DataChannel>? OnDataChannel;
    public Action<VideoTrack>? OnVideoTrack;
    public Action<AudioTrack>? OnAudioTrack;
    public Action<RtpTransceiver, RtpReceiver>? OnTrack;
    public Action<RtpReceiver>? OnRemoveTrack;
    public Action? OnRenegotiationNeeded;

    private LrtcPeerConnectionStateCb? _signalingStateCb;
    private LrtcPeerConnectionStateCb? _pcStateCb;
    private LrtcPeerConnectionStateCb? _iceGatheringCb;
    private LrtcPeerConnectionStateCb? _iceConnectionCb;
    private LrtcIceCandidateCb? _iceCandidateCb;
    private LrtcDataChannelCreatedCb? _dataChannelCb;
    private LrtcVideoTrackCb? _videoTrackCb;
    private LrtcAudioTrackCb? _audioTrackCb;
    private LrtcTrackCb? _trackCb;
    private LrtcTrackCb? _removeTrackCb;
    private LrtcVoidCb? _renegotiationCb;

    internal LrtcPeerConnectionCallbacks BuildNative()
    {
        _signalingStateCb = (ud, state) => OnSignalingState?.Invoke((SignalingState)state);
        _pcStateCb = (ud, state) => OnPeerConnectionState?.Invoke((PeerConnectionState)state);
        _iceGatheringCb = (ud, state) => OnIceGatheringState?.Invoke((IceGatheringState)state);
        _iceConnectionCb = (ud, state) => OnIceConnectionState?.Invoke((IceConnectionState)state);
        _iceCandidateCb = (ud, mid, mline, cand) =>
            OnIceCandidate?.Invoke(Utf8String.Read(mid), mline, Utf8String.Read(cand));
        _dataChannelCb = (ud, channelPtr) => OnDataChannel?.Invoke(new DataChannel(channelPtr));
        _videoTrackCb = (ud, trackPtr) => OnVideoTrack?.Invoke(new VideoTrack(trackPtr));
        _audioTrackCb = (ud, trackPtr) => OnAudioTrack?.Invoke(new AudioTrack(trackPtr));
        _trackCb = (ud, transceiverPtr, receiverPtr) =>
            OnTrack?.Invoke(new RtpTransceiver(transceiverPtr), new RtpReceiver(receiverPtr));
        _removeTrackCb = (ud, transceiverPtr, receiverPtr) =>
        {
            if (receiverPtr != IntPtr.Zero)
            {
                OnRemoveTrack?.Invoke(new RtpReceiver(receiverPtr));
            }
        };
        _renegotiationCb = ud => OnRenegotiationNeeded?.Invoke();

        return new LrtcPeerConnectionCallbacks
        {
            on_signaling_state = _signalingStateCb,
            on_peer_connection_state = _pcStateCb,
            on_ice_gathering_state = _iceGatheringCb,
            on_ice_connection_state = _iceConnectionCb,
            on_ice_candidate = _iceCandidateCb,
            on_data_channel = _dataChannelCb,
            on_video_track = _videoTrackCb,
            on_audio_track = _audioTrackCb,
            on_track = _trackCb,
            on_remove_track = _removeTrackCb,
            on_renegotiation_needed = _renegotiationCb,
        };
    }
}
