namespace LumenRTC;

public sealed class RtcConfiguration
{
    public List<IceServer> IceServers { get; } = new();

    public IceTransportsType IceTransportsType { get; set; } = IceTransportsType.All;
    public BundlePolicy BundlePolicy { get; set; } = BundlePolicy.Balanced;
    public RtcpMuxPolicy RtcpMuxPolicy { get; set; } = RtcpMuxPolicy.Require;
    public CandidateNetworkPolicy CandidateNetworkPolicy { get; set; } = CandidateNetworkPolicy.All;
    public TcpCandidatePolicy TcpCandidatePolicy { get; set; } = TcpCandidatePolicy.Enabled;
    public int IceCandidatePoolSize { get; set; } = 0;
    public MediaSecurityType SrtpType { get; set; } = MediaSecurityType.DtlsSrtp;
    public SdpSemantics SdpSemantics { get; set; } = SdpSemantics.UnifiedPlan;
    public bool OfferToReceiveAudio { get; set; } = true;
    public bool OfferToReceiveVideo { get; set; } = true;
    public bool DisableIpv6 { get; set; } = false;
    public bool DisableIpv6OnWifi { get; set; } = false;
    public int MaxIpv6Networks { get; set; } = 5;
    public bool DisableLinkLocalNetworks { get; set; } = false;
    public int ScreencastMinBitrate { get; set; } = -1;
    public bool EnableDscp { get; set; } = false;
    public bool UseRtpMux { get; set; } = true;
    public uint LocalAudioBandwidth { get; set; } = 128;
    public uint LocalVideoBandwidth { get; set; } = 512;
}
