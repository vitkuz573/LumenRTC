namespace LumenRTC;

/// <summary>
/// RTP transceiver combining sender and receiver.
/// </summary>
public sealed class RtpTransceiver : SafeHandle
{
    private delegate int TransceiverErrorInvoker(IntPtr transceiver, IntPtr error, uint errorLen);

    internal RtpTransceiver(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public MediaType MediaType
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_transceiver_get_media_type(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get transceiver media type.");
            }
            return (MediaType)value;
        }
    }

    public string Mid => NativeString.GetString(handle, NativeMethods.lrtc_rtp_transceiver_get_mid);

    public RtpTransceiverDirection Direction
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_transceiver_get_direction(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get transceiver direction.");
            }
            return (RtpTransceiverDirection)value;
        }
    }

    public RtpTransceiverDirection CurrentDirection
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_transceiver_get_current_direction(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get transceiver current direction.");
            }
            return (RtpTransceiverDirection)value;
        }
    }

    public RtpTransceiverDirection FiredDirection
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_transceiver_get_fired_direction(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get transceiver fired direction.");
            }
            return (RtpTransceiverDirection)value;
        }
    }

    public string TransceiverId => NativeString.GetString(handle, NativeMethods.lrtc_rtp_transceiver_get_id);

    public bool Stopped => NativeMethods.lrtc_rtp_transceiver_get_stopped(handle) != 0;

    public bool Stopping => NativeMethods.lrtc_rtp_transceiver_get_stopping(handle) != 0;

    public RtpSender? Sender
    {
        get
        {
            var sender = NativeMethods.lrtc_rtp_transceiver_get_sender(handle);
            return sender == IntPtr.Zero ? null : new RtpSender(sender);
        }
    }

    public RtpReceiver? Receiver
    {
        get
        {
            var receiver = NativeMethods.lrtc_rtp_transceiver_get_receiver(handle);
            return receiver == IntPtr.Zero ? null : new RtpReceiver(receiver);
        }
    }

    public bool TrySetDirection(RtpTransceiverDirection direction, out string? error)
    {
        return InvokeWithError(
            (transceiver, err, len) => NativeMethods.lrtc_rtp_transceiver_set_direction(
                transceiver,
                (LrtcRtpTransceiverDirection)direction,
                err,
                len),
            out error);
    }

    public void SetDirection(RtpTransceiverDirection direction)
    {
        if (!TrySetDirection(direction, out var error))
        {
            throw new InvalidOperationException(error ?? "Failed to set transceiver direction.");
        }
    }

    public bool TryStop(out string? error)
    {
        return InvokeWithError(
            (transceiver, err, len) => NativeMethods.lrtc_rtp_transceiver_stop(transceiver, err, len),
            out error);
    }

    public void Stop()
    {
        if (!TryStop(out var error))
        {
            throw new InvalidOperationException(error ?? "Failed to stop transceiver.");
        }
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_rtp_transceiver_release(handle);
        return true;
    }

    private bool InvokeWithError(TransceiverErrorInvoker invoker, out string? error)
    {
        const int BufferSize = 512;
        var buffer = Marshal.AllocHGlobal(BufferSize);
        try
        {
            unsafe
            {
                new Span<byte>((void*)buffer, BufferSize).Clear();
            }
            var result = invoker(handle, buffer, (uint)BufferSize);
            if (result != 0)
            {
                error = null;
                return true;
            }
            var message = Utf8String.Read(buffer);
            error = string.IsNullOrWhiteSpace(message) ? null : message;
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
