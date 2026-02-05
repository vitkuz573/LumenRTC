namespace LumenRTC;

public sealed class RtpSender : SafeHandle
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

    public bool ReplaceTrack(AudioTrack? track)
    {
        var result = NativeMethods.lrtc_rtp_sender_replace_audio_track(
            handle,
            track?.DangerousGetHandle() ?? IntPtr.Zero);
        return result != 0;
    }

    public bool ReplaceTrack(VideoTrack? track)
    {
        var result = NativeMethods.lrtc_rtp_sender_replace_video_track(
            handle,
            track?.DangerousGetHandle() ?? IntPtr.Zero);
        return result != 0;
    }

    public bool ClearTrack()
    {
        return MediaType == MediaType.Audio
            ? ReplaceTrack((AudioTrack?)null)
            : ReplaceTrack((VideoTrack?)null);
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

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_rtp_sender_release(handle);
        return true;
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
