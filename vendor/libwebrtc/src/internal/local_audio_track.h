#ifndef INTERNAL_CUSTOM_LOCAL_AUDIO_TRACK_HXX
#define INTERNAL_CUSTOM_LOCAL_AUDIO_TRACK_HXX

#include <algorithm>
#include <vector>

#include "api/audio_options.h"
#include "api/media_stream_interface.h"
#include "api/notifier.h"
#include "api/scoped_refptr.h"
#include "rtc_base/synchronization/mutex.h"

namespace libwebrtc {

class LocalAudioSource : public webrtc::Notifier<webrtc::AudioSourceInterface> {
 public:
  // Creates an instance of custom local audio source.
  static webrtc::scoped_refptr<LocalAudioSource> Create(
      const webrtc::AudioOptions* audio_options);

  SourceState state() const override { return SourceState::kLive; }
  bool remote() const override { return false; }

  const webrtc::AudioOptions options() const override { return options_; }

  void AddSink(webrtc::AudioTrackSinkInterface* sink) override {
    webrtc::MutexLock lock(&sink_lock_);
    if (std::find(sinks_.begin(), sinks_.end(), sink) != sinks_.end()) {
      return;  // Already added.
    }
    sinks_.push_back(sink);
  }

  void RemoveSink(webrtc::AudioTrackSinkInterface* sink) override {
    webrtc::MutexLock lock(&sink_lock_);
    auto it = std::remove(sinks_.begin(), sinks_.end(), sink);
    if (it != sinks_.end()) {
      sinks_.erase(it, sinks_.end());
    }
  }

  void OnData(const void* audio_data, int bits_per_sample, int sample_rate,
              size_t number_of_channels, size_t number_of_frames) {
    webrtc::MutexLock lock(&sink_lock_);
    for (auto* sink : sinks_) {
      sink->OnData(audio_data, bits_per_sample, sample_rate, number_of_channels,
                   number_of_frames);
    }
  }

 protected:
  LocalAudioSource() = default;
  ~LocalAudioSource() override {
    webrtc::MutexLock lock(&sink_lock_);
    sinks_.clear();
  }

 private:
  void Initialize(const webrtc::AudioOptions* audio_options);
  mutable webrtc::Mutex sink_lock_;
  std::vector<webrtc::AudioTrackSinkInterface*> sinks_ RTC_GUARDED_BY(sink_lock_);
  webrtc::AudioOptions options_;
};
}  // namespace libwebrtc

#endif  // INTERNAL_CUSTOM_LOCAL_AUDIO_TRACK_HXX
