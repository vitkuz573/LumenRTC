namespace LumenRTC;

/// <summary>
/// Represents a WebRTC peer connection.
/// </summary>
public sealed partial class PeerConnection : SafeHandle
{
    private readonly PeerConnectionCallbacks _callbacks;
    private readonly List<Delegate> _keepAlive = new();

    internal PeerConnection(IntPtr handle, PeerConnectionCallbacks callbacks)
        : base(IntPtr.Zero, true)
    {
        _callbacks = callbacks;
        SetHandle(handle);
    }

    public new void Close()
    {
        NativeMethods.lrtc_peer_connection_close(handle);
    }

    public DataChannel CreateDataChannel(string label, DataChannelInit? init = null)
    {
        var settings = init ?? DataChannelInit.Default;
        using var labelUtf8 = new Utf8String(label);
        using var protocolUtf8 = new Utf8String(settings.Protocol);
        var channel = NativeMethods.lrtc_peer_connection_create_data_channel(
            handle,
            labelUtf8.Pointer,
            settings.Ordered ? 1 : 0,
            settings.Reliable ? 1 : 0,
            settings.MaxRetransmitTime,
            settings.MaxRetransmits,
            protocolUtf8.Pointer,
            settings.Negotiated ? 1 : 0,
            settings.Id);
        if (channel == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create data channel.");
        }
        return new DataChannel(channel);
    }

    public void CreateOffer(Action<string, string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcSdpSuccessCb successCb = (_, sdpPtr, typePtr) =>
            onSuccess(Utf8String.Read(sdpPtr), Utf8String.Read(typePtr));
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_create_offer(handle, successCb, errorCb, IntPtr.Zero, IntPtr.Zero);
    }

    public void CreateOffer(Action<SessionDescription> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        CreateOffer((sdp, type) => onSuccess(new SessionDescription(sdp, type)), onFailure);
    }

    public void CreateAnswer(Action<string, string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcSdpSuccessCb successCb = (_, sdpPtr, typePtr) =>
            onSuccess(Utf8String.Read(sdpPtr), Utf8String.Read(typePtr));
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_create_answer(handle, successCb, errorCb, IntPtr.Zero, IntPtr.Zero);
    }

    public void CreateAnswer(Action<SessionDescription> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        CreateAnswer((sdp, type) => onSuccess(new SessionDescription(sdp, type)), onFailure);
    }

    public void RestartIce()
    {
        NativeMethods.lrtc_peer_connection_restart_ice(handle);
    }

