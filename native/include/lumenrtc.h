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
typedef struct lrtc_video_track_t lrtc_video_track_t;
typedef struct lrtc_audio_track_t lrtc_audio_track_t;
typedef struct lrtc_video_sink_t lrtc_video_sink_t;
typedef struct lrtc_video_frame_t lrtc_video_frame_t;

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

typedef void (LUMENRTC_CALL *lrtc_video_frame_cb)(void* user_data,
                                                  lrtc_video_frame_t* frame);

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
  void (LUMENRTC_CALL *on_renegotiation_needed)(void* user_data);
} lrtc_peer_connection_callbacks_t;

typedef struct lrtc_data_channel_callbacks_t {
  lrtc_data_channel_state_cb on_state_change;
  lrtc_data_channel_message_cb on_message;
} lrtc_data_channel_callbacks_t;

typedef struct lrtc_video_sink_callbacks_t {
  lrtc_video_frame_cb on_frame;
} lrtc_video_sink_callbacks_t;

LUMENRTC_API lrtc_result_t LUMENRTC_CALL lrtc_initialize(void);
LUMENRTC_API void LUMENRTC_CALL lrtc_terminate(void);

LUMENRTC_API lrtc_factory_t* LUMENRTC_CALL lrtc_factory_create(void);
LUMENRTC_API lrtc_result_t LUMENRTC_CALL lrtc_factory_initialize(
    lrtc_factory_t* factory);
LUMENRTC_API void LUMENRTC_CALL lrtc_factory_terminate(
    lrtc_factory_t* factory);
LUMENRTC_API void LUMENRTC_CALL lrtc_factory_release(lrtc_factory_t* factory);

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
LUMENRTC_API void LUMENRTC_CALL lrtc_peer_connection_add_ice_candidate(
    lrtc_peer_connection_t* pc, const char* sdp_mid, int sdp_mline_index,
    const char* candidate);

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

#ifdef __cplusplus
}  // extern "C"
#endif

#endif  // LUMENRTC_H
