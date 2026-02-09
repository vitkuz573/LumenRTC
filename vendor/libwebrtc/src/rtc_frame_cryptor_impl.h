#ifndef LIB_RTC_FRAME_CYRPTOR_IMPL_H_
#define LIB_RTC_FRAME_CYRPTOR_IMPL_H_

#include <mutex>
#include <string>

#include "rtc_frame_cryptor.h"

namespace libwebrtc {

class DefaultKeyProviderImpl : public KeyProvider {
 public:
  explicit DefaultKeyProviderImpl(KeyProviderOptions* options);
  ~DefaultKeyProviderImpl() override = default;

  bool SetSharedKey(int index, vector<uint8_t> key) override;
  vector<uint8_t> RatchetSharedKey(int key_index) override;
  vector<uint8_t> ExportSharedKey(int key_index) override;
  bool SetKey(const string participant_id, int index, vector<uint8_t> key) override;
  vector<uint8_t> RatchetKey(const string participant_id, int key_index) override;
  vector<uint8_t> ExportKey(const string participant_id, int key_index) override;
  void SetSifTrailer(vector<uint8_t> trailer) override;

 private:
  mutable std::mutex mutex_;
  bool shared_key_;
  map<int, vector<uint8_t>> shared_keys_;
  map<std::string, map<int, vector<uint8_t>>> participant_keys_;
  vector<uint8_t> sif_trailer_;
};

class RTCFrameCryptorImpl : public RTCFrameCryptor {
 public:
  RTCFrameCryptorImpl(scoped_refptr<RTCPeerConnectionFactory> factory,
                      const string participant_id, Algorithm algorithm,
                      scoped_refptr<KeyProvider> key_provider,
                      scoped_refptr<RTCRtpSender> sender);

  RTCFrameCryptorImpl(scoped_refptr<RTCPeerConnectionFactory> factory,
                      const string participant_id, Algorithm algorithm,
                      scoped_refptr<KeyProvider> key_provider,
                      scoped_refptr<RTCRtpReceiver> receiver);

  ~RTCFrameCryptorImpl() override = default;

  void RegisterRTCFrameCryptorObserver(
      scoped_refptr<RTCFrameCryptorObserver> observer) override;

  void DeRegisterRTCFrameCryptorObserver() override;

  bool SetEnabled(bool enabled) override;
  bool enabled() const override;
  bool SetKeyIndex(int index) override;
  int key_index() const override;
  const string participant_id() const override { return participant_id_; }

 private:
  string participant_id_;
  mutable std::mutex mutex_;
  bool enabled_;
  int key_index_;
  scoped_refptr<RTCFrameCryptorObserver> observer_;
};

}  // namespace libwebrtc

#endif  // LIB_RTC_FRAME_CYRPTOR_IMPL_H_
