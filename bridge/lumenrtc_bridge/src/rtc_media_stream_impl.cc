#include "rtc_media_stream_impl.h"

#include <algorithm>

#include "rtc_audio_track_impl.h"
#include "rtc_peerconnection.h"
#include "rtc_video_track_impl.h"

namespace lumenrtc_bridge {

namespace {

std::vector<scoped_refptr<RTCAudioTrack>> BuildAudioTracks(
    const webrtc::scoped_refptr<webrtc::MediaStreamInterface>& stream) {
  std::vector<scoped_refptr<RTCAudioTrack>> tracks;
  tracks.reserve(stream->GetAudioTracks().size());
  for (const auto& track : stream->GetAudioTracks()) {
    tracks.push_back(scoped_refptr<RTCAudioTrack>(
        new RefCountedObject<AudioTrackImpl>(track)));
  }
  return tracks;
}

std::vector<scoped_refptr<RTCVideoTrack>> BuildVideoTracks(
    const webrtc::scoped_refptr<webrtc::MediaStreamInterface>& stream) {
  std::vector<scoped_refptr<RTCVideoTrack>> tracks;
  tracks.reserve(stream->GetVideoTracks().size());
  for (const auto& track : stream->GetVideoTracks()) {
    tracks.push_back(scoped_refptr<RTCVideoTrack>(
        new RefCountedObject<VideoTrackImpl>(track)));
  }
  return tracks;
}

}  // namespace

MediaStreamImpl::MediaStreamImpl(
    webrtc::scoped_refptr<webrtc::MediaStreamInterface> rtc_media_stream)
    : rtc_media_stream_(rtc_media_stream) {
  rtc_media_stream_->RegisterObserver(this);
  audio_tracks_ = BuildAudioTracks(rtc_media_stream_);
  video_tracks_ = BuildVideoTracks(rtc_media_stream_);
  id_ = rtc_media_stream_->id();
  label_ = rtc_media_stream_->id();
}

MediaStreamImpl::~MediaStreamImpl() {
  RTC_LOG(LS_INFO) << __FUNCTION__ << ": dtor ";
  rtc_media_stream_->UnregisterObserver(this);
  audio_tracks_.clear();
  video_tracks_.clear();
}

bool MediaStreamImpl::AddTrack(scoped_refptr<RTCAudioTrack> track) {
  AudioTrackImpl* track_impl = static_cast<AudioTrackImpl*>(track.get());
  if (rtc_media_stream_->AddTrack(track_impl->rtc_track())) {
    audio_tracks_.push_back(track);
    return true;
  }
  return false;
}

bool MediaStreamImpl::AddTrack(scoped_refptr<RTCVideoTrack> track) {
  VideoTrackImpl* track_impl = static_cast<VideoTrackImpl*>(track.get());
  if (rtc_media_stream_->AddTrack(track_impl->rtc_track())) {
    video_tracks_.push_back(track);
    return true;
  }
  return false;
}

bool MediaStreamImpl::RemoveTrack(scoped_refptr<RTCAudioTrack> track) {
  AudioTrackImpl* track_impl = static_cast<AudioTrackImpl*>(track.get());
  if (rtc_media_stream_->RemoveTrack(track_impl->rtc_track())) {
    auto it = std::find(audio_tracks_.begin(), audio_tracks_.end(), track);
    if (it != audio_tracks_.end()) audio_tracks_.erase(it);
    return true;
  }
  return false;
}

bool MediaStreamImpl::RemoveTrack(scoped_refptr<RTCVideoTrack> track) {
  VideoTrackImpl* track_impl = static_cast<VideoTrackImpl*>(track.get());
  if (rtc_media_stream_->RemoveTrack(track_impl->rtc_track())) {
    auto it = std::find(video_tracks_.begin(), video_tracks_.end(), track);
    if (it != video_tracks_.end()) video_tracks_.erase(it);

    return true;
  }
  return false;
}

vector<scoped_refptr<RTCAudioTrack>> MediaStreamImpl::audio_tracks() {
  return audio_tracks_;
}

vector<scoped_refptr<RTCVideoTrack>> MediaStreamImpl::video_tracks() {
  return video_tracks_;
}

vector<scoped_refptr<RTCMediaTrack>> MediaStreamImpl::tracks() {
  std::vector<scoped_refptr<RTCMediaTrack>> tracks;
  for (auto track : audio_tracks_) {
    tracks.push_back(track);
  }
  for (auto track : video_tracks_) {
    tracks.push_back(track);
  }
  return tracks;
}

scoped_refptr<RTCAudioTrack> MediaStreamImpl::FindAudioTrack(
    const string track_id) {
  for (auto track : audio_tracks_) {
    if (track->id().std_string() == track_id.std_string()) return track;
  }

  return scoped_refptr<RTCAudioTrack>();
}

scoped_refptr<RTCVideoTrack> MediaStreamImpl::FindVideoTrack(
    const string track_id) {
  for (auto track : video_tracks_) {
    if (track->id().std_string() == track_id.std_string()) return track;
  }

  return scoped_refptr<RTCVideoTrack>();
}

void MediaStreamImpl::OnChanged() {
  audio_tracks_ = BuildAudioTracks(rtc_media_stream_);
  video_tracks_ = BuildVideoTracks(rtc_media_stream_);
}

}  // namespace lumenrtc_bridge
