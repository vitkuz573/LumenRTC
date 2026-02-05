namespace LumenRTC;

/// <summary>
/// DTMF sender event callbacks.
/// </summary>
public sealed class DtmfSenderCallbacks
{
    public Action<string, string?>? OnToneChange;
    private LrtcDtmfToneCb? _toneCb;

    internal LrtcDtmfSenderCallbacks BuildNative()
    {
        _toneCb = (ud, tonePtr, toneBufferPtr) =>
        {
            var tone = Utf8String.Read(tonePtr);
            var toneBuffer = Utf8String.Read(toneBufferPtr);
            OnToneChange?.Invoke(tone, string.IsNullOrEmpty(toneBuffer) ? null : toneBuffer);
        };

        return new LrtcDtmfSenderCallbacks
        {
            on_tone_change = _toneCb,
        };
    }
}
