#ifndef LUMENRTC_BRIDGE_RTC_MEDIA_TRACK_HXX
#define LUMENRTC_BRIDGE_RTC_MEDIA_TRACK_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {

class AudioTrackSink {
 public:
  virtual void OnData(const void* audio_data, int bits_per_sample,
                      int sample_rate, size_t number_of_channels,
                      size_t number_of_frames) = 0;
 protected:
  virtual ~AudioTrackSink() {}
};

/*Media Track interface*/
class RTCMediaTrack : public RefCountInterface {
 public:
  enum RTCTrackState {
    kLive,
    kEnded,
  };
  virtual RTCTrackState state() const = 0;

  /*track type: audio/video*/
  virtual const string kind() const = 0;

  /*track id*/
  virtual const string id() const = 0;

  virtual bool enabled() const = 0;

  /*mute track*/
  virtual bool set_enabled(bool enable) = 0;

 protected:
  ~RTCMediaTrack() {}
};

}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_MEDIA_TRACK_HXX
