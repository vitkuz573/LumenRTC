namespace LumenRTC;

/// <summary>
/// RTP sender for a local media track.
/// </summary>
public sealed partial class RtpSender : SafeHandle
{
    internal RtpSender(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public MediaType MediaType
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_sender_get_media_type(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get sender media type.");
            }
            return (MediaType)value;
        }
    }

    public string Id => NativeString.GetString(handle, NativeMethods.lrtc_rtp_sender_get_id);

    public uint Ssrc => NativeMethods.lrtc_rtp_sender_get_ssrc(handle);

    public IReadOnlyList<string> StreamIds => GetStreamIds();

    public bool IsAudio => MediaType == MediaType.Audio;

    public bool IsVideo => MediaType == MediaType.Video;

    public bool HasTrack => AudioTrack != null || VideoTrack != null;

    public AudioTrack? AudioTrack
    {
        get
        {
            var track = NativeMethods.lrtc_rtp_sender_get_audio_track(handle);
            return track == IntPtr.Zero ? null : new AudioTrack(track);
        }
    }

    public VideoTrack? VideoTrack
    {
        get
        {
            var track = NativeMethods.lrtc_rtp_sender_get_video_track(handle);
            return track == IntPtr.Zero ? null : new VideoTrack(track);
        }
    }

    public DtmfSender? DtmfSender
    {
        get
        {
            var sender = NativeMethods.lrtc_rtp_sender_get_dtmf_sender(handle);
            return sender == IntPtr.Zero ? null : new DtmfSender(sender);
        }
    }

    public bool TryGetAudioTrack([global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AudioTrack? track)
    {
        track = AudioTrack;
        return track != null;
    }

    public bool TryGetVideoTrack([global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out VideoTrack? track)
    {
        track = VideoTrack;
        return track != null;
    }

    public bool TryGetDtmfSender([global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DtmfSender? sender)
    {
        sender = DtmfSender;
        return sender != null;
    }

    public bool CanInsertDtmf => DtmfSender?.CanInsert == true;

    public bool TryInsertDtmf(string tones, out string? error)
    {
        return TryInsertDtmf(tones, DtmfInsertOptions.Default, out error);
    }

    public bool TryInsertDtmf(char tone, out string? error)
    {
        return TryInsertDtmf(new string(tone, 1), DtmfInsertOptions.Default, out error);
    }

    public bool TryInsertDtmf(string tones, DtmfInsertOptions options, out string? error)
    {
        if (!TryGetDtmfSender(out var dtmf))
        {
            error = "RTP sender does not expose DTMF sender.";
            return false;
        }

        return dtmf.TryInsert(tones, options, out error);
    }

    public bool TryInsertDtmf(char tone, DtmfInsertOptions options, out string? error)
    {
        return TryInsertDtmf(new string(tone, 1), options, out error);
    }

    public void InsertDtmf(string tones)
    {
        InsertDtmf(tones, DtmfInsertOptions.Default);
    }

    public void InsertDtmf(char tone)
    {
        InsertDtmf(new string(tone, 1), DtmfInsertOptions.Default);
    }

    public void InsertDtmf(string tones, DtmfInsertOptions options)
    {
        if (!TryInsertDtmf(tones, options, out var error))
        {
            throw new InvalidOperationException(error ?? "Failed to insert DTMF tones.");
        }
    }

    public void InsertDtmf(char tone, DtmfInsertOptions options)
    {
        InsertDtmf(new string(tone, 1), options);
    }

    public void SetDtmfToneChangeHandler(Action<DtmfToneChange>? handler)
    {
        if (!TryGetDtmfSender(out var dtmf))
        {
            if (handler == null)
            {
                return;
            }

            throw new InvalidOperationException("RTP sender does not expose DTMF sender.");
        }

        dtmf.SetToneChangeHandler(handler);
    }

    public bool ReplaceTrack(AudioTrack? track)
    {
        var result = NativeMethods.lrtc_rtp_sender_replace_audio_track(
            handle,
            track?.DangerousGetHandle() ?? IntPtr.Zero);
        return result != 0;
    }

    public bool TryReplaceTrack(AudioTrack? track, out string? error)
    {
        try
        {
            var replaced = ReplaceTrack(track);
            error = replaced ? null : "Failed to replace audio track.";
            return replaced;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool ReplaceTrack(VideoTrack? track)
    {
        var result = NativeMethods.lrtc_rtp_sender_replace_video_track(
            handle,
            track?.DangerousGetHandle() ?? IntPtr.Zero);
        return result != 0;
    }

    public bool TryReplaceTrack(VideoTrack? track, out string? error)
    {
        try
        {
            var replaced = ReplaceTrack(track);
            error = replaced ? null : "Failed to replace video track.";
            return replaced;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool ClearTrack()
    {
        return MediaType == MediaType.Audio
            ? ReplaceTrack((AudioTrack?)null)
            : ReplaceTrack((VideoTrack?)null);
    }

    public bool TryClearTrack(out string? error)
    {
        try
        {
            var cleared = ClearTrack();
            error = cleared ? null : "Failed to clear sender track.";
            return cleared;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool SetEncodingParameters(RtpEncodingSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        var native = settings.ToNative(out var rid, out var scalabilityMode);
        try
        {
            var result = NativeMethods.lrtc_rtp_sender_set_encoding_parameters(handle, ref native);
            return result != 0;
        }
        finally
        {
            rid?.Dispose();
            scalabilityMode?.Dispose();
        }
    }

    public bool TrySetEncodingParameters(RtpEncodingSettings settings, out string? error)
    {
        if (settings == null)
        {
            error = "Encoding settings cannot be null.";
            return false;
        }

        try
        {
            var applied = SetEncodingParameters(settings);
            error = applied ? null : "Failed to set sender encoding parameters.";
            return applied;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool SetEncodingParameters(int encodingIndex, RtpEncodingSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (encodingIndex < 0) throw new ArgumentOutOfRangeException(nameof(encodingIndex));
        var native = settings.ToNative(out var rid, out var scalabilityMode);
        try
        {
            var result = NativeMethods.lrtc_rtp_sender_set_encoding_parameters_at(
                handle,
                (uint)encodingIndex,
                ref native);
            return result != 0;
        }
        finally
        {
            rid?.Dispose();
            scalabilityMode?.Dispose();
        }
    }

    public bool TrySetEncodingParameters(int encodingIndex, RtpEncodingSettings settings, out string? error)
    {
        if (encodingIndex < 0)
        {
            error = "Encoding index must be non-negative.";
            return false;
        }

        if (settings == null)
        {
            error = "Encoding settings cannot be null.";
            return false;
        }

        try
        {
            var applied = SetEncodingParameters(encodingIndex, settings);
            error = applied ? null : $"Failed to set sender encoding parameters at index {encodingIndex}.";
            return applied;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public IReadOnlyList<RtpEncodingInfo> GetEncodings()
    {
        var count = NativeMethods.lrtc_rtp_sender_encoding_count(handle);
        if (count == 0)
        {
            return Array.Empty<RtpEncodingInfo>();
        }

        var list = new List<RtpEncodingInfo>((int)count);
        for (uint i = 0; i < count; i++)
        {
            if (NativeMethods.lrtc_rtp_sender_get_encoding_info(handle, i, out var info) == 0)
            {
                continue;
            }
            var rid = NativeString.GetIndexedString(handle, i, NativeMethods.lrtc_rtp_sender_get_encoding_rid);
            var scalability = NativeString.GetIndexedString(handle, i, NativeMethods.lrtc_rtp_sender_get_encoding_scalability_mode);
            list.Add(new RtpEncodingInfo(
                info.ssrc,
                info.max_bitrate_bps,
                info.min_bitrate_bps,
                info.max_framerate,
                info.scale_resolution_down_by,
                info.active != 0,
                info.bitrate_priority,
                (RtpPriority)info.network_priority,
                info.num_temporal_layers,
                info.adaptive_ptime != 0,
                rid,
                scalability));
        }

        return list;
    }

    public DegradationPreference? GetDegradationPreference()
    {
        var value = NativeMethods.lrtc_rtp_sender_get_degradation_preference(handle);
        return value < 0 ? null : (DegradationPreference)value;
    }

    public string ParametersMid => NativeString.GetString(handle, NativeMethods.lrtc_rtp_sender_get_parameters_mid);

    public DtlsTransportInfo? GetDtlsInfo()
    {
        if (NativeMethods.lrtc_rtp_sender_get_dtls_info(handle, out var info) == 0)
        {
            return null;
        }
        return new DtlsTransportInfo(
            (DtlsTransportState)info.state,
            info.ssl_cipher_suite,
            info.srtp_cipher_suite);
    }

    public bool SetStreamIds(IReadOnlyList<string> streamIds)
    {
        if (streamIds == null) throw new ArgumentNullException(nameof(streamIds));
        using var ids = new Utf8StringArray(streamIds);
        var result = NativeMethods.lrtc_rtp_sender_set_stream_ids(handle, ids.Pointer, (uint)ids.Count);
        return result != 0;
    }

    public bool TrySetStreamIds(IReadOnlyList<string> streamIds, out string? error)
    {
        if (streamIds == null)
        {
            error = "Stream IDs cannot be null.";
            return false;
        }

        try
        {
            var updated = SetStreamIds(streamIds);
            error = updated ? null : "Failed to set sender stream IDs.";
            return updated;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private IReadOnlyList<string> GetStreamIds()
    {
        var count = NativeMethods.lrtc_rtp_sender_stream_id_count(handle);
        if (count == 0)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var value = NativeString.GetIndexedString(handle, i, NativeMethods.lrtc_rtp_sender_get_stream_id);
            if (!string.IsNullOrEmpty(value))
            {
                list.Add(value);
            }
        }
        return list;
    }
}
