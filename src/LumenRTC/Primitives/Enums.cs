namespace LumenRTC;

public enum PeerConnectionState
{
    New = 0,
    Connecting = 1,
    Connected = 2,
    Disconnected = 3,
    Failed = 4,
    Closed = 5,
}

public enum SignalingState
{
    Stable = 0,
    HaveLocalOffer = 1,
    HaveRemoteOffer = 2,
    HaveLocalPrAnswer = 3,
    HaveRemotePrAnswer = 4,
    Closed = 5,
}

public enum IceGatheringState
{
    New = 0,
    Gathering = 1,
    Complete = 2,
}

public enum IceConnectionState
{
    New = 0,
    Checking = 1,
    Completed = 2,
    Connected = 3,
    Failed = 4,
    Disconnected = 5,
    Closed = 6,
    Max = 7,
}

public enum DataChannelState
{
    Connecting = 0,
    Open = 1,
    Closing = 2,
    Closed = 3,
}

public enum MediaType
{
    Audio = 0,
    Video = 1,
    Data = 2,
}

public enum IceTransportsType
{
    None = 0,
    Relay = 1,
    NoHost = 2,
    All = 3,
}

public enum BundlePolicy
{
    Balanced = 0,
    MaxBundle = 1,
    MaxCompat = 2,
}

public enum RtcpMuxPolicy
{
    Negotiate = 0,
    Require = 1,
}

public enum CandidateNetworkPolicy
{
    All = 0,
    LowCost = 1,
}

public enum TcpCandidatePolicy
{
    Enabled = 0,
    Disabled = 1,
}

public enum MediaSecurityType
{
    SrtpNone = 0,
    SdesSrtp = 1,
    DtlsSrtp = 2,
}

public enum SdpSemantics
{
    PlanB = 0,
    UnifiedPlan = 1,
}

public enum AudioSourceType
{
    Microphone = 0,
    Custom = 1,
}

public enum DesktopType
{
    Screen = 0,
    Window = 1,
}

public enum DesktopCaptureState
{
    Running = 0,
    Stopped = 1,
    Failed = 2,
}

public enum TrackState
{
    Live = 0,
    Ended = 1,
}

public enum DegradationPreference
{
    Disabled = 0,
    MaintainFramerate = 1,
    MaintainResolution = 2,
    Balanced = 3,
}

public enum RtpPriority
{
    VeryLow = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

public enum RtpTransceiverDirection
{
    SendRecv = 0,
    SendOnly = 1,
    RecvOnly = 2,
    Inactive = 3,
    Stopped = 4,
}

public enum DtlsTransportState
{
    New = 0,
    Connecting = 1,
    Connected = 2,
    Closed = 3,
    Failed = 4,
}

public enum LogSeverity
{
    Verbose = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    None = 4,
}

public enum VideoFrameFormat
{
    Argb = 0,
    Bgra = 1,
    Abgr = 2,
    Rgba = 3,
}
