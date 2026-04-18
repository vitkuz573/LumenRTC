
#ifndef LUMENRTC_BRIDGE_DTMF_SENDER_INTERFACE_H_
#define LUMENRTC_BRIDGE_DTMF_SENDER_INTERFACE_H_

#include <string>

#include "api/dtmf_sender_interface.h"
#include "api/scoped_refptr.h"
#include "rtc_dtmf_sender.h"

namespace lumenrtc_bridge {

class RTCDtmfSenderImpl : public RTCDtmfSender,
                          public webrtc::DtmfSenderObserverInterface {
 public:
  RTCDtmfSenderImpl(
      webrtc::scoped_refptr<webrtc::DtmfSenderInterface> dtmf_sender);

  virtual void RegisterObserver(RTCDtmfSenderObserver* observer) override;
  virtual void UnregisterObserver() override;
  virtual bool CanInsertDtmf() override;
  virtual int duration() const override;
  virtual int inter_tone_gap() const override;
  virtual int comma_delay() const override;
  virtual bool InsertDtmf(const string tones, int duration,
                          int inter_tone_gap) override;
  virtual bool InsertDtmf(const string tones, int duration, int inter_tone_gap,
                          int comma_delay) override;
  virtual const string tones() const override;

  virtual void OnToneChange(const std::string& tone,
                            const std::string& tone_buffer) override;

  virtual void OnToneChange(const std::string& tone) override;

  webrtc::scoped_refptr<webrtc::DtmfSenderInterface> dtmf_sender();

 private:
  webrtc::scoped_refptr<webrtc::DtmfSenderInterface> dtmf_sender_;
  RTCDtmfSenderObserver* observer_;
};

}  // namespace lumenrtc_bridge

#endif  // API_DTMF_SENDER_INTERFACE_H_
