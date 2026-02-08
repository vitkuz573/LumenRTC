#include "lumenrtc.h"

#include "libwebrtc.h"
#include "rtc_audio_device.h"
#include "rtc_audio_source.h"
#include "rtc_audio_track.h"
#include "rtc_data_channel.h"
#include "rtc_dtls_transport.h"
#include "rtc_dtmf_sender.h"
#include "rtc_desktop_capturer.h"
#include "rtc_desktop_device.h"
#include "rtc_desktop_media_list.h"
#include "rtc_ice_candidate.h"
#include "rtc_logging.h"
#include "rtc_media_stream.h"
#include "rtc_media_track.h"
#include "rtc_mediaconstraints.h"
#include "rtc_peerconnection.h"
#include "rtc_peerconnection_factory.h"
#include "rtc_rtp_capabilities.h"
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
#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>
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
using libwebrtc::RTCDtmfSender;
using libwebrtc::RTCDtmfSenderObserver;
using libwebrtc::RTCDesktopCapturer;
using libwebrtc::RTCDesktopDevice;
using libwebrtc::RTCDesktopMediaList;
using libwebrtc::RTCIceCandidate;
using libwebrtc::LibWebRTCLogging;
using libwebrtc::RTCLoggingSeverity;
using libwebrtc::RTCMediaConstraints;
using libwebrtc::RTCMediaStream;
using libwebrtc::RTCMediaTrack;
using libwebrtc::MediaRTCStats;
using libwebrtc::RTCPeerConnection;
using libwebrtc::RTCPeerConnectionFactory;
using libwebrtc::RTCPeerConnectionObserver;
using libwebrtc::RTCRtpCapabilities;
using libwebrtc::RTCRtpCodecCapability;
using libwebrtc::RTCRtpHeaderExtensionCapability;
using libwebrtc::RTCRtpReceiver;
using libwebrtc::RTCRtpTransceiver;
using libwebrtc::RTCDtlsTransport;
using libwebrtc::RTCDtlsTransportInformation;
using libwebrtc::RTCVideoFrame;
using libwebrtc::RTCVideoRenderer;
using libwebrtc::RTCVideoSource;
using libwebrtc::RTCVideoTrack;
using libwebrtc::RTCVideoDevice;
using libwebrtc::RTCVideoCapturer;
using libwebrtc::MediaSource;
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

struct lrtc_desktop_device_t {
  scoped_refptr<RTCDesktopDevice> ref;
};

struct lrtc_desktop_media_list_t {
  scoped_refptr<RTCDesktopMediaList> ref;
};

struct lrtc_media_source_t {
  scoped_refptr<MediaSource> ref;
};