    public void SetLocalDescription(string sdp, string type, Action onSuccess, Action<string> onFailure)
    {
        using var sdpUtf8 = new Utf8String(sdp);
        using var typeUtf8 = new Utf8String(type);

        LrtcVoidCb successCb = _ => onSuccess?.Invoke();
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure?.Invoke(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_set_local_description(
            handle, sdpUtf8.Pointer, typeUtf8.Pointer, successCb, errorCb, IntPtr.Zero);
    }

    public void SetLocalDescription(SessionDescription description, Action onSuccess, Action<string> onFailure)
    {
        SetLocalDescription(description.Sdp, description.Type, onSuccess, onFailure);
    }

    public void SetRemoteDescription(string sdp, string type, Action onSuccess, Action<string> onFailure)
    {
        using var sdpUtf8 = new Utf8String(sdp);
        using var typeUtf8 = new Utf8String(type);

        LrtcVoidCb successCb = _ => onSuccess?.Invoke();
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure?.Invoke(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_set_remote_description(
            handle, sdpUtf8.Pointer, typeUtf8.Pointer, successCb, errorCb, IntPtr.Zero);
    }

    public void SetRemoteDescription(SessionDescription description, Action onSuccess, Action<string> onFailure)
    {
        SetRemoteDescription(description.Sdp, description.Type, onSuccess, onFailure);
    }

    public void GetLocalDescription(Action<string, string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcSdpSuccessCb successCb = (_, sdpPtr, typePtr) =>
            onSuccess(Utf8String.Read(sdpPtr), Utf8String.Read(typePtr));
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_get_local_description(handle, successCb, errorCb, IntPtr.Zero);
    }

    public void GetLocalDescription(Action<SessionDescription> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        GetLocalDescription((sdp, type) => onSuccess(new SessionDescription(sdp, type)), onFailure);
    }

    public void GetRemoteDescription(Action<string, string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcSdpSuccessCb successCb = (_, sdpPtr, typePtr) =>
            onSuccess(Utf8String.Read(sdpPtr), Utf8String.Read(typePtr));
        LrtcSdpErrorCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_get_remote_description(handle, successCb, errorCb, IntPtr.Zero);
    }

    public void GetRemoteDescription(Action<SessionDescription> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        GetRemoteDescription((sdp, type) => onSuccess(new SessionDescription(sdp, type)), onFailure);
    }

    public void AddIceCandidate(string sdpMid, int sdpMlineIndex, string candidate)
    {
        using var midUtf8 = new Utf8String(sdpMid);
        using var candUtf8 = new Utf8String(candidate);
        NativeMethods.lrtc_peer_connection_add_ice_candidate(
            handle,
            midUtf8.Pointer,
            sdpMlineIndex,
            candUtf8.Pointer);
    }

    public bool TryAddIceCandidate(string sdpMid, int sdpMlineIndex, string candidate)
    {
        if (string.IsNullOrWhiteSpace(sdpMid))
        {
            return false;
        }
        if (candidate == null)
        {
            return false;
        }

        using var midUtf8 = new Utf8String(sdpMid);
        using var candUtf8 = new Utf8String(candidate);
        var result = NativeMethods.lrtc_peer_connection_add_ice_candidate_ex(
            handle,
            midUtf8.Pointer,
            sdpMlineIndex,
            candUtf8.Pointer);
        return result != 0;
    }

    public void AddIceCandidate(IceCandidate candidate)
    {
        AddIceCandidate(candidate.SdpMid, candidate.SdpMlineIndex, candidate.Candidate);
    }

    public void GetStats(Action<string> onSuccess, Action<string> onFailure)
    {
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcStatsSuccessCb successCb = (_, jsonPtr) => onSuccess(Utf8String.Read(jsonPtr));
        LrtcStatsFailureCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_get_stats(handle, successCb, errorCb, IntPtr.Zero);
    }

    public void GetSenderStats(RtpSender sender, Action<string> onSuccess, Action<string> onFailure)
    {
        if (sender == null) throw new ArgumentNullException(nameof(sender));
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcStatsSuccessCb successCb = (_, jsonPtr) => onSuccess(Utf8String.Read(jsonPtr));
        LrtcStatsFailureCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_get_sender_stats(
            handle,
            sender.DangerousGetHandle(),
            successCb,
            errorCb,
            IntPtr.Zero);
    }

    public void GetReceiverStats(RtpReceiver receiver, Action<string> onSuccess, Action<string> onFailure)
    {
        if (receiver == null) throw new ArgumentNullException(nameof(receiver));
        if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));

        LrtcStatsSuccessCb successCb = (_, jsonPtr) => onSuccess(Utf8String.Read(jsonPtr));
        LrtcStatsFailureCb errorCb = (_, errPtr) => onFailure(Utf8String.Read(errPtr));

        _keepAlive.Add(successCb);
        _keepAlive.Add(errorCb);

        NativeMethods.lrtc_peer_connection_get_receiver_stats(
            handle,
            receiver.DangerousGetHandle(),
            successCb,
            errorCb,
            IntPtr.Zero);
    }

    public bool AddStream(MediaStream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        return NativeMethods.lrtc_peer_connection_add_stream(handle, stream.DangerousGetHandle());
    }

    public bool RemoveStream(MediaStream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        return NativeMethods.lrtc_peer_connection_remove_stream(handle, stream.DangerousGetHandle());
    }

    public bool AddAudioTrack(AudioTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        using var ids = new Utf8StringArray(streamIds);
        var result = NativeMethods.lrtc_peer_connection_add_audio_track(handle, track.DangerousGetHandle(), ids.Pointer, (uint)ids.Count);
        return result != 0;
    }

    public bool AddVideoTrack(VideoTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        using var ids = new Utf8StringArray(streamIds);
        var result = NativeMethods.lrtc_peer_connection_add_video_track(handle, track.DangerousGetHandle(), ids.Pointer, (uint)ids.Count);
        return result != 0;
    }

    public RtpSender AddAudioTrackSender(AudioTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        using var ids = new Utf8StringArray(streamIds);
        var sender = NativeMethods.lrtc_peer_connection_add_audio_track_sender(handle, track.DangerousGetHandle(), ids.Pointer, (uint)ids.Count);
        if (sender == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to add audio track sender.");
        }
        return new RtpSender(sender);
    }

    public RtpSender AddVideoTrackSender(VideoTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        using var ids = new Utf8StringArray(streamIds);
        var sender = NativeMethods.lrtc_peer_connection_add_video_track_sender(handle, track.DangerousGetHandle(), ids.Pointer, (uint)ids.Count);
        if (sender == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to add video track sender.");
        }
        return new RtpSender(sender);
    }

