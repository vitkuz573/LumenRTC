namespace LumenRTC;

/// <summary>
/// Options for DTMF insertion.
/// </summary>
public readonly record struct DtmfInsertOptions(int DurationMs, int InterToneGapMs, int CommaDelayMs)
{
    public static DtmfInsertOptions Default => new(
        DtmfSender.DefaultDurationMs,
        DtmfSender.DefaultInterToneGapMs,
        DtmfSender.DefaultCommaDelayMs);
}
