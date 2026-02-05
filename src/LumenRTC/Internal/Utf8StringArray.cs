namespace LumenRTC;

internal sealed class Utf8StringArray : IDisposable
{
    private readonly Utf8String[] _strings;
    private IntPtr _buffer;

    public Utf8StringArray(IReadOnlyList<string>? values)
    {
        if (values == null || values.Count == 0)
        {
            _strings = Array.Empty<Utf8String>();
            _buffer = IntPtr.Zero;
            Count = 0;
            return;
        }

        _strings = new Utf8String[values.Count];
        _buffer = Marshal.AllocHGlobal(IntPtr.Size * values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            _strings[i] = new Utf8String(values[i]);
            Marshal.WriteIntPtr(_buffer, i * IntPtr.Size, _strings[i].Pointer);
        }
        Count = values.Count;
    }

    public int Count { get; }
    public IntPtr Pointer => _buffer;

    public void Dispose()
    {
        foreach (var str in _strings)
        {
            str.Dispose();
        }
        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }
    }
}
