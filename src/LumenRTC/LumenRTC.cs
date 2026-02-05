using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LumenRTC.Interop;

namespace LumenRTC;

public static class LumenRtc
{
    public static void Initialize()
    {
        var result = NativeMethods.lrtc_initialize();
        if (result != LrtcResult.Ok)
        {
            throw new InvalidOperationException($"LumenRTC init failed: {result}");
        }
    }

    public static void Terminate()
    {
        NativeMethods.lrtc_terminate();
    }
}

public enum PeerConnectionState
{
    New = 0,
    Connecting = 1,
    Connected = 2,
    Disconnected = 3,
    Failed = 4,
    Closed = 5,
}

public enum SignalingState
{
    Stable = 0,
    HaveLocalOffer = 1,
    HaveRemoteOffer = 2,
    HaveLocalPrAnswer = 3,
    HaveRemotePrAnswer = 4,
    Closed = 5,
}

public enum IceGatheringState
{
    New = 0,
    Gathering = 1,
    Complete = 2,
}

public enum IceConnectionState
{
    New = 0,
    Checking = 1,
    Completed = 2,
    Connected = 3,
    Failed = 4,
    Disconnected = 5,
    Closed = 6,
    Max = 7,
}

public enum DataChannelState
{
    Connecting = 0,
    Open = 1,
    Closing = 2,
    Closed = 3,
}

public sealed class PeerConnectionFactory : SafeHandle
{
    private PeerConnectionFactory() : base(IntPtr.Zero, true) { }

    public static PeerConnectionFactory Create()
    {
        var handle = NativeMethods.lrtc_factory_create();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create peer connection factory.");
        }
        var factory = new PeerConnectionFactory();
        factory.SetHandle(handle);
        return factory;
    }

    public void Initialize()
    {
        var result = NativeMethods.lrtc_factory_initialize(handle);
        if (result != LrtcResult.Ok)
        {
            throw new InvalidOperationException($"Factory initialize failed: {result}");
        }
    }

    public void Terminate()
    {
        NativeMethods.lrtc_factory_terminate(handle);
    }

    public PeerConnection CreatePeerConnection(PeerConnectionCallbacks callbacks)
    {
        if (callbacks == null)
        {
            throw new ArgumentNullException(nameof(callbacks));
        }
        var native = callbacks.BuildNative();
        var pcHandle = NativeMethods.lrtc_peer_connection_create(
            handle, IntPtr.Zero, IntPtr.Zero, ref native, IntPtr.Zero);
        if (pcHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create peer connection.");
        }
        return new PeerConnection(pcHandle, callbacks);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_factory_release(handle);
        return true;
    }
}

public sealed class PeerConnection : SafeHandle
{
    private readonly PeerConnectionCallbacks _callbacks;
    private readonly List<Delegate> _keepAlive = new();

    internal PeerConnection(IntPtr handle, PeerConnectionCallbacks callbacks)
        : base(IntPtr.Zero, true)
    {
        _callbacks = callbacks;
        SetHandle(handle);
    }

    public void Close()
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

    public void AddIceCandidate(string sdpMid, int sdpMlineIndex, string candidate)
    {
        using var midUtf8 = new Utf8String(sdpMid);
        using var candUtf8 = new Utf8String(candidate);
        NativeMethods.lrtc_peer_connection_add_ice_candidate(handle, midUtf8.Pointer, sdpMlineIndex, candUtf8.Pointer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_peer_connection_release(handle);
        return true;
    }
}

public sealed class DataChannel : SafeHandle
{
    private DataChannelCallbacks? _callbacks;

    internal DataChannel(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public void SetCallbacks(DataChannelCallbacks callbacks)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        NativeMethods.lrtc_data_channel_set_callbacks(handle, ref native, IntPtr.Zero);
    }

    public void Send(ReadOnlySpan<byte> data, bool binary = true)
    {
        unsafe
        {
            fixed (byte* ptr = data)
            {
                NativeMethods.lrtc_data_channel_send(handle, (IntPtr)ptr, (uint)data.Length, binary ? 1 : 0);
            }
        }
    }

    public void Close()
    {
        NativeMethods.lrtc_data_channel_close(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_data_channel_release(handle);
        return true;
    }
}

public sealed class VideoTrack : SafeHandle
{
    internal VideoTrack(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public void AddSink(VideoSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        NativeMethods.lrtc_video_track_add_sink(handle, sink.DangerousGetHandle());
    }

    public void RemoveSink(VideoSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        NativeMethods.lrtc_video_track_remove_sink(handle, sink.DangerousGetHandle());
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_track_release(handle);
        return true;
    }
}

public sealed class VideoSink : SafeHandle
{
    private readonly VideoSinkCallbacks _callbacks;

