#ifndef LUMENRTC_H
#define LUMENRTC_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>

#if defined(_WIN32)
  #if defined(LUMENRTC_EXPORTS)
    #define LUMENRTC_API __declspec(dllexport)
  #elif defined(LUMENRTC_DLL)
    #define LUMENRTC_API __declspec(dllimport)
  #else
    #define LUMENRTC_API
  #endif
  #define LUMENRTC_CALL __cdecl
#else
  #define LUMENRTC_API __attribute__((visibility("default")))
  #define LUMENRTC_CALL
#endif

#define LRTC_MAX_ICE_SERVERS 8

typedef struct lrtc_factory_t lrtc_factory_t;
typedef struct lrtc_peer_connection_t lrtc_peer_connection_t;
typedef struct lrtc_media_constraints_t lrtc_media_constraints_t;
typedef struct lrtc_data_channel_t lrtc_data_channel_t;
typedef struct lrtc_audio_device_t lrtc_audio_device_t;
typedef struct lrtc_video_device_t lrtc_video_device_t;
typedef struct lrtc_desktop_device_t lrtc_desktop_device_t;
typedef struct lrtc_desktop_media_list_t lrtc_desktop_media_list_t;
typedef struct lrtc_media_source_t lrtc_media_source_t;
typedef struct lrtc_desktop_capturer_t lrtc_desktop_capturer_t;
typedef struct lrtc_video_capturer_t lrtc_video_capturer_t;
typedef struct lrtc_video_source_t lrtc_video_source_t;
typedef struct lrtc_audio_source_t lrtc_audio_source_t;
typedef struct lrtc_media_stream_t lrtc_media_stream_t;
typedef struct lrtc_video_track_t lrtc_video_track_t;
typedef struct lrtc_audio_track_t lrtc_audio_track_t;
typedef struct lrtc_audio_sink_t lrtc_audio_sink_t;
typedef struct lrtc_video_sink_t lrtc_video_sink_t;
typedef struct lrtc_video_frame_t lrtc_video_frame_t;
typedef struct lrtc_rtp_sender_t lrtc_rtp_sender_t;
typedef struct lrtc_rtp_receiver_t lrtc_rtp_receiver_t;
typedef struct lrtc_rtp_transceiver_t lrtc_rtp_transceiver_t;

typedef enum lrtc_result_t {
  LRTC_OK = 0,
  LRTC_ERROR = 1,
  LRTC_INVALID_ARG = 2,
  LRTC_NOT_IMPLEMENTED = 3,
} lrtc_result_t;

typedef enum lrtc_media_security_type {
  LRTC_SRTP_NONE = 0,
  LRTC_SDES_SRTP = 1,
  LRTC_DTLS_SRTP = 2,
} lrtc_media_security_type;

typedef enum lrtc_media_type {
  LRTC_MEDIA_AUDIO = 0,
  LRTC_MEDIA_VIDEO = 1,
  LRTC_MEDIA_DATA = 2,
} lrtc_media_type;

typedef enum lrtc_desktop_type {
  LRTC_DESKTOP_SCREEN = 0,
  LRTC_DESKTOP_WINDOW = 1,
} lrtc_desktop_type;

typedef enum lrtc_desktop_capture_state {
  LRTC_DESKTOP_CAPTURE_RUNNING = 0,
  LRTC_DESKTOP_CAPTURE_STOPPED = 1,
  LRTC_DESKTOP_CAPTURE_FAILED = 2,
} lrtc_desktop_capture_state;

typedef enum lrtc_rtp_transceiver_direction {
  LRTC_RTP_TRANSCEIVER_SEND_RECV = 0,
  LRTC_RTP_TRANSCEIVER_SEND_ONLY = 1,
  LRTC_RTP_TRANSCEIVER_RECV_ONLY = 2,
  LRTC_RTP_TRANSCEIVER_INACTIVE = 3,
  LRTC_RTP_TRANSCEIVER_STOPPED = 4,
} lrtc_rtp_transceiver_direction;

typedef enum lrtc_audio_source_type {
  LRTC_AUDIO_SOURCE_MICROPHONE = 0,
  LRTC_AUDIO_SOURCE_CUSTOM = 1,
} lrtc_audio_source_type;

typedef enum lrtc_ice_transports_type {
  LRTC_ICE_TRANSPORTS_NONE = 0,
  LRTC_ICE_TRANSPORTS_RELAY = 1,
  LRTC_ICE_TRANSPORTS_NO_HOST = 2,
  LRTC_ICE_TRANSPORTS_ALL = 3,
} lrtc_ice_transports_type;

typedef enum lrtc_tcp_candidate_policy {
  LRTC_TCP_CANDIDATE_ENABLED = 0,
  LRTC_TCP_CANDIDATE_DISABLED = 1,
} lrtc_tcp_candidate_policy;

