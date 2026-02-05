namespace LumenRTC;

public readonly struct AudioFrame
{
    public AudioFrame(ReadOnlyMemory<byte> data, int bitsPerSample, int sampleRate, int channels, int frames)
    {
        Data = data;
        BitsPerSample = bitsPerSample;
        SampleRate = sampleRate;
        Channels = channels;
        Frames = frames;
    }

    public ReadOnlyMemory<byte> Data { get; }
    public int BitsPerSample { get; }
    public int SampleRate { get; }
    public int Channels { get; }
    public int Frames { get; }
}
