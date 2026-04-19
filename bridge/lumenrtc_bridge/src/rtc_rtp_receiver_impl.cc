#include "rtc_rtp_receiver_impl.h"

#include <cstring>

#include "base/refcountedobject.h"
#include "rtc_audio_track_impl.h"
#include "rtc_dtls_transport_impl.h"
#include "rtc_media_stream_impl.h"
#include "rtc_rtp_parameters_impl.h"
#include "rtc_video_track_impl.h"

namespace lumenrtc_bridge {
RTCRtpReceiverImpl::RTCRtpReceiverImpl(
    webrtc::scoped_refptr<webrtc::RtpReceiverInterface> rtp_receiver)
    : rtp_receiver_(rtp_receiver), observer_(nullptr) {}

webrtc::scoped_refptr<webrtc::RtpReceiverInterface>
RTCRtpReceiverImpl::rtp_receiver() {
  return rtp_receiver_;
}

void RTCRtpReceiverImpl::OnFirstPacketReceived(webrtc::MediaType media_type) {
  if (nullptr != observer_) {
    observer_->OnFirstPacketReceived(static_cast<RTCMediaType>(media_type));
  }
}

scoped_refptr<RTCMediaTrack> RTCRtpReceiverImpl::track() const {
  webrtc::scoped_refptr<webrtc::MediaStreamTrackInterface> track =
      rtp_receiver_->track();
  if (nullptr == track.get()) {
    return scoped_refptr<RTCMediaTrack>();
  }

  const auto kind = track->kind();
  if (std::strcmp(kind.c_str(),
                  webrtc::MediaStreamTrackInterface::kVideoKind) == 0) {
    return scoped_refptr<RTCMediaTrack>(new RefCountedObject<VideoTrackImpl>(
        webrtc::scoped_refptr<webrtc::VideoTrackInterface>(
            static_cast<webrtc::VideoTrackInterface*>(track.get()))));
  }
  if (std::strcmp(kind.c_str(),
                  webrtc::MediaStreamTrackInterface::kAudioKind) == 0) {
    return scoped_refptr<RTCMediaTrack>(new RefCountedObject<AudioTrackImpl>(
        webrtc::scoped_refptr<webrtc::AudioTrackInterface>(
            static_cast<webrtc::AudioTrackInterface*>(track.get()))));
  }
  return scoped_refptr<RTCMediaTrack>();
}
scoped_refptr<RTCDtlsTransport> RTCRtpReceiverImpl::dtls_transport() const {
  auto transport = rtp_receiver_->dtls_transport();
  if (nullptr == transport.get()) {
    return scoped_refptr<RTCDtlsTransport>();
  }

  return new RefCountedObject<RTCDtlsTransportImpl>(transport);
}

const vector<string> RTCRtpReceiverImpl::stream_ids() const {
  const auto native_stream_ids = rtp_receiver_->stream_ids();
  std::vector<string> vec;
  vec.reserve(native_stream_ids.size());
  for (const auto& item : native_stream_ids) {
    vec.push_back(item);
  }
  return vec;
}

vector<scoped_refptr<RTCMediaStream>> RTCRtpReceiverImpl::streams() const {
  const auto native_streams = rtp_receiver_->streams();
  std::vector<scoped_refptr<RTCMediaStream>> streams;
  streams.reserve(native_streams.size());
  for (const auto& item : native_streams) {
    streams.push_back(new RefCountedObject<MediaStreamImpl>(item));
  }
  return streams;
}

RTCMediaType RTCRtpReceiverImpl::media_type() const {
  return static_cast<RTCMediaType>(rtp_receiver_->media_type());
}

const string RTCRtpReceiverImpl::id() const { return rtp_receiver_->id(); }
scoped_refptr<RTCRtpParameters> RTCRtpReceiverImpl::parameters() const {
  return new RefCountedObject<RTCRtpParametersImpl>(
      rtp_receiver_->GetParameters());
}
bool RTCRtpReceiverImpl::set_parameters(
    scoped_refptr<RTCRtpParameters> parameters) {
  return rtp_receiver_->SetParameters(
      static_cast<RTCRtpParametersImpl*>(parameters.get())->rtp_parameters());
}
void RTCRtpReceiverImpl::SetObserver(RTCRtpReceiverObserver* observer) {
  observer_ = observer;
  if (nullptr == observer) {
    rtp_receiver_->SetObserver(nullptr);
  } else {
    rtp_receiver_->SetObserver(this);
  }
}

void RTCRtpReceiverImpl::SetJitterBufferMinimumDelay(double delay_seconds) {
  rtp_receiver_->SetJitterBufferMinimumDelay(delay_seconds);
}

}  // namespace lumenrtc_bridge
