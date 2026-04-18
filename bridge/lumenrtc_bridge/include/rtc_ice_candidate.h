#ifndef LUMENRTC_BRIDGE_RTC_ICE_CANDIDATE_HXX
#define LUMENRTC_BRIDGE_RTC_ICE_CANDIDATE_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {

class RTCIceCandidate : public RefCountInterface {
 public:
  static LUMENRTC_BRIDGE_API scoped_refptr<RTCIceCandidate> Create(
      const string sdp, const string sdp_mid, int sdp_mline_index,
      SdpParseError* error);

 public:
  virtual const string candidate() const = 0;

  virtual const string sdp_mid() const = 0;

  virtual int sdp_mline_index() const = 0;

  virtual bool ToString(string& out) = 0;

 protected:
  virtual ~RTCIceCandidate() {}
};

}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_ICE_CANDIDATE_HXX
