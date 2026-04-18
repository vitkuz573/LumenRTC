#ifndef LUMENRTC_BRIDGE_RTC_MEDIA_STREAM_HXX
#define LUMENRTC_BRIDGE_RTC_MEDIA_STREAM_HXX

#include "rtc_audio_track.h"
#include "rtc_types.h"
#include "rtc_video_track.h"

namespace lumenrtc_bridge {

class RTCMediaStream : public RefCountInterface {
 public:
  virtual bool AddTrack(scoped_refptr<RTCAudioTrack> track) = 0;

  virtual bool AddTrack(scoped_refptr<RTCVideoTrack> track) = 0;

  virtual bool RemoveTrack(scoped_refptr<RTCAudioTrack> track) = 0;

  virtual bool RemoveTrack(scoped_refptr<RTCVideoTrack> track) = 0;

  virtual vector<scoped_refptr<RTCAudioTrack>> audio_tracks() = 0;

  virtual vector<scoped_refptr<RTCVideoTrack>> video_tracks() = 0;

  virtual vector<scoped_refptr<RTCMediaTrack>> tracks() = 0;

  virtual scoped_refptr<RTCAudioTrack> FindAudioTrack(
      const string track_id) = 0;

  virtual scoped_refptr<RTCVideoTrack> FindVideoTrack(
      const string track_id) = 0;

  virtual const string label() = 0;

  virtual const string id() = 0;

 protected:
  ~RTCMediaStream() {}
};

}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_MEDIA_STREAM_HXX
