#ifndef LUMENRTC_BRIDGE_RTC_SESSION_DESCRIPTION_HXX
#define LUMENRTC_BRIDGE_RTC_SESSION_DESCRIPTION_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {

class RTCSessionDescription : public RefCountInterface {
 public:
  enum SdpType { kOffer = 0, kPrAnswer, kAnswer };

  static LUMENRTC_BRIDGE_API scoped_refptr<RTCSessionDescription> Create(
      const string type, const string sdp, SdpParseError* error);

 public:
  virtual const string sdp() const = 0;

  virtual const string type() = 0;

  virtual SdpType GetType() = 0;

  virtual bool ToString(string& out) = 0;

 protected:
  virtual ~RTCSessionDescription() {}
};

}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_SESSION_DESCRIPTION_HXX