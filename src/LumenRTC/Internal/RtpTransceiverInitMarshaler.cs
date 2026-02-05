namespace LumenRTC;

internal sealed class RtpTransceiverInitMarshaler : IDisposable
{
    private readonly Utf8StringArray _streamIds;
    private readonly IntPtr _encodingsPtr;
    private readonly uint _encodingCount;
    private readonly List<Utf8String> _encodingStrings = new();

    public LrtcRtpTransceiverInit Native { get; }

    public RtpTransceiverInitMarshaler(RtpTransceiverInit init)
    {
        if (init == null) throw new ArgumentNullException(nameof(init));

        _streamIds = new Utf8StringArray(init.StreamIds);
        if (init.SendEncodings.Count > 0)
        {
            _encodingCount = (uint)init.SendEncodings.Count;
            var size = Marshal.SizeOf<LrtcRtpEncodingSettings>();
            _encodingsPtr = Marshal.AllocHGlobal(size * init.SendEncodings.Count);
            for (var i = 0; i < init.SendEncodings.Count; i++)
            {
                if (init.SendEncodings[i] == null)
                {
                    var empty = new LrtcRtpEncodingSettings
                    {
                        max_bitrate_bps = -1,
                        min_bitrate_bps = -1,
                        max_framerate = -1,
                        scale_resolution_down_by = -1,
                        active = -1,
                        degradation_preference = -1,
                        bitrate_priority = -1,
                        network_priority = -1,
                        num_temporal_layers = -1,
                        scalability_mode = IntPtr.Zero,
                        rid = IntPtr.Zero,
                        adaptive_ptime = -1,
                    };
                    Marshal.StructureToPtr(empty, IntPtr.Add(_encodingsPtr, i * size), false);
                    continue;
                }
                var native = init.SendEncodings[i].ToNative(out var rid, out var scalabilityMode);
                if (rid != null)
                {
                    _encodingStrings.Add(rid);
                }
                if (scalabilityMode != null)
                {
                    _encodingStrings.Add(scalabilityMode);
                }
                Marshal.StructureToPtr(native, IntPtr.Add(_encodingsPtr, i * size), false);
            }
        }
        else
        {
            _encodingsPtr = IntPtr.Zero;
            _encodingCount = 0;
        }

        Native = new LrtcRtpTransceiverInit
        {
            direction = (LrtcRtpTransceiverDirection)init.Direction,
            stream_ids = _streamIds.Pointer,
            stream_id_count = (uint)_streamIds.Count,
            send_encodings = _encodingsPtr,
            send_encoding_count = _encodingCount,
        };
    }

    public void Dispose()
    {
        _streamIds.Dispose();
        foreach (var str in _encodingStrings)
        {
            str.Dispose();
        }
        if (_encodingsPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_encodingsPtr);
        }
    }
}