typedef enum lrtc_candidate_network_policy {
  LRTC_CANDIDATE_NETWORK_ALL = 0,
  LRTC_CANDIDATE_NETWORK_LOW_COST = 1,
} lrtc_candidate_network_policy;

typedef enum lrtc_rtcp_mux_policy {
  LRTC_RTCP_MUX_NEGOTIATE = 0,
  LRTC_RTCP_MUX_REQUIRE = 1,
} lrtc_rtcp_mux_policy;

typedef enum lrtc_bundle_policy {
  LRTC_BUNDLE_BALANCED = 0,
  LRTC_BUNDLE_MAX_BUNDLE = 1,
  LRTC_BUNDLE_MAX_COMPAT = 2,
} lrtc_bundle_policy;

typedef enum lrtc_sdp_semantics {
  LRTC_SDP_PLAN_B = 0,
  LRTC_SDP_UNIFIED_PLAN = 1,
} lrtc_sdp_semantics;

typedef enum lrtc_degradation_preference {
  LRTC_DEGRADATION_DISABLED = 0,
  LRTC_DEGRADATION_MAINTAIN_FRAMERATE = 1,
  LRTC_DEGRADATION_MAINTAIN_RESOLUTION = 2,
  LRTC_DEGRADATION_BALANCED = 3,
} lrtc_degradation_preference;

typedef enum lrtc_peer_connection_state {
  LRTC_PC_STATE_NEW = 0,
  LRTC_PC_STATE_CONNECTING = 1,
  LRTC_PC_STATE_CONNECTED = 2,
  LRTC_PC_STATE_DISCONNECTED = 3,
  LRTC_PC_STATE_FAILED = 4,
  LRTC_PC_STATE_CLOSED = 5,
} lrtc_peer_connection_state;

typedef enum lrtc_signaling_state {
  LRTC_SIGNALING_STABLE = 0,
  LRTC_SIGNALING_HAVE_LOCAL_OFFER = 1,
  LRTC_SIGNALING_HAVE_REMOTE_OFFER = 2,
  LRTC_SIGNALING_HAVE_LOCAL_PRANSWER = 3,
  LRTC_SIGNALING_HAVE_REMOTE_PRANSWER = 4,
  LRTC_SIGNALING_CLOSED = 5,
} lrtc_signaling_state;

typedef enum lrtc_ice_gathering_state {
  LRTC_ICE_GATHERING_NEW = 0,
  LRTC_ICE_GATHERING_GATHERING = 1,
  LRTC_ICE_GATHERING_COMPLETE = 2,
} lrtc_ice_gathering_state;

typedef enum lrtc_ice_connection_state {
  LRTC_ICE_CONNECTION_NEW = 0,
  LRTC_ICE_CONNECTION_CHECKING = 1,
  LRTC_ICE_CONNECTION_COMPLETED = 2,
  LRTC_ICE_CONNECTION_CONNECTED = 3,
  LRTC_ICE_CONNECTION_FAILED = 4,
  LRTC_ICE_CONNECTION_DISCONNECTED = 5,
  LRTC_ICE_CONNECTION_CLOSED = 6,
  LRTC_ICE_CONNECTION_MAX = 7,
} lrtc_ice_connection_state;

typedef enum lrtc_data_channel_state {
  LRTC_DATA_CHANNEL_CONNECTING = 0,
  LRTC_DATA_CHANNEL_OPEN = 1,
  LRTC_DATA_CHANNEL_CLOSING = 2,
  LRTC_DATA_CHANNEL_CLOSED = 3,
} lrtc_data_channel_state;

typedef struct lrtc_ice_server_t {
  const char* uri;
  const char* username;
  const char* password;
} lrtc_ice_server_t;

typedef struct lrtc_rtc_config_t {
  lrtc_ice_server_t ice_servers[LRTC_MAX_ICE_SERVERS];
  uint32_t ice_server_count;

  lrtc_ice_transports_type ice_transports_type;
  lrtc_bundle_policy bundle_policy;
  lrtc_rtcp_mux_policy rtcp_mux_policy;
  lrtc_candidate_network_policy candidate_network_policy;
  lrtc_tcp_candidate_policy tcp_candidate_policy;

  int ice_candidate_pool_size;

  lrtc_media_security_type srtp_type;
  lrtc_sdp_semantics sdp_semantics;
  bool offer_to_receive_audio;
  bool offer_to_receive_video;

  bool disable_ipv6;
  bool disable_ipv6_on_wifi;
  int max_ipv6_networks;
  bool disable_link_local_networks;
  int screencast_min_bitrate;
  bool enable_dscp;

  bool use_rtp_mux;
  uint32_t local_audio_bandwidth;
  uint32_t local_video_bandwidth;
} lrtc_rtc_config_t;

typedef struct lrtc_audio_options_t {
  bool echo_cancellation;
  bool auto_gain_control;
  bool noise_suppression;
  bool highpass_filter;
} lrtc_audio_options_t;

