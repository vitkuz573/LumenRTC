namespace LumenRTC;

/// <summary>
/// RTP receiver for a remote media track.
/// </summary>
public sealed partial class RtpReceiver : SafeHandle
{
    internal RtpReceiver(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public MediaType MediaType
    {
        get
        {
            var value = NativeMethods.lrtc_rtp_receiver_get_media_type(handle);
            if (value < 0)
            {
                throw new InvalidOperationException("Failed to get receiver media type.");
            }
            return (MediaType)value;
        }
    }

    public string Id => NativeString.GetString(handle, NativeMethods.lrtc_rtp_receiver_get_id);

    public IReadOnlyList<string> StreamIds => GetStreamIds();

    public IReadOnlyList<MediaStream> Streams => GetStreams();

    public AudioTrack? AudioTrack
    {
        get
        {
            var track = NativeMethods.lrtc_rtp_receiver_get_audio_track(handle);
            return track == IntPtr.Zero ? null : new AudioTrack(track);
        }
    }

    public VideoTrack? VideoTrack
    {
        get
        {
            var track = NativeMethods.lrtc_rtp_receiver_get_video_track(handle);
            return track == IntPtr.Zero ? null : new VideoTrack(track);
        }
    }

    public void SetJitterBufferMinimumDelay(double seconds)
    {
        if (seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }
        var result = NativeMethods.lrtc_rtp_receiver_set_jitter_buffer_min_delay(handle, seconds);
        if (result == 0)
        {
            throw new InvalidOperationException("Failed to set jitter buffer minimum delay.");
        }
    }

    public void SetJitterBufferMinimumDelay(TimeSpan delay)
    {
        SetJitterBufferMinimumDelay(delay.TotalSeconds);
    }

    public IReadOnlyList<RtpEncodingInfo> GetEncodings()
    {
        var count = NativeMethods.lrtc_rtp_receiver_encoding_count(handle);
        if (count == 0)
        {
            return Array.Empty<RtpEncodingInfo>();
        }

        var list = new List<RtpEncodingInfo>((int)count);
        for (uint i = 0; i < count; i++)
        {
            if (NativeMethods.lrtc_rtp_receiver_get_encoding_info(handle, i, out var info) == 0)
            {
                continue;
            }
            var rid = NativeString.GetIndexedString(handle, i, NativeMethods.lrtc_rtp_receiver_get_encoding_rid);
            var scalability = NativeString.GetIndexedString(handle, i, NativeMethods.lrtc_rtp_receiver_get_encoding_scalability_mode);
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
        var value = NativeMethods.lrtc_rtp_receiver_get_degradation_preference(handle);
        return value < 0 ? null : (DegradationPreference)value;
    }

    public string ParametersMid => NativeString.GetString(handle, NativeMethods.lrtc_rtp_receiver_get_parameters_mid);

    public DtlsTransportInfo? GetDtlsInfo()
    {
        if (NativeMethods.lrtc_rtp_receiver_get_dtls_info(handle, out var info) == 0)
        {
            return null;
        }
        return new DtlsTransportInfo(
            (DtlsTransportState)info.state,
            info.ssl_cipher_suite,
            info.srtp_cipher_suite);
    }
private IReadOnlyList<string> GetStreamIds()
    {
        var count = NativeMethods.lrtc_rtp_receiver_stream_id_count(handle);
        if (count == 0)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var value = NativeString.GetIndexedString(handle, i, NativeMethods.lrtc_rtp_receiver_get_stream_id);
            if (!string.IsNullOrEmpty(value))
            {
                list.Add(value);
            }
        }
        return list;
    }

    private IReadOnlyList<MediaStream> GetStreams()
    {
        var count = NativeMethods.lrtc_rtp_receiver_stream_count(handle);
        if (count == 0)
        {
            return Array.Empty<MediaStream>();
        }

        var list = new List<MediaStream>((int)count);
        for (uint i = 0; i < count; i++)
        {
            var stream = NativeMethods.lrtc_rtp_receiver_get_stream(handle, i);
            if (stream != IntPtr.Zero)
            {
                list.Add(new MediaStream(stream));
            }
        }
        return list;
    }
}
