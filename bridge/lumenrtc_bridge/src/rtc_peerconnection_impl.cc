#include "rtc_peerconnection_impl.h"

#include <functional>
#include <optional>
#include <utility>
#include <vector>

#include "api/data_channel_interface.h"
#include "api/jsep.h"
#include "pc/media_session.h"
#include "rtc_base/logging.h"
#include "rtc_data_channel_impl.h"
#include "rtc_ice_candidate_impl.h"
#include "rtc_media_stream_impl.h"
#include "rtc_mediaconstraints_impl.h"
#include "rtc_rtp_receiver_impl.h"
#include "rtc_rtp_sender_impl.h"
#include "rtc_rtp_transceiver_impl.h"

using webrtc::Thread;

namespace lumenrtc_bridge {
namespace {

webrtc::PeerConnectionInterface::RtcpMuxPolicy ToNativeRtcpMuxPolicy(
    RtcpMuxPolicy value) {
  switch (value) {
    case RtcpMuxPolicy::kRtcpMuxPolicyNegotiate:
      return webrtc::PeerConnectionInterface::kRtcpMuxPolicyNegotiate;
    case RtcpMuxPolicy::kRtcpMuxPolicyRequire:
      return webrtc::PeerConnectionInterface::kRtcpMuxPolicyRequire;
  }

  return webrtc::PeerConnectionInterface::kRtcpMuxPolicyRequire;
}

webrtc::SdpSemantics ToNativeSdpSemantics(SdpSemantics value) {
  switch (value) {
    case SdpSemantics::kPlanB:
      return webrtc::SdpSemantics::kPlanB_DEPRECATED;
    case SdpSemantics::kUnifiedPlan:
      return webrtc::SdpSemantics::kUnifiedPlan;
  }

  return webrtc::SdpSemantics::kUnifiedPlan;
}

webrtc::PeerConnectionInterface::CandidateNetworkPolicy ToNativeCandidateNetworkPolicy(
    CandidateNetworkPolicy value) {
  switch (value) {
    case CandidateNetworkPolicy::kCandidateNetworkPolicyAll:
      return webrtc::PeerConnectionInterface::kCandidateNetworkPolicyAll;
    case CandidateNetworkPolicy::kCandidateNetworkPolicyLowCost:
      return webrtc::PeerConnectionInterface::kCandidateNetworkPolicyLowCost;
  }

  return webrtc::PeerConnectionInterface::kCandidateNetworkPolicyAll;
}

webrtc::PeerConnectionInterface::BundlePolicy ToNativeBundlePolicy(
    BundlePolicy value) {
  switch (value) {
    case kBundlePolicyBalanced:
      return webrtc::PeerConnectionInterface::kBundlePolicyBalanced;
    case kBundlePolicyMaxBundle:
      return webrtc::PeerConnectionInterface::kBundlePolicyMaxBundle;
    case kBundlePolicyMaxCompat:
      return webrtc::PeerConnectionInterface::kBundlePolicyMaxCompat;
  }

  return webrtc::PeerConnectionInterface::kBundlePolicyBalanced;
}

webrtc::PeerConnectionInterface::IceTransportsType ToNativeIceTransportType(
    IceTransportsType value) {
  switch (value) {
    case IceTransportsType::kAll:
      return webrtc::PeerConnectionInterface::kAll;
    case IceTransportsType::kNoHost:
      return webrtc::PeerConnectionInterface::kNoHost;
    case IceTransportsType::kNone:
      return webrtc::PeerConnectionInterface::kNone;
    case IceTransportsType::kRelay:
      return webrtc::PeerConnectionInterface::kRelay;
  }

  return webrtc::PeerConnectionInterface::kAll;
}

webrtc::PeerConnectionInterface::TcpCandidatePolicy ToNativeTcpCandidatePolicy(
    TcpCandidatePolicy value) {
  switch (value) {
    case TcpCandidatePolicy::kTcpCandidatePolicyDisabled:
      return webrtc::PeerConnectionInterface::kTcpCandidatePolicyDisabled;
    case TcpCandidatePolicy::kTcpCandidatePolicyEnabled:
      return webrtc::PeerConnectionInterface::kTcpCandidatePolicyEnabled;
  }

  return webrtc::PeerConnectionInterface::kTcpCandidatePolicyEnabled;
}

RTCPeerConnectionState ToBridgePeerConnectionState(
    webrtc::PeerConnectionInterface::PeerConnectionState value) {
  switch (value) {
    case webrtc::PeerConnectionInterface::PeerConnectionState::kNew:
      return RTCPeerConnectionState::RTCPeerConnectionStateNew;
    case webrtc::PeerConnectionInterface::PeerConnectionState::kConnecting:
      return RTCPeerConnectionState::RTCPeerConnectionStateConnecting;
    case webrtc::PeerConnectionInterface::PeerConnectionState::kConnected:
      return RTCPeerConnectionState::RTCPeerConnectionStateConnected;
    case webrtc::PeerConnectionInterface::PeerConnectionState::kDisconnected:
      return RTCPeerConnectionState::RTCPeerConnectionStateDisconnected;
    case webrtc::PeerConnectionInterface::PeerConnectionState::kFailed:
      return RTCPeerConnectionState::RTCPeerConnectionStateFailed;
    case webrtc::PeerConnectionInterface::PeerConnectionState::kClosed:
      return RTCPeerConnectionState::RTCPeerConnectionStateClosed;
  }

  return RTCPeerConnectionState::RTCPeerConnectionStateNew;
}

RTCIceGatheringState ToBridgeIceGatheringState(
    webrtc::PeerConnectionInterface::IceGatheringState value) {
  switch (value) {
    case webrtc::PeerConnectionInterface::kIceGatheringNew:
      return RTCIceGatheringState::RTCIceGatheringStateNew;
    case webrtc::PeerConnectionInterface::kIceGatheringGathering:
      return RTCIceGatheringState::RTCIceGatheringStateGathering;
    case webrtc::PeerConnectionInterface::kIceGatheringComplete:
      return RTCIceGatheringState::RTCIceGatheringStateComplete;
  }

  return RTCIceGatheringState::RTCIceGatheringStateNew;
}

RTCIceConnectionState ToBridgeIceConnectionState(
    webrtc::PeerConnectionInterface::IceConnectionState value) {
  switch (value) {
    case webrtc::PeerConnectionInterface::kIceConnectionNew:
      return RTCIceConnectionState::RTCIceConnectionStateNew;
    case webrtc::PeerConnectionInterface::kIceConnectionChecking:
      return RTCIceConnectionState::RTCIceConnectionStateChecking;
    case webrtc::PeerConnectionInterface::kIceConnectionCompleted:
      return RTCIceConnectionState::RTCIceConnectionStateCompleted;
    case webrtc::PeerConnectionInterface::kIceConnectionConnected:
      return RTCIceConnectionState::RTCIceConnectionStateConnected;
    case webrtc::PeerConnectionInterface::kIceConnectionFailed:
      return RTCIceConnectionState::RTCIceConnectionStateFailed;
    case webrtc::PeerConnectionInterface::kIceConnectionDisconnected:
      return RTCIceConnectionState::RTCIceConnectionStateDisconnected;
    case webrtc::PeerConnectionInterface::kIceConnectionClosed:
      return RTCIceConnectionState::RTCIceConnectionStateClosed;
    case webrtc::PeerConnectionInterface::kIceConnectionMax:
      return RTCIceConnectionState::RTCIceConnectionStateMax;
  }

  return RTCIceConnectionState::RTCIceConnectionStateNew;
}

RTCSignalingState ToBridgeSignalingState(
    webrtc::PeerConnectionInterface::SignalingState value) {
  switch (value) {
    case webrtc::PeerConnectionInterface::kStable:
      return RTCSignalingState::RTCSignalingStateStable;
    case webrtc::PeerConnectionInterface::kHaveLocalOffer:
      return RTCSignalingState::RTCSignalingStateHaveLocalOffer;
    case webrtc::PeerConnectionInterface::kHaveRemoteOffer:
      return RTCSignalingState::RTCSignalingStateHaveRemoteOffer;
    case webrtc::PeerConnectionInterface::kHaveLocalPrAnswer:
      return RTCSignalingState::RTCSignalingStateHaveLocalPrAnswer;
    case webrtc::PeerConnectionInterface::kHaveRemotePrAnswer:
      return RTCSignalingState::RTCSignalingStateHaveRemotePrAnswer;
    case webrtc::PeerConnectionInterface::kClosed:
      return RTCSignalingState::RTCSignalingStateClosed;
  }

  return RTCSignalingState::RTCSignalingStateStable;
}

webrtc::PeerConnectionInterface::IceServer ToNativeIceServer(
    const IceServer& value) {
  webrtc::PeerConnectionInterface::IceServer server;
  server.uri = to_std_string(value.uri);
  server.username = to_std_string(value.username);
  server.password = to_std_string(value.password);
  return server;
}

webrtc::MediaType ToNativeMediaType(RTCMediaType media_type) {
  switch (media_type) {
    case RTCMediaType::AUDIO:
      return webrtc::MediaType::AUDIO;
    case RTCMediaType::VIDEO:
      return webrtc::MediaType::VIDEO;
    default:
      return webrtc::MediaType::UNSUPPORTED;
  }
}

webrtc::scoped_refptr<webrtc::MediaStreamTrackInterface> ToNativeTrack(
    const scoped_refptr<RTCMediaTrack>& track) {
  if (!track) {
    return nullptr;
  }

  const auto kind = to_std_string(track->kind());
  if (kind == webrtc::MediaStreamTrackInterface::kVideoKind) {
    auto* impl = static_cast<VideoTrackImpl*>(track.get());
    return impl->rtc_track();
  }
  if (kind == webrtc::MediaStreamTrackInterface::kAudioKind) {
    auto* impl = static_cast<AudioTrackImpl*>(track.get());
    return impl->rtc_track();
  }

  return nullptr;
}

}  // namespace
class SetSessionDescriptionObserverProxy
    : public webrtc::SetLocalDescriptionObserverInterface,
      public webrtc::SetRemoteDescriptionObserverInterface {
 public:
  SetSessionDescriptionObserverProxy(OnSetSdpSuccess success_callback,
                                     OnSetSdpFailure failure_callback)
      : success_callback_(success_callback),
        failure_callback_(failure_callback) {}
  ~SetSessionDescriptionObserverProxy() {}
  static webrtc::scoped_refptr<SetSessionDescriptionObserverProxy> Create(
      OnSetSdpSuccess success_callback, OnSetSdpFailure failure_callback) {
    return webrtc::make_ref_counted<SetSessionDescriptionObserverProxy>(
        success_callback, failure_callback);
  }
  virtual void OnSetLocalDescriptionComplete(webrtc::RTCError error) override {
    RTC_LOG(LS_INFO) << __FUNCTION__;
    if (error.ok()) {
      success_callback_();
    } else {
      failure_callback_(error.message());
    }
  }

  virtual void OnSetRemoteDescriptionComplete(webrtc::RTCError error) override {
    RTC_LOG(LS_INFO) << __FUNCTION__;
    if (error.ok()) {
      success_callback_();
    } else {
      failure_callback_(error.message());
    }
  }

  virtual void OnSuccess() {
    RTC_LOG(LS_INFO) << __FUNCTION__;
    success_callback_();
  }
  virtual void OnFailure(webrtc::RTCError error) {
    RTC_LOG(LS_INFO) << __FUNCTION__ << " " << error.message();
    failure_callback_(error.message());
  }

 private:
  OnSetSdpSuccess success_callback_;
  OnSetSdpFailure failure_callback_;
};

class CreateSessionDescriptionObserverProxy
    : public webrtc::CreateSessionDescriptionObserver {
 public:
  static CreateSessionDescriptionObserverProxy* Create(
      OnSdpCreateSuccess success_callback,
      OnSdpCreateFailure failure_callback) {
    return new webrtc::RefCountedObject<CreateSessionDescriptionObserverProxy>(
        success_callback, failure_callback);
  }

  CreateSessionDescriptionObserverProxy(OnSdpCreateSuccess success_callback,
                                        OnSdpCreateFailure failure_callback)
      : success_callback_(success_callback),
        failure_callback_(failure_callback) {}

 public:
  virtual void OnSuccess(webrtc::SessionDescriptionInterface* desc) {
    std::string sdp;
    desc->ToString(&sdp);
    std::string type = desc->type();
    success_callback_(sdp.c_str(), type.c_str());
  }

  virtual void OnFailure(webrtc::RTCError error) {
    failure_callback_(error.message());
  }

 private:
  OnSdpCreateSuccess success_callback_;
  OnSdpCreateFailure failure_callback_;
};

RTCPeerConnectionImpl::RTCPeerConnectionImpl(
    const RTCConfiguration& configuration,
    scoped_refptr<RTCMediaConstraints> constraints,
    webrtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
        peer_connection_factory)
    : rtc_peerconnection_factory_(peer_connection_factory),
      configuration_(configuration),
      constraints_(constraints),
      callback_crt_sec_(new webrtc::Mutex()) {
  RTC_LOG(LS_INFO) << __FUNCTION__ << ": ctor";
  Initialize();
}

RTCPeerConnectionImpl::~RTCPeerConnectionImpl() {
  Close();
  RTC_LOG(LS_INFO) << __FUNCTION__ << ": dtor";
}

void RTCPeerConnectionImpl::OnAddTrack(
    webrtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver,
    const std::vector<webrtc::scoped_refptr<webrtc::MediaStreamInterface>>&
        streams) {
  if (nullptr != observer_) {
    std::vector<scoped_refptr<RTCMediaStream>> out_streams;
    for (auto item : streams) {
      out_streams.push_back(new RefCountedObject<MediaStreamImpl>(item));
    }
    scoped_refptr<RTCRtpReceiver> rtc_receiver =
        new RefCountedObject<RTCRtpReceiverImpl>(receiver);
    observer_->OnAddTrack(out_streams, rtc_receiver);
  }
}

void RTCPeerConnectionImpl::OnTrack(
    webrtc::scoped_refptr<webrtc::RtpTransceiverInterface> transceiver) {
  if (nullptr != observer_) {
    observer_->OnTrack(
        new RefCountedObject<RTCRtpTransceiverImpl>(transceiver));
  }
}

void RTCPeerConnectionImpl::OnRemoveTrack(
    webrtc::scoped_refptr<webrtc::RtpReceiverInterface> receiver) {
  if (nullptr != observer_) {
    observer_->OnRemoveTrack(
        new RefCountedObject<RTCRtpReceiverImpl>(receiver));
  }
}

// Called when a remote stream is added
void RTCPeerConnectionImpl::OnAddStream(
    webrtc::scoped_refptr<webrtc::MediaStreamInterface> stream) {
  RTC_LOG(LS_INFO) << __FUNCTION__ << " " << stream->id();

  scoped_refptr<MediaStreamImpl> remote_stream = scoped_refptr<MediaStreamImpl>(
      new RefCountedObject<MediaStreamImpl>(stream));

  remote_stream->RegisterRTCPeerConnectionObserver(observer_);

  remote_streams_.push_back(remote_stream);

  if (observer_) {
    observer_->OnAddStream(remote_stream);
  }
}

void RTCPeerConnectionImpl::OnRemoveStream(
    webrtc::scoped_refptr<webrtc::MediaStreamInterface> stream) {
  RTC_LOG(LS_INFO) << __FUNCTION__ << " " << stream->id();

  MediaStreamImpl* recv_stream = nullptr;

  for (auto kv : remote_streams_) {
    MediaStreamImpl* recv_st = static_cast<MediaStreamImpl*>(kv.get());
    if (recv_st->rtc_media_stream() == stream) {
      recv_stream = recv_st;
    }
  }

  if (nullptr != recv_stream) {
    if (observer_) {
      observer_->OnRemoveStream(recv_stream);
    }

    remote_streams_.erase(
        std::find(remote_streams_.begin(), remote_streams_.end(), recv_stream));
  }
}

void RTCPeerConnectionImpl::OnDataChannel(
    webrtc::scoped_refptr<webrtc::DataChannelInterface> rtc_data_channel) {
  data_channel_ = scoped_refptr<RTCDataChannelImpl>(
      new RefCountedObject<RTCDataChannelImpl>(rtc_data_channel));

  if (observer_) observer_->OnDataChannel(data_channel_);
}

void RTCPeerConnectionImpl::OnRenegotiationNeeded() {
  if (observer_) {
    observer_->OnRenegotiationNeeded();
  }
}

void RTCPeerConnectionImpl::OnConnectionChange(
    webrtc::PeerConnectionInterface::PeerConnectionState new_state) {
  if (observer_)
    observer_->OnPeerConnectionState(ToBridgePeerConnectionState(new_state));
}

void RTCPeerConnectionImpl::OnIceGatheringChange(
    webrtc::PeerConnectionInterface::IceGatheringState new_state) {
  if (observer_)
    observer_->OnIceGatheringState(ToBridgeIceGatheringState(new_state));
}

void RTCPeerConnectionImpl::OnIceConnectionChange(
    webrtc::PeerConnectionInterface::IceConnectionState new_state) {
  if (observer_)
    observer_->OnIceConnectionState(ToBridgeIceConnectionState(new_state));
}

void RTCPeerConnectionImpl::OnSignalingChange(
    webrtc::PeerConnectionInterface::SignalingState new_state) {
  if (observer_) observer_->OnSignalingState(ToBridgeSignalingState(new_state));
}

bool RTCPeerConnectionImpl::AddCandidate(const string mid, int mid_mline_index,
                                         const string cand_sdp) {
  if (!rtc_peerconnection_) {
    return false;
  }

  webrtc::SdpParseError error;
  std::unique_ptr<webrtc::IceCandidateInterface> candidate(
      webrtc::CreateIceCandidate(
          to_std_string(mid), mid_mline_index, to_std_string(cand_sdp),
          &error));
  if (!candidate) {
    return false;
  }

  return rtc_peerconnection_->AddIceCandidate(candidate.get());
}

void RTCPeerConnectionImpl::OnIceCandidate(
    const webrtc::IceCandidateInterface* candidate) {
  if (!rtc_peerconnection_) return;

#if 0
    if (candidate->candidate().protocol() != "tcp")
        return;


  // For loopback test. To save some connecting delay.
  if (type_ == kLoopBack) {
    if (!rtc_peerconnection_->AddIceCandidate(candidate)) {
      RTC_LOG(LS_WARNING) << "Failed to apply the received candidate";
    }
    return;
  }
#endif

  std::string cand_sdp;
  if (observer_ && candidate->ToString(&cand_sdp)) {
    SdpParseError error;
    scoped_refptr<RTCIceCandidate> cand =
        RTCIceCandidate::Create(cand_sdp.c_str(), candidate->sdp_mid().c_str(),
                                candidate->sdp_mline_index(), &error);
    observer_->OnIceCandidate(cand);
  }

  RTC_LOG(LS_INFO) << __FUNCTION__ << ", mid " << candidate->sdp_mid()
                   << ", mline " << candidate->sdp_mline_index()
                   << ", sdp_length " << cand_sdp.size();
}

void RTCPeerConnectionImpl::RegisterRTCPeerConnectionObserver(
    RTCPeerConnectionObserver* observer) {
  webrtc::MutexLock cs(callback_crt_sec_.get());
  observer_ = observer;
}

void RTCPeerConnectionImpl::DeRegisterRTCPeerConnectionObserver() {
  webrtc::MutexLock cs(callback_crt_sec_.get());
  observer_ = nullptr;
}

bool RTCPeerConnectionImpl::Initialize() {
  RTC_DCHECK(rtc_peerconnection_factory_ != nullptr);
  RTC_DCHECK(rtc_peerconnection_ == nullptr);

  webrtc::PeerConnectionInterface::RTCConfiguration config;
  for (int i = 0; i < kMaxIceServerSize; ++i) {
    const IceServer& ice_server = configuration_.ice_servers[i];
    if (ice_server.uri.size() == 0) {
      continue;
    }

    config.servers.push_back(ToNativeIceServer(ice_server));
  }

  config.bundle_policy = ToNativeBundlePolicy(configuration_.bundle_policy);
  config.sdp_semantics = ToNativeSdpSemantics(configuration_.sdp_semantics);
  config.candidate_network_policy = ToNativeCandidateNetworkPolicy(
      configuration_.candidate_network_policy);
  config.tcp_candidate_policy =
      ToNativeTcpCandidatePolicy(configuration_.tcp_candidate_policy);
  config.type = ToNativeIceTransportType(configuration_.type);
  config.rtcp_mux_policy = ToNativeRtcpMuxPolicy(configuration_.rtcp_mux_policy);
  config.ice_candidate_pool_size = configuration_.ice_candidate_pool_size;
  config.disable_ipv6_on_wifi = configuration_.disable_ipv6_on_wifi;
  config.disable_link_local_networks =
      configuration_.disable_link_local_networks;
  config.max_ipv6_networks = configuration_.max_ipv6_networks;
  if (configuration_.screencast_min_bitrate > 0) {
    config.screencast_min_bitrate = configuration_.screencast_min_bitrate;
  }
  config.set_dscp(configuration_.enable_dscp);

  offer_answer_options_.offer_to_receive_audio =
      configuration_.offer_to_receive_audio;
  offer_answer_options_.offer_to_receive_video =
      configuration_.offer_to_receive_video;
  offer_answer_options_.use_rtp_mux = configuration_.use_rtp_mux;

  if (constraints_) {
    auto* media_constraints =
        static_cast<RTCMediaConstraintsImpl*>(constraints_.get());
    webrtc::MediaConstraints rtc_constraints(
        media_constraints->GetMandatory(), media_constraints->GetOptional());
    CopyConstraintsIntoRtcConfiguration(&rtc_constraints, &config);
  }

  webrtc::PeerConnectionFactoryInterface::Options options;
  options.disable_encryption =
      configuration_.srtp_type == MediaSecurityType::kSRTP_None;
  rtc_peerconnection_factory_->SetOptions(options);

  webrtc::PeerConnectionDependencies dependencies(this);
  auto result = rtc_peerconnection_factory_->CreatePeerConnectionOrError(
      config, std::move(dependencies));
  if (!result.ok()) {
    RTC_LOG(LS_WARNING) << "CreatePeerConnection failed: "
                        << result.error().message();
    Close();
    return false;
  }

  rtc_peerconnection_ = result.MoveValue();
  return true;
}

scoped_refptr<RTCDataChannel> RTCPeerConnectionImpl::CreateDataChannel(
    const string label, RTCDataChannelInit* dataChannelDict) {
  webrtc::DataChannelInit init;
  init.id = dataChannelDict->id;
  init.maxRetransmits = dataChannelDict->maxRetransmits;
  init.maxRetransmitTime = dataChannelDict->maxRetransmitTime;
  init.negotiated = dataChannelDict->negotiated;
  init.ordered = dataChannelDict->ordered;
  init.protocol = to_std_string(dataChannelDict->protocol);
  init.reliable = dataChannelDict->reliable;

  webrtc::RTCErrorOr<webrtc::scoped_refptr<webrtc::DataChannelInterface>>
      result = rtc_peerconnection_->CreateDataChannelOrError(
          to_std_string(label), &init);

  if (!result.ok()) {
    RTC_LOG(LS_ERROR) << "CreateDataChannel failed: "
                      << ToString(result.error().type()) << " "
                      << result.error().message();
    return nullptr;
  }

  data_channel_ = scoped_refptr<RTCDataChannelImpl>(
      new RefCountedObject<RTCDataChannelImpl>(result.MoveValue()));

  dataChannelDict->id = init.id;
  return data_channel_;
}

void RTCPeerConnectionImpl::SetLocalDescription(const string sdp,
                                                const string type,
                                                OnSetSdpSuccess success,
                                                OnSetSdpFailure failure) {
  webrtc::SdpParseError error;
  std::optional<webrtc::SdpType> maybe_type =
      webrtc::SdpTypeFromString(to_std_string(type));
  if (!maybe_type) {
    if (failure) {
      failure("Invalid session description type.");
    }
    return;
  }
  std::unique_ptr<webrtc::SessionDescriptionInterface> session_description(
      webrtc::CreateSessionDescription(*maybe_type, to_std_string(sdp),
                                       &error));

  if (!session_description) {
    std::string error = "Can't parse received session description message.";
    RTC_LOG(LS_WARNING) << error;
    failure(error.c_str());
    return;
  }
  webrtc::scoped_refptr<webrtc::SetLocalDescriptionObserverInterface> observer =
      webrtc::make_ref_counted<SetSessionDescriptionObserverProxy>(success,
                                                                   failure);
  rtc_peerconnection_->SetLocalDescription(std::move(session_description),
                                           observer);
}

void RTCPeerConnectionImpl::SetRemoteDescription(const string sdp,
                                                 const string type,
                                                 OnSetSdpSuccess success,
                                                 OnSetSdpFailure failure) {
  RTC_LOG(LS_INFO) << "Received session description. Type="
                   << to_std_string(type) << ", Length="
                   << static_cast<int>(sdp.std_string().size());
  webrtc::SdpParseError error;
  std::optional<webrtc::SdpType> maybe_type =
      webrtc::SdpTypeFromString(type.std_string());
  if (!maybe_type) {
    if (failure) {
      failure("Invalid session description type.");
    }
    return;
  }
  std::unique_ptr<webrtc::SessionDescriptionInterface> session_description(
      webrtc::CreateSessionDescription(*maybe_type, sdp.std_string(), &error));

  if (!session_description) {
    std::string error = "Can't parse received session description message.";
    RTC_LOG(LS_WARNING) << error;
    failure(error.c_str());
    return;
  }

  auto* video_content = static_cast<webrtc::MediaContentDescription*>(
      session_description->description()->GetContentDescriptionByName("video"));
  if (video_content && configuration_.local_video_bandwidth > 0) {
    video_content->set_bandwidth(configuration_.local_video_bandwidth * 1000);
  }

  auto* audio_content = static_cast<webrtc::MediaContentDescription*>(
      session_description->description()->GetContentDescriptionByName("audio"));
  if (audio_content && configuration_.local_audio_bandwidth > 0) {
    audio_content->set_bandwidth(configuration_.local_audio_bandwidth * 1000);
  }
  webrtc::scoped_refptr<webrtc::SetRemoteDescriptionObserverInterface>
      observer = webrtc::make_ref_counted<SetSessionDescriptionObserverProxy>(
          success, failure);
  rtc_peerconnection_->SetRemoteDescription(std::move(session_description),
                                            observer);

  return;
}

void RTCPeerConnectionImpl::GetLocalDescription(OnGetSdpSuccess success,
                                                OnGetSdpFailure failure) {
  auto local_description = rtc_peerconnection_->local_description();
  if (!local_description) {
    if (failure) {
      failure("not local description");
    }
    return;
  }

  if (success) {
    std::string dsp;
    local_description->ToString(&dsp);
    success(dsp.c_str(), webrtc::SdpTypeToString(local_description->GetType()));
  }
}

void RTCPeerConnectionImpl::GetRemoteDescription(OnGetSdpSuccess success,
                                                 OnGetSdpFailure failure) {
  auto remote_description = rtc_peerconnection_->remote_description();
  if (!remote_description) {
    if (failure) {
      failure("not remote description");
    }
    return;
  }

  if (success) {
    std::string dsp;
    remote_description->ToString(&dsp);
    success(dsp.c_str(),
            webrtc::SdpTypeToString(remote_description->GetType()));
  }
}

void RTCPeerConnectionImpl::CreateOffer(
    OnSdpCreateSuccess success, OnSdpCreateFailure failure,
    scoped_refptr<RTCMediaConstraints> constraints) {
  if (!rtc_peerconnection_.get() || !rtc_peerconnection_factory_.get()) {
    webrtc::MutexLock cs(callback_crt_sec_.get());
    failure("Failed to initialize PeerConnection");
    return;
  }

  RTCMediaConstraintsImpl* media_constraints =
      static_cast<RTCMediaConstraintsImpl*>(constraints.get());
  webrtc::PeerConnectionInterface::RTCOfferAnswerOptions offer_answer_options;
  webrtc::MediaConstraints rtc_constraints(media_constraints->GetMandatory(),
                                           media_constraints->GetOptional());
  if (CopyConstraintsIntoOfferAnswerOptions(&rtc_constraints,
                                            &offer_answer_options) == false) {
    offer_answer_options = offer_answer_options_;
  }

  rtc_peerconnection_->CreateOffer(
      CreateSessionDescriptionObserverProxy::Create(success, failure),
      offer_answer_options);
}

void RTCPeerConnectionImpl::CreateAnswer(
    OnSdpCreateSuccess success, OnSdpCreateFailure failure,
    scoped_refptr<RTCMediaConstraints> constraints) {
  if (!rtc_peerconnection_.get() || !rtc_peerconnection_factory_.get()) {
    webrtc::MutexLock cs(callback_crt_sec_.get());
    failure("Failed to initialize PeerConnection");
    return;
  }
  RTCMediaConstraintsImpl* media_constraints =
      static_cast<RTCMediaConstraintsImpl*>(constraints.get());
  webrtc::PeerConnectionInterface::RTCOfferAnswerOptions offer_answer_options;
  webrtc::MediaConstraints rtc_constraints(media_constraints->GetMandatory(),
                                           media_constraints->GetOptional());
  if (CopyConstraintsIntoOfferAnswerOptions(&rtc_constraints,
                                            &offer_answer_options) == false) {
    offer_answer_options = offer_answer_options_;
  }
  rtc_peerconnection_->CreateAnswer(
      CreateSessionDescriptionObserverProxy::Create(success, failure),
      offer_answer_options);
}

void RTCPeerConnectionImpl::RestartIce() {
  RTC_LOG(LS_INFO) << __FUNCTION__;
  if (rtc_peerconnection_.get()) {
    rtc_peerconnection_->RestartIce();
  }
}

void RTCPeerConnectionImpl::Close() {
  RTC_LOG(LS_INFO) << __FUNCTION__;
  if (rtc_peerconnection_.get()) {
    rtc_peerconnection_->Close();
    rtc_peerconnection_ = nullptr;
    data_channel_ = nullptr;
    local_streams_.clear();
    for (auto stream : remote_streams_) {
      if (observer_) {
        observer_->OnRemoveStream(stream);
      }
      /*   stream->GetAudioTracks([&](scoped_refptr<RTCMediaTrack> track) {
           observer_->OnRemoveTrack([&](OnRTCMediaStream on) { on(stream); },
                                    track);
         });
         stream->GetVideoTracks([&](scoped_refptr<RTCMediaTrack> track) {
           observer_->OnRemoveTrack([&](OnRTCMediaStream on) { on(stream); },
                                    track);
         });*/
    }
    remote_streams_.clear();
  }
}

int RTCPeerConnectionImpl::AddStream(scoped_refptr<RTCMediaStream> stream) {
  MediaStreamImpl* send_stream = static_cast<MediaStreamImpl*>(stream.get());
  webrtc::scoped_refptr<webrtc::MediaStreamInterface> rtc_media_stream =
      send_stream->rtc_media_stream();

  send_stream->RegisterRTCPeerConnectionObserver(observer_);

  if (std::find(local_streams_.begin(), local_streams_.end(), stream) !=
      local_streams_.end())
    return -1;  // Already added.

  if (!rtc_peerconnection_->AddStream(rtc_media_stream.get())) {
    RTC_LOG(LS_ERROR) << "Adding stream to PeerConnection failed";
  }

  local_streams_.push_back(stream);
  return 0;
}

int RTCPeerConnectionImpl::RemoveStream(scoped_refptr<RTCMediaStream> stream) {
  MediaStreamImpl* send_stream = static_cast<MediaStreamImpl*>(stream.get());

  webrtc::scoped_refptr<webrtc::MediaStreamInterface> rtc_media_stream =
      send_stream->rtc_media_stream();

  if (std::find(local_streams_.begin(), local_streams_.end(), stream) ==
      local_streams_.end())
    return -1;  // Not found.

  rtc_peerconnection_->RemoveStream(rtc_media_stream.get());

  local_streams_.erase(
      std::find(local_streams_.begin(), local_streams_.end(), stream));
  return 0;
}

scoped_refptr<RTCMediaStream> RTCPeerConnectionImpl::CreateLocalMediaStream(
    const string stream_id) {
  if (!rtc_peerconnection_factory_.get()) {
    return nullptr;
  }
  auto stream =
      rtc_peerconnection_factory_->CreateLocalMediaStream(stream_id.c_string());
  auto rtc_stream = new RefCountedObject<MediaStreamImpl>(stream);
  local_streams_.push_back(rtc_stream);
  return rtc_stream;
}

bool RTCPeerConnectionImpl::GetStats(scoped_refptr<RTCRtpSender> sender,
                                     OnStatsCollectorSuccess success,
                                     OnStatsCollectorFailure failure) {
  webrtc::scoped_refptr<WebRTCStatsCollectorCallback> rtc_callback =
      WebRTCStatsCollectorCallback::Create(success, failure);
  if (!rtc_peerconnection_.get() || !rtc_peerconnection_factory_.get()) {
    webrtc::MutexLock cs(callback_crt_sec_.get());
    failure("Failed to initialize PeerConnection");
    return false;
  }
  RTCRtpSenderImpl* impl = static_cast<RTCRtpSenderImpl*>(sender.get());
  rtc_peerconnection_->GetStats(impl->rtc_rtp_sender(), rtc_callback);
  return true;
}

bool RTCPeerConnectionImpl::GetStats(scoped_refptr<RTCRtpReceiver> receiver,
                                     OnStatsCollectorSuccess success,
                                     OnStatsCollectorFailure failure) {
  webrtc::scoped_refptr<WebRTCStatsCollectorCallback> rtc_callback =
      WebRTCStatsCollectorCallback::Create(success, failure);
  if (!rtc_peerconnection_.get() || !rtc_peerconnection_factory_.get()) {
    webrtc::MutexLock cs(callback_crt_sec_.get());
    failure("Failed to initialize PeerConnection");
    return false;
  }
  RTCRtpReceiverImpl* impl = static_cast<RTCRtpReceiverImpl*>(receiver.get());
  rtc_peerconnection_->GetStats(impl->rtp_receiver(), rtc_callback);
  return true;
}

void RTCPeerConnectionImpl::GetStats(OnStatsCollectorSuccess success,
                                     OnStatsCollectorFailure failure) {
  webrtc::scoped_refptr<WebRTCStatsCollectorCallback> rtc_callback =
      WebRTCStatsCollectorCallback::Create(success, failure);
  if (!rtc_peerconnection_.get() || !rtc_peerconnection_factory_.get()) {
    webrtc::MutexLock cs(callback_crt_sec_.get());
    failure("Failed to initialize PeerConnection");
    return;
  }
  rtc_peerconnection_->GetStats(rtc_callback.get());
}

scoped_refptr<RTCRtpTransceiver> RTCPeerConnectionImpl::AddTransceiver(
    scoped_refptr<RTCMediaTrack> track,
    scoped_refptr<RTCRtpTransceiverInit> init) {
  if (!track || !init || !rtc_peerconnection_) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  auto* initImpl = static_cast<RTCRtpTransceiverInitImpl*>(init.get());
  auto native_track = ToNativeTrack(track);
  if (!native_track) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  auto errorOr = rtc_peerconnection_->AddTransceiver(
      native_track, initImpl->rtp_transceiver_init());
  if (!errorOr.ok()) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  return new RefCountedObject<RTCRtpTransceiverImpl>(errorOr.value());
}

scoped_refptr<RTCRtpTransceiver> RTCPeerConnectionImpl::AddTransceiver(
    scoped_refptr<RTCMediaTrack> track) {
  if (!track || !rtc_peerconnection_) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  auto native_track = ToNativeTrack(track);
  if (!native_track) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  auto errorOr = rtc_peerconnection_->AddTransceiver(native_track);
  if (!errorOr.ok()) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  return new RefCountedObject<RTCRtpTransceiverImpl>(errorOr.value());
}

scoped_refptr<RTCRtpTransceiver> RTCPeerConnectionImpl::AddTransceiver(
    RTCMediaType media_type) {
  if (!rtc_peerconnection_) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  const auto native_type = ToNativeMediaType(media_type);
  if (native_type == webrtc::MediaType::UNSUPPORTED) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  auto errorOr = rtc_peerconnection_->AddTransceiver(native_type);
  if (!errorOr.ok()) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  return new RefCountedObject<RTCRtpTransceiverImpl>(errorOr.value());
}

scoped_refptr<RTCRtpTransceiver> RTCPeerConnectionImpl::AddTransceiver(
    RTCMediaType media_type, scoped_refptr<RTCRtpTransceiverInit> init) {
  if (!init || !rtc_peerconnection_) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  const auto native_type = ToNativeMediaType(media_type);
  if (native_type == webrtc::MediaType::UNSUPPORTED) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  auto* initImpl = static_cast<RTCRtpTransceiverInitImpl*>(init.get());
  auto errorOr = rtc_peerconnection_->AddTransceiver(
      native_type, initImpl->rtp_transceiver_init());
  if (!errorOr.ok()) {
    return scoped_refptr<RTCRtpTransceiver>();
  }

  return new RefCountedObject<RTCRtpTransceiverImpl>(errorOr.value());
}

scoped_refptr<RTCRtpSender> RTCPeerConnectionImpl::AddTrack(
    scoped_refptr<RTCMediaTrack> track, vector<string> streamIds) {
  if (!track || !rtc_peerconnection_) {
    return scoped_refptr<RTCRtpSender>();
  }

  auto native_track = ToNativeTrack(track);
  if (!native_track) {
    return scoped_refptr<RTCRtpSender>();
  }

  std::vector<std::string> stream_ids;
  for (const auto& id : streamIds.std_vector()) {
    stream_ids.push_back(to_std_string(id));
  }

  auto errorOr = rtc_peerconnection_->AddTrack(native_track, stream_ids);
  if (!errorOr.ok()) {
    return scoped_refptr<RTCRtpSender>();
  }

  return new RefCountedObject<RTCRtpSenderImpl>(errorOr.value());
}

bool RTCPeerConnectionImpl::RemoveTrack(scoped_refptr<RTCRtpSender> render) {
  RTCRtpSenderImpl* impl = static_cast<RTCRtpSenderImpl*>(render.get());
  webrtc::RTCError err =
      rtc_peerconnection_->RemoveTrackOrError(impl->rtc_rtp_sender());
  if (err.ok()) {
    return true;
  }
  return false;
}

vector<scoped_refptr<RTCRtpSender>> RTCPeerConnectionImpl::senders() {
  std::vector<scoped_refptr<RTCRtpSender>> vec;
  for (auto item : rtc_peerconnection_->GetSenders()) {
    vec.push_back(new RefCountedObject<RTCRtpSenderImpl>(item));
  }
  return vec;
}

vector<scoped_refptr<RTCRtpTransceiver>> RTCPeerConnectionImpl::transceivers() {
  std::vector<scoped_refptr<RTCRtpTransceiver>> vec;
  for (auto item : rtc_peerconnection_->GetTransceivers()) {
    vec.push_back(new RefCountedObject<RTCRtpTransceiverImpl>(item));
  }
  return vec;
}

vector<scoped_refptr<RTCRtpReceiver>> RTCPeerConnectionImpl::receivers() {
  std::vector<scoped_refptr<RTCRtpReceiver>> vec;
  for (auto item : rtc_peerconnection_->GetReceivers()) {
    vec.push_back(new RefCountedObject<RTCRtpReceiverImpl>(item));
  }
  return vec;
}

RTCSignalingState RTCPeerConnectionImpl::signaling_state() {
  return signaling_state_map[rtc_peerconnection_->signaling_state()];
}

RTCIceConnectionState RTCPeerConnectionImpl::ice_connection_state() {
  return ice_connection_state_map[rtc_peerconnection_->ice_connection_state()];
}

RTCIceConnectionState
RTCPeerConnectionImpl::standardized_ice_connection_state() {
  return ice_connection_state_map[rtc_peerconnection_
                                      ->standardized_ice_connection_state()];
}

RTCPeerConnectionState RTCPeerConnectionImpl::peer_connection_state() {
  return peer_connection_state_map[rtc_peerconnection_
                                       ->peer_connection_state()];
}

RTCIceGatheringState RTCPeerConnectionImpl::ice_gathering_state() {
  return ice_gathering_state_map[rtc_peerconnection_->ice_gathering_state()];
}

void WebRTCStatsCollectorCallback::OnStatsDelivered(
    const webrtc::scoped_refptr<const webrtc::RTCStatsReport>& report) {
  webrtc::RTCStatsReport::ConstIterator iter = report->begin();
  std::vector<scoped_refptr<MediaRTCStats>> reports;
  while (iter != report->end()) {
    reports.push_back(new RefCountedObject<MediaRTCStatsImpl>(iter->copy()));
    iter++;
  }
  success_(reports);
}

MediaRTCStatsImpl::MediaRTCStatsImpl(std::unique_ptr<webrtc::RTCStats> stats)
    : stats_(std::move(stats)) {}

const string MediaRTCStatsImpl::id() { return stats_->id(); }

const string MediaRTCStatsImpl::type() { return stats_->type(); }

int64_t MediaRTCStatsImpl::timestamp_us() { return stats_->timestamp().us(); }

const string MediaRTCStatsImpl::ToJson() { return stats_->ToJson(); }

}  // namespace lumenrtc_bridge
