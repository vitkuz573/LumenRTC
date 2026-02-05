namespace LumenRTC;

/// <summary>
/// Helper for selecting presets based on resolution.
/// </summary>
public static class VideoQuality
{
    public static bool ApplyPreset(
        RtpSender sender,
        VideoQualityPreset preset,
        int? encodingIndex = null)
    {
        if (sender == null) throw new ArgumentNullException(nameof(sender));
        if (preset == null) throw new ArgumentNullException(nameof(preset));
        if (sender.MediaType != MediaType.Video)
        {
            throw new InvalidOperationException("Video quality presets can only be applied to video senders.");
        }

        var settings = preset.ToEncodingSettings();
        if (encodingIndex.HasValue)
        {
            return sender.SetEncodingParameters(encodingIndex.Value, settings);
        }

        return sender.SetEncodingParameters(settings);
    }

    public static bool ApplyPresetToAllEncodings(
        RtpSender sender,
        VideoQualityPreset preset)
    {
        if (sender == null) throw new ArgumentNullException(nameof(sender));
        if (preset == null) throw new ArgumentNullException(nameof(preset));
        if (sender.MediaType != MediaType.Video)
        {
            throw new InvalidOperationException("Video quality presets can only be applied to video senders.");
        }

        var settings = preset.ToEncodingSettings();
        var count = sender.GetEncodings().Count;
        if (count == 0)
        {
            return sender.SetEncodingParameters(settings);
        }

        var success = true;
        for (var i = 0; i < count; i++)
        {
            if (!sender.SetEncodingParameters(i, settings))
            {
                success = false;
            }
        }
        return success;
    }
}