struct lrtc_desktop_capturer_t {
  scoped_refptr<RTCDesktopCapturer> ref;
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
  scoped_refptr<RTCPeerConnectionFactory> factory;
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

struct lrtc_rtp_sender_t {
  scoped_refptr<libwebrtc::RTCRtpSender> ref;
};

struct lrtc_dtmf_sender_t {
  scoped_refptr<RTCDtmfSender> ref;
  class DtmfSenderObserverImpl* observer = nullptr;
};

struct lrtc_rtp_receiver_t {
  scoped_refptr<libwebrtc::RTCRtpReceiver> ref;
};

struct lrtc_rtp_transceiver_t {
  scoped_refptr<libwebrtc::RTCRtpTransceiver> ref;
};

static lrtc_result_t LrtcFailIfNull(const void* ptr) {
  return ptr ? LRTC_OK : LRTC_INVALID_ARG;
}

static bool CStringEqualsIgnoreCase(const char* a, const char* b) {
  if (!a || !b) {
    return false;
  }
  while (*a && *b) {
    if (std::tolower(static_cast<unsigned char>(*a)) !=
        std::tolower(static_cast<unsigned char>(*b))) {
      return false;
    }
    ++a;
    ++b;
  }
  return *a == '\0' && *b == '\0';
}

static bool IsTraceIceNativeEnabled() {
  const char* value = std::getenv("LUMENRTC_TRACE_ICE_NATIVE");
  if (!value || value[0] == '\0') {
    return false;
  }
  if (CStringEqualsIgnoreCase(value, "0") ||
      CStringEqualsIgnoreCase(value, "false") ||
      CStringEqualsIgnoreCase(value, "no") ||
      CStringEqualsIgnoreCase(value, "off")) {
    return false;
  }
  return true;
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

static scoped_refptr<libwebrtc::RTCRtpEncodingParameters>
BuildEncodingParameters(const lrtc_rtp_encoding_settings_t* settings) {
  scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding =
      libwebrtc::RTCRtpEncodingParameters::Create();
  if (!encoding.get()) {
    return nullptr;
  }
  if (!settings) {
    return encoding;
  }
  if (settings->max_bitrate_bps >= 0) {
    encoding->set_max_bitrate_bps(settings->max_bitrate_bps);
  }
  if (settings->min_bitrate_bps >= 0) {
    encoding->set_min_bitrate_bps(settings->min_bitrate_bps);
  }
  if (settings->max_framerate > 0.0) {
    encoding->set_max_framerate(settings->max_framerate);
  }
  if (settings->scale_resolution_down_by > 0.0) {
    encoding->set_scale_resolution_down_by(
        settings->scale_resolution_down_by);
  }
  if (settings->active >= 0) {
    encoding->set_active(settings->active != 0);
  }
  if (settings->bitrate_priority >= 0.0) {
    encoding->set_bitrate_priority(settings->bitrate_priority);
  }
  if (settings->network_priority >= 0 &&
      settings->network_priority <= 3) {
    encoding->set_network_priority(
        static_cast<libwebrtc::RTCPriority>(settings->network_priority));
  }
  if (settings->num_temporal_layers >= 0) {
    encoding->set_num_temporal_layers(settings->num_temporal_layers);
  }
  if (settings->scalability_mode && settings->scalability_mode[0] != '\0') {
    encoding->set_scalability_mode(string(settings->scalability_mode));
  }
  if (settings->rid && settings->rid[0] != '\0') {
    encoding->set_rid(string(settings->rid));
  }
  if (settings->adaptive_ptime >= 0) {
    encoding->set_adaptive_ptime(settings->adaptive_ptime != 0);
  }
  return encoding;
}

static libwebrtc::RTCRtpTransceiverDirection NormalizeTransceiverDirection(
    int direction) {
  if (direction < static_cast<int>(LRTC_RTP_TRANSCEIVER_SEND_RECV) ||
      direction > static_cast<int>(LRTC_RTP_TRANSCEIVER_STOPPED)) {
    return libwebrtc::RTCRtpTransceiverDirection::kSendRecv;
  }
  return static_cast<libwebrtc::RTCRtpTransceiverDirection>(direction);
}

static scoped_refptr<libwebrtc::RTCRtpTransceiverInit> BuildTransceiverInit(
    const lrtc_rtp_transceiver_init_t* init) {
  if (!init) {
    return nullptr;
  }
  vector<string> stream_ids =
      BuildStringVector(init->stream_ids, init->stream_id_count);
  std::vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> list;
  if (init->send_encodings && init->send_encoding_count > 0) {
    list.reserve(init->send_encoding_count);
    for (uint32_t i = 0; i < init->send_encoding_count; ++i) {
      scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding =
          BuildEncodingParameters(&init->send_encodings[i]);
      if (encoding.get()) {
        list.push_back(encoding);
      }
    }
  }
  vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> encodings(list);
  return libwebrtc::RTCRtpTransceiverInit::Create(
      NormalizeTransceiverDirection(init->direction),
      stream_ids,
      encodings);
}

static std::string BuildStatsJson(
    const vector<scoped_refptr<MediaRTCStats>>& reports) {
  std::string json;
  json.push_back('[');
  const size_t count = reports.size();
  for (size_t i = 0; i < count; ++i) {
    if (i > 0) {
      json.push_back(',');
    }
    scoped_refptr<MediaRTCStats> report = reports[i];
    if (!report.get()) {
      json.append("null");
      continue;
    }
    string report_json = report->ToJson();
    json.append(report_json.c_string(), report_json.size());
  }
  json.push_back(']');
  return json;
}

static void AppendJsonString(std::string& out, const char* value);

static std::string BuildRtpCapabilitiesJson(
    scoped_refptr<RTCRtpCapabilities> caps) {
  if (!caps.get()) {
    return "{}";
  }

  std::string json;
  json.append("{\"codecs\":[");
  vector<scoped_refptr<RTCRtpCodecCapability>> codecs = caps->codecs();
  for (size_t i = 0; i < codecs.size(); ++i) {
    if (i > 0) {
      json.push_back(',');
    }
    scoped_refptr<RTCRtpCodecCapability> codec = codecs[i];
    json.push_back('{');
    json.append("\"mimeType\":");
    string mime = codec->mime_type();
    AppendJsonString(json, mime.c_string());
    json.append(",\"clockRate\":");
    json.append(std::to_string(codec->clock_rate()));
    json.append(",\"channels\":");
    json.append(std::to_string(codec->channels()));
    json.append(",\"sdpFmtpLine\":");
    string fmtp = codec->sdp_fmtp_line();
    AppendJsonString(json, fmtp.c_string());
    json.push_back('}');
  }
  json.append("],\"headerExtensions\":[");

  vector<scoped_refptr<RTCRtpHeaderExtensionCapability>> extensions =
      caps->header_extensions();
  for (size_t i = 0; i < extensions.size(); ++i) {
    if (i > 0) {
      json.push_back(',');
    }
    scoped_refptr<RTCRtpHeaderExtensionCapability> ext = extensions[i];
    json.push_back('{');
    json.append("\"uri\":");
    string uri = ext->uri();
    AppendJsonString(json, uri.c_string());
    json.append(",\"preferredId\":");
    json.append(std::to_string(ext->preferred_id()));
    json.append(",\"preferredEncrypt\":");
    json.append(ext->preferred_encrypt() ? "true" : "false");
    json.push_back('}');
  }

  json.append("]}");
  return json;
}

static bool FillDtlsInfo(scoped_refptr<RTCDtlsTransport> transport,
                         lrtc_dtls_transport_info_t* info) {
  if (!info) {
    return false;
  }
  info->state = static_cast<int>(LRTC_DTLS_NEW);
  info->ssl_cipher_suite = 0;
  info->srtp_cipher_suite = 0;
  if (!transport.get()) {
    return false;
  }
  scoped_refptr<RTCDtlsTransportInformation> dtls_info =
      transport->GetInformation();
  if (!dtls_info.get()) {
    return false;
  }
  info->state = static_cast<int>(dtls_info->state());
  info->ssl_cipher_suite = dtls_info->ssl_cipher_suite();
  info->srtp_cipher_suite = dtls_info->srtp_cipher_suite();
  return true;
}

static int32_t CopyPortableString(const string& value, char* buffer,
                                  uint32_t buffer_len) {
  string tmp = value;
  const size_t len = tmp.size();
  const size_t needed = len + 1;
  if (!buffer || buffer_len == 0) {
    return static_cast<int32_t>(needed);
  }
  if (buffer_len < needed) {
    return -1;
  }
  if (len > 0) {
    std::memcpy(buffer, tmp.c_string(), len);
  }
  buffer[len] = '\0';
  return static_cast<int32_t>(len);
}

struct LogCallbackState {
  std::mutex mutex;
  lrtc_log_message_cb callback = nullptr;
  void* user_data = nullptr;
};

static LogCallbackState g_log_callback;

static void LrtcLogMessageHandler(const string& message) {
  lrtc_log_message_cb callback = nullptr;
  void* user_data = nullptr;
  {
    std::lock_guard<std::mutex> lock(g_log_callback.mutex);
    callback = g_log_callback.callback;
    user_data = g_log_callback.user_data;
  }
  if (callback) {
    callback(user_data, message.c_string());
  }
}

static void AppendJsonEscaped(std::string& out, const char* value) {
  if (!value) {
    return;
  }
  for (const char* p = value; *p; ++p) {
    switch (*p) {
      case '\"':
        out.append("\\\"");
        break;
      case '\\':
        out.append("\\\\");
        break;
      case '\b':
        out.append("\\b");
        break;
      case '\f':
        out.append("\\f");
        break;
      case '\n':
        out.append("\\n");
        break;
      case '\r':
        out.append("\\r");
        break;
      case '\t':
        out.append("\\t");
        break;
      default:
        out.push_back(*p);
        break;
    }
  }
}

static void AppendJsonString(std::string& out, const char* value) {
  out.push_back('\"');
  if (value) {
    AppendJsonEscaped(out, value);
  }
  out.push_back('\"');
}

static std::string BuildCodecMimeJson(
    const vector<scoped_refptr<RTCRtpCodecCapability>>& codecs) {
  std::string json;
  json.push_back('[');
  const size_t count = codecs.size();
  for (size_t i = 0; i < count; ++i) {
    if (i > 0) {
      json.push_back(',');
    }
    json.push_back('\"');
    if (codecs[i].get()) {
      string mime = codecs[i]->mime_type();
      AppendJsonEscaped(json, mime.c_string());
    }
    json.push_back('\"');
  }
  json.push_back(']');
  return json;
}

static bool MimeEquals(const string& a, const char* b) {
  if (!b) {
    return false;
  }
  const char* a_str = a.c_string();
  if (!a_str) {
    return false;
  }
  while (*a_str && *b) {
    if (std::tolower(static_cast<unsigned char>(*a_str)) !=
        std::tolower(static_cast<unsigned char>(*b))) {
      return false;
    }
    ++a_str;
    ++b;
  }
  return *a_str == '\0' && *b == '\0';
}

static vector<scoped_refptr<RTCRtpCodecCapability>>
BuildCodecPreferences(
    const vector<scoped_refptr<RTCRtpCodecCapability>>& codecs,
    const char** mime_types, uint32_t count) {
  if (!mime_types || count == 0) {
    return vector<scoped_refptr<RTCRtpCodecCapability>>();
  }
  std::vector<scoped_refptr<RTCRtpCodecCapability>> selected;
  selected.reserve(count);
  for (uint32_t i = 0; i < count; ++i) {
    const char* mime = mime_types[i];
    if (!mime) {
      continue;
    }
    for (size_t j = 0; j < codecs.size(); ++j) {
      scoped_refptr<RTCRtpCodecCapability> codec = codecs[j];
      if (!codec.get()) {
        continue;
      }
      if (MimeEquals(codec->mime_type(), mime)) {
        selected.push_back(codec);
        break;
      }
    }
  }
  return vector<scoped_refptr<RTCRtpCodecCapability>>(selected);
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
    if (cb.callbacks.on_track) {
      auto transceiver_handle = new lrtc_rtp_transceiver_t();
      transceiver_handle->ref = transceiver;
      auto receiver_handle = new lrtc_rtp_receiver_t();
      receiver_handle->ref = receiver;
      cb.callbacks.on_track(cb.user_data, transceiver_handle, receiver_handle);
    }
    string kind = track->kind();
    if (std::strcmp(kind.c_string(), "video") == 0) {
      if (cb.callbacks.on_video_track) {
        auto handle = new lrtc_video_track_t();
        handle->ref = static_cast<RTCVideoTrack*>(track.get());
        cb.callbacks.on_video_track(cb.user_data, handle);
      }
      return;
    }
    if (std::strcmp(kind.c_string(), "audio") == 0) {
      if (cb.callbacks.on_audio_track) {
        auto handle = new lrtc_audio_track_t();
        handle->ref = static_cast<RTCAudioTrack*>(track.get());
        cb.callbacks.on_audio_track(cb.user_data, handle);
      }
      return;
    }
  }

  void OnAddTrack(vector<scoped_refptr<libwebrtc::RTCMediaStream>> streams,
                  scoped_refptr<libwebrtc::RTCRtpReceiver> receiver) override {
    (void)streams;
    (void)receiver;
  }

  void OnRemoveTrack(scoped_refptr<libwebrtc::RTCRtpReceiver> receiver) override {
    auto cb = GetCallbacks();
    if (!cb.callbacks.on_remove_track || !receiver.get()) {
      return;
    }
    auto receiver_handle = new lrtc_rtp_receiver_t();
    receiver_handle->ref = receiver;
    cb.callbacks.on_remove_track(cb.user_data, nullptr, receiver_handle);
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

class DtmfSenderObserverImpl : public RTCDtmfSenderObserver {
 public:
  DtmfSenderObserverImpl() = default;

  void SetCallbacks(const lrtc_dtmf_sender_callbacks_t* callbacks,
                    void* user_data) {
    std::lock_guard<std::mutex> lock(mutex_);
    if (callbacks) {
      callbacks_ = *callbacks;
    } else {
      std::memset(&callbacks_, 0, sizeof(callbacks_));
    }
    user_data_ = user_data;
  }

  void OnToneChange(const string tone, const string tone_buffer) override {
    auto cb = GetCallbacks();
    if (cb.callbacks.on_tone_change) {
      cb.callbacks.on_tone_change(cb.user_data, tone.c_string(),
                                  tone_buffer.c_string());
    }
  }

  void OnToneChange(const string tone) override {
    auto cb = GetCallbacks();
    if (cb.callbacks.on_tone_change) {
      cb.callbacks.on_tone_change(cb.user_data, tone.c_string(), nullptr);
    }
  }

 private:
  struct CallbackSnapshot {
    lrtc_dtmf_sender_callbacks_t callbacks;
    void* user_data;
  };

  CallbackSnapshot GetCallbacks() {
    std::lock_guard<std::mutex> lock(mutex_);
    return {callbacks_, user_data_};
  }

  std::mutex mutex_;
  lrtc_dtmf_sender_callbacks_t callbacks_{};
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

LUMENRTC_API lrtc_result_t LUMENRTC_CALL lrtc_initialize(void) {
  return libwebrtc::LibWebRTC::Initialize() ? LRTC_OK : LRTC_ERROR;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_terminate(void) {
  libwebrtc::LibWebRTC::Terminate();
}

LUMENRTC_API void LUMENRTC_CALL lrtc_logging_set_min_level(int severity) {
  LibWebRTCLogging::setMinDebugLogLevel(
      static_cast<RTCLoggingSeverity>(severity));
}

LUMENRTC_API void LUMENRTC_CALL lrtc_logging_set_callback(int severity,
                                             lrtc_log_message_cb callback,
                                             void* user_data) {
  {
    std::lock_guard<std::mutex> lock(g_log_callback.mutex);
    g_log_callback.callback = callback;
    g_log_callback.user_data = user_data;
  }
  if (callback) {
    LibWebRTCLogging::setLogSink(static_cast<RTCLoggingSeverity>(severity),
                                 LrtcLogMessageHandler);
  } else {
    LibWebRTCLogging::removeLogSink();
  }
}

LUMENRTC_API void LUMENRTC_CALL lrtc_logging_remove_callback(void) {
  {
    std::lock_guard<std::mutex> lock(g_log_callback.mutex);
    g_log_callback.callback = nullptr;
    g_log_callback.user_data = nullptr;
  }
  LibWebRTCLogging::removeLogSink();
}

LUMENRTC_API lrtc_factory_t* LUMENRTC_CALL lrtc_factory_create(void) {
  auto handle = new lrtc_factory_t();
  handle->ref = libwebrtc::LibWebRTC::CreateRTCPeerConnectionFactory();
  if (!handle->ref.get()) {
    delete handle;
    return nullptr;
  }
  return handle;
}

LUMENRTC_API lrtc_result_t LUMENRTC_CALL lrtc_factory_initialize(lrtc_factory_t* factory) {
  if (LrtcFailIfNull(factory) != LRTC_OK) {
    return LRTC_INVALID_ARG;
  }
  return factory->ref->Initialize() ? LRTC_OK : LRTC_ERROR;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_factory_terminate(lrtc_factory_t* factory) {
  if (!factory) {
    return;
  }
  factory->ref->Terminate();
}

LUMENRTC_API void LUMENRTC_CALL lrtc_factory_release(lrtc_factory_t* factory) {
  delete factory;
}

LUMENRTC_API lrtc_audio_device_t* LUMENRTC_CALL lrtc_factory_get_audio_device(
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

LUMENRTC_API lrtc_video_device_t* LUMENRTC_CALL lrtc_factory_get_video_device(
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

LUMENRTC_API lrtc_desktop_device_t* LUMENRTC_CALL lrtc_factory_get_desktop_device(
    lrtc_factory_t* factory) {
#ifdef RTC_DESKTOP_DEVICE
  if (!factory || !factory->ref.get()) {
    return nullptr;
  }
  auto handle = new lrtc_desktop_device_t();
  handle->ref = factory->ref->GetDesktopDevice();
  if (!handle->ref.get()) {
    delete handle;
    return nullptr;
  }
  return handle;
#else
  (void)factory;
  return nullptr;
#endif
}

LUMENRTC_API lrtc_audio_source_t* LUMENRTC_CALL lrtc_factory_create_audio_source(
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

LUMENRTC_API lrtc_video_source_t* LUMENRTC_CALL lrtc_factory_create_video_source(
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

LUMENRTC_API lrtc_video_source_t* LUMENRTC_CALL lrtc_factory_create_desktop_source(
    lrtc_factory_t* factory, lrtc_desktop_capturer_t* capturer,
    const char* label, lrtc_media_constraints_t* constraints) {
#ifdef RTC_DESKTOP_DEVICE
  if (!factory || !factory->ref.get() || !capturer || !capturer->ref.get() ||
      !label) {
    return nullptr;
  }
  scoped_refptr<RTCMediaConstraints> mc;
  if (constraints) {
    mc = constraints->ref;
  }
  scoped_refptr<RTCVideoSource> source =
      factory->ref->CreateDesktopSource(capturer->ref, string(label), mc);
  if (!source.get()) {
    return nullptr;
  }
  auto handle = new lrtc_video_source_t();
  handle->ref = source;
  return handle;
#else
  (void)factory;
  (void)capturer;
  (void)label;
  (void)constraints;
  return nullptr;
#endif
}

LUMENRTC_API lrtc_audio_track_t* LUMENRTC_CALL lrtc_factory_create_audio_track(
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

LUMENRTC_API lrtc_video_track_t* LUMENRTC_CALL lrtc_factory_create_video_track(
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

LUMENRTC_API lrtc_media_stream_t* LUMENRTC_CALL lrtc_factory_create_stream(
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

LUMENRTC_API lrtc_media_constraints_t* LUMENRTC_CALL lrtc_media_constraints_create(void) {
  auto handle = new lrtc_media_constraints_t();
  handle->ref = RTCMediaConstraints::Create();
  if (!handle->ref.get()) {
    delete handle;
    return nullptr;
  }
  return handle;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_media_constraints_add_mandatory(
    lrtc_media_constraints_t* constraints, const char* key,
    const char* value) {
  if (!constraints || !constraints->ref.get() || !key || !value) {
    return;
  }
  constraints->ref->AddMandatoryConstraint(string(key), string(value));
}

LUMENRTC_API void LUMENRTC_CALL lrtc_media_constraints_add_optional(
    lrtc_media_constraints_t* constraints, const char* key,
    const char* value) {
  if (!constraints || !constraints->ref.get() || !key || !value) {
    return;
  }
  constraints->ref->AddOptionalConstraint(string(key), string(value));
}

LUMENRTC_API void LUMENRTC_CALL lrtc_media_constraints_release(
    lrtc_media_constraints_t* constraints) {
  delete constraints;
}

LUMENRTC_API int16_t LUMENRTC_CALL lrtc_audio_device_playout_devices(
    lrtc_audio_device_t* device) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->PlayoutDevices();
}

LUMENRTC_API int16_t LUMENRTC_CALL lrtc_audio_device_recording_devices(
    lrtc_audio_device_t* device) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->RecordingDevices();
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_playout_device_name(
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

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_recording_device_name(
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

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_set_playout_device(
    lrtc_audio_device_t* device, uint16_t index) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->SetPlayoutDevice(index);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_set_recording_device(
    lrtc_audio_device_t* device, uint16_t index) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->SetRecordingDevice(index);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_set_microphone_volume(
    lrtc_audio_device_t* device, uint32_t volume) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->SetMicrophoneVolume(volume);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_microphone_volume(
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

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_set_speaker_volume(
    lrtc_audio_device_t* device, uint32_t volume) {
  if (!device || !device->ref.get()) {
    return -1;
  }
  return device->ref->SetSpeakerVolume(volume);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_speaker_volume(
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

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_device_release(lrtc_audio_device_t* device) {
  delete device;
}

LUMENRTC_API lrtc_desktop_media_list_t* LUMENRTC_CALL lrtc_desktop_device_get_media_list(
    lrtc_desktop_device_t* device, lrtc_desktop_type type) {
#ifdef RTC_DESKTOP_DEVICE
  if (!device || !device->ref.get()) {
    return nullptr;
  }
  scoped_refptr<RTCDesktopMediaList> list =
      device->ref->GetDesktopMediaList(
          static_cast<libwebrtc::DesktopType>(type));
  if (!list.get()) {
    return nullptr;
  }
  auto handle = new lrtc_desktop_media_list_t();
  handle->ref = list;
  return handle;
#else
  (void)device;
  (void)type;
  return nullptr;
#endif
}

LUMENRTC_API lrtc_desktop_capturer_t* LUMENRTC_CALL lrtc_desktop_device_create_capturer(
    lrtc_desktop_device_t* device, lrtc_media_source_t* source,
    bool show_cursor) {
#ifdef RTC_DESKTOP_DEVICE
  if (!device || !device->ref.get() || !source || !source->ref.get()) {
    return nullptr;
  }
  scoped_refptr<RTCDesktopCapturer> capturer =
      device->ref->CreateDesktopCapturer(source->ref, show_cursor);
  if (!capturer.get()) {
    return nullptr;
  }
  auto handle = new lrtc_desktop_capturer_t();
  handle->ref = capturer;
  return handle;
#else
  (void)device;
  (void)source;
  (void)show_cursor;
  return nullptr;
#endif
}

LUMENRTC_API void LUMENRTC_CALL lrtc_desktop_device_release(lrtc_desktop_device_t* device) {
  delete device;
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_desktop_media_list_update(
    lrtc_desktop_media_list_t* list, bool force_reload, bool get_thumbnail) {
#ifdef RTC_DESKTOP_DEVICE
  if (!list || !list->ref.get()) {
    return -1;
  }
  return list->ref->UpdateSourceList(force_reload, get_thumbnail);
#else
  (void)list;
  (void)force_reload;
  (void)get_thumbnail;
  return -1;
#endif
}

LUMENRTC_API int LUMENRTC_CALL lrtc_desktop_media_list_get_source_count(
    lrtc_desktop_media_list_t* list) {
#ifdef RTC_DESKTOP_DEVICE
  if (!list || !list->ref.get()) {
    return 0;
  }
  return list->ref->GetSourceCount();
#else
  (void)list;
  return 0;
#endif
}

LUMENRTC_API lrtc_media_source_t* LUMENRTC_CALL lrtc_desktop_media_list_get_source(
    lrtc_desktop_media_list_t* list, int index) {
#ifdef RTC_DESKTOP_DEVICE
  if (!list || !list->ref.get()) {
    return nullptr;
  }
  scoped_refptr<MediaSource> source = list->ref->GetSource(index);
  if (!source.get()) {
    return nullptr;
  }
  auto handle = new lrtc_media_source_t();
  handle->ref = source;
  return handle;
#else
  (void)list;
  (void)index;
  return nullptr;
#endif
}

LUMENRTC_API void LUMENRTC_CALL lrtc_desktop_media_list_release(
    lrtc_desktop_media_list_t* list) {
  delete list;
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_media_source_get_id(
    lrtc_media_source_t* source, char* buffer, uint32_t buffer_len) {
#ifdef RTC_DESKTOP_DEVICE
  if (!source || !source->ref.get()) {
    return -1;
  }
  return CopyPortableString(source->ref->id(), buffer, buffer_len);
#else
  (void)source;
  (void)buffer;
  (void)buffer_len;
  return -1;
#endif
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_media_source_get_name(
    lrtc_media_source_t* source, char* buffer, uint32_t buffer_len) {
#ifdef RTC_DESKTOP_DEVICE
  if (!source || !source->ref.get()) {
    return -1;
  }
  return CopyPortableString(source->ref->name(), buffer, buffer_len);
#else
  (void)source;
  (void)buffer;
  (void)buffer_len;
  return -1;
#endif
}

LUMENRTC_API int LUMENRTC_CALL lrtc_media_source_get_type(lrtc_media_source_t* source) {
#ifdef RTC_DESKTOP_DEVICE
  if (!source || !source->ref.get()) {
    return -1;
  }
  return static_cast<int>(source->ref->type());
#else
  (void)source;
  return -1;
#endif
}

LUMENRTC_API void LUMENRTC_CALL lrtc_media_source_release(lrtc_media_source_t* source) {
  delete source;
}

LUMENRTC_API lrtc_desktop_capture_state LUMENRTC_CALL lrtc_desktop_capturer_start(
    lrtc_desktop_capturer_t* capturer, uint32_t fps) {
#ifdef RTC_DESKTOP_DEVICE
  if (!capturer || !capturer->ref.get()) {
    return LRTC_DESKTOP_CAPTURE_FAILED;
  }
  return static_cast<lrtc_desktop_capture_state>(capturer->ref->Start(fps));
#else
  (void)capturer;
  (void)fps;
  return LRTC_DESKTOP_CAPTURE_FAILED;
#endif
}

LUMENRTC_API lrtc_desktop_capture_state LUMENRTC_CALL lrtc_desktop_capturer_start_region(
    lrtc_desktop_capturer_t* capturer, uint32_t fps, uint32_t x, uint32_t y,
    uint32_t w, uint32_t h) {
#ifdef RTC_DESKTOP_DEVICE
  if (!capturer || !capturer->ref.get()) {
    return LRTC_DESKTOP_CAPTURE_FAILED;
  }
  return static_cast<lrtc_desktop_capture_state>(
      capturer->ref->Start(fps, x, y, w, h));
#else
  (void)capturer;
  (void)fps;
  (void)x;
  (void)y;
  (void)w;
  (void)h;
  return LRTC_DESKTOP_CAPTURE_FAILED;
#endif
}

LUMENRTC_API void LUMENRTC_CALL lrtc_desktop_capturer_stop(
    lrtc_desktop_capturer_t* capturer) {
#ifdef RTC_DESKTOP_DEVICE
  if (!capturer || !capturer->ref.get()) {
    return;
  }
  capturer->ref->Stop();
#else
  (void)capturer;
#endif
}

LUMENRTC_API bool LUMENRTC_CALL lrtc_desktop_capturer_is_running(
    lrtc_desktop_capturer_t* capturer) {
#ifdef RTC_DESKTOP_DEVICE
  if (!capturer || !capturer->ref.get()) {
    return false;
  }
  return capturer->ref->IsRunning();
#else
  (void)capturer;
  return false;
#endif
}

LUMENRTC_API void LUMENRTC_CALL lrtc_desktop_capturer_release(
    lrtc_desktop_capturer_t* capturer) {
  delete capturer;
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_video_device_number_of_devices(
    lrtc_video_device_t* device) {
  if (!device || !device->ref.get()) {
    return 0;
  }
  return device->ref->NumberOfDevices();
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_video_device_get_device_name(
    lrtc_video_device_t* device, uint32_t index, char* name,
    uint32_t name_length, char* unique_id, uint32_t unique_id_length) {
  if (!device || !device->ref.get() || !name || !unique_id) {
    return -1;
  }
  return device->ref->GetDeviceName(index, name, name_length, unique_id,
                                    unique_id_length);
}

LUMENRTC_API lrtc_video_capturer_t* LUMENRTC_CALL lrtc_video_device_create_capturer(
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

LUMENRTC_API void LUMENRTC_CALL lrtc_video_device_release(lrtc_video_device_t* device) {
  delete device;
}

LUMENRTC_API bool LUMENRTC_CALL lrtc_video_capturer_start(
    lrtc_video_capturer_t* capturer) {
  if (!capturer || !capturer->ref.get()) {
    return false;
  }
  return capturer->ref->StartCapture();
}

LUMENRTC_API bool LUMENRTC_CALL lrtc_video_capturer_capture_started(
    lrtc_video_capturer_t* capturer) {
  if (!capturer || !capturer->ref.get()) {
    return false;
  }
  return capturer->ref->CaptureStarted();
}

LUMENRTC_API void LUMENRTC_CALL lrtc_video_capturer_stop(
    lrtc_video_capturer_t* capturer) {
  if (!capturer || !capturer->ref.get()) {
    return;
  }
  capturer->ref->StopCapture();
}

LUMENRTC_API void LUMENRTC_CALL lrtc_video_capturer_release(
    lrtc_video_capturer_t* capturer) {
  delete capturer;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_source_capture_frame(
    lrtc_audio_source_t* source, const void* audio_data, int bits_per_sample,
    int sample_rate, size_t number_of_channels, size_t number_of_frames) {
  if (!source || !source->ref.get() || !audio_data) {
    return;
  }
  source->ref->CaptureFrame(audio_data, bits_per_sample, sample_rate,
                            number_of_channels, number_of_frames);
}

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_source_release(lrtc_audio_source_t* source) {
  delete source;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_video_source_release(lrtc_video_source_t* source) {
  delete source;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_track_set_volume(lrtc_audio_track_t* track,
                                               double volume) {
  if (!track || !track->ref.get()) {
    return;
  }
  track->ref->SetVolume(volume);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_track_get_id(lrtc_audio_track_t* track,
                                              char* buffer,
                                              uint32_t buffer_len) {
  if (!track || !track->ref.get()) {
    return -1;
  }
  return CopyPortableString(track->ref->id(), buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_audio_track_get_state(lrtc_audio_track_t* track) {
  if (!track || !track->ref.get()) {
    return -1;
  }
  return static_cast<int>(track->ref->state());
}

LUMENRTC_API int LUMENRTC_CALL lrtc_audio_track_get_enabled(lrtc_audio_track_t* track) {
  if (!track || !track->ref.get()) {
    return 0;
  }
  return track->ref->enabled() ? 1 : 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_audio_track_set_enabled(lrtc_audio_track_t* track,
                                               int enabled) {
  if (!track || !track->ref.get()) {
    return 0;
  }
  return track->ref->set_enabled(enabled != 0) ? 1 : 0;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_track_add_sink(lrtc_audio_track_t* track,
                                             lrtc_audio_sink_t* sink) {
  if (!track || !track->ref.get() || !sink || !sink->sink) {
    return;
  }
  track->ref->AddSink(sink->sink);
}

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_track_remove_sink(lrtc_audio_track_t* track,
                                                lrtc_audio_sink_t* sink) {
  if (!track || !track->ref.get() || !sink || !sink->sink) {
    return;
  }
  track->ref->RemoveSink(sink->sink);
}

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_track_release(lrtc_audio_track_t* track) {
  delete track;
}

LUMENRTC_API lrtc_audio_sink_t* LUMENRTC_CALL lrtc_audio_sink_create(
    const lrtc_audio_sink_callbacks_t* callbacks, void* user_data) {
  auto handle = new lrtc_audio_sink_t();
  auto* sink = new AudioSinkImpl();
  sink->SetCallbacks(callbacks, user_data);
  handle->sink = sink;
  return handle;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_sink_release(lrtc_audio_sink_t* sink) {
  if (!sink) {
    return;
  }
  delete sink->sink;
  sink->sink = nullptr;
  delete sink;
}

LUMENRTC_API bool LUMENRTC_CALL lrtc_media_stream_add_audio_track(
    lrtc_media_stream_t* stream, lrtc_audio_track_t* track) {
  if (!stream || !stream->ref.get() || !track || !track->ref.get()) {
    return false;
  }
  return stream->ref->AddTrack(track->ref);
}

LUMENRTC_API bool LUMENRTC_CALL lrtc_media_stream_add_video_track(
    lrtc_media_stream_t* stream, lrtc_video_track_t* track) {
  if (!stream || !stream->ref.get() || !track || !track->ref.get()) {
    return false;
  }
  return stream->ref->AddTrack(track->ref);
}

LUMENRTC_API bool LUMENRTC_CALL lrtc_media_stream_remove_audio_track(
    lrtc_media_stream_t* stream, lrtc_audio_track_t* track) {
  if (!stream || !stream->ref.get() || !track || !track->ref.get()) {
    return false;
  }
  return stream->ref->RemoveTrack(track->ref);
}

LUMENRTC_API bool LUMENRTC_CALL lrtc_media_stream_remove_video_track(
    lrtc_media_stream_t* stream, lrtc_video_track_t* track) {
  if (!stream || !stream->ref.get() || !track || !track->ref.get()) {
    return false;
  }
  return stream->ref->RemoveTrack(track->ref);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_media_stream_get_id(lrtc_media_stream_t* stream,
                                               char* buffer,
                                               uint32_t buffer_len) {
  if (!stream || !stream->ref.get()) {
    return -1;
  }
  return CopyPortableString(stream->ref->id(), buffer, buffer_len);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_media_stream_get_label(lrtc_media_stream_t* stream,
                                                  char* buffer,
                                                  uint32_t buffer_len) {
  if (!stream || !stream->ref.get()) {
    return -1;
  }
  return CopyPortableString(stream->ref->label(), buffer, buffer_len);
}

LUMENRTC_API void LUMENRTC_CALL lrtc_media_stream_release(lrtc_media_stream_t* stream) {
  delete stream;
}

LUMENRTC_API lrtc_peer_connection_t* LUMENRTC_CALL lrtc_peer_connection_create(
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
  if (!mc.get()) {
    // Some libwebrtc wrappers assume non-null constraints.
    mc = RTCMediaConstraints::Create();
  }
  scoped_refptr<RTCPeerConnection> pc = factory->ref->Create(cfg, mc);
  if (!pc.get()) {
    return nullptr;
  }
  auto handle = new lrtc_peer_connection_t();
  handle->ref = pc;
  handle->factory = factory->ref;
  auto* observer = new PeerConnectionObserverImpl();
  observer->SetCallbacks(callbacks, user_data);
  pc->RegisterRTCPeerConnectionObserver(observer);
  handle->observer = observer;
  return handle;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_set_callbacks(
    lrtc_peer_connection_t* pc,
    const lrtc_peer_connection_callbacks_t* callbacks, void* user_data) {
  if (!pc || !pc->observer) {
    return;
  }
  pc->observer->SetCallbacks(callbacks, user_data);
}

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_close(lrtc_peer_connection_t* pc) {
  if (!pc || !pc->ref.get()) {
    return;
  }
  pc->ref->Close();
}

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_release(lrtc_peer_connection_t* pc) {
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

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_create_offer(
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
  if (!mc.get()) {
    // Some libwebrtc wrappers assume non-null constraints.
    mc = RTCMediaConstraints::Create();
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

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_create_answer(
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
  if (!mc.get()) {
    // Some libwebrtc wrappers assume non-null constraints.
    mc = RTCMediaConstraints::Create();
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

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_restart_ice(
    lrtc_peer_connection_t* pc) {
  if (!pc || !pc->ref.get()) {
    return;
  }
  pc->ref->RestartIce();
}

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_set_local_description(
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

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_set_remote_description(
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

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_local_description(
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

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_remote_description(
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

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_stats(
    lrtc_peer_connection_t* pc, lrtc_stats_success_cb success,
    lrtc_stats_failure_cb failure, void* user_data) {
  if (!pc || !pc->ref.get()) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  pc->ref->GetStats(
      [success, user_data](vector<scoped_refptr<MediaRTCStats>> reports) {
        if (!success) {
          return;
        }
        std::string json = BuildStatsJson(reports);
        success(user_data, json.c_str());
      },
      [failure, user_data](const char* error) {
        if (failure) {
          failure(user_data, error);
        }
      });
}

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_sender_stats(
    lrtc_peer_connection_t* pc, lrtc_rtp_sender_t* sender,
    lrtc_stats_success_cb success, lrtc_stats_failure_cb failure,
    void* user_data) {
  if (!pc || !pc->ref.get() || !sender || !sender->ref.get()) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  pc->ref->GetStats(
      sender->ref,
      [success, user_data](vector<scoped_refptr<MediaRTCStats>> reports) {
        if (!success) {
          return;
        }
        std::string json = BuildStatsJson(reports);
        success(user_data, json.c_str());
      },
      [failure, user_data](const char* error) {
        if (failure) {
          failure(user_data, error);
        }
      });
}

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_receiver_stats(
    lrtc_peer_connection_t* pc, lrtc_rtp_receiver_t* receiver,
    lrtc_stats_success_cb success, lrtc_stats_failure_cb failure,
    void* user_data) {
  if (!pc || !pc->ref.get() || !receiver || !receiver->ref.get()) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  pc->ref->GetStats(
      receiver->ref,
      [success, user_data](vector<scoped_refptr<MediaRTCStats>> reports) {
        if (!success) {
          return;
        }
        std::string json = BuildStatsJson(reports);
        success(user_data, json.c_str());
      },
      [failure, user_data](const char* error) {
        if (failure) {
          failure(user_data, error);
        }
      });
}

LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_set_codec_preferences(
    lrtc_peer_connection_t* pc, lrtc_media_type media_type,
    const char** mime_types, uint32_t mime_type_count) {
  if (!pc || !pc->ref.get() || !pc->factory.get()) {
    return 0;
  }
  scoped_refptr<RTCRtpCapabilities> caps =
      pc->factory->GetRtpSenderCapabilities(
          static_cast<libwebrtc::RTCMediaType>(media_type));
  if (!caps.get()) {
    return 0;
  }
  vector<scoped_refptr<RTCRtpCodecCapability>> selected =
      BuildCodecPreferences(caps->codecs(), mime_types, mime_type_count);
  if (selected.size() == 0) {
    return 0;
  }
  vector<scoped_refptr<RTCRtpTransceiver>> transceivers =
      pc->ref->transceivers();
  bool applied = false;
  for (size_t i = 0; i < transceivers.size(); ++i) {
    scoped_refptr<RTCRtpTransceiver> transceiver = transceivers[i];
    if (!transceiver.get()) {
      continue;
    }
    if (transceiver->media_type() ==
        static_cast<libwebrtc::RTCMediaType>(media_type)) {
      transceiver->SetCodecPreferences(selected);
      applied = true;
    }
  }
  return applied ? 1 : 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_set_transceiver_codec_preferences(
    lrtc_peer_connection_t* pc, lrtc_rtp_transceiver_t* transceiver,
    const char** mime_types, uint32_t mime_type_count) {
  if (!pc || !pc->ref.get() || !pc->factory.get() || !transceiver ||
      !transceiver->ref.get()) {
    return 0;
  }
  scoped_refptr<RTCRtpCapabilities> caps =
      pc->factory->GetRtpSenderCapabilities(
          transceiver->ref->media_type());
  if (!caps.get()) {
    return 0;
  }
  vector<scoped_refptr<RTCRtpCodecCapability>> selected =
      BuildCodecPreferences(caps->codecs(), mime_types, mime_type_count);
  if (selected.size() == 0) {
    return 0;
  }
  transceiver->ref->SetCodecPreferences(selected);
  return 1;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_add_ice_candidate_ex(
    lrtc_peer_connection_t* pc, const char* sdp_mid, int sdp_mline_index,
    const char* candidate) {
  const bool trace_ice_native = IsTraceIceNativeEnabled();
  if (!pc || !pc->ref.get() || !sdp_mid || !candidate) {
    if (trace_ice_native) {
      std::fprintf(stderr,
                   "[lumenrtc:ice] add candidate rejected: invalid args "
                   "pc=%d mid=%d candidate=%d\n",
                   (pc && pc->ref.get()) ? 1 : 0, sdp_mid ? 1 : 0,
                   candidate ? 1 : 0);
    }
    return 0;
  }
  libwebrtc::SdpParseError parse_error;
  scoped_refptr<RTCIceCandidate> parsed = RTCIceCandidate::Create(
      string(candidate), string(sdp_mid), sdp_mline_index, &parse_error);
  if (!parsed.get()) {
    if (trace_ice_native) {
      std::fprintf(
          stderr,
          "[lumenrtc:ice] add candidate parse failed: mid=%s mline=%d err=%s\n",
          sdp_mid, sdp_mline_index, parse_error.description.c_string());
    }
    return 0;
  }

  pc->ref->AddCandidate(string(sdp_mid), sdp_mline_index, string(candidate));
  if (trace_ice_native) {
    std::fprintf(
        stderr,
        "[lumenrtc:ice] add candidate applied: mid=%s mline=%d len=%zu\n",
        sdp_mid, sdp_mline_index, std::strlen(candidate));
  }
  return 1;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_add_ice_candidate(
    lrtc_peer_connection_t* pc, const char* sdp_mid, int sdp_mline_index,
    const char* candidate) {
  lrtc_peer_connection_add_ice_candidate_ex(pc, sdp_mid, sdp_mline_index,
                                            candidate);
}

LUMENRTC_API bool LUMENRTC_CALL lrtc_peer_connection_add_stream(
    lrtc_peer_connection_t* pc, lrtc_media_stream_t* stream) {
  if (!pc || !pc->ref.get() || !stream || !stream->ref.get()) {
    return false;
  }
  return pc->ref->AddStream(stream->ref) == 0;
}

LUMENRTC_API bool LUMENRTC_CALL lrtc_peer_connection_remove_stream(
    lrtc_peer_connection_t* pc, lrtc_media_stream_t* stream) {
  if (!pc || !pc->ref.get() || !stream || !stream->ref.get()) {
    return false;
  }
  return pc->ref->RemoveStream(stream->ref) == 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_add_audio_track(
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

LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_add_video_track(
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

LUMENRTC_API lrtc_rtp_sender_t* LUMENRTC_CALL lrtc_peer_connection_add_audio_track_sender(
    lrtc_peer_connection_t* pc, lrtc_audio_track_t* track,
    const char** stream_ids, uint32_t stream_id_count) {
  if (!pc || !pc->ref.get() || !track || !track->ref.get()) {
    return nullptr;
  }
  vector<string> streams = BuildStringVector(stream_ids, stream_id_count);
  scoped_refptr<libwebrtc::RTCRtpSender> sender =
      pc->ref->AddTrack(track->ref, streams);
  if (!sender.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_sender_t();
  handle->ref = sender;
  return handle;
}

LUMENRTC_API lrtc_rtp_sender_t* LUMENRTC_CALL lrtc_peer_connection_add_video_track_sender(
    lrtc_peer_connection_t* pc, lrtc_video_track_t* track,
    const char** stream_ids, uint32_t stream_id_count) {
  if (!pc || !pc->ref.get() || !track || !track->ref.get()) {
    return nullptr;
  }
  vector<string> streams = BuildStringVector(stream_ids, stream_id_count);
  scoped_refptr<libwebrtc::RTCRtpSender> sender =
      pc->ref->AddTrack(track->ref, streams);
  if (!sender.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_sender_t();
  handle->ref = sender;
  return handle;
}

LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL lrtc_peer_connection_add_transceiver(
    lrtc_peer_connection_t* pc, lrtc_media_type media_type) {
  if (!pc || !pc->ref.get()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiver> transceiver =
      pc->ref->AddTransceiver(
          static_cast<libwebrtc::RTCMediaType>(media_type));
  if (!transceiver.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_transceiver_t();
  handle->ref = transceiver;
  return handle;
}

LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL lrtc_peer_connection_add_audio_track_transceiver(
    lrtc_peer_connection_t* pc, lrtc_audio_track_t* track) {
  if (!pc || !pc->ref.get() || !track || !track->ref.get()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiver> transceiver =
      pc->ref->AddTransceiver(track->ref);
  if (!transceiver.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_transceiver_t();
  handle->ref = transceiver;
  return handle;
}

LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL lrtc_peer_connection_add_video_track_transceiver(
    lrtc_peer_connection_t* pc, lrtc_video_track_t* track) {
  if (!pc || !pc->ref.get() || !track || !track->ref.get()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiver> transceiver =
      pc->ref->AddTransceiver(track->ref);
  if (!transceiver.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_transceiver_t();
  handle->ref = transceiver;
  return handle;
}

LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL lrtc_peer_connection_add_transceiver_with_init(
    lrtc_peer_connection_t* pc, lrtc_media_type media_type,
    const lrtc_rtp_transceiver_init_t* init) {
  if (!pc || !pc->ref.get() || !init) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiverInit> transceiver_init =
      BuildTransceiverInit(init);
  if (!transceiver_init.get()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiver> transceiver =
      pc->ref->AddTransceiver(
          static_cast<libwebrtc::RTCMediaType>(media_type), transceiver_init);
  if (!transceiver.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_transceiver_t();
  handle->ref = transceiver;
  return handle;
}

LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL lrtc_peer_connection_add_audio_track_transceiver_with_init(
    lrtc_peer_connection_t* pc, lrtc_audio_track_t* track,
    const lrtc_rtp_transceiver_init_t* init) {
  if (!pc || !pc->ref.get() || !track || !track->ref.get() || !init) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiverInit> transceiver_init =
      BuildTransceiverInit(init);
  if (!transceiver_init.get()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiver> transceiver =
      pc->ref->AddTransceiver(track->ref, transceiver_init);
  if (!transceiver.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_transceiver_t();
  handle->ref = transceiver;
  return handle;
}

LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL lrtc_peer_connection_add_video_track_transceiver_with_init(
    lrtc_peer_connection_t* pc, lrtc_video_track_t* track,
    const lrtc_rtp_transceiver_init_t* init) {
  if (!pc || !pc->ref.get() || !track || !track->ref.get() || !init) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiverInit> transceiver_init =
      BuildTransceiverInit(init);
  if (!transceiver_init.get()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiver> transceiver =
      pc->ref->AddTransceiver(track->ref, transceiver_init);
  if (!transceiver.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_transceiver_t();
  handle->ref = transceiver;
  return handle;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_remove_track(
    lrtc_peer_connection_t* pc, lrtc_rtp_sender_t* sender) {
  if (!pc || !pc->ref.get() || !sender || !sender->ref.get()) {
    return 0;
  }
  return pc->ref->RemoveTrack(sender->ref) ? 1 : 0;
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_peer_connection_sender_count(
    lrtc_peer_connection_t* pc) {
  if (!pc || !pc->ref.get()) {
    return 0;
  }
  return static_cast<uint32_t>(pc->ref->senders().size());
}

LUMENRTC_API lrtc_rtp_sender_t* LUMENRTC_CALL lrtc_peer_connection_get_sender(
    lrtc_peer_connection_t* pc, uint32_t index) {
  if (!pc || !pc->ref.get()) {
    return nullptr;
  }
  vector<scoped_refptr<libwebrtc::RTCRtpSender>> senders = pc->ref->senders();
  if (index >= senders.size()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpSender> sender = senders[index];
  if (!sender.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_sender_t();
  handle->ref = sender;
  return handle;
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_peer_connection_receiver_count(
    lrtc_peer_connection_t* pc) {
  if (!pc || !pc->ref.get()) {
    return 0;
  }
  return static_cast<uint32_t>(pc->ref->receivers().size());
}

LUMENRTC_API lrtc_rtp_receiver_t* LUMENRTC_CALL lrtc_peer_connection_get_receiver(
    lrtc_peer_connection_t* pc, uint32_t index) {
  if (!pc || !pc->ref.get()) {
    return nullptr;
  }
  vector<scoped_refptr<libwebrtc::RTCRtpReceiver>> receivers =
      pc->ref->receivers();
  if (index >= receivers.size()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpReceiver> receiver = receivers[index];
  if (!receiver.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_receiver_t();
  handle->ref = receiver;
  return handle;
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_peer_connection_transceiver_count(
    lrtc_peer_connection_t* pc) {
  if (!pc || !pc->ref.get()) {
    return 0;
  }
  return static_cast<uint32_t>(pc->ref->transceivers().size());
}

LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL lrtc_peer_connection_get_transceiver(
    lrtc_peer_connection_t* pc, uint32_t index) {
  if (!pc || !pc->ref.get()) {
    return nullptr;
  }
  vector<scoped_refptr<libwebrtc::RTCRtpTransceiver>> transceivers =
      pc->ref->transceivers();
  if (index >= transceivers.size()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpTransceiver> transceiver =
      transceivers[index];
  if (!transceiver.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_transceiver_t();
  handle->ref = transceiver;
  return handle;
}

LUMENRTC_API lrtc_data_channel_t* LUMENRTC_CALL lrtc_peer_connection_create_data_channel(
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

LUMENRTC_API void LUMENRTC_CALL lrtc_data_channel_set_callbacks(
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

LUMENRTC_API void LUMENRTC_CALL lrtc_data_channel_send(lrtc_data_channel_t* channel,
                                         const uint8_t* data, uint32_t size,
                                         int binary) {
  if (!channel || !channel->ref.get() || (!data && size > 0)) {
    return;
  }
  channel->ref->Send(data, size, binary != 0);
}

LUMENRTC_API void LUMENRTC_CALL lrtc_data_channel_close(lrtc_data_channel_t* channel) {
  if (!channel || !channel->ref.get()) {
    return;
  }
  channel->ref->Close();
}

LUMENRTC_API void LUMENRTC_CALL lrtc_data_channel_release(lrtc_data_channel_t* channel) {
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

LUMENRTC_API lrtc_video_sink_t* LUMENRTC_CALL lrtc_video_sink_create(
    const lrtc_video_sink_callbacks_t* callbacks, void* user_data) {
  auto handle = new lrtc_video_sink_t();
  auto* renderer = new VideoSinkImpl();
  renderer->SetCallbacks(callbacks, user_data);
  handle->renderer = renderer;
  return handle;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_video_sink_release(lrtc_video_sink_t* sink) {
  if (!sink) {
    return;
  }
  delete sink->renderer;
  sink->renderer = nullptr;
  delete sink;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_video_track_add_sink(lrtc_video_track_t* track,
                                             lrtc_video_sink_t* sink) {
  if (!track || !track->ref.get() || !sink || !sink->renderer) {
    return;
  }
  track->ref->AddRenderer(sink->renderer);
}

LUMENRTC_API void LUMENRTC_CALL lrtc_video_track_remove_sink(lrtc_video_track_t* track,
                                                lrtc_video_sink_t* sink) {
  if (!track || !track->ref.get() || !sink || !sink->renderer) {
    return;
  }
  track->ref->RemoveRenderer(sink->renderer);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_video_track_get_id(lrtc_video_track_t* track,
                                              char* buffer,
                                              uint32_t buffer_len) {
  if (!track || !track->ref.get()) {
    return -1;
  }
  return CopyPortableString(track->ref->id(), buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_video_track_get_state(lrtc_video_track_t* track) {
  if (!track || !track->ref.get()) {
    return -1;
  }
  return static_cast<int>(track->ref->state());
}

LUMENRTC_API int LUMENRTC_CALL lrtc_video_track_get_enabled(lrtc_video_track_t* track) {
  if (!track || !track->ref.get()) {
    return 0;
  }
  return track->ref->enabled() ? 1 : 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_video_track_set_enabled(lrtc_video_track_t* track,
                                               int enabled) {
  if (!track || !track->ref.get()) {
    return 0;
  }
  return track->ref->set_enabled(enabled != 0) ? 1 : 0;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_video_track_release(lrtc_video_track_t* track) {
  delete track;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_width(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->width();
}

LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_height(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->height();
}

LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_stride_y(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->StrideY();
}

LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_stride_u(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->StrideU();
}

LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_stride_v(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return 0;
  }
  return frame->ref->StrideV();
}

LUMENRTC_API const uint8_t* LUMENRTC_CALL lrtc_video_frame_data_y(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return nullptr;
  }
  return frame->ref->DataY();
}

LUMENRTC_API const uint8_t* LUMENRTC_CALL lrtc_video_frame_data_u(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return nullptr;
  }
  return frame->ref->DataU();
}

LUMENRTC_API const uint8_t* LUMENRTC_CALL lrtc_video_frame_data_v(lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return nullptr;
  }
  return frame->ref->DataV();
}

LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_copy_i420(
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

LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_to_argb(
    lrtc_video_frame_t* frame, uint8_t* dst_argb, int dst_stride_argb,
    int dest_width, int dest_height, int format) {
  if (!frame || !frame->ref.get() || !dst_argb) {
    return 0;
  }
  return frame->ref->ConvertToARGB(
      static_cast<RTCVideoFrame::Type>(format), dst_argb, dst_stride_argb,
      dest_width, dest_height);
}

LUMENRTC_API lrtc_video_frame_t* LUMENRTC_CALL lrtc_video_frame_retain(
    lrtc_video_frame_t* frame) {
  if (!frame || !frame->ref.get()) {
    return nullptr;
  }
  auto handle = new lrtc_video_frame_t();
  handle->ref = frame->ref;
  return handle;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_video_frame_release(lrtc_video_frame_t* frame) {
  delete frame;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_set_encoding_parameters(
    lrtc_rtp_sender_t* sender, const lrtc_rtp_encoding_settings_t* settings) {
  if (!sender || !sender->ref.get() || !settings) {
    return 0;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      sender->ref->parameters();
  if (!parameters.get()) {
    return 0;
  }

  vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> encodings =
      parameters->encodings();

  std::vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> list;
  list.reserve(encodings.size());
  for (size_t i = 0; i < encodings.size(); ++i) {
    list.push_back(encodings[i]);
  }

  if (list.empty()) {
    scoped_refptr<libwebrtc::RTCRtpEncodingParameters> created =
        libwebrtc::RTCRtpEncodingParameters::Create();
    if (created.get()) {
      list.push_back(created);
    }
  }

  if (list.empty()) {
    return 0;
  }

  scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding = list[0];
  if (encoding.get()) {
    if (settings->max_bitrate_bps >= 0) {
      encoding->set_max_bitrate_bps(settings->max_bitrate_bps);
    }
    if (settings->min_bitrate_bps >= 0) {
      encoding->set_min_bitrate_bps(settings->min_bitrate_bps);
    }
    if (settings->max_framerate > 0.0) {
      encoding->set_max_framerate(settings->max_framerate);
    }
    if (settings->scale_resolution_down_by > 0.0) {
      encoding->set_scale_resolution_down_by(
          settings->scale_resolution_down_by);
    }
    if (settings->active >= 0) {
      encoding->set_active(settings->active != 0);
    }
    if (settings->bitrate_priority >= 0.0) {
      encoding->set_bitrate_priority(settings->bitrate_priority);
    }
    if (settings->network_priority >= 0 &&
        settings->network_priority <= 3) {
      encoding->set_network_priority(
          static_cast<libwebrtc::RTCPriority>(settings->network_priority));
    }
    if (settings->num_temporal_layers >= 0) {
      encoding->set_num_temporal_layers(settings->num_temporal_layers);
    }
    if (settings->scalability_mode && settings->scalability_mode[0] != '\0') {
      encoding->set_scalability_mode(string(settings->scalability_mode));
    }
    if (settings->rid && settings->rid[0] != '\0') {
      encoding->set_rid(string(settings->rid));
    }
    if (settings->adaptive_ptime >= 0) {
      encoding->set_adaptive_ptime(settings->adaptive_ptime != 0);
    }
  }

  parameters->set_encodings(
      vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>>(list));

  if (settings->degradation_preference >= 0) {
    parameters->SetDegradationPreference(
        static_cast<libwebrtc::RTCDegradationPreference>(
            settings->degradation_preference));
  }

  return sender->ref->set_parameters(parameters) ? 1 : 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_set_encoding_parameters_at(
    lrtc_rtp_sender_t* sender, uint32_t index,
    const lrtc_rtp_encoding_settings_t* settings) {
  if (!sender || !sender->ref.get() || !settings) {
    return 0;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      sender->ref->parameters();
  if (!parameters.get()) {
    return 0;
  }

  vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> encodings =
      parameters->encodings();

  std::vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> list;
  list.reserve(encodings.size());
  for (size_t i = 0; i < encodings.size(); ++i) {
    list.push_back(encodings[i]);
  }

  if (list.empty()) {
    if (index != 0) {
      return 0;
    }
    scoped_refptr<libwebrtc::RTCRtpEncodingParameters> created =
        libwebrtc::RTCRtpEncodingParameters::Create();
    if (created.get()) {
      list.push_back(created);
    }
  }

  if (index >= list.size()) {
    return 0;
  }

  scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding = list[index];
  if (encoding.get()) {
    if (settings->max_bitrate_bps >= 0) {
      encoding->set_max_bitrate_bps(settings->max_bitrate_bps);
    }
    if (settings->min_bitrate_bps >= 0) {
      encoding->set_min_bitrate_bps(settings->min_bitrate_bps);
    }
    if (settings->max_framerate > 0.0) {
      encoding->set_max_framerate(settings->max_framerate);
    }
    if (settings->scale_resolution_down_by > 0.0) {
      encoding->set_scale_resolution_down_by(
          settings->scale_resolution_down_by);
    }
    if (settings->active >= 0) {
      encoding->set_active(settings->active != 0);
    }
    if (settings->bitrate_priority >= 0.0) {
      encoding->set_bitrate_priority(settings->bitrate_priority);
    }
    if (settings->network_priority >= 0 &&
        settings->network_priority <= 3) {
      encoding->set_network_priority(
          static_cast<libwebrtc::RTCPriority>(settings->network_priority));
    }
    if (settings->num_temporal_layers >= 0) {
      encoding->set_num_temporal_layers(settings->num_temporal_layers);
    }
    if (settings->scalability_mode && settings->scalability_mode[0] != '\0') {
      encoding->set_scalability_mode(string(settings->scalability_mode));
    }
    if (settings->rid && settings->rid[0] != '\0') {
      encoding->set_rid(string(settings->rid));
    }
    if (settings->adaptive_ptime >= 0) {
      encoding->set_adaptive_ptime(settings->adaptive_ptime != 0);
    }
  }

  parameters->set_encodings(
      vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>>(list));

  if (settings->degradation_preference >= 0) {
    parameters->SetDegradationPreference(
        static_cast<libwebrtc::RTCDegradationPreference>(
            settings->degradation_preference));
  }

  return sender->ref->set_parameters(parameters) ? 1 : 0;
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_sender_encoding_count(
    lrtc_rtp_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return 0;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      sender->ref->parameters();
  if (!parameters.get()) {
    return 0;
  }
  return static_cast<uint32_t>(parameters->encodings().size());
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_get_encoding_info(
    lrtc_rtp_sender_t* sender, uint32_t index,
    lrtc_rtp_encoding_info_t* info) {
  if (!sender || !sender->ref.get() || !info) {
    return 0;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      sender->ref->parameters();
  if (!parameters.get()) {
    return 0;
  }
  vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> encodings =
      parameters->encodings();
  if (index >= encodings.size()) {
    return 0;
  }
  scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding =
      encodings[index];
  if (!encoding.get()) {
    return 0;
  }
  info->ssrc = encoding->ssrc();
  info->max_bitrate_bps = encoding->max_bitrate_bps();
  info->min_bitrate_bps = encoding->min_bitrate_bps();
  info->max_framerate = encoding->max_framerate();
  info->scale_resolution_down_by = encoding->scale_resolution_down_by();
  info->active = encoding->active() ? 1 : 0;
  info->bitrate_priority = encoding->bitrate_priority();
  info->network_priority = static_cast<int>(encoding->network_priority());
  info->num_temporal_layers = encoding->num_temporal_layers();
  info->adaptive_ptime = encoding->adaptive_ptime() ? 1 : 0;
  return 1;
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_sender_get_encoding_rid(
    lrtc_rtp_sender_t* sender, uint32_t index, char* buffer,
    uint32_t buffer_len) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      sender->ref->parameters();
  if (!parameters.get()) {
    return -1;
  }
  vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> encodings =
      parameters->encodings();
  if (index >= encodings.size()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding =
      encodings[index];
  if (!encoding.get()) {
    return -1;
  }
  return CopyPortableString(encoding->rid(), buffer, buffer_len);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_sender_get_encoding_scalability_mode(
    lrtc_rtp_sender_t* sender, uint32_t index, char* buffer,
    uint32_t buffer_len) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      sender->ref->parameters();
  if (!parameters.get()) {
    return -1;
  }
  vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> encodings =
      parameters->encodings();
  if (index >= encodings.size()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding =
      encodings[index];
  if (!encoding.get()) {
    return -1;
  }
  return CopyPortableString(encoding->scalability_mode(),
                            buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_get_degradation_preference(
    lrtc_rtp_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      sender->ref->parameters();
  if (!parameters.get()) {
    return -1;
  }
  return static_cast<int>(parameters->GetDegradationPreference());
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_sender_get_parameters_mid(
    lrtc_rtp_sender_t* sender, char* buffer, uint32_t buffer_len) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      sender->ref->parameters();
  if (!parameters.get()) {
    return -1;
  }
  return CopyPortableString(parameters->mid(), buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_get_dtls_info(
    lrtc_rtp_sender_t* sender, lrtc_dtls_transport_info_t* info) {
  if (!sender || !sender->ref.get()) {
    return 0;
  }
  return FillDtlsInfo(sender->ref->dtls_transport(), info) ? 1 : 0;
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_sender_get_ssrc(lrtc_rtp_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return 0;
  }
  return sender->ref->ssrc();
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_replace_audio_track(
    lrtc_rtp_sender_t* sender, lrtc_audio_track_t* track) {
  if (!sender || !sender->ref.get()) {
    return 0;
  }
  if (sender->ref->media_type() != libwebrtc::RTCMediaType::AUDIO) {
    return 0;
  }
  scoped_refptr<RTCMediaTrack> media_track;
  if (track) {
    if (!track->ref.get()) {
      return 0;
    }
    media_track = static_cast<RTCMediaTrack*>(track->ref.get());
  }
  return sender->ref->set_track(media_track) ? 1 : 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_replace_video_track(
    lrtc_rtp_sender_t* sender, lrtc_video_track_t* track) {
  if (!sender || !sender->ref.get()) {
    return 0;
  }
  if (sender->ref->media_type() != libwebrtc::RTCMediaType::VIDEO) {
    return 0;
  }
  scoped_refptr<RTCMediaTrack> media_track;
  if (track) {
    if (!track->ref.get()) {
      return 0;
    }
    media_track = static_cast<RTCMediaTrack*>(track->ref.get());
  }
  return sender->ref->set_track(media_track) ? 1 : 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_get_media_type(lrtc_rtp_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  return static_cast<int>(sender->ref->media_type());
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_sender_get_id(
    lrtc_rtp_sender_t* sender, char* buffer, uint32_t buffer_len) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  return CopyPortableString(sender->ref->id(), buffer, buffer_len);
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_sender_stream_id_count(
    lrtc_rtp_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return 0;
  }
  return static_cast<uint32_t>(sender->ref->stream_ids().size());
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_sender_get_stream_id(
    lrtc_rtp_sender_t* sender, uint32_t index, char* buffer,
    uint32_t buffer_len) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  vector<string> ids = sender->ref->stream_ids();
  if (index >= ids.size()) {
    return -1;
  }
  return CopyPortableString(ids[index], buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_set_stream_ids(
    lrtc_rtp_sender_t* sender, const char** stream_ids,
    uint32_t stream_id_count) {
  if (!sender || !sender->ref.get()) {
    return 0;
  }
  vector<string> ids = BuildStringVector(stream_ids, stream_id_count);
  sender->ref->set_stream_ids(ids);
  return 1;
}

LUMENRTC_API lrtc_audio_track_t* LUMENRTC_CALL lrtc_rtp_sender_get_audio_track(
    lrtc_rtp_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return nullptr;
  }
  scoped_refptr<RTCMediaTrack> track = sender->ref->track();
  if (!track.get()) {
    return nullptr;
  }
  string kind = track->kind();
  if (std::strcmp(kind.c_string(), "audio") != 0) {
    return nullptr;
  }
  auto handle = new lrtc_audio_track_t();
  handle->ref = static_cast<RTCAudioTrack*>(track.get());
  return handle;
}

LUMENRTC_API lrtc_video_track_t* LUMENRTC_CALL lrtc_rtp_sender_get_video_track(
    lrtc_rtp_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return nullptr;
  }
  scoped_refptr<RTCMediaTrack> track = sender->ref->track();
  if (!track.get()) {
    return nullptr;
  }
  string kind = track->kind();
  if (std::strcmp(kind.c_string(), "video") != 0) {
    return nullptr;
  }
  auto handle = new lrtc_video_track_t();
  handle->ref = static_cast<RTCVideoTrack*>(track.get());
  return handle;
}

LUMENRTC_API lrtc_dtmf_sender_t* LUMENRTC_CALL lrtc_rtp_sender_get_dtmf_sender(
    lrtc_rtp_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return nullptr;
  }
  scoped_refptr<RTCDtmfSender> dtmf = sender->ref->dtmf_sender();
  if (!dtmf.get()) {
    return nullptr;
  }
  auto handle = new lrtc_dtmf_sender_t();
  handle->ref = dtmf;
  return handle;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_dtmf_sender_set_callbacks(
    lrtc_dtmf_sender_t* sender,
    const lrtc_dtmf_sender_callbacks_t* callbacks,
    void* user_data) {
  if (!sender || !sender->ref.get()) {
    return;
  }
  if (!sender->observer) {
    sender->observer = new DtmfSenderObserverImpl();
  }
  sender->observer->SetCallbacks(callbacks, user_data);
  sender->ref->UnregisterObserver();
  if (callbacks && callbacks->on_tone_change) {
    sender->ref->RegisterObserver(sender->observer);
  }
}

LUMENRTC_API int LUMENRTC_CALL lrtc_dtmf_sender_can_insert(lrtc_dtmf_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return 0;
  }
  return sender->ref->CanInsertDtmf() ? 1 : 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_dtmf_sender_insert(lrtc_dtmf_sender_t* sender,
                                         const char* tones, int duration,
                                         int inter_tone_gap,
                                         int comma_delay) {
  if (!sender || !sender->ref.get() || !tones) {
    return 0;
  }
  if (comma_delay >= 0) {
    return sender->ref->InsertDtmf(string(tones), duration, inter_tone_gap,
                                   comma_delay)
               ? 1
               : 0;
  }
  return sender->ref->InsertDtmf(string(tones), duration, inter_tone_gap) ? 1
                                                                          : 0;
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_dtmf_sender_tones(lrtc_dtmf_sender_t* sender,
                                            char* buffer,
                                            uint32_t buffer_len) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  return CopyPortableString(sender->ref->tones(), buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_dtmf_sender_duration(lrtc_dtmf_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  return sender->ref->duration();
}

LUMENRTC_API int LUMENRTC_CALL lrtc_dtmf_sender_inter_tone_gap(
    lrtc_dtmf_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  return sender->ref->inter_tone_gap();
}

LUMENRTC_API int LUMENRTC_CALL lrtc_dtmf_sender_comma_delay(lrtc_dtmf_sender_t* sender) {
  if (!sender || !sender->ref.get()) {
    return -1;
  }
  return sender->ref->comma_delay();
}

LUMENRTC_API void LUMENRTC_CALL lrtc_dtmf_sender_release(lrtc_dtmf_sender_t* sender) {
  if (!sender) {
    return;
  }
  if (sender->ref.get()) {
    sender->ref->UnregisterObserver();
  }
  delete sender->observer;
  delete sender;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_rtp_sender_release(lrtc_rtp_sender_t* sender) {
  delete sender;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_receiver_get_media_type(
    lrtc_rtp_receiver_t* receiver) {
  if (!receiver || !receiver->ref.get()) {
    return -1;
  }
  return static_cast<int>(receiver->ref->media_type());
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_receiver_get_id(
    lrtc_rtp_receiver_t* receiver, char* buffer, uint32_t buffer_len) {
  if (!receiver || !receiver->ref.get()) {
    return -1;
  }
  return CopyPortableString(receiver->ref->id(), buffer, buffer_len);
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_receiver_encoding_count(
    lrtc_rtp_receiver_t* receiver) {
  if (!receiver || !receiver->ref.get()) {
    return 0;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      receiver->ref->parameters();
  if (!parameters.get()) {
    return 0;
  }
  return static_cast<uint32_t>(parameters->encodings().size());
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_receiver_get_encoding_info(
    lrtc_rtp_receiver_t* receiver, uint32_t index,
    lrtc_rtp_encoding_info_t* info) {
  if (!receiver || !receiver->ref.get() || !info) {
    return 0;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      receiver->ref->parameters();
  if (!parameters.get()) {
    return 0;
  }
  vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> encodings =
      parameters->encodings();
  if (index >= encodings.size()) {
    return 0;
  }
  scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding =
      encodings[index];
  if (!encoding.get()) {
    return 0;
  }
  info->ssrc = encoding->ssrc();
  info->max_bitrate_bps = encoding->max_bitrate_bps();
  info->min_bitrate_bps = encoding->min_bitrate_bps();
  info->max_framerate = encoding->max_framerate();
  info->scale_resolution_down_by = encoding->scale_resolution_down_by();
  info->active = encoding->active() ? 1 : 0;
  info->bitrate_priority = encoding->bitrate_priority();
  info->network_priority = static_cast<int>(encoding->network_priority());
  info->num_temporal_layers = encoding->num_temporal_layers();
  info->adaptive_ptime = encoding->adaptive_ptime() ? 1 : 0;
  return 1;
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_receiver_get_encoding_rid(
    lrtc_rtp_receiver_t* receiver, uint32_t index, char* buffer,
    uint32_t buffer_len) {
  if (!receiver || !receiver->ref.get()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      receiver->ref->parameters();
  if (!parameters.get()) {
    return -1;
  }
  vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> encodings =
      parameters->encodings();
  if (index >= encodings.size()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding =
      encodings[index];
  if (!encoding.get()) {
    return -1;
  }
  return CopyPortableString(encoding->rid(), buffer, buffer_len);
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_receiver_get_encoding_scalability_mode(
    lrtc_rtp_receiver_t* receiver, uint32_t index, char* buffer,
    uint32_t buffer_len) {
  if (!receiver || !receiver->ref.get()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      receiver->ref->parameters();
  if (!parameters.get()) {
    return -1;
  }
  vector<scoped_refptr<libwebrtc::RTCRtpEncodingParameters>> encodings =
      parameters->encodings();
  if (index >= encodings.size()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpEncodingParameters> encoding =
      encodings[index];
  if (!encoding.get()) {
    return -1;
  }
  return CopyPortableString(encoding->scalability_mode(),
                            buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_receiver_get_degradation_preference(
    lrtc_rtp_receiver_t* receiver) {
  if (!receiver || !receiver->ref.get()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      receiver->ref->parameters();
  if (!parameters.get()) {
    return -1;
  }
  return static_cast<int>(parameters->GetDegradationPreference());
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_receiver_get_parameters_mid(
    lrtc_rtp_receiver_t* receiver, char* buffer, uint32_t buffer_len) {
  if (!receiver || !receiver->ref.get()) {
    return -1;
  }
  scoped_refptr<libwebrtc::RTCRtpParameters> parameters =
      receiver->ref->parameters();
  if (!parameters.get()) {
    return -1;
  }
  return CopyPortableString(parameters->mid(), buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_receiver_get_dtls_info(
    lrtc_rtp_receiver_t* receiver, lrtc_dtls_transport_info_t* info) {
  if (!receiver || !receiver->ref.get()) {
    return 0;
  }
  return FillDtlsInfo(receiver->ref->dtls_transport(), info) ? 1 : 0;
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_receiver_stream_id_count(
    lrtc_rtp_receiver_t* receiver) {
  if (!receiver || !receiver->ref.get()) {
    return 0;
  }
  return static_cast<uint32_t>(receiver->ref->stream_ids().size());
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_receiver_get_stream_id(
    lrtc_rtp_receiver_t* receiver, uint32_t index, char* buffer,
    uint32_t buffer_len) {
  if (!receiver || !receiver->ref.get()) {
    return -1;
  }
  vector<string> ids = receiver->ref->stream_ids();
  if (index >= ids.size()) {
    return -1;
  }
  return CopyPortableString(ids[index], buffer, buffer_len);
}

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_receiver_stream_count(
    lrtc_rtp_receiver_t* receiver) {
  if (!receiver || !receiver->ref.get()) {
    return 0;
  }
  return static_cast<uint32_t>(receiver->ref->streams().size());
}

LUMENRTC_API lrtc_media_stream_t* LUMENRTC_CALL lrtc_rtp_receiver_get_stream(
    lrtc_rtp_receiver_t* receiver, uint32_t index) {
  if (!receiver || !receiver->ref.get()) {
    return nullptr;
  }
  vector<scoped_refptr<RTCMediaStream>> streams = receiver->ref->streams();
  if (index >= streams.size()) {
    return nullptr;
  }
  scoped_refptr<RTCMediaStream> stream = streams[index];
  if (!stream.get()) {
    return nullptr;
  }
  auto handle = new lrtc_media_stream_t();
  handle->ref = stream;
  return handle;
}

LUMENRTC_API lrtc_audio_track_t* LUMENRTC_CALL lrtc_rtp_receiver_get_audio_track(
    lrtc_rtp_receiver_t* receiver) {
  if (!receiver || !receiver->ref.get()) {
    return nullptr;
  }
  scoped_refptr<RTCMediaTrack> track = receiver->ref->track();
  if (!track.get()) {
    return nullptr;
  }
  string kind = track->kind();
  if (std::strcmp(kind.c_string(), "audio") != 0) {
    return nullptr;
  }
  auto handle = new lrtc_audio_track_t();
  handle->ref = static_cast<RTCAudioTrack*>(track.get());
  return handle;
}

LUMENRTC_API lrtc_video_track_t* LUMENRTC_CALL lrtc_rtp_receiver_get_video_track(
    lrtc_rtp_receiver_t* receiver) {
  if (!receiver || !receiver->ref.get()) {
    return nullptr;
  }
  scoped_refptr<RTCMediaTrack> track = receiver->ref->track();
  if (!track.get()) {
    return nullptr;
  }
  string kind = track->kind();
  if (std::strcmp(kind.c_string(), "video") != 0) {
    return nullptr;
  }
  auto handle = new lrtc_video_track_t();
  handle->ref = static_cast<RTCVideoTrack*>(track.get());
  return handle;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_receiver_set_jitter_buffer_min_delay(
    lrtc_rtp_receiver_t* receiver, double delay_seconds) {
  if (!receiver || !receiver->ref.get()) {
    return 0;
  }
  receiver->ref->SetJitterBufferMinimumDelay(delay_seconds);
  return 1;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_rtp_receiver_release(lrtc_rtp_receiver_t* receiver) {
  delete receiver;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_media_type(
    lrtc_rtp_transceiver_t* transceiver) {
  if (!transceiver || !transceiver->ref.get()) {
    return -1;
  }
  return static_cast<int>(transceiver->ref->media_type());
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_transceiver_get_mid(
    lrtc_rtp_transceiver_t* transceiver, char* buffer, uint32_t buffer_len) {
  if (!transceiver || !transceiver->ref.get()) {
    return -1;
  }
  return CopyPortableString(transceiver->ref->mid(), buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_direction(
    lrtc_rtp_transceiver_t* transceiver) {
  if (!transceiver || !transceiver->ref.get()) {
    return -1;
  }
  return static_cast<int>(transceiver->ref->direction());
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_current_direction(
    lrtc_rtp_transceiver_t* transceiver) {
  if (!transceiver || !transceiver->ref.get()) {
    return -1;
  }
  return static_cast<int>(transceiver->ref->current_direction());
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_fired_direction(
    lrtc_rtp_transceiver_t* transceiver) {
  if (!transceiver || !transceiver->ref.get()) {
    return -1;
  }
  return static_cast<int>(transceiver->ref->fired_direction());
}

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_transceiver_get_id(
    lrtc_rtp_transceiver_t* transceiver, char* buffer, uint32_t buffer_len) {
  if (!transceiver || !transceiver->ref.get()) {
    return -1;
  }
  return CopyPortableString(transceiver->ref->transceiver_id(),
                            buffer, buffer_len);
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_stopped(
    lrtc_rtp_transceiver_t* transceiver) {
  if (!transceiver || !transceiver->ref.get()) {
    return 0;
  }
  return transceiver->ref->Stopped() ? 1 : 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_stopping(
    lrtc_rtp_transceiver_t* transceiver) {
  if (!transceiver || !transceiver->ref.get()) {
    return 0;
  }
  return transceiver->ref->Stopping() ? 1 : 0;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_set_direction(
    lrtc_rtp_transceiver_t* transceiver, int direction, char* error,
    uint32_t error_len) {
  if (!transceiver || !transceiver->ref.get()) {
    return 0;
  }
  string err = transceiver->ref->SetDirectionWithError(
      static_cast<libwebrtc::RTCRtpTransceiverDirection>(direction));
  string tmp = err;
  if (tmp.size() > 0) {
    if (error_len > 0 && error) {
      CopyPortableString(tmp, error, error_len);
    }
    return 0;
  }
  return 1;
}

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_stop(
    lrtc_rtp_transceiver_t* transceiver, char* error, uint32_t error_len) {
  if (!transceiver || !transceiver->ref.get()) {
    return 0;
  }
  string err = transceiver->ref->StopStandard();
  string tmp = err;
  if (tmp.size() > 0) {
    if (error_len > 0 && error) {
      CopyPortableString(tmp, error, error_len);
    }
    return 0;
  }
  return 1;
}

LUMENRTC_API lrtc_rtp_sender_t* LUMENRTC_CALL lrtc_rtp_transceiver_get_sender(
    lrtc_rtp_transceiver_t* transceiver) {
  if (!transceiver || !transceiver->ref.get()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpSender> sender = transceiver->ref->sender();
  if (!sender.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_sender_t();
  handle->ref = sender;
  return handle;
}

LUMENRTC_API lrtc_rtp_receiver_t* LUMENRTC_CALL lrtc_rtp_transceiver_get_receiver(
    lrtc_rtp_transceiver_t* transceiver) {
  if (!transceiver || !transceiver->ref.get()) {
    return nullptr;
  }
  scoped_refptr<libwebrtc::RTCRtpReceiver> receiver =
      transceiver->ref->receiver();
  if (!receiver.get()) {
    return nullptr;
  }
  auto handle = new lrtc_rtp_receiver_t();
  handle->ref = receiver;
  return handle;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_rtp_transceiver_release(
    lrtc_rtp_transceiver_t* transceiver) {
  delete transceiver;
}

LUMENRTC_API void LUMENRTC_CALL lrtc_factory_get_rtp_sender_codec_mime_types(
    lrtc_factory_t* factory, lrtc_media_type media_type,
    lrtc_stats_success_cb success, lrtc_stats_failure_cb failure,
    void* user_data) {
  if (!factory || !factory->ref.get()) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  scoped_refptr<RTCRtpCapabilities> caps =
      factory->ref->GetRtpSenderCapabilities(
          static_cast<libwebrtc::RTCMediaType>(media_type));
  if (!caps.get()) {
    if (failure) {
      failure(user_data, "capabilities not available");
    }
    return;
  }
  if (success) {
    std::string json = BuildCodecMimeJson(caps->codecs());
    success(user_data, json.c_str());
  }
}

LUMENRTC_API void LUMENRTC_CALL lrtc_factory_get_rtp_sender_capabilities(
    lrtc_factory_t* factory, lrtc_media_type media_type,
    lrtc_stats_success_cb success, lrtc_stats_failure_cb failure,
    void* user_data) {
  if (!factory || !factory->ref.get()) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  scoped_refptr<RTCRtpCapabilities> caps =
      factory->ref->GetRtpSenderCapabilities(
          static_cast<libwebrtc::RTCMediaType>(media_type));
  if (!caps.get()) {
    if (failure) {
      failure(user_data, "capabilities not available");
    }
    return;
  }
  if (success) {
    std::string json = BuildRtpCapabilitiesJson(caps);
    success(user_data, json.c_str());
  }
}

LUMENRTC_API void LUMENRTC_CALL lrtc_factory_get_rtp_receiver_capabilities(
    lrtc_factory_t* factory, lrtc_media_type media_type,
    lrtc_stats_success_cb success, lrtc_stats_failure_cb failure,
    void* user_data) {
  if (!factory || !factory->ref.get()) {
    if (failure) {
      failure(user_data, "invalid arguments");
    }
    return;
  }
  scoped_refptr<RTCRtpCapabilities> caps =
      factory->ref->GetRtpReceiverCapabilities(
          static_cast<libwebrtc::RTCMediaType>(media_type));
  if (!caps.get()) {
    if (failure) {
      failure(user_data, "capabilities not available");
    }
    return;
  }
  if (success) {
    std::string json = BuildRtpCapabilitiesJson(caps);
    success(user_data, json.c_str());
  }
}

}  // extern "C"
