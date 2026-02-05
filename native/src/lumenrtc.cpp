#include "lumenrtc.h"

#include "libwebrtc.h"
#include "rtc_audio_device.h"
#include "rtc_audio_source.h"
#include "rtc_audio_track.h"
#include "rtc_data_channel.h"
#include "rtc_ice_candidate.h"
#include "rtc_media_stream.h"
#include "rtc_media_track.h"
#include "rtc_mediaconstraints.h"
#include "rtc_peerconnection.h"
#include "rtc_peerconnection_factory.h"
#include "rtc_rtp_sender.h"
#include "rtc_rtp_receiver.h"
#include "rtc_rtp_transceiver.h"
#include "rtc_session_description.h"
#include "rtc_video_device.h"
#include "rtc_video_frame.h"
#include "rtc_video_source.h"
#include "rtc_video_track.h"
#include "rtc_video_renderer.h"

#include <algorithm>
#include <cstring>
#include <mutex>
#include <utility>
#include <vector>

using libwebrtc::RTCAudioTrack;
using libwebrtc::RTCAudioDevice;
using libwebrtc::RTCAudioOptions;
using libwebrtc::RTCAudioSource;
using libwebrtc::RTCConfiguration;
using libwebrtc::RTCDataChannel;
using libwebrtc::RTCDataChannelInit;
using libwebrtc::RTCDataChannelObserver;
using libwebrtc::RTCIceCandidate;
using libwebrtc::RTCMediaConstraints;
using libwebrtc::RTCMediaStream;
using libwebrtc::RTCMediaTrack;
using libwebrtc::RTCPeerConnection;
using libwebrtc::RTCPeerConnectionFactory;
using libwebrtc::RTCPeerConnectionObserver;
using libwebrtc::RTCRtpReceiver;
using libwebrtc::RTCRtpTransceiver;
using libwebrtc::RTCVideoFrame;
using libwebrtc::RTCVideoRenderer;
using libwebrtc::RTCVideoSource;
using libwebrtc::RTCVideoTrack;
using libwebrtc::RTCVideoDevice;
using libwebrtc::RTCVideoCapturer;
using libwebrtc::scoped_refptr;
using libwebrtc::string;
using libwebrtc::vector;

struct lrtc_factory_t {
  scoped_refptr<RTCPeerConnectionFactory> ref;
};

struct lrtc_media_constraints_t {
  scoped_refptr<RTCMediaConstraints> ref;
};

struct lrtc_audio_device_t {
  scoped_refptr<RTCAudioDevice> ref;
};

struct lrtc_video_device_t {
  scoped_refptr<RTCVideoDevice> ref;
};

struct lrtc_video_capturer_t {
  scoped_refptr<RTCVideoCapturer> ref;
};

struct lrtc_video_source_t {
  scoped_refptr<RTCVideoSource> ref;
};

struct lrtc_audio_source_t {
  scoped_refptr<RTCAudioSource> ref;
};

struct lrtc_media_stream_t {
  scoped_refptr<RTCMediaStream> ref;
};

struct lrtc_peer_connection_t {
  scoped_refptr<RTCPeerConnection> ref;
  class PeerConnectionObserverImpl* observer = nullptr;
};

struct lrtc_data_channel_t {
  scoped_refptr<RTCDataChannel> ref;
  class DataChannelObserverImpl* observer = nullptr;
};

struct lrtc_video_track_t {
  scoped_refptr<RTCVideoTrack> ref;
};

struct lrtc_audio_track_t {
  scoped_refptr<RTCAudioTrack> ref;
};

struct lrtc_audio_sink_t {
  class AudioSinkImpl* sink = nullptr;
};

struct lrtc_video_sink_t {
  class VideoSinkImpl* renderer = nullptr;
};

struct lrtc_video_frame_t {
  scoped_refptr<RTCVideoFrame> ref;
};

static lrtc_result_t LrtcFailIfNull(const void* ptr) {
  return ptr ? LRTC_OK : LRTC_INVALID_ARG;
}

static vector<string> BuildStringVector(const char** items,
                                        uint32_t count) {
  if (!items || count == 0) {
    return vector<string>();
  }
  std::vector<string> tmp;
  tmp.reserve(count);
  for (uint32_t i = 0; i < count; ++i) {
    if (items[i]) {
      tmp.push_back(string(items[i]));
    }
  }
  return vector<string>(tmp);
}

static void CopyConfig(const lrtc_rtc_config_t* src, RTCConfiguration* dst) {
  if (!dst) {
    return;
  }
  if (!src) {
    return;
  }
  const uint32_t count = std::min<uint32_t>(src->ice_server_count,
                                            LRTC_MAX_ICE_SERVERS);
  for (uint32_t i = 0; i < count; ++i) {
    dst->ice_servers[i].uri = string(
        src->ice_servers[i].uri ? src->ice_servers[i].uri : "");
    dst->ice_servers[i].username = string(
        src->ice_servers[i].username ? src->ice_servers[i].username : "");
    dst->ice_servers[i].password = string(
        src->ice_servers[i].password ? src->ice_servers[i].password : "");
  }
  dst->type = static_cast<libwebrtc::IceTransportsType>(
      src->ice_transports_type);
  dst->bundle_policy = static_cast<libwebrtc::BundlePolicy>(
      src->bundle_policy);
  dst->rtcp_mux_policy = static_cast<libwebrtc::RtcpMuxPolicy>(
      src->rtcp_mux_policy);
  dst->candidate_network_policy = static_cast<libwebrtc::CandidateNetworkPolicy>(
      src->candidate_network_policy);
  dst->tcp_candidate_policy = static_cast<libwebrtc::TcpCandidatePolicy>(
      src->tcp_candidate_policy);
  dst->ice_candidate_pool_size = src->ice_candidate_pool_size;
  dst->srtp_type = static_cast<libwebrtc::MediaSecurityType>(src->srtp_type);
  dst->sdp_semantics = static_cast<libwebrtc::SdpSemantics>(src->sdp_semantics);
  dst->offer_to_receive_audio = src->offer_to_receive_audio;
  dst->offer_to_receive_video = src->offer_to_receive_video;
  dst->disable_ipv6 = src->disable_ipv6;
  dst->disable_ipv6_on_wifi = src->disable_ipv6_on_wifi;
  dst->max_ipv6_networks = src->max_ipv6_networks;
  dst->disable_link_local_networks = src->disable_link_local_networks;
  dst->screencast_min_bitrate = src->screencast_min_bitrate;
  dst->enable_dscp = src->enable_dscp;
  dst->use_rtp_mux = src->use_rtp_mux;
  dst->local_audio_bandwidth = src->local_audio_bandwidth;
  dst->local_video_bandwidth = src->local_video_bandwidth;
}

