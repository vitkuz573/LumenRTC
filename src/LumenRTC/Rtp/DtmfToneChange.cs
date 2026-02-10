namespace LumenRTC;

/// <summary>
/// DTMF tone change payload emitted by sender callbacks.
/// </summary>
public readonly record struct DtmfToneChange(string Tone, string? RemainingBuffer);
