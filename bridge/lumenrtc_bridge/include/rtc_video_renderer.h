#ifndef LUMENRTC_BRIDGE_RTC_VIDEO_RENDERER_HXX
#define LUMENRTC_BRIDGE_RTC_VIDEO_RENDERER_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {

template <typename VideoFrameT>
class RTCVideoRenderer {
 public:
  virtual ~RTCVideoRenderer() {}

  virtual void OnFrame(VideoFrameT frame) = 0;
};

}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_VIDEO_RENDERER_HXX
