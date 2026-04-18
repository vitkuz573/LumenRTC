#ifndef LUMENRTC_BRIDGE_VIDEO_SOURCE_IMPL_HXX
#define LUMENRTC_BRIDGE_VIDEO_SOURCE_IMPL_HXX

#include "api/media_stream_interface.h"
#include "media/base/video_broadcaster.h"
#include "media/base/video_source_base.h"
#include "rtc_peerconnection_factory_impl.h"
#include "rtc_video_frame.h"
#include "rtc_video_source.h"
#include "rtc_video_track.h"

namespace lumenrtc_bridge {

class RTCVideoSourceImpl : public RTCVideoSource {
 public:
  RTCVideoSourceImpl(
      webrtc::scoped_refptr<webrtc::VideoTrackSourceInterface> video_source_track);
  virtual ~RTCVideoSourceImpl();

  virtual webrtc::scoped_refptr<webrtc::VideoTrackSourceInterface>
  rtc_source_track() {
    return rtc_source_track_;
  }

 private:
  webrtc::scoped_refptr<webrtc::VideoTrackSourceInterface> rtc_source_track_;
};
}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_VIDEO_SOURCE_IMPL_HXX
