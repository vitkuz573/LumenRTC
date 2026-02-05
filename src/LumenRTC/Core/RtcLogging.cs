namespace LumenRTC;

/// <summary>
/// Configures native WebRTC logging callbacks and verbosity.
/// </summary>
public static class RtcLogging
{
    private static LrtcLogMessageCb? _callback;
    private static GCHandle _callbackHandle;
    private static Action<string>? _managedCallback;

    public static void SetMinLevel(LogSeverity severity)
    {
        NativeMethods.lrtc_logging_set_min_level((int)severity);
    }

    public static void SetLogSink(LogSeverity severity, Action<string> onMessage)
    {
        if (onMessage == null) throw new ArgumentNullException(nameof(onMessage));
        RemoveLogSink();
        _managedCallback = onMessage;
        _callback = (_, messagePtr) => _managedCallback?.Invoke(Utf8String.Read(messagePtr));
        _callbackHandle = GCHandle.Alloc(_callback);
        NativeMethods.lrtc_logging_set_callback((int)severity, _callback, IntPtr.Zero);
    }

    public static void RemoveLogSink()
    {
        NativeMethods.lrtc_logging_remove_callback();
        if (_callbackHandle.IsAllocated)
        {
            _callbackHandle.Free();
        }
        _callback = null;
        _managedCallback = null;
    }
}
