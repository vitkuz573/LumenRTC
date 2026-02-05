namespace LumenRTC;

public sealed class AudioSinkCallbacks
{
    public Action<AudioFrame>? OnData;
    private LrtcAudioFrameCb? _frameCb;

    internal LrtcAudioSinkCallbacks BuildNative()
    {
        _frameCb = (ud, audioPtr, bitsPerSample, sampleRate, channels, frames) =>
        {
            var channelsInt = (int)channels;
            var framesInt = (int)frames;
            if (audioPtr == IntPtr.Zero || bitsPerSample <= 0 || channelsInt <= 0 || framesInt <= 0)
            {
                OnData?.Invoke(new AudioFrame(ReadOnlyMemory<byte>.Empty, bitsPerSample, sampleRate, channelsInt, framesInt));
                return;
            }

            var bytesPerSample = Math.Max(1, (bitsPerSample + 7) / 8);
            var totalBytes = (long)channelsInt * framesInt * bytesPerSample;
            if (totalBytes <= 0 || totalBytes > int.MaxValue)
            {
                OnData?.Invoke(new AudioFrame(ReadOnlyMemory<byte>.Empty, bitsPerSample, sampleRate, channelsInt, framesInt));
                return;
            }

            var data = new byte[(int)totalBytes];
            Marshal.Copy(audioPtr, data, 0, (int)totalBytes);
            OnData?.Invoke(new AudioFrame(data, bitsPerSample, sampleRate, channelsInt, framesInt));
        };

        return new LrtcAudioSinkCallbacks
        {
            on_data = _frameCb,
        };
    }
}