typedef struct lrtc_rtp_encoding_settings_t {
  int max_bitrate_bps;            // -1 keeps current
  int min_bitrate_bps;            // -1 keeps current
  double max_framerate;           // <= 0 keeps current
  double scale_resolution_down_by;  // <= 0 keeps current
  int active;                     // -1 keeps current, 0/1 sets
  int degradation_preference;     // -1 keeps current, else lrtc_degradation_preference
  double bitrate_priority;        // < 0 keeps current
  int network_priority;           // < 0 keeps current, else RTCPriority enum
  int num_temporal_layers;        // < 0 keeps current
  const char* scalability_mode;   // null/empty keeps current
  const char* rid;                // null/empty keeps current
  int adaptive_ptime;             // -1 keeps current, 0/1 sets
} lrtc_rtp_encoding_settings_t;

typedef struct lrtc_rtp_transceiver_init_t {
  int direction;  // lrtc_rtp_transceiver_direction
  const char** stream_ids;
  uint32_t stream_id_count;
  const lrtc_rtp_encoding_settings_t* send_encodings;
  uint32_t send_encoding_count;
} lrtc_rtp_transceiver_init_t;

typedef void (LUMENRTC_CALL *lrtc_sdp_success_cb)(void* user_data,
                                                  const char* sdp,
                                                  const char* type);

typedef void (LUMENRTC_CALL *lrtc_sdp_error_cb)(void* user_data,
                                                const char* error);

typedef void (LUMENRTC_CALL *lrtc_void_cb)(void* user_data);

typedef void (LUMENRTC_CALL *lrtc_peer_connection_state_cb)(void* user_data,
                                                             int state);

typedef void (LUMENRTC_CALL *lrtc_ice_candidate_cb)(void* user_data,
                                                     const char* sdp_mid,
                                                     int sdp_mline_index,
                                                     const char* candidate);

typedef void (LUMENRTC_CALL *lrtc_data_channel_state_cb)(void* user_data,
                                                         int state);

typedef void (LUMENRTC_CALL *lrtc_data_channel_message_cb)(void* user_data,
                                                           const uint8_t* data,
                                                           int length,
                                                           int binary);

typedef void (LUMENRTC_CALL *lrtc_audio_frame_cb)(void* user_data,
                                                  const void* audio_data,
                                                  int bits_per_sample,
                                                  int sample_rate,
                                                  size_t number_of_channels,
                                                  size_t number_of_frames);

typedef void (LUMENRTC_CALL *lrtc_video_frame_cb)(void* user_data,
                                                  lrtc_video_frame_t* frame);

typedef void (LUMENRTC_CALL *lrtc_track_cb)(
    void* user_data,
    lrtc_rtp_transceiver_t* transceiver,
    lrtc_rtp_receiver_t* receiver);

typedef void (LUMENRTC_CALL *lrtc_stats_success_cb)(void* user_data,
                                                    const char* json);

typedef void (LUMENRTC_CALL *lrtc_stats_failure_cb)(void* user_data,
                                                    const char* error);

typedef struct lrtc_peer_connection_callbacks_t {
  lrtc_peer_connection_state_cb on_signaling_state;
  lrtc_peer_connection_state_cb on_peer_connection_state;
  lrtc_peer_connection_state_cb on_ice_gathering_state;
  lrtc_peer_connection_state_cb on_ice_connection_state;
  lrtc_ice_candidate_cb on_ice_candidate;
  void (LUMENRTC_CALL *on_data_channel)(void* user_data,
                                       lrtc_data_channel_t* channel);
  void (LUMENRTC_CALL *on_video_track)(void* user_data,
                                      lrtc_video_track_t* track);
  void (LUMENRTC_CALL *on_audio_track)(void* user_data,
                                      lrtc_audio_track_t* track);
  lrtc_track_cb on_track;
  lrtc_track_cb on_remove_track;
  void (LUMENRTC_CALL *on_renegotiation_needed)(void* user_data);
} lrtc_peer_connection_callbacks_t;

typedef struct lrtc_data_channel_callbacks_t {
  lrtc_data_channel_state_cb on_state_change;
  lrtc_data_channel_message_cb on_message;
} lrtc_data_channel_callbacks_t;

typedef struct lrtc_video_sink_callbacks_t {
  lrtc_video_frame_cb on_frame;
} lrtc_video_sink_callbacks_t;

typedef struct lrtc_audio_sink_callbacks_t {
  lrtc_audio_frame_cb on_data;
} lrtc_audio_sink_callbacks_t;

LUMENRTC_API lrtc_result_t LUMENRTC_CALL lrtc_initialize(void);
LUMENRTC_API void LUMENRTC_CALL lrtc_terminate(void);

LUMENRTC_API lrtc_factory_t* LUMENRTC_CALL lrtc_factory_create(void);
LUMENRTC_API lrtc_result_t LUMENRTC_CALL lrtc_factory_initialize(
    lrtc_factory_t* factory);
LUMENRTC_API void LUMENRTC_CALL lrtc_factory_terminate(
    lrtc_factory_t* factory);
