#ifndef LUMENRTC_BRIDGE_RTC_VIDEO_SOURCE_HXX
#define LUMENRTC_BRIDGE_RTC_VIDEO_SOURCE_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {

class RTCVideoSource : public RefCountInterface {
 public:
  ~RTCVideoSource() {}
};
}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_VIDEO_SOURCE_HXX