class PeerConnectionObserverImpl : public RTCPeerConnectionObserver {
 public:
  PeerConnectionObserverImpl() = default;

  void SetCallbacks(const lrtc_peer_connection_callbacks_t* callbacks,
                    void* user_data) {
    std::lock_guard<std::mutex> lock(mutex_);
    if (callbacks) {
      callbacks_ = *callbacks;
    } else {
      std::memset(&callbacks_, 0, sizeof(callbacks_));
    }
    user_data_ = user_data;
  }

  void OnSignalingState(libwebrtc::RTCSignalingState state) override {
    auto cb = GetCallbacks();
    if (cb.callbacks.on_signaling_state) {
      cb.callbacks.on_signaling_state(cb.user_data,
                                      static_cast<int>(state));
    }
  }

  void OnPeerConnectionState(libwebrtc::RTCPeerConnectionState state) override {
    auto cb = GetCallbacks();
    if (cb.callbacks.on_peer_connection_state) {
      cb.callbacks.on_peer_connection_state(cb.user_data,
                                            static_cast<int>(state));
    }
  }

  void OnIceGatheringState(libwebrtc::RTCIceGatheringState state) override {
    auto cb = GetCallbacks();
    if (cb.callbacks.on_ice_gathering_state) {
      cb.callbacks.on_ice_gathering_state(cb.user_data,
                                          static_cast<int>(state));
    }
  }

  void OnIceConnectionState(libwebrtc::RTCIceConnectionState state) override {
    auto cb = GetCallbacks();
    if (cb.callbacks.on_ice_connection_state) {
      cb.callbacks.on_ice_connection_state(cb.user_data,
                                           static_cast<int>(state));
    }
  }

  void OnIceCandidate(scoped_refptr<RTCIceCandidate> candidate) override {
    auto cb = GetCallbacks();
    if (!cb.callbacks.on_ice_candidate || !candidate.get()) {
      return;
    }
    string sdp_mid = candidate->sdp_mid();
    string cand = candidate->candidate();
    cb.callbacks.on_ice_candidate(cb.user_data, sdp_mid.c_string(),
                                  candidate->sdp_mline_index(),
                                  cand.c_string());
  }

  void OnAddStream(scoped_refptr<libwebrtc::RTCMediaStream> stream) override {
    (void)stream;
  }

  void OnRemoveStream(scoped_refptr<libwebrtc::RTCMediaStream> stream) override {
    (void)stream;
  }

  void OnDataChannel(scoped_refptr<RTCDataChannel> data_channel) override {
    auto cb = GetCallbacks();
    if (!cb.callbacks.on_data_channel || !data_channel.get()) {
      return;
    }
    auto handle = new lrtc_data_channel_t();
    handle->ref = data_channel;
    cb.callbacks.on_data_channel(cb.user_data, handle);
  }

  void OnRenegotiationNeeded() override {
    auto cb = GetCallbacks();
    if (cb.callbacks.on_renegotiation_needed) {
      cb.callbacks.on_renegotiation_needed(cb.user_data);
    }
  }

  void OnTrack(scoped_refptr<RTCRtpTransceiver> transceiver) override {
    auto cb = GetCallbacks();
    if (!transceiver.get()) {
      return;
    }
    scoped_refptr<RTCRtpReceiver> receiver = transceiver->receiver();
    if (!receiver.get()) {
      return;
    }
    scoped_refptr<RTCMediaTrack> track = receiver->track();
    if (!track.get()) {
      return;
    }
    string kind = track->kind();
    if (cb.callbacks.on_video_track &&
        std::strcmp(kind.c_string(), "video") == 0) {
      auto handle = new lrtc_video_track_t();
      handle->ref = static_cast<RTCVideoTrack*>(track.get());
      cb.callbacks.on_video_track(cb.user_data, handle);
      return;
    }
    if (cb.callbacks.on_audio_track &&
        std::strcmp(kind.c_string(), "audio") == 0) {
      auto handle = new lrtc_audio_track_t();
      handle->ref = static_cast<RTCAudioTrack*>(track.get());
      cb.callbacks.on_audio_track(cb.user_data, handle);
    }
  }

  void OnAddTrack(vector<scoped_refptr<libwebrtc::RTCMediaStream>> streams,
                  scoped_refptr<libwebrtc::RTCRtpReceiver> receiver) override {
    (void)streams;
    (void)receiver;
  }

  void OnRemoveTrack(scoped_refptr<libwebrtc::RTCRtpReceiver> receiver) override {
    (void)receiver;
  }

 private:
  struct CallbackSnapshot {
    lrtc_peer_connection_callbacks_t callbacks;
    void* user_data;
  };