    public VideoSink(VideoSinkCallbacks callbacks)
        : base(IntPtr.Zero, true)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        var handle = NativeMethods.lrtc_video_sink_create(ref native, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create video sink.");
        }
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_sink_release(handle);
        return true;
    }
}

public sealed class VideoFrame : SafeHandle
{
    internal VideoFrame(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public int Width => NativeMethods.lrtc_video_frame_width(handle);
    public int Height => NativeMethods.lrtc_video_frame_height(handle);

    public void CopyToI420(Span<byte> y, int strideY, Span<byte> u, int strideU, Span<byte> v, int strideV)
    {
        unsafe
        {
            fixed (byte* yPtr = y)
            fixed (byte* uPtr = u)
            fixed (byte* vPtr = v)
            {
                NativeMethods.lrtc_video_frame_copy_i420(handle, (IntPtr)yPtr, strideY, (IntPtr)uPtr, strideU, (IntPtr)vPtr, strideV);
            }
        }
    }

    public void CopyToArgb(Span<byte> argb, int strideArgb, int width, int height, int format)
    {
        unsafe
        {
            fixed (byte* argbPtr = argb)
            {
                NativeMethods.lrtc_video_frame_to_argb(handle, (IntPtr)argbPtr, strideArgb, width, height, format);
            }
        }
    }

    public VideoFrame Retain()
    {
        var retained = NativeMethods.lrtc_video_frame_retain(handle);
        if (retained == IntPtr.Zero)
        {
            throw new InvalidOperationException(\"Failed to retain video frame.\");
        }
        return new VideoFrame(retained);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_frame_release(handle);
        return true;
    }
}

public sealed class PeerConnectionCallbacks
{
    public Action<SignalingState>? OnSignalingState;
    public Action<PeerConnectionState>? OnPeerConnectionState;
    public Action<IceGatheringState>? OnIceGatheringState;
    public Action<IceConnectionState>? OnIceConnectionState;
    public Action<string, int, string>? OnIceCandidate;
    public Action<DataChannel>? OnDataChannel;
    public Action<VideoTrack>? OnVideoTrack;
    public Action? OnRenegotiationNeeded;

    private LrtcPeerConnectionStateCb? _signalingStateCb;
    private LrtcPeerConnectionStateCb? _pcStateCb;
    private LrtcPeerConnectionStateCb? _iceGatheringCb;
    private LrtcPeerConnectionStateCb? _iceConnectionCb;
    private LrtcIceCandidateCb? _iceCandidateCb;
    private LrtcDataChannelCreatedCb? _dataChannelCb;
    private LrtcVideoTrackCb? _videoTrackCb;
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
            on_audio_track = null,
            on_renegotiation_needed = _renegotiationCb,
        };
    }
}

public sealed class DataChannelCallbacks
{
    public Action<DataChannelState>? OnStateChange;
    public Action<ReadOnlyMemory<byte>, bool>? OnMessage;

    private LrtcDataChannelStateCb? _stateCb;
    private LrtcDataChannelMessageCb? _messageCb;

    internal LrtcDataChannelCallbacks BuildNative()
    {
        _stateCb = (ud, state) => OnStateChange?.Invoke((DataChannelState)state);
        _messageCb = (ud, dataPtr, length, binary) =>
        {
            if (dataPtr == IntPtr.Zero || length <= 0)
            {
                OnMessage?.Invoke(ReadOnlyMemory<byte>.Empty, binary != 0);
                return;
            }
            var managed = new byte[length];
            Marshal.Copy(dataPtr, managed, 0, length);
            OnMessage?.Invoke(managed, binary != 0);
        };

        return new LrtcDataChannelCallbacks
        {
            on_state_change = _stateCb,
            on_message = _messageCb,
        };
    }
}

public sealed class VideoSinkCallbacks
{
    public Action<VideoFrame>? OnFrame;
    private LrtcVideoFrameCb? _frameCb;

    internal LrtcVideoSinkCallbacks BuildNative()
    {
        _frameCb = (ud, framePtr) =>
        {
            if (framePtr == IntPtr.Zero) return;
            using var frame = new VideoFrame(framePtr);
            OnFrame?.Invoke(frame);
        };

        return new LrtcVideoSinkCallbacks
        {
            on_frame = _frameCb,
        };
    }
}

public readonly struct DataChannelInit
{
    public static DataChannelInit Default => new(
        ordered: true,
        reliable: true,
        maxRetransmitTime: -1,
        maxRetransmits: -1,
        protocol: "sctp",
        negotiated: false,
        id: 0);

    public DataChannelInit(
        bool ordered,
        bool reliable,
        int maxRetransmitTime,
        int maxRetransmits,
        string protocol,
        bool negotiated,
        int id)
    {
        Ordered = ordered;
        Reliable = reliable;
        MaxRetransmitTime = maxRetransmitTime;
        MaxRetransmits = maxRetransmits;
        Protocol = protocol ?? "sctp";
        Negotiated = negotiated;
        Id = id;
    }

    public bool Ordered { get; }
    public bool Reliable { get; }
    public int MaxRetransmitTime { get; }
    public int MaxRetransmits { get; }
    public string Protocol { get; }
    public bool Negotiated { get; }
    public int Id { get; }
}

internal sealed class Utf8String : IDisposable
{
    public IntPtr Pointer { get; private set; }

    public Utf8String(string? value)
    {
        Pointer = value == null ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(value);
    }

    public static string Read(IntPtr ptr)
    {
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    public void Dispose()
    {
        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(Pointer);
            Pointer = IntPtr.Zero;
        }
    }
}
