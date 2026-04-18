#ifndef HELPER_HXX
#define HELPER_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {
class Helper {
 public:
  LUMENRTC_BRIDGE_API static string CreateRandomUuid();
};
}  // namespace lumenrtc_bridge

#endif  // HELPER_HXX
