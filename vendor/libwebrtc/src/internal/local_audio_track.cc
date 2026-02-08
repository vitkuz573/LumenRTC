#include "src/internal/local_audio_track.h"

namespace libwebrtc {

webrtc::scoped_refptr<LocalAudioSource> LocalAudioSource::Create(
    const webrtc::AudioOptions* audio_options) {
  auto source = webrtc::make_ref_counted<LocalAudioSource>();
  source->Initialize(audio_options);
  return source;
}

void LocalAudioSource::Initialize(const webrtc::AudioOptions* audio_options) {
  if (!audio_options) return;

  options_ = *audio_options;
}

}  // namespace libwebrtc
