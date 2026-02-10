namespace LumenRTC;

/// <summary>
/// Sends DTMF tones over an audio RTP sender.
/// </summary>
public sealed partial class DtmfSender : SafeHandle
{
    public const int DefaultDurationMs = 100;
    public const int DefaultInterToneGapMs = 70;
    public const int DefaultCommaDelayMs = -1;
    public const int MinDurationMs = 40;
    public const int MaxDurationMs = 6000;
    public const int MinInterToneGapMs = 30;
    public const int MinCommaDelayMs = 30;

    public const string SupportedToneCharacters = "0123456789ABCD*#,";

    private DtmfSenderCallbacks? _callbacks;

    internal DtmfSender(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public void SetCallbacks(DtmfSenderCallbacks callbacks)
    {
        _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        var native = callbacks.BuildNative();
        NativeMethods.lrtc_dtmf_sender_set_callbacks(handle, ref native, IntPtr.Zero);
    }

    public void ClearCallbacks()
    {
        _callbacks = null;
        if (IsInvalid)
        {
            return;
        }

        var callbacks = default(LrtcDtmfSenderCallbacks);
        NativeMethods.lrtc_dtmf_sender_set_callbacks(handle, ref callbacks, IntPtr.Zero);
    }

    public void SetToneChangeHandler(Action<DtmfToneChange>? handler)
    {
        if (handler == null)
        {
            ClearCallbacks();
            return;
        }

        var callbacks = new DtmfSenderCallbacks
        {
            OnToneChange = (tone, toneBuffer) => handler(new DtmfToneChange(tone ?? string.Empty, toneBuffer)),
        };
        SetCallbacks(callbacks);
    }

    public bool TryInsert(string tones, out string? error)
    {
        return TryInsert(tones, DtmfInsertOptions.Default, out error);
    }

    public bool TryInsert(char tone, out string? error)
    {
        return TryInsert(new string(tone, 1), DtmfInsertOptions.Default, out error);
    }

    public bool TryInsert(string tones, DtmfInsertOptions options, out string? error)
    {
        if (!TryNormalizeTones(tones, out var normalizedTones, out error))
        {
            return false;
        }

        if (!TryValidateOptions(options, out error))
        {
            return false;
        }

        if (!CanInsert)
        {
            error = "DTMF insertion is not available for this RTP sender.";
            return false;
        }

        var ok = InsertDtmf(normalizedTones, options.DurationMs, options.InterToneGapMs, options.CommaDelayMs);
        if (!ok)
        {
            error = "Native DTMF insertion failed.";
            return false;
        }

        error = null;
        return true;
    }

    public bool TryInsert(char tone, DtmfInsertOptions options, out string? error)
    {
        return TryInsert(new string(tone, 1), options, out error);
    }

    public void Insert(string tones)
    {
        Insert(tones, DtmfInsertOptions.Default);
    }

    public void Insert(char tone)
    {
        Insert(new string(tone, 1), DtmfInsertOptions.Default);
    }

    public void Insert(string tones, DtmfInsertOptions options)
    {
        if (!TryInsert(tones, options, out var error))
        {
            throw new InvalidOperationException(error ?? "Failed to insert DTMF tones.");
        }
    }

    public void Insert(char tone, DtmfInsertOptions options)
    {
        Insert(new string(tone, 1), options);
    }

    public static bool IsSupportedToneCharacter(char tone)
    {
        if (tone is >= '0' and <= '9')
        {
            return true;
        }

        if (tone is '*' or '#' or ',')
        {
            return true;
        }

        var normalized = char.ToUpperInvariant(tone);
        return normalized is >= 'A' and <= 'D';
    }

    public static bool IsValidToneSequence(string? tones)
    {
        return TryNormalizeTones(tones, out _, out _);
    }

    private static bool TryNormalizeTones(string? tones, out string normalized, out string? error)
    {
        if (tones == null)
        {
            normalized = string.Empty;
            error = "Tone sequence cannot be null.";
            return false;
        }

        if (tones.Length == 0)
        {
            normalized = string.Empty;
            error = null;
            return true;
        }

        var chars = new char[tones.Length];
        for (var index = 0; index < tones.Length; index++)
        {
            var tone = tones[index];
            if (!IsSupportedToneCharacter(tone))
            {
                normalized = string.Empty;
                error = $"Unsupported tone character '{tone}' at index {index}. " +
                        $"Allowed characters: {SupportedToneCharacters}.";
                return false;
            }

            chars[index] = char.ToUpperInvariant(tone);
        }

        normalized = new string(chars);
        error = null;
        return true;
    }

    private static bool TryValidateOptions(DtmfInsertOptions options, out string? error)
    {
        if (options.DurationMs < MinDurationMs || options.DurationMs > MaxDurationMs)
        {
            error = $"Duration must be in range [{MinDurationMs}, {MaxDurationMs}] ms.";
            return false;
        }

        if (options.InterToneGapMs < MinInterToneGapMs)
        {
            error = $"Inter-tone gap must be >= {MinInterToneGapMs} ms.";
            return false;
        }

        if (options.CommaDelayMs != DefaultCommaDelayMs && options.CommaDelayMs < MinCommaDelayMs)
        {
            error = $"Comma delay must be {DefaultCommaDelayMs} (native default) or >= {MinCommaDelayMs} ms.";
            return false;
        }

        error = null;
        return true;
    }
}