  CallbackSnapshot GetCallbacks() {
    std::lock_guard<std::mutex> lock(mutex_);
    return {callbacks_, user_data_};
  }

  std::mutex mutex_;
  lrtc_peer_connection_callbacks_t callbacks_{};
  void* user_data_ = nullptr;
};

class DataChannelObserverImpl : public RTCDataChannelObserver {
 public:
  DataChannelObserverImpl() = default;

  void SetCallbacks(const lrtc_data_channel_callbacks_t* callbacks,
                    void* user_data) {
    std::lock_guard<std::mutex> lock(mutex_);
    if (callbacks) {
      callbacks_ = *callbacks;
    } else {
      std::memset(&callbacks_, 0, sizeof(callbacks_));
    }
    user_data_ = user_data;
  }

  void OnStateChange(libwebrtc::RTCDataChannelState state) override {
    auto cb = GetCallbacks();
    if (cb.callbacks.on_state_change) {
      cb.callbacks.on_state_change(cb.user_data, static_cast<int>(state));
    }
  }

  void OnMessage(const char* buffer, int length, bool binary) override {
    auto cb = GetCallbacks();
    if (cb.callbacks.on_message) {
      cb.callbacks.on_message(cb.user_data,
                              reinterpret_cast<const uint8_t*>(buffer),
                              length, binary ? 1 : 0);
    }
  }

 private:
  struct CallbackSnapshot {
    lrtc_data_channel_callbacks_t callbacks;
    void* user_data;
  };

  CallbackSnapshot GetCallbacks() {
    std::lock_guard<std::mutex> lock(mutex_);
    return {callbacks_, user_data_};
  }

  std::mutex mutex_;
  lrtc_data_channel_callbacks_t callbacks_{};
  void* user_data_ = nullptr;
};

class AudioSinkImpl : public libwebrtc::AudioTrackSink {
 public:
  AudioSinkImpl() = default;

  void SetCallbacks(const lrtc_audio_sink_callbacks_t* callbacks,
                    void* user_data) {
    std::lock_guard<std::mutex> lock(mutex_);
    if (callbacks) {
      callbacks_ = *callbacks;
    } else {
      std::memset(&callbacks_, 0, sizeof(callbacks_));
    }
    user_data_ = user_data;
  }

  void OnData(const void* audio_data, int bits_per_sample, int sample_rate,
              size_t number_of_channels, size_t number_of_frames) override {
    lrtc_audio_sink_callbacks_t callbacks;
    void* user_data = nullptr;
    {
      std::lock_guard<std::mutex> lock(mutex_);
      callbacks = callbacks_;
      user_data = user_data_;
    }
    if (!callbacks.on_data) {
      return;
    }
    callbacks.on_data(user_data, audio_data, bits_per_sample, sample_rate,
                      number_of_channels, number_of_frames);
  }

 private:
  std::mutex mutex_;
  lrtc_audio_sink_callbacks_t callbacks_{};
  void* user_data_ = nullptr;
};

class VideoSinkImpl : public RTCVideoRenderer<scoped_refptr<RTCVideoFrame>> {
 public:
  VideoSinkImpl() = default;

  void SetCallbacks(const lrtc_video_sink_callbacks_t* callbacks,
                    void* user_data) {
    std::lock_guard<std::mutex> lock(mutex_);
    if (callbacks) {
      callbacks_ = *callbacks;
    } else {
      std::memset(&callbacks_, 0, sizeof(callbacks_));
    }
    user_data_ = user_data;
  }

  void OnFrame(scoped_refptr<RTCVideoFrame> frame) override {
    lrtc_video_sink_callbacks_t callbacks;
    void* user_data = nullptr;
    {
      std::lock_guard<std::mutex> lock(mutex_);
      callbacks = callbacks_;
      user_data = user_data_;
    }
    if (!callbacks.on_frame || !frame.get()) {
      return;
    }
    auto handle = new lrtc_video_frame_t();
    handle->ref = frame;
    callbacks.on_frame(user_data, handle);
  }

 private:
  std::mutex mutex_;
  lrtc_video_sink_callbacks_t callbacks_{};
  void* user_data_ = nullptr;
};

