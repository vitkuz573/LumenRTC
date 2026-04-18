#include "rtc_rtp_sender_impl.h"

#include "base/refcountedobject.h"
#include "rtc_audio_track_impl.h"
#include "rtc_dtls_transport_impl.h"
#include "rtc_dtmf_sender_impl.h"
#include "rtc_rtp_parameters_impl.h"
#include "rtc_video_track_impl.h"

namespace lumenrtc_bridge {
namespace {

webrtc::scoped_refptr<webrtc::MediaStreamTrackInterface> ToNativeTrack(
    const scoped_refptr<RTCMediaTrack>& track) {
  if (!track) {
    return nullptr;
  }

  const auto kind = track->kind().std_string();
  if (kind == webrtc::MediaStreamTrackInterface::kVideoKind) {
    return static_cast<VideoTrackImpl*>(track.get())->rtc_track();
  }
  if (kind == webrtc::MediaStreamTrackInterface::kAudioKind) {
    return static_cast<AudioTrackImpl*>(track.get())->rtc_track();
  }

  return nullptr;
}

scoped_refptr<RTCMediaTrack> ToBridgeTrack(
    const webrtc::scoped_refptr<webrtc::MediaStreamTrackInterface>& track) {
  if (!track) {
    return scoped_refptr<RTCMediaTrack>();
  }

  if (track->kind() == webrtc::MediaStreamTrackInterface::kVideoKind) {
    return scoped_refptr<RTCMediaTrack>(new RefCountedObject<VideoTrackImpl>(
        webrtc::scoped_refptr<webrtc::VideoTrackInterface>(
            static_cast<webrtc::VideoTrackInterface*>(track.get()))));
  }
  if (track->kind() == webrtc::MediaStreamTrackInterface::kAudioKind) {
    return scoped_refptr<RTCMediaTrack>(new RefCountedObject<AudioTrackImpl>(
        webrtc::scoped_refptr<webrtc::AudioTrackInterface>(
            static_cast<webrtc::AudioTrackInterface*>(track.get()))));
  }

  return scoped_refptr<RTCMediaTrack>();
}

}  // namespace

RTCRtpSenderImpl::RTCRtpSenderImpl(
    webrtc::scoped_refptr<webrtc::RtpSenderInterface> rtp_sender)
    : rtp_sender_(rtp_sender) {}

bool RTCRtpSenderImpl::set_track(scoped_refptr<RTCMediaTrack> track) {
  return rtp_sender_->SetTrack(ToNativeTrack(track));
}

scoped_refptr<RTCMediaTrack> RTCRtpSenderImpl::track() const {
  return ToBridgeTrack(rtp_sender_->track());
}

scoped_refptr<RTCDtlsTransport> RTCRtpSenderImpl::dtls_transport() const {
  if (nullptr == rtp_sender_->dtls_transport().get()) {
    return scoped_refptr<RTCDtlsTransport>();
  }
  return new RefCountedObject<RTCDtlsTransportImpl>(
      rtp_sender_->dtls_transport());
}

uint32_t RTCRtpSenderImpl::ssrc() const { return rtp_sender_->ssrc(); }

RTCMediaType RTCRtpSenderImpl::media_type() const {
  return static_cast<RTCMediaType>(rtp_sender_->media_type());
}

const string RTCRtpSenderImpl::id() const { return rtp_sender_->id(); }

const vector<string> RTCRtpSenderImpl::stream_ids() const {
  std::vector<string> values;
  values.reserve(rtp_sender_->stream_ids().size());
  for (const auto& item : rtp_sender_->stream_ids()) {
    values.push_back(item.c_str());
  }
  return values;
}

void RTCRtpSenderImpl::set_stream_ids(const vector<string> stream_ids) const {
  std::vector<std::string> list;
  for (auto id : stream_ids.std_vector()) {
    list.push_back(to_std_string(id));
  }
  rtp_sender_->SetStreams(list);
}

const vector<scoped_refptr<RTCRtpEncodingParameters>>
RTCRtpSenderImpl::init_send_encodings() const {
  std::vector<scoped_refptr<RTCRtpEncodingParameters>> vec;
  for (webrtc::RtpEncodingParameters item :
       rtp_sender_->init_send_encodings()) {
    vec.push_back(new RefCountedObject<RTCRtpEncodingParametersImpl>(item));
  }
  return vec;
}

scoped_refptr<RTCRtpParameters> RTCRtpSenderImpl::parameters() const {
  return new RefCountedObject<RTCRtpParametersImpl>(
      rtp_sender_->GetParameters());
}

bool RTCRtpSenderImpl::set_parameters(
    const scoped_refptr<RTCRtpParameters> parameters) {
  RTCRtpParametersImpl* impl =
      static_cast<RTCRtpParametersImpl*>(parameters.get());
  return rtp_sender_->SetParameters(impl->rtp_parameters()).ok();
}

scoped_refptr<RTCDtmfSender> RTCRtpSenderImpl::dtmf_sender() const {
  if (nullptr == rtp_sender_->GetDtmfSender().get()) {
    return scoped_refptr<RTCDtmfSender>();
  }
  return new RefCountedObject<RTCDtmfSenderImpl>(rtp_sender_->GetDtmfSender());
}

}  // namespace lumenrtc_bridge
