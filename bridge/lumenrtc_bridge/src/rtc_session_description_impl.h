#ifndef LUMENRTC_BRIDGE_RTC_SESSION_DESCRIPTION_IMPL_HXX
#define LUMENRTC_BRIDGE_RTC_SESSION_DESCRIPTION_IMPL_HXX
#include "api/jsep.h"
#include "rtc_session_description.h"
#include "rtc_types.h"

namespace lumenrtc_bridge {

class RTCSessionDescriptionImpl : public RTCSessionDescription {
 public:
  RTCSessionDescriptionImpl(
      std::unique_ptr<webrtc::SessionDescriptionInterface> description);
  virtual ~RTCSessionDescriptionImpl() {}

  virtual const string sdp() const override;

  virtual SdpType GetType() override;

  virtual const string type() override;

  virtual bool ToString(string& out) override;

  webrtc::SessionDescriptionInterface* description() {
    return description_.get();
  }

 private:
  std::unique_ptr<webrtc::SessionDescriptionInterface> description_;
  std::string sdp_;
  std::string type_;
};

}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_SESSION_DESCRIPTION_IMPL_HXX