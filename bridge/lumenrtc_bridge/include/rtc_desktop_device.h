#ifndef LUMENRTC_BRIDGE_RTC_DESKTOP_DEVICE_HXX
#define LUMENRTC_BRIDGE_RTC_DESKTOP_DEVICE_HXX

#include "rtc_types.h"
#include <map>
#include <string>

namespace lumenrtc_bridge {

class MediaSource;
class RTCDesktopCapturer;
class RTCDesktopMediaList;

class RTCDesktopDevice : public RefCountInterface {
 public:
  virtual scoped_refptr<RTCDesktopCapturer> CreateDesktopCapturer(
      scoped_refptr<MediaSource> source, bool showCursor = true) = 0;
  virtual scoped_refptr<RTCDesktopMediaList> GetDesktopMediaList(
      DesktopType type) = 0;

 protected:
  virtual ~RTCDesktopDevice() {}
};

}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_VIDEO_DEVICE_HXX