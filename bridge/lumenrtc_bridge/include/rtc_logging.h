#ifndef LUMENRTC_BRIDGE_RTC_LOGGING_HXX
#define LUMENRTC_BRIDGE_RTC_LOGGING_HXX

#include "rtc_types.h"

namespace lumenrtc_bridge {

  enum RTCLoggingSeverity {
    Verbose,
    Info,
    Warning,
    Error,
    None,
  };

  typedef void (*RTCCallbackLoggerMessageHandler)(const string& message);

  class LumenRtcBridgeRuntimeLogging {
    public:
      LUMENRTC_BRIDGE_API static void setMinDebugLogLevel(RTCLoggingSeverity severity);
      LUMENRTC_BRIDGE_API static void setLogSink(RTCLoggingSeverity severity, RTCCallbackLoggerMessageHandler callbackHandler);
      LUMENRTC_BRIDGE_API static void removeLogSink();
  };
}  // namespace lumenrtc_bridge

#endif  // LUMENRTC_BRIDGE_RTC_LOGGING_HXX