    public RtpTransceiver AddTransceiver(MediaType mediaType, RtpTransceiverInit? init = null)
    {
        IntPtr transceiver;
        if (init == null)
        {
            transceiver = NativeMethods.lrtc_peer_connection_add_transceiver(handle, (LrtcMediaType)mediaType);
        }
        else
        {
            using var marshaler = new RtpTransceiverInitMarshaler(init);
            var native = marshaler.Native;
            transceiver = NativeMethods.lrtc_peer_connection_add_transceiver_with_init(
                handle,
                (LrtcMediaType)mediaType,
                ref native);
        }
        if (transceiver == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to add transceiver.");
        }
        return new RtpTransceiver(transceiver);
    }

    public RtpTransceiver AddTransceiver(AudioTrack track, RtpTransceiverInit? init = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        IntPtr transceiver;
        if (init == null)
        {
            transceiver = NativeMethods.lrtc_peer_connection_add_audio_track_transceiver(
                handle,
                track.DangerousGetHandle());
        }
        else
        {
            using var marshaler = new RtpTransceiverInitMarshaler(init);
            var native = marshaler.Native;
            transceiver = NativeMethods.lrtc_peer_connection_add_audio_track_transceiver_with_init(
                handle,
                track.DangerousGetHandle(),
                ref native);
        }
        if (transceiver == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to add audio transceiver.");
        }
        return new RtpTransceiver(transceiver);
    }

    public RtpTransceiver AddTransceiver(VideoTrack track, RtpTransceiverInit? init = null)
    {
        if (track == null) throw new ArgumentNullException(nameof(track));
        IntPtr transceiver;
        if (init == null)
        {
            transceiver = NativeMethods.lrtc_peer_connection_add_video_track_transceiver(
                handle,
                track.DangerousGetHandle());
        }
        else
        {
            using var marshaler = new RtpTransceiverInitMarshaler(init);
            var native = marshaler.Native;
            transceiver = NativeMethods.lrtc_peer_connection_add_video_track_transceiver_with_init(
                handle,
                track.DangerousGetHandle(),
                ref native);
        }
        if (transceiver == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to add video transceiver.");
        }
        return new RtpTransceiver(transceiver);
    }

    public bool RemoveTrack(RtpSender sender)
    {
        if (sender == null) throw new ArgumentNullException(nameof(sender));
        var result = NativeMethods.lrtc_peer_connection_remove_track(handle, sender.DangerousGetHandle());
        return result != 0;
    }

    public IReadOnlyList<RtpSender> GetSenders()
    {
        var count = NativeMethods.lrtc_peer_connection_sender_count(handle);
        if (count == 0)
        {
            return Array.Empty<RtpSender>();
        }

        var list = new List<RtpSender>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var sender = NativeMethods.lrtc_peer_connection_get_sender(handle, i);
            if (sender != IntPtr.Zero)
            {
                list.Add(new RtpSender(sender));
            }
        }
        return list;
    }

    public IReadOnlyList<RtpReceiver> GetReceivers()
    {
        var count = NativeMethods.lrtc_peer_connection_receiver_count(handle);
        if (count == 0)
        {
            return Array.Empty<RtpReceiver>();
        }

        var list = new List<RtpReceiver>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var receiver = NativeMethods.lrtc_peer_connection_get_receiver(handle, i);
            if (receiver != IntPtr.Zero)
            {
                list.Add(new RtpReceiver(receiver));
            }
        }
        return list;
    }

    public IReadOnlyList<RtpTransceiver> GetTransceivers()
    {
        var count = NativeMethods.lrtc_peer_connection_transceiver_count(handle);
        if (count == 0)
        {
            return Array.Empty<RtpTransceiver>();
        }

        var list = new List<RtpTransceiver>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var transceiver = NativeMethods.lrtc_peer_connection_get_transceiver(handle, i);
            if (transceiver != IntPtr.Zero)
            {
                list.Add(new RtpTransceiver(transceiver));
            }
        }
        return list;
    }

    public bool SetCodecPreferences(MediaType mediaType, IReadOnlyList<string> mimeTypes)
    {
        if (mimeTypes == null) throw new ArgumentNullException(nameof(mimeTypes));
        using var mimes = new Utf8StringArray(mimeTypes);
        var result = NativeMethods.lrtc_peer_connection_set_codec_preferences(
            handle,
            (LrtcMediaType)mediaType,
            mimes.Pointer,
            (uint)mimes.Count);
        return result != 0;
    }

    public bool SetTransceiverCodecPreferences(RtpTransceiver transceiver, IReadOnlyList<string> mimeTypes)
    {
        if (transceiver == null) throw new ArgumentNullException(nameof(transceiver));
        if (mimeTypes == null) throw new ArgumentNullException(nameof(mimeTypes));
        using var mimes = new Utf8StringArray(mimeTypes);
        var result = NativeMethods.lrtc_peer_connection_set_transceiver_codec_preferences(
            handle,
            transceiver.DangerousGetHandle(),
            mimes.Pointer,
            (uint)mimes.Count);
        return result != 0;
    }
}
