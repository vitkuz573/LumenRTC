namespace LumenRTC;

/// <summary>
/// RTC data channel for arbitrary message transport.
/// </summary>
public sealed partial class DataChannel : SafeHandle
{
    private readonly object _callbacksSync = new();
    private DataChannelCallbacks? _userCallbacks;
    private DataChannelCallbacks? _effectiveCallbacks;
    private Action<DataChannelState>? _stateHandler;
    private Action<DataChannelMessage>? _messageHandler;
    private Action<DataChannelState>? _stateChanged;
    private Action<DataChannelMessage>? _messageReceived;
    private DataChannelState _state = DataChannelState.Connecting;

    internal DataChannel(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public DataChannelState State
    {
        get
        {
            lock (_callbacksSync)
            {
                return _state;
            }
        }
    }

    public bool IsOpen => State == DataChannelState.Open;

    public bool IsChannelClosed => State == DataChannelState.Closed;

    public bool IsClosing => State == DataChannelState.Closing;

    public event Action<DataChannelState>? StateChanged
    {
        add
        {
            lock (_callbacksSync)
            {
                _stateChanged += value;
                ApplyCallbacksUnsafe();
            }
        }
        remove
        {
            lock (_callbacksSync)
            {
                _stateChanged -= value;
                ApplyCallbacksUnsafe();
            }
        }
    }

    public event Action<DataChannelMessage>? MessageReceived
    {
        add
        {
            lock (_callbacksSync)
            {
                _messageReceived += value;
                ApplyCallbacksUnsafe();
            }
        }
        remove
        {
            lock (_callbacksSync)
            {
                _messageReceived -= value;
                ApplyCallbacksUnsafe();
            }
        }
    }

    public void SetCallbacks(DataChannelCallbacks callbacks)
    {
        if (callbacks == null)
        {
            throw new ArgumentNullException(nameof(callbacks));
        }

        lock (_callbacksSync)
        {
            _userCallbacks = callbacks;
            ApplyCallbacksUnsafe();
        }
    }

    public void ClearCallbacks()
    {
        lock (_callbacksSync)
        {
            _userCallbacks = null;
            _stateHandler = null;
            _messageHandler = null;
            _stateChanged = null;
            _messageReceived = null;
            _effectiveCallbacks = null;

            if (IsInvalid)
            {
                return;
            }

            var native = default(LrtcDataChannelCallbacks);
            NativeMethods.lrtc_data_channel_set_callbacks(handle, ref native, IntPtr.Zero);
        }
    }

    public void SetStateChangeHandler(Action<DataChannelState>? handler)
    {
        lock (_callbacksSync)
        {
            _stateHandler = handler;
            ApplyCallbacksUnsafe();
        }
    }

    public void SetMessageHandler(Action<DataChannelMessage>? handler)
    {
        lock (_callbacksSync)
        {
            _messageHandler = handler;
            ApplyCallbacksUnsafe();
        }
    }

    public void SetTextMessageHandler(Action<string>? handler)
    {
        if (handler == null)
        {
            SetMessageHandler(null);
            return;
        }

        SetMessageHandler(message =>
        {
            if (message.TryGetText(out var text))
            {
                handler(text);
            }
        });
    }

    public void SendBinary(ReadOnlySpan<byte> payload)
    {
        Send(payload, binary: true);
    }

    public void SendBinary(byte[] payload)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        SendBinary(payload.AsSpan());
    }

    public void SendText(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        Send(bytes, binary: false);
    }

    public void SendJson<T>(T value, JsonSerializerOptions? options = null)
    {
        SendText(JsonSerializer.Serialize(value, options));
    }

    public bool TrySendBinary(ReadOnlySpan<byte> payload, out string? error)
    {
        try
        {
            SendBinary(payload);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySendBinary(byte[] payload, out string? error)
    {
        if (payload == null)
        {
            error = "Payload cannot be null.";
            return false;
        }

        return TrySendBinary(payload.AsSpan(), out error);
    }

    public bool TrySendText(string text, out string? error)
    {
        if (text == null)
        {
            error = "Text payload cannot be null.";
            return false;
        }

        try
        {
            SendText(text);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySendJson<T>(T value, out string? error, JsonSerializerOptions? options = null)
    {
        try
        {
            SendJson(value, options);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void CloseChannel()
    {
        lock (_callbacksSync)
        {
            if (!IsInvalid && _state is not DataChannelState.Closed and not DataChannelState.Closing)
            {
                _state = DataChannelState.Closing;
            }
        }

        Close();
    }

    private void ApplyCallbacksUnsafe()
    {
        if (IsInvalid)
        {
            return;
        }

        if (!HasAnyManagedCallbackUnsafe())
        {
            _effectiveCallbacks = null;
            var native = default(LrtcDataChannelCallbacks);
            NativeMethods.lrtc_data_channel_set_callbacks(handle, ref native, IntPtr.Zero);
            return;
        }

        var effectiveCallbacks = new DataChannelCallbacks
        {
            OnStateChange = HandleStateChange,
            OnMessage = (payload, isBinary) => HandleMessage(new DataChannelMessage(payload, isBinary)),
        };
        _effectiveCallbacks = effectiveCallbacks;

        var nativeCallbacks = effectiveCallbacks.BuildNative();
        NativeMethods.lrtc_data_channel_set_callbacks(handle, ref nativeCallbacks, IntPtr.Zero);
    }

    private bool HasAnyManagedCallbackUnsafe()
    {
        return _userCallbacks?.OnStateChange != null
            || _userCallbacks?.OnMessage != null
            || _stateHandler != null
            || _messageHandler != null
            || _stateChanged != null
            || _messageReceived != null;
    }

    private void HandleStateChange(DataChannelState state)
    {
        Action<DataChannelState>? userState;
        Action<DataChannelState>? stateHandler;
        Action<DataChannelState>? stateChanged;
        var normalized = NormalizeState(state);

        lock (_callbacksSync)
        {
            _state = normalized;
            userState = _userCallbacks?.OnStateChange;
            stateHandler = _stateHandler;
            stateChanged = _stateChanged;
        }

        userState?.Invoke(normalized);
        stateHandler?.Invoke(normalized);
        stateChanged?.Invoke(normalized);
    }

    private void HandleMessage(DataChannelMessage message)
    {
        Action<ReadOnlyMemory<byte>, bool>? userMessage;
        Action<DataChannelMessage>? messageHandler;
        Action<DataChannelMessage>? messageReceived;

        lock (_callbacksSync)
        {
            userMessage = _userCallbacks?.OnMessage;
            messageHandler = _messageHandler;
            messageReceived = _messageReceived;
        }

        userMessage?.Invoke(message.Payload, message.IsBinary);
        messageHandler?.Invoke(message);
        messageReceived?.Invoke(message);
    }

    private static DataChannelState NormalizeState(DataChannelState state)
    {
        return state switch
        {
            DataChannelState.Connecting => DataChannelState.Connecting,
            DataChannelState.Open => DataChannelState.Open,
            DataChannelState.Closing => DataChannelState.Closing,
            DataChannelState.Closed => DataChannelState.Closed,
            _ => DataChannelState.Connecting,
        };
    }
}
