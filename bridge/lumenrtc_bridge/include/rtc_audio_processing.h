#ifndef LUMENRTC_BRIDGE_RTC_AUDIO_PROCESSING_HXX
#define LUMENRTC_BRIDGE_RTC_AUDIO_PROCESSING_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {

class RTCAudioProcessing : public RefCountInterface {
 public:
  class CustomProcessing {
   public:
    virtual void Initialize(int sample_rate_hz, int num_channels) = 0;

    virtual void Process(int num_bands, int num_frames, int buffer_size,
                         float* buffer) = 0;

    virtual void Reset(int new_rate) = 0;

    virtual void Release() = 0;

   protected:
    virtual ~CustomProcessing() {}
  };

 public:
  virtual void SetCapturePostProcessing(
      CustomProcessing* capture_post_processing) = 0;

  virtual void SetRenderPreProcessing(
      CustomProcessing* render_pre_processing) = 0;
};

}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_AUDIO_PROCESSING_HXX