extern "C" {

lrtc_result_t LUMENRTC_CALL lrtc_initialize(void) {
  return libwebrtc::LibWebRTC::Initialize() ? LRTC_OK : LRTC_ERROR;
}

void LUMENRTC_CALL lrtc_terminate(void) {
  libwebrtc::LibWebRTC::Terminate();
}

lrtc_factory_t* LUMENRTC_CALL lrtc_factory_create(void) {
  auto handle = new lrtc_factory_t();
  handle->ref = libwebrtc::LibWebRTC::CreateRTCPeerConnectionFactory();
  if (!handle->ref.get()) {
    delete handle;
    return nullptr;
  }
  return handle;
}

lrtc_result_t LUMENRTC_CALL lrtc_factory_initialize(lrtc_factory_t* factory) {
  if (LrtcFailIfNull(factory) != LRTC_OK) {
    return LRTC_INVALID_ARG;
  }
  return factory->ref->Initialize() ? LRTC_OK : LRTC_ERROR;
}

void LUMENRTC_CALL lrtc_factory_terminate(lrtc_factory_t* factory) {
  if (!factory) {
    return;
  }
  factory->ref->Terminate();
}

void LUMENRTC_CALL lrtc_factory_release(lrtc_factory_t* factory) {
  delete factory;
}

lrtc_audio_device_t* LUMENRTC_CALL lrtc_factory_get_audio_device(
    lrtc_factory_t* factory) {
  if (!factory || !factory->ref.get()) {
    return nullptr;
  }
  auto handle = new lrtc_audio_device_t();
  handle->ref = factory->ref->GetAudioDevice();
  if (!handle->ref.get()) {
    delete handle;
    return nullptr;
  }
  return handle;
}

lrtc_video_device_t* LUMENRTC_CALL lrtc_factory_get_video_device(
    lrtc_factory_t* factory) {
  if (!factory || !factory->ref.get()) {
    return nullptr;
  }
  auto handle = new lrtc_video_device_t();
  handle->ref = factory->ref->GetVideoDevice();
  if (!handle->ref.get()) {
    delete handle;
    return nullptr;
  }
  return handle;
}

lrtc_audio_source_t* LUMENRTC_CALL lrtc_factory_create_audio_source(
    lrtc_factory_t* factory, const char* label,
    lrtc_audio_source_type source_type, const lrtc_audio_options_t* options) {
  if (!factory || !factory->ref.get() || !label) {
    return nullptr;
  }
  RTCAudioOptions rtc_options;
  if (options) {
    rtc_options.echo_cancellation = options->echo_cancellation;
    rtc_options.auto_gain_control = options->auto_gain_control;
    rtc_options.noise_suppression = options->noise_suppression;
    rtc_options.highpass_filter = options->highpass_filter;
  }
  scoped_refptr<RTCAudioSource> source =
      factory->ref->CreateAudioSource(
          string(label),
          static_cast<RTCAudioSource::SourceType>(source_type),
          rtc_options);
  if (!source.get()) {
    return nullptr;
  }
  auto handle = new lrtc_audio_source_t();
  handle->ref = source;
  return handle;
}

lrtc_video_source_t* LUMENRTC_CALL lrtc_factory_create_video_source(
    lrtc_factory_t* factory, lrtc_video_capturer_t* capturer,
    const char* label, lrtc_media_constraints_t* constraints) {
  if (!factory || !factory->ref.get() || !capturer || !capturer->ref.get() ||
      !label) {
    return nullptr;
  }
  scoped_refptr<RTCMediaConstraints> mc;
  if (constraints) {
    mc = constraints->ref;
  }
  scoped_refptr<RTCVideoSource> source =
      factory->ref->CreateVideoSource(capturer->ref, string(label), mc);
  if (!source.get()) {
    return nullptr;
  }
  auto handle = new lrtc_video_source_t();
  handle->ref = source;
  return handle;
}

lrtc_audio_track_t* LUMENRTC_CALL lrtc_factory_create_audio_track(
    lrtc_factory_t* factory, lrtc_audio_source_t* source,
    const char* track_id) {
  if (!factory || !factory->ref.get() || !source || !source->ref.get() ||
      !track_id) {
    return nullptr;
  }
  scoped_refptr<RTCAudioTrack> track =
      factory->ref->CreateAudioTrack(source->ref, string(track_id));
  if (!track.get()) {
    return nullptr;
  }
  auto handle = new lrtc_audio_track_t();
  handle->ref = track;
  return handle;
}

lrtc_video_track_t* LUMENRTC_CALL lrtc_factory_create_video_track(
    lrtc_factory_t* factory, lrtc_video_source_t* source,
    const char* track_id) {
  if (!factory || !factory->ref.get() || !source || !source->ref.get() ||
      !track_id) {
    return nullptr;
  }
  scoped_refptr<RTCVideoTrack> track =
      factory->ref->CreateVideoTrack(source->ref, string(track_id));
  if (!track.get()) {
    return nullptr;
  }
  auto handle = new lrtc_video_track_t();
  handle->ref = track;
  return handle;
}

lrtc_media_stream_t* LUMENRTC_CALL lrtc_factory_create_stream(
    lrtc_factory_t* factory, const char* stream_id) {
  if (!factory || !factory->ref.get() || !stream_id) {
    return nullptr;
  }
  scoped_refptr<RTCMediaStream> stream =
      factory->ref->CreateStream(string(stream_id));
  if (!stream.get()) {
    return nullptr;
  }
  auto handle = new lrtc_media_stream_t();
  handle->ref = stream;
  return handle;
}

lrtc_media_constraints_t* LUMENRTC_CALL lrtc_media_constraints_create(void) {
  auto handle = new lrtc_media_constraints_t();
  handle->ref = RTCMediaConstraints::Create();
  if (!handle->ref.get()) {
    delete handle;
    return nullptr;
  }
  return handle;
}

void LUMENRTC_CALL lrtc_media_constraints_add_mandatory(
    lrtc_media_constraints_t* constraints, const char* key,
    const char* value) {
  if (!constraints || !constraints->ref.get() || !key || !value) {
    return;
  }
  constraints->ref->AddMandatoryConstraint(string(key), string(value));
}

void LUMENRTC_CALL lrtc_media_constraints_add_optional(
    lrtc_media_constraints_t* constraints, const char* key,
    const char* value) {
  if (!constraints || !constraints->ref.get() || !key || !value) {
    return;
  }
  constraints->ref->AddOptionalConstraint(string(key), string(value));
}

void LUMENRTC_CALL lrtc_media_constraints_release(
    lrtc_media_constraints_t* constraints) {
  delete constraints;
}

int16_t LUMENRTC_CALL lrtc_audio_device_playout_devices(
    lrtc_audio_device_t* device) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->PlayoutDevices();
}

int16_t LUMENRTC_CALL lrtc_audio_device_recording_devices(
    lrtc_audio_device_t* device) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->RecordingDevices();
}

int32_t LUMENRTC_CALL lrtc_audio_device_playout_device_name(
    lrtc_audio_device_t* device, uint16_t index, char* name,
    uint32_t name_len, char* guid, uint32_t guid_len) {
  if (!device || !device->ref.get() || !name || !guid) {
    return -1;
  }
  if (name_len < RTCAudioDevice::kAdmMaxDeviceNameSize ||
      guid_len < RTCAudioDevice::kAdmMaxGuidSize) {
    return -1;
  }
  return device->ref->PlayoutDeviceName(
      index, name, guid);
}

