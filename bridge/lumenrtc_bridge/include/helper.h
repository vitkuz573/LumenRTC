#ifndef HELPER_HXX
#define HELPER_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {
/**
 * @brief A helper class with static methods for generating random UUIDs.
 *
 */
class Helper {
 public:
  /**
   * @brief Generates a random UUID string.
   *
   * @return The generated UUID string.
   */
  LUMENRTC_BRIDGE_API static string CreateRandomUuid();
};
}  // namespace lumenrtc_bridge

#endif  // HELPER_HXX
