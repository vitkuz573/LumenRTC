namespace LumenRTC;

/// <summary>
/// RTP transceiver combining sender and receiver.
/// </summary>
public sealed partial class RtpTransceiver : SafeHandle
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

    public bool IsStoppedOrStopping => Stopped || Stopping;

    public bool CanSendDesired
    {
        get
        {
            var direction = Direction;
            return direction == RtpTransceiverDirection.SendRecv
                || direction == RtpTransceiverDirection.SendOnly;
        }
    }

    public bool CanReceiveDesired
    {
        get
        {
            var direction = Direction;
            return direction == RtpTransceiverDirection.SendRecv
                || direction == RtpTransceiverDirection.RecvOnly;
        }
    }

    public bool CanSendCurrent
    {
        get
        {
            var direction = CurrentDirection;
            return direction == RtpTransceiverDirection.SendRecv
                || direction == RtpTransceiverDirection.SendOnly;
        }
    }

    public bool CanReceiveCurrent
    {
        get
        {
            var direction = CurrentDirection;
            return direction == RtpTransceiverDirection.SendRecv
                || direction == RtpTransceiverDirection.RecvOnly;
        }
    }

    public bool IsInactiveDesired => Direction == RtpTransceiverDirection.Inactive;

    public bool TryGetSender([global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out RtpSender? sender)
    {
        sender = Sender;
        return sender != null;
    }

    public bool TryGetReceiver([global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out RtpReceiver? receiver)
    {
        receiver = Receiver;
        return receiver != null;
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

    public bool TrySetSendEnabled(bool enabled, out string? error)
    {
        try
        {
            var targetDirection = WithSendEnabled(Direction, enabled);
            return TrySetDirection(targetDirection, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void SetSendEnabled(bool enabled)
    {
        var targetDirection = WithSendEnabled(Direction, enabled);
        SetDirection(targetDirection);
    }

    public bool TrySetReceiveEnabled(bool enabled, out string? error)
    {
        try
        {
            var targetDirection = WithReceiveEnabled(Direction, enabled);
            return TrySetDirection(targetDirection, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void SetReceiveEnabled(bool enabled)
    {
        var targetDirection = WithReceiveEnabled(Direction, enabled);
        SetDirection(targetDirection);
    }

    public bool TryPause(out string? error)
    {
        return TrySetDirection(RtpTransceiverDirection.Inactive, out error);
    }

    public void Pause()
    {
        SetDirection(RtpTransceiverDirection.Inactive);
    }

    public bool TryResumeBidirectional(out string? error)
    {
        return TrySetDirection(RtpTransceiverDirection.SendRecv, out error);
    }

    public void ResumeBidirectional()
    {
        SetDirection(RtpTransceiverDirection.SendRecv);
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

    private static RtpTransceiverDirection WithSendEnabled(RtpTransceiverDirection direction, bool enabled)
    {
        if (direction == RtpTransceiverDirection.Stopped)
        {
            return direction;
        }

        return (direction, enabled) switch
        {
            (RtpTransceiverDirection.SendRecv, true) => RtpTransceiverDirection.SendRecv,
            (RtpTransceiverDirection.SendOnly, true) => RtpTransceiverDirection.SendOnly,
            (RtpTransceiverDirection.RecvOnly, true) => RtpTransceiverDirection.SendRecv,
            (RtpTransceiverDirection.Inactive, true) => RtpTransceiverDirection.SendOnly,
            (RtpTransceiverDirection.SendRecv, false) => RtpTransceiverDirection.RecvOnly,
            (RtpTransceiverDirection.SendOnly, false) => RtpTransceiverDirection.Inactive,
            (RtpTransceiverDirection.RecvOnly, false) => RtpTransceiverDirection.RecvOnly,
            (RtpTransceiverDirection.Inactive, false) => RtpTransceiverDirection.Inactive,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported transceiver direction."),
        };
    }

    private static RtpTransceiverDirection WithReceiveEnabled(RtpTransceiverDirection direction, bool enabled)
    {
        if (direction == RtpTransceiverDirection.Stopped)
        {
            return direction;
        }

        return (direction, enabled) switch
        {
            (RtpTransceiverDirection.SendRecv, true) => RtpTransceiverDirection.SendRecv,
            (RtpTransceiverDirection.SendOnly, true) => RtpTransceiverDirection.SendRecv,
            (RtpTransceiverDirection.RecvOnly, true) => RtpTransceiverDirection.RecvOnly,
            (RtpTransceiverDirection.Inactive, true) => RtpTransceiverDirection.RecvOnly,
            (RtpTransceiverDirection.SendRecv, false) => RtpTransceiverDirection.SendOnly,
            (RtpTransceiverDirection.SendOnly, false) => RtpTransceiverDirection.SendOnly,
            (RtpTransceiverDirection.RecvOnly, false) => RtpTransceiverDirection.Inactive,
            (RtpTransceiverDirection.Inactive, false) => RtpTransceiverDirection.Inactive,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported transceiver direction."),
        };
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