int32_t LUMENRTC_CALL lrtc_audio_device_recording_device_name(
    lrtc_audio_device_t* device, uint16_t index, char* name,
    uint32_t name_len, char* guid, uint32_t guid_len) {
  if (!device || !device->ref.get() || !name || !guid) {
    return -1;
  }
  if (name_len < RTCAudioDevice::kAdmMaxDeviceNameSize ||
      guid_len < RTCAudioDevice::kAdmMaxGuidSize) {
    return -1;
  }
  return device->ref->RecordingDeviceName(
      index, name, guid);
}

int32_t LUMENRTC_CALL lrtc_audio_device_set_playout_device(
    lrtc_audio_device_t* device, uint16_t index) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->SetPlayoutDevice(index);
}

int32_t LUMENRTC_CALL lrtc_audio_device_set_recording_device(
    lrtc_audio_device_t* device, uint16_t index) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->SetRecordingDevice(index);
}

int32_t LUMENRTC_CALL lrtc_audio_device_set_microphone_volume(
    lrtc_audio_device_t* device, uint32_t volume) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->SetMicrophoneVolume(volume);
}

int32_t LUMENRTC_CALL lrtc_audio_device_microphone_volume(
    lrtc_audio_device_t* device, uint32_t* volume) {
  if (!device || !device->ref.get() || !volume) {
    return -1;
  }
  uint32_t out = 0;
  int32_t result = device->ref->MicrophoneVolume(out);
  if (result == 0) {
    *volume = out;
  }
  return result;
}

int32_t LUMENRTC_CALL lrtc_audio_device_set_speaker_volume(
    lrtc_audio_device_t* device, uint32_t volume) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->SetSpeakerVolume(volume);
}

int32_t LUMENRTC_CALL lrtc_audio_device_speaker_volume(
    lrtc_audio_device_t* device, uint32_t* volume) {
  if (!device || !device->ref.get() || !volume) {
    return -1;
  }
  uint32_t out = 0;
  int32_t result = device->ref->SpeakerVolume(out);
  if (result == 0) {
    *volume = out;
  }
  return result;
}

void LUMENRTC_CALL lrtc_audio_device_release(lrtc_audio_device_t* device) {
  delete device;
}

uint32_t LUMENRTC_CALL lrtc_video_device_number_of_devices(
    lrtc_video_device_t* device) {
  if (!device || !device->ref.get()) {
    return 0;
  }
  return device->ref->NumberOfDevices();
}

int32_t LUMENRTC_CALL lrtc_video_device_get_device_name(
    lrtc_video_device_t* device, uint32_t index, char* name,
    uint32_t name_length, char* unique_id, uint32_t unique_id_length) {
  if (!device || !device->ref.get() || !name || !unique_id) {
    return -1;
  }
  return device->ref->GetDeviceName(index, name, name_length, unique_id,
                                    unique_id_length);
}

lrtc_video_capturer_t* LUMENRTC_CALL lrtc_video_device_create_capturer(
    lrtc_video_device_t* device, const char* name, uint32_t index, size_t width,
    size_t height, size_t target_fps) {
  if (!device || !device->ref.get() || !name) {
    return nullptr;
  }
  scoped_refptr<RTCVideoCapturer> capturer =
      device->ref->Create(name, index, width, height, target_fps);
  if (!capturer.get()) {
    return nullptr;
  }
  auto handle = new lrtc_video_capturer_t();
  handle->ref = capturer;
  return handle;
}

void LUMENRTC_CALL lrtc_video_device_release(lrtc_video_device_t* device) {
  delete device;
}

bool LUMENRTC_CALL lrtc_video_capturer_start(
    lrtc_video_capturer_t* capturer) {
  if (!capturer || !capturer->ref.get()) {
    return false;
  }
  return capturer->ref->StartCapture();
}

bool LUMENRTC_CALL lrtc_video_capturer_capture_started(
    lrtc_video_capturer_t* capturer) {
  if (!capturer || !capturer->ref.get()) {
    return false;
  }
  return capturer->ref->CaptureStarted();
}

void LUMENRTC_CALL lrtc_video_capturer_stop(
    lrtc_video_capturer_t* capturer) {
  if (!capturer || !capturer->ref.get()) {
    return;
  }
  capturer->ref->StopCapture();
}

void LUMENRTC_CALL lrtc_video_capturer_release(
    lrtc_video_capturer_t* capturer) {
  delete capturer;
}

void LUMENRTC_CALL lrtc_audio_source_capture_frame(
    lrtc_audio_source_t* source, const void* audio_data, int bits_per_sample,
    int sample_rate, size_t number_of_channels, size_t number_of_frames) {
  if (!source || !source->ref.get() || !audio_data) {
    return;
  }
  source->ref->CaptureFrame(audio_data, bits_per_sample, sample_rate,
                            number_of_channels, number_of_frames);
}

void LUMENRTC_CALL lrtc_audio_source_release(lrtc_audio_source_t* source) {
  delete source;
}

void LUMENRTC_CALL lrtc_video_source_release(lrtc_video_source_t* source) {
  delete source;
}

void LUMENRTC_CALL lrtc_audio_track_set_volume(lrtc_audio_track_t* track,
                                               double volume) {
  if (!track || !track->ref.get()) {
    return;
  }
  track->ref->SetVolume(volume);
}

void LUMENRTC_CALL lrtc_audio_track_add_sink(lrtc_audio_track_t* track,
                                             lrtc_audio_sink_t* sink) {
  if (!track || !track->ref.get() || !sink || !sink->sink) {
    return;
  }
  track->ref->AddSink(sink->sink);
}

