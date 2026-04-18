#ifndef INTERNAL_JPEG_UTIL_HXX
#define INTERNAL_JPEG_UTIL_HXX

#include <inttypes.h>

#include <vector>

namespace lumenrtc_bridge {
// Encodes the given RGB data into a JPEG image.
std::vector<unsigned char> EncodeRGBToJpeg(const unsigned char* data, int width,
                                           int height, int color_planes,
                                           int quality);
}  // namespace lumenrtc_bridge

#endif  // INTERNAL_JPEG_UTIL_HXX
