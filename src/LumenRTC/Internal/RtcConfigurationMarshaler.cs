namespace LumenRTC.Internal;

internal sealed class RtcConfigurationMarshaler : IDisposable
{
    private readonly List<Utf8String> _strings = new();
    private IntPtr _ptr;

    public IntPtr Pointer => _ptr;

    public RtcConfigurationMarshaler(RtcConfiguration config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        var native = new LrtcRtcConfig
        {
            ice_servers = new LrtcIceServer[LrtcConstants.MaxIceServers],
            ice_server_count = 0,
            ice_transports_type = (int)config.IceTransportsType,
            bundle_policy = (int)config.BundlePolicy,
            rtcp_mux_policy = (int)config.RtcpMuxPolicy,
            candidate_network_policy = (int)config.CandidateNetworkPolicy,
            tcp_candidate_policy = (int)config.TcpCandidatePolicy,
            ice_candidate_pool_size = config.IceCandidatePoolSize,
            srtp_type = (int)config.SrtpType,
            sdp_semantics = (int)config.SdpSemantics,
            offer_to_receive_audio = config.OfferToReceiveAudio,
            offer_to_receive_video = config.OfferToReceiveVideo,
            disable_ipv6 = config.DisableIpv6,
            disable_ipv6_on_wifi = config.DisableIpv6OnWifi,
            max_ipv6_networks = config.MaxIpv6Networks,
            disable_link_local_networks = config.DisableLinkLocalNetworks,
            screencast_min_bitrate = config.ScreencastMinBitrate,
            enable_dscp = config.EnableDscp,
            use_rtp_mux = config.UseRtpMux,
            local_audio_bandwidth = config.LocalAudioBandwidth,
            local_video_bandwidth = config.LocalVideoBandwidth,
        };

        var max = Math.Min(config.IceServers.Count, LrtcConstants.MaxIceServers);
        for (var i = 0; i < max; i++)
        {
            var server = config.IceServers[i];
            var uri = new Utf8String(server.Uri);
            var username = new Utf8String(server.Username);
            var password = new Utf8String(server.Password);
            _strings.Add(uri);
            _strings.Add(username);
            _strings.Add(password);
            native.ice_servers[i] = new LrtcIceServer
            {
                uri = uri.Pointer,
                username = username.Pointer,
                password = password.Pointer,
            };
        }
        native.ice_server_count = (uint)max;

        _ptr = Marshal.AllocHGlobal(Marshal.SizeOf<LrtcRtcConfig>());
        Marshal.StructureToPtr(native, _ptr, false);
    }

    public void Dispose()
    {
        foreach (var str in _strings)
        {
            str.Dispose();
        }
        if (_ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_ptr);
            _ptr = IntPtr.Zero;
        }
    }
}