void LUMENRTC_CALL lrtc_audio_track_remove_sink(lrtc_audio_track_t* track,
                                                lrtc_audio_sink_t* sink) {
  if (!track || !track->ref.get() || !sink || !sink->sink) {
    return;
  }
  track->ref->RemoveSink(sink->sink);
}

void LUMENRTC_CALL lrtc_audio_track_release(lrtc_audio_track_t* track) {
  delete track;
}

lrtc_audio_sink_t* LUMENRTC_CALL lrtc_audio_sink_create(
    const lrtc_audio_sink_callbacks_t* callbacks, void* user_data) {
  auto handle = new lrtc_audio_sink_t();
  auto* sink = new AudioSinkImpl();
  sink->SetCallbacks(callbacks, user_data);
  handle->sink = sink;
  return handle;
}

void LUMENRTC_CALL lrtc_audio_sink_release(lrtc_audio_sink_t* sink) {
  if (!sink) {
    return;
  }
  delete sink->sink;
  sink->sink = nullptr;
  delete sink;
}

bool LUMENRTC_CALL lrtc_media_stream_add_audio_track(
    lrtc_media_stream_t* stream, lrtc_audio_track_t* track) {
  if (!stream || !stream->ref.get() || !track || !track->ref.get()) {
    return false;
  }
  return stream->ref->AddTrack(track->ref);
}

bool LUMENRTC_CALL lrtc_media_stream_add_video_track(
    lrtc_media_stream_t* stream, lrtc_video_track_t* track) {
  if (!stream || !stream->ref.get() || !track || !track->ref.get()) {
    return false;
  }
  return stream->ref->AddTrack(track->ref);
}

bool LUMENRTC_CALL lrtc_media_stream_remove_audio_track(
    lrtc_media_stream_t* stream, lrtc_audio_track_t* track) {
  if (!stream || !stream->ref.get() || !track || !track->ref.get()) {
    return false;
  }
  return stream->ref->RemoveTrack(track->ref);
}

bool LUMENRTC_CALL lrtc_media_stream_remove_video_track(
    lrtc_media_stream_t* stream, lrtc_video_track_t* track) {
  if (!stream || !stream->ref.get() || !track || !track->ref.get()) {
    return false;
  }
  return stream->ref->RemoveTrack(track->ref);
}

void LUMENRTC_CALL lrtc_media_stream_release(lrtc_media_stream_t* stream) {
  delete stream;
}

lrtc_peer_connection_t* LUMENRTC_CALL lrtc_peer_connection_create(
    lrtc_factory_t* factory, const lrtc_rtc_config_t* config,
    lrtc_media_constraints_t* constraints,
    const lrtc_peer_connection_callbacks_t* callbacks, void* user_data) {
  if (!factory || !factory->ref.get()) {
    return nullptr;
  }
  RTCConfiguration cfg;
  if (config) {
    CopyConfig(config, &cfg);
  }
  scoped_refptr<RTCMediaConstraints> mc;
  if (constraints) {
    mc = constraints->ref;
  }
  scoped_refptr<RTCPeerConnection> pc = factory->ref->Create(cfg, mc);
  if (!pc.get()) {
    return nullptr;
  }
  auto handle = new lrtc_peer_connection_t();
  handle->ref = pc;
  auto* observer = new PeerConnectionObserverImpl();
  observer->SetCallbacks(callbacks, user_data);
  pc->RegisterRTCPeerConnectionObserver(observer);
  handle->observer = observer;
  return handle;
}

void LUMENRTC_CALL lrtc_peer_connection_set_callbacks(
    lrtc_peer_connection_t* pc,
    const lrtc_peer_connection_callbacks_t* callbacks, void* user_data) {
  if (!pc || !pc->observer) {
    return;
  }
  pc->observer->SetCallbacks(callbacks, user_data);
}

void LUMENRTC_CALL lrtc_peer_connection_close(lrtc_peer_connection_t* pc) {
  if (!pc || !pc->ref.get()) {
    return;
  }
  pc->ref->Close();
}

void LUMENRTC_CALL lrtc_peer_connection_release(lrtc_peer_connection_t* pc) {
  if (!pc) {
    return;
  }
  if (pc->ref.get()) {
    pc->ref->DeRegisterRTCPeerConnectionObserver();
  }
  delete pc->observer;
  pc->observer = nullptr;
  delete pc;
}

void LUMENRTC_CALL lrtc_peer_connection_create_offer(
    lrtc_peer_connection_t* pc, lrtc_sdp_success_cb success,
    lrtc_sdp_error_cb failure, void* user_data,
    lrtc_media_constraints_t* constraints) {
  if (!pc || !pc->ref.get()) {
    return;
  }
  scoped_refptr<RTCMediaConstraints> mc;
  if (constraints) {
    mc = constraints->ref;
  }
  pc->ref->CreateOffer(
      [success, user_data](const string sdp, const string type) {
        if (success) {
          success(user_data, sdp.c_string(), type.c_string());
        }
      },
      [failure, user_data](const char* error) {
        if (failure) {
          failure(user_data, error);
        }
      },
      mc);
}

void LUMENRTC_CALL lrtc_peer_connection_create_answer(
    lrtc_peer_connection_t* pc, lrtc_sdp_success_cb success,
    lrtc_sdp_error_cb failure, void* user_data,
    lrtc_media_constraints_t* constraints) {
  if (!pc || !pc->ref.get()) {
    return;
  }
  scoped_refptr<RTCMediaConstraints> mc;
  if (constraints) {
    mc = constraints->ref;
  }
  pc->ref->CreateAnswer(
      [success, user_data](const string sdp, const string type) {
        if (success) {
          success(user_data, sdp.c_string(), type.c_string());
        }
      },
      [failure, user_data](const char* error) {
        if (failure) {
          failure(user_data, error);
        }
      },
      mc);
}

