#ifndef LIB_WEBRTC_AUDIO_SOURCE_IMPL_HXX
#define LIB_WEBRTC_AUDIO_SOURCE_IMPL_HXX

#include "api/media_stream_interface.h"
#include "rtc_audio_source.h"
#include "rtc_base/logging.h"
#include "src/internal/local_audio_track.h"

namespace libwebrtc {

class RTCAudioSourceImpl : public RTCAudioSource {
 public:
  RTCAudioSourceImpl(
      webrtc::scoped_refptr<webrtc::AudioSourceInterface> rtc_audio_source,
      webrtc::scoped_refptr<libwebrtc::LocalAudioSource> custom_audio_source,
      SourceType source_type);

  void CaptureFrame(const void* audio_data, int bits_per_sample,
                    int sample_rate, size_t number_of_channels,
                    size_t number_of_frames) override {
    if (source_type_ != SourceType::kCustom || !custom_audio_source_ ||
        !audio_data) {
      return;
    }
    custom_audio_source_->OnData(audio_data, bits_per_sample, sample_rate,
                                 number_of_channels, number_of_frames);
  }

  SourceType GetSourceType() const override { return source_type_; }

  virtual ~RTCAudioSourceImpl();

  webrtc::scoped_refptr<webrtc::AudioSourceInterface> rtc_audio_source() {
    return rtc_audio_source_;
  }

 private:
  webrtc::scoped_refptr<webrtc::AudioSourceInterface> rtc_audio_source_;
  webrtc::scoped_refptr<libwebrtc::LocalAudioSource> custom_audio_source_;
  SourceType source_type_;
};

}  // namespace libwebrtc

#endif  // LIB_WEBRTC_AUDIO_SOURCE_IMPL_HXX