LUMENRTC_API void LUMENRTC_CALL lrtc_factory_release(lrtc_factory_t* factory);

LUMENRTC_API lrtc_audio_device_t* LUMENRTC_CALL
lrtc_factory_get_audio_device(lrtc_factory_t* factory);
LUMENRTC_API lrtc_video_device_t* LUMENRTC_CALL
lrtc_factory_get_video_device(lrtc_factory_t* factory);
LUMENRTC_API lrtc_desktop_device_t* LUMENRTC_CALL
lrtc_factory_get_desktop_device(lrtc_factory_t* factory);

LUMENRTC_API lrtc_audio_source_t* LUMENRTC_CALL
lrtc_factory_create_audio_source(lrtc_factory_t* factory, const char* label,
                                 lrtc_audio_source_type source_type,
                                 const lrtc_audio_options_t* options);
LUMENRTC_API lrtc_video_source_t* LUMENRTC_CALL
lrtc_factory_create_video_source(lrtc_factory_t* factory,
                                 lrtc_video_capturer_t* capturer,
                                 const char* label,
                                 lrtc_media_constraints_t* constraints);
LUMENRTC_API lrtc_video_source_t* LUMENRTC_CALL
lrtc_factory_create_desktop_source(lrtc_factory_t* factory,
                                   lrtc_desktop_capturer_t* capturer,
                                   const char* label,
                                   lrtc_media_constraints_t* constraints);
LUMENRTC_API lrtc_audio_track_t* LUMENRTC_CALL
lrtc_factory_create_audio_track(lrtc_factory_t* factory,
                                lrtc_audio_source_t* source,
                                const char* track_id);
LUMENRTC_API lrtc_video_track_t* LUMENRTC_CALL
lrtc_factory_create_video_track(lrtc_factory_t* factory,
                                lrtc_video_source_t* source,
                                const char* track_id);
LUMENRTC_API lrtc_media_stream_t* LUMENRTC_CALL
lrtc_factory_create_stream(lrtc_factory_t* factory, const char* stream_id);

LUMENRTC_API lrtc_media_constraints_t* LUMENRTC_CALL
lrtc_media_constraints_create(void);
LUMENRTC_API void LUMENRTC_CALL lrtc_media_constraints_add_mandatory(
    lrtc_media_constraints_t* constraints, const char* key,
    const char* value);
LUMENRTC_API void LUMENRTC_CALL lrtc_media_constraints_add_optional(
    lrtc_media_constraints_t* constraints, const char* key,
    const char* value);
LUMENRTC_API void LUMENRTC_CALL lrtc_media_constraints_release(
    lrtc_media_constraints_t* constraints);

LUMENRTC_API int16_t LUMENRTC_CALL lrtc_audio_device_playout_devices(
    lrtc_audio_device_t* device);
LUMENRTC_API int16_t LUMENRTC_CALL lrtc_audio_device_recording_devices(
    lrtc_audio_device_t* device);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_playout_device_name(
    lrtc_audio_device_t* device, uint16_t index, char* name, uint32_t name_len,
    char* guid, uint32_t guid_len);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_recording_device_name(
    lrtc_audio_device_t* device, uint16_t index, char* name, uint32_t name_len,
    char* guid, uint32_t guid_len);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_set_playout_device(
    lrtc_audio_device_t* device, uint16_t index);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_set_recording_device(
    lrtc_audio_device_t* device, uint16_t index);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_set_microphone_volume(
    lrtc_audio_device_t* device, uint32_t volume);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_microphone_volume(
    lrtc_audio_device_t* device, uint32_t* volume);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_set_speaker_volume(
    lrtc_audio_device_t* device, uint32_t volume);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_audio_device_speaker_volume(
    lrtc_audio_device_t* device, uint32_t* volume);
LUMENRTC_API void LUMENRTC_CALL lrtc_audio_device_release(
    lrtc_audio_device_t* device);

LUMENRTC_API lrtc_desktop_media_list_t* LUMENRTC_CALL
lrtc_desktop_device_get_media_list(lrtc_desktop_device_t* device,
                                   lrtc_desktop_type type);
LUMENRTC_API lrtc_desktop_capturer_t* LUMENRTC_CALL
lrtc_desktop_device_create_capturer(lrtc_desktop_device_t* device,
                                    lrtc_media_source_t* source,
                                    bool show_cursor);
LUMENRTC_API void LUMENRTC_CALL lrtc_desktop_device_release(
    lrtc_desktop_device_t* device);

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_desktop_media_list_update(
    lrtc_desktop_media_list_t* list, bool force_reload, bool get_thumbnail);
LUMENRTC_API int LUMENRTC_CALL lrtc_desktop_media_list_get_source_count(
    lrtc_desktop_media_list_t* list);