void LUMENRTC_CALL lrtc_peer_connection_set_local_description(
    lrtc_peer_connection_t* pc, const char* sdp, const char* type,
    lrtc_void_cb success, lrtc_sdp_error_cb failure, void* user_data) {
  if (!pc || !pc->ref.get() || !sdp || !type) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  pc->ref->SetLocalDescription(
      string(sdp), string(type),
      [success, user_data]() {
        if (success) {
          success(user_data);
        }
      },
      [failure, user_data](const char* error) {
        if (failure) {
          failure(user_data, error);
        }
      });
}

void LUMENRTC_CALL lrtc_peer_connection_set_remote_description(
    lrtc_peer_connection_t* pc, const char* sdp, const char* type,
    lrtc_void_cb success, lrtc_sdp_error_cb failure, void* user_data) {
  if (!pc || !pc->ref.get() || !sdp || !type) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  pc->ref->SetRemoteDescription(
      string(sdp), string(type),
      [success, user_data]() {
        if (success) {
          success(user_data);
        }
      },
      [failure, user_data](const char* error) {
        if (failure) {
          failure(user_data, error);
        }
      });
}

void LUMENRTC_CALL lrtc_peer_connection_get_local_description(
    lrtc_peer_connection_t* pc, lrtc_sdp_success_cb success,
    lrtc_sdp_error_cb failure, void* user_data) {
  if (!pc || !pc->ref.get()) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  pc->ref->GetLocalDescription(
      [success, user_data](const char* sdp, const char* type) {
        if (success) {
          success(user_data, sdp, type);
        }
      },
      [failure, user_data](const char* error) {
        if (failure) {
          failure(user_data, error);
        }
      });
}

void LUMENRTC_CALL lrtc_peer_connection_get_remote_description(
    lrtc_peer_connection_t* pc, lrtc_sdp_success_cb success,
    lrtc_sdp_error_cb failure, void* user_data) {
  if (!pc || !pc->ref.get()) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  pc->ref->GetRemoteDescription(
      [success, user_data](const char* sdp, const char* type) {
        if (success) {
          success(user_data, sdp, type);
        }
      },
      [failure, user_data](const char* error) {
        if (failure) {
          failure(user_data, error);
        }
      });
}

void LUMENRTC_CALL lrtc_peer_connection_add_ice_candidate(
    lrtc_peer_connection_t* pc, const char* sdp_mid, int sdp_mline_index,
    const char* candidate) {
  if (!pc || !pc->ref.get() || !sdp_mid || !candidate) {
    return;
  }
  pc->ref->AddCandidate(string(sdp_mid), sdp_mline_index, string(candidate));
}

bool LUMENRTC_CALL lrtc_peer_connection_add_stream(
    lrtc_peer_connection_t* pc, lrtc_media_stream_t* stream) {
  if (!pc || !pc->ref.get() || !stream || !stream->ref.get()) {
    return false;
  }
  return pc->ref->AddStream(stream->ref) == 0;
}

bool LUMENRTC_CALL lrtc_peer_connection_remove_stream(
    lrtc_peer_connection_t* pc, lrtc_media_stream_t* stream) {
  if (!pc || !pc->ref.get() || !stream || !stream->ref.get()) {
    return false;
  }
  return pc->ref->RemoveStream(stream->ref) == 0;
}

int LUMENRTC_CALL lrtc_peer_connection_add_audio_track(
    lrtc_peer_connection_t* pc, lrtc_audio_track_t* track,
    const char** stream_ids, uint32_t stream_id_count) {
  if (!pc || !pc->ref.get() || !track || !track->ref.get()) {
    return 0;
  }
  vector<string> streams = BuildStringVector(stream_ids, stream_id_count);
  scoped_refptr<libwebrtc::RTCRtpSender> sender =
      pc->ref->AddTrack(track->ref, streams);
  return sender.get() ? 1 : 0;
}

int LUMENRTC_CALL lrtc_peer_connection_add_video_track(
    lrtc_peer_connection_t* pc, lrtc_video_track_t* track,
    const char** stream_ids, uint32_t stream_id_count) {
  if (!pc || !pc->ref.get() || !track || !track->ref.get()) {
    return 0;
  }
  vector<string> streams = BuildStringVector(stream_ids, stream_id_count);
  scoped_refptr<libwebrtc::RTCRtpSender> sender =
      pc->ref->AddTrack(track->ref, streams);
  return sender.get() ? 1 : 0;
}

lrtc_data_channel_t* LUMENRTC_CALL lrtc_peer_connection_create_data_channel(
    lrtc_peer_connection_t* pc, const char* label, int ordered, int reliable,
    int max_retransmit_time, int max_retransmits, const char* protocol,
    int negotiated, int id) {
  if (!pc || !pc->ref.get() || !label) {
    return nullptr;
  }
  RTCDataChannelInit init;
  init.ordered = ordered != 0;
  init.reliable = reliable != 0;
  init.maxRetransmitTime = max_retransmit_time;
  init.maxRetransmits = max_retransmits;
  if (protocol) {
    init.protocol = string(protocol);
  }
  init.negotiated = negotiated != 0;
  init.id = id;

  scoped_refptr<RTCDataChannel> channel =
      pc->ref->CreateDataChannel(string(label), &init);
  if (!channel.get()) {
    return nullptr;
  }
  auto handle = new lrtc_data_channel_t();
  handle->ref = channel;
  return handle;
}

