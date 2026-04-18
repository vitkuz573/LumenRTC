#ifndef LUMENRTC_BRIDGE_RTC_MEDIA_CONSTRAINTS_HXX
#define LUMENRTC_BRIDGE_RTC_MEDIA_CONSTRAINTS_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {

class RTCMediaConstraints : public RefCountInterface {
 public:
  // These keys are google specific.
  LUMENRTC_BRIDGE_API static const char*
      kGoogEchoCancellation;  // googEchoCancellation

  LUMENRTC_BRIDGE_API static const char*
      kExtendedFilterEchoCancellation;  // googEchoCancellation2
  LUMENRTC_BRIDGE_API static const char*
      kDAEchoCancellation;                             // googDAEchoCancellation
  LUMENRTC_BRIDGE_API static const char* kAutoGainControl;  // googAutoGainControl
  LUMENRTC_BRIDGE_API static const char* kNoiseSuppression;  // googNoiseSuppression
  LUMENRTC_BRIDGE_API static const char* kHighpassFilter;    // googHighpassFilter
  LUMENRTC_BRIDGE_API static const char* kAudioMirroring;    // googAudioMirroring
  LUMENRTC_BRIDGE_API static const char*
      kAudioNetworkAdaptorConfig;  // goodAudioNetworkAdaptorConfig

  // Constraint keys for CreateOffer / CreateAnswer
  // Specified by the W3C PeerConnection spec
  LUMENRTC_BRIDGE_API static const char*
      kOfferToReceiveVideo;  // OfferToReceiveVideo
  LUMENRTC_BRIDGE_API static const char*
      kOfferToReceiveAudio;  // OfferToReceiveAudio
  LUMENRTC_BRIDGE_API static const char*
      kVoiceActivityDetection;                    // VoiceActivityDetection
  LUMENRTC_BRIDGE_API static const char* kIceRestart;  // IceRestart
  // These keys are google specific.
  LUMENRTC_BRIDGE_API static const char* kUseRtpMux;  // googUseRtpMUX

  // Constraints values.
  LUMENRTC_BRIDGE_API static const char* kValueTrue;   // true
  LUMENRTC_BRIDGE_API static const char* kValueFalse;  // false

  // PeerConnection constraint keys.
  // Temporary pseudo-constraints used to enable DataChannels
  LUMENRTC_BRIDGE_API static const char*
      kEnableRtpDataChannels;  // Enable RTP DataChannels
  // Google-specific constraint keys.
  // Temporary pseudo-constraint for enabling DSCP through JS.
  LUMENRTC_BRIDGE_API static const char* kEnableDscp;  // googDscp
  // Constraint to enable IPv6 through JS.
  LUMENRTC_BRIDGE_API static const char* kEnableIPv6;  // googIPv6
  // Temporary constraint to enable suspend below min bitrate feature.
  LUMENRTC_BRIDGE_API static const char* kEnableVideoSuspendBelowMinBitrate;
  // googSuspendBelowMinBitrate
  // Constraint to enable combined audio+video bandwidth estimation.
  //LUMENRTC_BRIDGE_API static const char*
  //    kCombinedAudioVideoBwe;  // googCombinedAudioVideoBwe
  LUMENRTC_BRIDGE_API static const char*
      kScreencastMinBitrate;  // googScreencastMinBitrate
  LUMENRTC_BRIDGE_API static const char*
      kCpuOveruseDetection;  // googCpuOveruseDetection

  // Specifies number of simulcast layers for all video tracks
  // with a Plan B offer/answer
  // (see RTCOfferAnswerOptions::num_simulcast_layers).
  LUMENRTC_BRIDGE_API static const char* kNumSimulcastLayers;

 public:
  LUMENRTC_BRIDGE_API static scoped_refptr<RTCMediaConstraints> Create();

  virtual void AddMandatoryConstraint(const string key, const string value) = 0;

  virtual void AddOptionalConstraint(const string key, const string value) = 0;

 protected:
  virtual ~RTCMediaConstraints() {}
};

}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_MEDIA_CONSTRAINTS_HXX