LUMENRTC_API lrtc_media_source_t* LUMENRTC_CALL
lrtc_desktop_media_list_get_source(lrtc_desktop_media_list_t* list, int index);
LUMENRTC_API void LUMENRTC_CALL lrtc_desktop_media_list_release(
    lrtc_desktop_media_list_t* list);

LUMENRTC_API int32_t LUMENRTC_CALL lrtc_media_source_get_id(
    lrtc_media_source_t* source, char* buffer, uint32_t buffer_len);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_media_source_get_name(
    lrtc_media_source_t* source, char* buffer, uint32_t buffer_len);
LUMENRTC_API int LUMENRTC_CALL lrtc_media_source_get_type(
    lrtc_media_source_t* source);
LUMENRTC_API void LUMENRTC_CALL lrtc_media_source_release(
    lrtc_media_source_t* source);

LUMENRTC_API lrtc_desktop_capture_state LUMENRTC_CALL
lrtc_desktop_capturer_start(lrtc_desktop_capturer_t* capturer, uint32_t fps);
LUMENRTC_API lrtc_desktop_capture_state LUMENRTC_CALL
lrtc_desktop_capturer_start_region(lrtc_desktop_capturer_t* capturer,
                                   uint32_t fps, uint32_t x, uint32_t y,
                                   uint32_t w, uint32_t h);
LUMENRTC_API void LUMENRTC_CALL lrtc_desktop_capturer_stop(
    lrtc_desktop_capturer_t* capturer);
LUMENRTC_API bool LUMENRTC_CALL lrtc_desktop_capturer_is_running(
    lrtc_desktop_capturer_t* capturer);
LUMENRTC_API void LUMENRTC_CALL lrtc_desktop_capturer_release(
    lrtc_desktop_capturer_t* capturer);

LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_video_device_number_of_devices(
    lrtc_video_device_t* device);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_video_device_get_device_name(
    lrtc_video_device_t* device, uint32_t index, char* name,
    uint32_t name_length, char* unique_id, uint32_t unique_id_length);
LUMENRTC_API lrtc_video_capturer_t* LUMENRTC_CALL
lrtc_video_device_create_capturer(lrtc_video_device_t* device,
                                  const char* name, uint32_t index,
                                  size_t width, size_t height,
                                  size_t target_fps);
LUMENRTC_API void LUMENRTC_CALL lrtc_video_device_release(
    lrtc_video_device_t* device);

LUMENRTC_API bool LUMENRTC_CALL lrtc_video_capturer_start(
    lrtc_video_capturer_t* capturer);
LUMENRTC_API bool LUMENRTC_CALL lrtc_video_capturer_capture_started(
    lrtc_video_capturer_t* capturer);
LUMENRTC_API void LUMENRTC_CALL lrtc_video_capturer_stop(
    lrtc_video_capturer_t* capturer);
LUMENRTC_API void LUMENRTC_CALL lrtc_video_capturer_release(
    lrtc_video_capturer_t* capturer);

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_source_capture_frame(
    lrtc_audio_source_t* source, const void* audio_data, int bits_per_sample,
    int sample_rate, size_t number_of_channels, size_t number_of_frames);
LUMENRTC_API void LUMENRTC_CALL lrtc_audio_source_release(
    lrtc_audio_source_t* source);
LUMENRTC_API void LUMENRTC_CALL lrtc_video_source_release(
    lrtc_video_source_t* source);

LUMENRTC_API void LUMENRTC_CALL lrtc_audio_track_set_volume(
    lrtc_audio_track_t* track, double volume);
LUMENRTC_API void LUMENRTC_CALL lrtc_audio_track_add_sink(
    lrtc_audio_track_t* track, lrtc_audio_sink_t* sink);
LUMENRTC_API void LUMENRTC_CALL lrtc_audio_track_remove_sink(
    lrtc_audio_track_t* track, lrtc_audio_sink_t* sink);
LUMENRTC_API void LUMENRTC_CALL lrtc_audio_track_release(
    lrtc_audio_track_t* track);