void LUMENRTC_CALL lrtc_data_channel_set_callbacks(
    lrtc_data_channel_t* channel, const lrtc_data_channel_callbacks_t* callbacks,
    void* user_data) {
  if (!channel || !channel->ref.get()) {
    return;
  }
  if (!channel->observer) {
    channel->observer = new DataChannelObserverImpl();
    channel->ref->RegisterObserver(channel->observer);
  }
  channel->observer->SetCallbacks(callbacks, user_data);
}

void LUMENRTC_CALL lrtc_data_channel_send(lrtc_data_channel_t* channel,
                                         const uint8_t* data, uint32_t size,
                                         int binary) {
  if (!channel || !channel->ref.get() || (!data && size > 0)) {
    return;
  }
  channel->ref->Send(data, size, binary != 0);
}

void LUMENRTC_CALL lrtc_data_channel_close(lrtc_data_channel_t* channel) {
  if (!channel || !channel->ref.get()) {
    return;
  }
  channel->ref->Close();
}

void LUMENRTC_CALL lrtc_data_channel_release(lrtc_data_channel_t* channel) {
  if (!channel) {
    return;
  }
  if (channel->ref.get()) {
    channel->ref->UnregisterObserver();
  }
  delete channel->observer;
  channel->observer = nullptr;
  delete channel;
}

lrtc_video_sink_t* LUMENRTC_CALL lrtc_video_sink_create(
    const lrtc_video_sink_callbacks_t* callbacks, void* user_data) {
  auto handle = new lrtc_video_sink_t();
  auto* renderer = new VideoSinkImpl();
  renderer->SetCallbacks(callbacks, user_data);
  handle->renderer = renderer;
  return handle;
}

void LUMENRTC_CALL lrtc_video_sink_release(lrtc_video_sink_t* sink) {
  if (!sink) {
    return;
  }
  delete sink->renderer;
  sink->renderer = nullptr;
  delete sink;
}

void LUMENRTC_CALL lrtc_video_track_add_sink(lrtc_video_track_t* track,
                                             lrtc_video_sink_t* sink) {
  if (!track || !track->ref.get() || !sink || !sink->renderer) {
    return;
  }
  track->ref->AddRenderer(sink->renderer);
}

void LUMENRTC_CALL lrtc_video_track_remove_sink(lrtc_video_track_t* track,
                                                lrtc_video_sink_t* sink) {
  if (!track || !track->ref.get() || !sink || !sink->renderer) {
    return;
  }
  track->ref->RemoveRenderer(sink->renderer);
}

void LUMENRTC_CALL lrtc_video_track_release(lrtc_video_track_t* track) {
  delete track;
}

int LUMENRTC_CALL lrtc_video_frame_width(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->width();
}

int LUMENRTC_CALL lrtc_video_frame_height(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->height();
}

int LUMENRTC_CALL lrtc_video_frame_stride_y(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->StrideY();
}

int LUMENRTC_CALL lrtc_video_frame_stride_u(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->StrideU();
}

int LUMENRTC_CALL lrtc_video_frame_stride_v(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->StrideV();
}

const uint8_t* LUMENRTC_CALL lrtc_video_frame_data_y(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return nullptr;
  }
  return frame->ref->DataY();
}

const uint8_t* LUMENRTC_CALL lrtc_video_frame_data_u(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return nullptr;
  }
  return frame->ref->DataU();
}

const uint8_t* LUMENRTC_CALL lrtc_video_frame_data_v(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return nullptr;
  }
  return frame->ref->DataV();
}

int LUMENRTC_CALL lrtc_video_frame_copy_i420(
    lrtc_video_frame_t* frame, uint8_t* dst_y, int dst_stride_y,
    uint8_t* dst_u, int dst_stride_u, uint8_t* dst_v, int dst_stride_v) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  const uint8_t* src_y = frame->ref->DataY();
  const uint8_t* src_u = frame->ref->DataU();
  const uint8_t* src_v = frame->ref->DataV();
  if (!src_y || !src_u || !src_v || !dst_y || !dst_u || !dst_v) {
    return 0;
  }
  const int width = frame->ref->width();
  const int height = frame->ref->height();
  const int src_stride_y = frame->ref->StrideY();
  const int src_stride_u = frame->ref->StrideU();
  const int src_stride_v = frame->ref->StrideV();

  for (int y = 0; y < height; ++y) {
    std::memcpy(dst_y + y * dst_stride_y,
                src_y + y * src_stride_y,
                static_cast<size_t>(width));
  }

  const int chroma_width = (width + 1) / 2;
  const int chroma_height = (height + 1) / 2;

  for (int y = 0; y < chroma_height; ++y) {
    std::memcpy(dst_u + y * dst_stride_u,
                src_u + y * src_stride_u,
                static_cast<size_t>(chroma_width));
    std::memcpy(dst_v + y * dst_stride_v,
                src_v + y * src_stride_v,
                static_cast<size_t>(chroma_width));
  }
  return 1;
}

int LUMENRTC_CALL lrtc_video_frame_to_argb(
    lrtc_video_frame_t* frame, uint8_t* dst_argb, int dst_stride_argb,
    int dest_width, int dest_height, int format) {
  if (!frame || !frame->ref.get() || !dst_argb) {
    return 0;
  }
  return frame->ref->ConvertToARGB(
      static_cast<RTCVideoFrame::Type>(format), dst_argb, dst_stride_argb,
      dest_width, dest_height);
}

lrtc_video_frame_t* LUMENRTC_CALL lrtc_video_frame_retain(
    lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return nullptr;
  }
  auto handle = new lrtc_video_frame_t();
  handle->ref = frame->ref;
  return handle;
}

void LUMENRTC_CALL lrtc_video_frame_release(lrtc_video_frame_t* frame) {
  delete frame;
}

}  // extern "C"
