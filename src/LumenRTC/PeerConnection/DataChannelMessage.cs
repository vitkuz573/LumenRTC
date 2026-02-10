namespace LumenRTC;

/// <summary>
/// Message payload delivered by a data channel callback.
/// </summary>
public readonly record struct DataChannelMessage(ReadOnlyMemory<byte> Payload, bool IsBinary)
{
    public int Length => Payload.Length;

    public bool IsEmpty => Payload.IsEmpty;

    public string GetText()
    {
        return System.Text.Encoding.UTF8.GetString(Payload.Span);
    }

    public bool TryGetText([global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? text)
    {
        if (IsBinary)
        {
            text = null;
            return false;
        }

        text = GetText();
        return true;
    }
}