LUMENRTC_API lrtc_audio_sink_t* LUMENRTC_CALL lrtc_audio_sink_create(
    const lrtc_audio_sink_callbacks_t* callbacks, void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_audio_sink_release(
    lrtc_audio_sink_t* sink);

LUMENRTC_API bool LUMENRTC_CALL lrtc_media_stream_add_audio_track(
    lrtc_media_stream_t* stream, lrtc_audio_track_t* track);
LUMENRTC_API bool LUMENRTC_CALL lrtc_media_stream_add_video_track(
    lrtc_media_stream_t* stream, lrtc_video_track_t* track);
LUMENRTC_API bool LUMENRTC_CALL lrtc_media_stream_remove_audio_track(
    lrtc_media_stream_t* stream, lrtc_audio_track_t* track);
LUMENRTC_API bool LUMENRTC_CALL lrtc_media_stream_remove_video_track(
    lrtc_media_stream_t* stream, lrtc_video_track_t* track);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_media_stream_get_id(
    lrtc_media_stream_t* stream, char* buffer, uint32_t buffer_len);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_media_stream_get_label(
    lrtc_media_stream_t* stream, char* buffer, uint32_t buffer_len);
LUMENRTC_API void LUMENRTC_CALL lrtc_media_stream_release(
    lrtc_media_stream_t* stream);

LUMENRTC_API lrtc_peer_connection_t* LUMENRTC_CALL
lrtc_peer_connection_create(lrtc_factory_t* factory,
                            const lrtc_rtc_config_t* config,
                            lrtc_media_constraints_t* constraints,
                            const lrtc_peer_connection_callbacks_t* callbacks,
                            void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_set_callbacks(
    lrtc_peer_connection_t* pc,
    const lrtc_peer_connection_callbacks_t* callbacks,
    void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_close(
    lrtc_peer_connection_t* pc);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_release(
    lrtc_peer_connection_t* pc);

LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_create_offer(
    lrtc_peer_connection_t* pc, lrtc_sdp_success_cb success,
    lrtc_sdp_error_cb failure, void* user_data,
    lrtc_media_constraints_t* constraints);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_create_answer(
    lrtc_peer_connection_t* pc, lrtc_sdp_success_cb success,
    lrtc_sdp_error_cb failure, void* user_data,
    lrtc_media_constraints_t* constraints);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_restart_ice(
    lrtc_peer_connection_t* pc);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_set_local_description(
    lrtc_peer_connection_t* pc, const char* sdp, const char* type,
    lrtc_void_cb success, lrtc_sdp_error_cb failure, void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_set_remote_description(
    lrtc_peer_connection_t* pc, const char* sdp, const char* type,
    lrtc_void_cb success, lrtc_sdp_error_cb failure, void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_local_description(
    lrtc_peer_connection_t* pc, lrtc_sdp_success_cb success,
    lrtc_sdp_error_cb failure, void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_remote_description(
    lrtc_peer_connection_t* pc, lrtc_sdp_success_cb success,
    lrtc_sdp_error_cb failure, void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_stats(
    lrtc_peer_connection_t* pc, lrtc_stats_success_cb success,
    lrtc_stats_failure_cb failure, void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_sender_stats(
    lrtc_peer_connection_t* pc, lrtc_rtp_sender_t* sender,
    lrtc_stats_success_cb success, lrtc_stats_failure_cb failure,
    void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_get_receiver_stats(
    lrtc_peer_connection_t* pc, lrtc_rtp_receiver_t* receiver,
    lrtc_stats_success_cb success, lrtc_stats_failure_cb failure,
    void* user_data);
LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_set_codec_preferences(
    lrtc_peer_connection_t* pc, lrtc_media_type media_type,
    const char** mime_types, uint32_t mime_type_count);
LUMENRTC_API int LUMENRTC_CALL
lrtc_peer_connection_set_transceiver_codec_preferences(
    lrtc_peer_connection_t* pc, lrtc_rtp_transceiver_t* transceiver,
    const char** mime_types, uint32_t mime_type_count);
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_add_ice_candidate(
    lrtc_peer_connection_t* pc, const char* sdp_mid, int sdp_mline_index,
    const char* candidate);

LUMENRTC_API bool LUMENRTC_CALL lrtc_peer_connection_add_stream(
    lrtc_peer_connection_t* pc, lrtc_media_stream_t* stream);
LUMENRTC_API bool LUMENRTC_CALL lrtc_peer_connection_remove_stream(
    lrtc_peer_connection_t* pc, lrtc_media_stream_t* stream);
LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_add_audio_track(
    lrtc_peer_connection_t* pc, lrtc_audio_track_t* track,
    const char** stream_ids, uint32_t stream_id_count);
LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_add_video_track(
    lrtc_peer_connection_t* pc, lrtc_video_track_t* track,
    const char** stream_ids, uint32_t stream_id_count);
LUMENRTC_API lrtc_rtp_sender_t* LUMENRTC_CALL
lrtc_peer_connection_add_audio_track_sender(
    lrtc_peer_connection_t* pc, lrtc_audio_track_t* track,
    const char** stream_ids, uint32_t stream_id_count);
LUMENRTC_API lrtc_rtp_sender_t* LUMENRTC_CALL
lrtc_peer_connection_add_video_track_sender(
    lrtc_peer_connection_t* pc, lrtc_video_track_t* track,
    const char** stream_ids, uint32_t stream_id_count);
LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL
lrtc_peer_connection_add_transceiver(lrtc_peer_connection_t* pc,
                                     lrtc_media_type media_type);
LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL
lrtc_peer_connection_add_audio_track_transceiver(
    lrtc_peer_connection_t* pc, lrtc_audio_track_t* track);
LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL
lrtc_peer_connection_add_video_track_transceiver(
    lrtc_peer_connection_t* pc, lrtc_video_track_t* track);
LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL
lrtc_peer_connection_add_transceiver_with_init(
    lrtc_peer_connection_t* pc, lrtc_media_type media_type,
    const lrtc_rtp_transceiver_init_t* init);
LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL
lrtc_peer_connection_add_audio_track_transceiver_with_init(
    lrtc_peer_connection_t* pc, lrtc_audio_track_t* track,
    const lrtc_rtp_transceiver_init_t* init);
LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL
lrtc_peer_connection_add_video_track_transceiver_with_init(
    lrtc_peer_connection_t* pc, lrtc_video_track_t* track,
    const lrtc_rtp_transceiver_init_t* init);
LUMENRTC_API int LUMENRTC_CALL lrtc_peer_connection_remove_track(
    lrtc_peer_connection_t* pc, lrtc_rtp_sender_t* sender);
LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_peer_connection_sender_count(
    lrtc_peer_connection_t* pc);
LUMENRTC_API lrtc_rtp_sender_t* LUMENRTC_CALL
lrtc_peer_connection_get_sender(lrtc_peer_connection_t* pc, uint32_t index);
LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_peer_connection_receiver_count(
    lrtc_peer_connection_t* pc);
LUMENRTC_API lrtc_rtp_receiver_t* LUMENRTC_CALL
lrtc_peer_connection_get_receiver(lrtc_peer_connection_t* pc, uint32_t index);
LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_peer_connection_transceiver_count(
    lrtc_peer_connection_t* pc);
LUMENRTC_API lrtc_rtp_transceiver_t* LUMENRTC_CALL
lrtc_peer_connection_get_transceiver(lrtc_peer_connection_t* pc,
                                     uint32_t index);

LUMENRTC_API lrtc_data_channel_t* LUMENRTC_CALL
lrtc_peer_connection_create_data_channel(lrtc_peer_connection_t* pc,
                                         const char* label,
                                         int ordered,
                                         int reliable,
                                         int max_retransmit_time,
                                         int max_retransmits,
                                         const char* protocol,
                                         int negotiated,
                                         int id);
LUMENRTC_API void LUMENRTC_CALL lrtc_data_channel_set_callbacks(
    lrtc_data_channel_t* channel, const lrtc_data_channel_callbacks_t* callbacks,
    void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_data_channel_send(
    lrtc_data_channel_t* channel, const uint8_t* data, uint32_t size,
    int binary);
LUMENRTC_API void LUMENRTC_CALL lrtc_data_channel_close(
    lrtc_data_channel_t* channel);
LUMENRTC_API void LUMENRTC_CALL lrtc_data_channel_release(
    lrtc_data_channel_t* channel);

LUMENRTC_API lrtc_video_sink_t* LUMENRTC_CALL lrtc_video_sink_create(
    const lrtc_video_sink_callbacks_t* callbacks, void* user_data);
LUMENRTC_API void LUMENRTC_CALL lrtc_video_sink_release(
    lrtc_video_sink_t* sink);

LUMENRTC_API void LUMENRTC_CALL lrtc_video_track_add_sink(
    lrtc_video_track_t* track, lrtc_video_sink_t* sink);
LUMENRTC_API void LUMENRTC_CALL lrtc_video_track_remove_sink(
    lrtc_video_track_t* track, lrtc_video_sink_t* sink);
LUMENRTC_API void LUMENRTC_CALL lrtc_video_track_release(
    lrtc_video_track_t* track);

LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_width(
    lrtc_video_frame_t* frame);
LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_height(
    lrtc_video_frame_t* frame);
LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_stride_y(
    lrtc_video_frame_t* frame);
LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_stride_u(
    lrtc_video_frame_t* frame);
LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_stride_v(
    lrtc_video_frame_t* frame);
LUMENRTC_API const uint8_t* LUMENRTC_CALL lrtc_video_frame_data_y(
    lrtc_video_frame_t* frame);
LUMENRTC_API const uint8_t* LUMENRTC_CALL lrtc_video_frame_data_u(
    lrtc_video_frame_t* frame);
LUMENRTC_API const uint8_t* LUMENRTC_CALL lrtc_video_frame_data_v(
    lrtc_video_frame_t* frame);
LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_copy_i420(
    lrtc_video_frame_t* frame, uint8_t* dst_y, int dst_stride_y,
    uint8_t* dst_u, int dst_stride_u, uint8_t* dst_v, int dst_stride_v);
LUMENRTC_API int LUMENRTC_CALL lrtc_video_frame_to_argb(
    lrtc_video_frame_t* frame, uint8_t* dst_argb, int dst_stride_argb,
    int dest_width, int dest_height, int format);
LUMENRTC_API lrtc_video_frame_t* LUMENRTC_CALL lrtc_video_frame_retain(
    lrtc_video_frame_t* frame);
LUMENRTC_API void LUMENRTC_CALL lrtc_video_frame_release(
    lrtc_video_frame_t* frame);

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_set_encoding_parameters(
    lrtc_rtp_sender_t* sender, const lrtc_rtp_encoding_settings_t* settings);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_set_encoding_parameters_at(
    lrtc_rtp_sender_t* sender, uint32_t index,
    const lrtc_rtp_encoding_settings_t* settings);
LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_sender_get_ssrc(
    lrtc_rtp_sender_t* sender);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_replace_audio_track(
    lrtc_rtp_sender_t* sender, lrtc_audio_track_t* track);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_replace_video_track(
    lrtc_rtp_sender_t* sender, lrtc_video_track_t* track);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_get_media_type(
    lrtc_rtp_sender_t* sender);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_sender_get_id(
    lrtc_rtp_sender_t* sender, char* buffer, uint32_t buffer_len);
LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_sender_stream_id_count(
    lrtc_rtp_sender_t* sender);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_sender_get_stream_id(
    lrtc_rtp_sender_t* sender, uint32_t index, char* buffer,
    uint32_t buffer_len);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_sender_set_stream_ids(
    lrtc_rtp_sender_t* sender, const char** stream_ids,
    uint32_t stream_id_count);
LUMENRTC_API lrtc_audio_track_t* LUMENRTC_CALL lrtc_rtp_sender_get_audio_track(
    lrtc_rtp_sender_t* sender);
LUMENRTC_API lrtc_video_track_t* LUMENRTC_CALL lrtc_rtp_sender_get_video_track(
    lrtc_rtp_sender_t* sender);
LUMENRTC_API void LUMENRTC_CALL lrtc_rtp_sender_release(
    lrtc_rtp_sender_t* sender);

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_receiver_get_media_type(
    lrtc_rtp_receiver_t* receiver);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_receiver_get_id(
    lrtc_rtp_receiver_t* receiver, char* buffer, uint32_t buffer_len);
LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_receiver_stream_id_count(
    lrtc_rtp_receiver_t* receiver);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_receiver_get_stream_id(
    lrtc_rtp_receiver_t* receiver, uint32_t index, char* buffer,
    uint32_t buffer_len);
LUMENRTC_API uint32_t LUMENRTC_CALL lrtc_rtp_receiver_stream_count(
    lrtc_rtp_receiver_t* receiver);
LUMENRTC_API lrtc_media_stream_t* LUMENRTC_CALL
lrtc_rtp_receiver_get_stream(lrtc_rtp_receiver_t* receiver, uint32_t index);
LUMENRTC_API lrtc_audio_track_t* LUMENRTC_CALL
lrtc_rtp_receiver_get_audio_track(lrtc_rtp_receiver_t* receiver);
LUMENRTC_API lrtc_video_track_t* LUMENRTC_CALL
lrtc_rtp_receiver_get_video_track(lrtc_rtp_receiver_t* receiver);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_receiver_set_jitter_buffer_min_delay(
    lrtc_rtp_receiver_t* receiver, double delay_seconds);
LUMENRTC_API void LUMENRTC_CALL lrtc_rtp_receiver_release(
    lrtc_rtp_receiver_t* receiver);

LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_media_type(
    lrtc_rtp_transceiver_t* transceiver);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_transceiver_get_mid(
    lrtc_rtp_transceiver_t* transceiver, char* buffer, uint32_t buffer_len);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_direction(
    lrtc_rtp_transceiver_t* transceiver);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_current_direction(
    lrtc_rtp_transceiver_t* transceiver);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_fired_direction(
    lrtc_rtp_transceiver_t* transceiver);
LUMENRTC_API int32_t LUMENRTC_CALL lrtc_rtp_transceiver_get_id(
    lrtc_rtp_transceiver_t* transceiver, char* buffer, uint32_t buffer_len);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_stopped(
    lrtc_rtp_transceiver_t* transceiver);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_get_stopping(
    lrtc_rtp_transceiver_t* transceiver);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_set_direction(
    lrtc_rtp_transceiver_t* transceiver, int direction, char* error,
    uint32_t error_len);
LUMENRTC_API int LUMENRTC_CALL lrtc_rtp_transceiver_stop(
    lrtc_rtp_transceiver_t* transceiver, char* error, uint32_t error_len);
LUMENRTC_API lrtc_rtp_sender_t* LUMENRTC_CALL
lrtc_rtp_transceiver_get_sender(lrtc_rtp_transceiver_t* transceiver);
LUMENRTC_API lrtc_rtp_receiver_t* LUMENRTC_CALL
lrtc_rtp_transceiver_get_receiver(lrtc_rtp_transceiver_t* transceiver);
LUMENRTC_API void LUMENRTC_CALL lrtc_rtp_transceiver_release(
    lrtc_rtp_transceiver_t* transceiver);

LUMENRTC_API void LUMENRTC_CALL
lrtc_factory_get_rtp_sender_codec_mime_types(
    lrtc_factory_t* factory, lrtc_media_type media_type,
    lrtc_stats_success_cb success, lrtc_stats_failure_cb failure,
    void* user_data);

#ifdef __cplusplus
}  // extern "C"
#endif

#endif  // LUMENRTC_H
