namespace LumenRTC;

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
