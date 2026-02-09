#include "rtc_frame_cryptor_impl.h"

#include <mutex>
#include <utility>

#include "base/refcountedobject.h"

namespace libwebrtc {

scoped_refptr<RTCFrameCryptor> FrameCryptorFactory::frameCryptorFromRtpSender(
    scoped_refptr<RTCPeerConnectionFactory> factory,
    const string participant_id, scoped_refptr<RTCRtpSender> sender,
    Algorithm algorithm, scoped_refptr<KeyProvider> key_provider) {
  return new RefCountedObject<RTCFrameCryptorImpl>(
      factory, participant_id, algorithm, key_provider, sender);
}

scoped_refptr<RTCFrameCryptor> FrameCryptorFactory::frameCryptorFromRtpReceiver(
    scoped_refptr<RTCPeerConnectionFactory> factory,
    const string participant_id, scoped_refptr<RTCRtpReceiver> receiver,
    Algorithm algorithm, scoped_refptr<KeyProvider> key_provider) {
  return new RefCountedObject<RTCFrameCryptorImpl>(
      factory, participant_id, algorithm, key_provider, receiver);
}

DefaultKeyProviderImpl::DefaultKeyProviderImpl(KeyProviderOptions* options)
    : shared_key_(options != nullptr && options->shared_key) {}

bool DefaultKeyProviderImpl::SetSharedKey(int index, vector<uint8_t> key) {
  std::lock_guard<std::mutex> lock(mutex_);
  shared_keys_[index] = std::move(key);
  return true;
}

vector<uint8_t> DefaultKeyProviderImpl::RatchetSharedKey(int key_index) {
  return ExportSharedKey(key_index);
}

vector<uint8_t> DefaultKeyProviderImpl::ExportSharedKey(int key_index) {
  auto it = shared_keys_.find(key_index);
  if (it == shared_keys_.end()) {
    return {};
  }

  return it->second;
}

bool DefaultKeyProviderImpl::SetKey(const string participant_id, int index,
                                    vector<uint8_t> key) {
  std::lock_guard<std::mutex> lock(mutex_);
  participant_keys_[participant_id.std_string()][index] = std::move(key);
  return true;
}

vector<uint8_t> DefaultKeyProviderImpl::RatchetKey(const string participant_id,
                                                   int key_index) {
  return ExportKey(participant_id, key_index);
}

vector<uint8_t> DefaultKeyProviderImpl::ExportKey(const string participant_id,
                                                  int key_index) {
  std::lock_guard<std::mutex> lock(mutex_);
  auto participant_it = participant_keys_.find(participant_id.std_string());
  if (participant_it == participant_keys_.end()) {
    return shared_key_ ? ExportSharedKey(key_index) : vector<uint8_t>();
  }

  auto& keys = participant_it->second;
  auto key_it = keys.find(key_index);
  if (key_it == keys.end()) {
    return shared_key_ ? ExportSharedKey(key_index) : vector<uint8_t>();
  }

  return key_it->second;
}

void DefaultKeyProviderImpl::SetSifTrailer(vector<uint8_t> trailer) {
  std::lock_guard<std::mutex> lock(mutex_);
  sif_trailer_ = std::move(trailer);
}

RTCFrameCryptorImpl::RTCFrameCryptorImpl(
    scoped_refptr<RTCPeerConnectionFactory> factory,
    const string participant_id, Algorithm algorithm,
    scoped_refptr<KeyProvider> key_provider, scoped_refptr<RTCRtpSender> sender)
    : participant_id_(participant_id), enabled_(false), key_index_(0) {
  (void)factory;
  (void)algorithm;
  (void)key_provider;
  (void)sender;
}

RTCFrameCryptorImpl::RTCFrameCryptorImpl(
    scoped_refptr<RTCPeerConnectionFactory> factory,
    const string participant_id, Algorithm algorithm,
    scoped_refptr<KeyProvider> key_provider,
    scoped_refptr<RTCRtpReceiver> receiver)
    : participant_id_(participant_id), enabled_(false), key_index_(0) {
  (void)factory;
  (void)algorithm;
  (void)key_provider;
  (void)receiver;
}

bool RTCFrameCryptorImpl::SetEnabled(bool enabled) {
  std::lock_guard<std::mutex> lock(mutex_);
  enabled_ = enabled;
  return true;
}

bool RTCFrameCryptorImpl::enabled() const {
  std::lock_guard<std::mutex> lock(mutex_);
  return enabled_;
}

bool RTCFrameCryptorImpl::SetKeyIndex(int index) {
  std::lock_guard<std::mutex> lock(mutex_);
  key_index_ = index;
  return true;
}

int RTCFrameCryptorImpl::key_index() const {
  std::lock_guard<std::mutex> lock(mutex_);
  return key_index_;
}

void RTCFrameCryptorImpl::RegisterRTCFrameCryptorObserver(
    scoped_refptr<RTCFrameCryptorObserver> observer) {
  std::lock_guard<std::mutex> lock(mutex_);
  observer_ = observer;
}

void RTCFrameCryptorImpl::DeRegisterRTCFrameCryptorObserver() {
  std::lock_guard<std::mutex> lock(mutex_);
  observer_ = nullptr;
}

scoped_refptr<KeyProvider> KeyProvider::Create(KeyProviderOptions* options) {
  return new RefCountedObject<DefaultKeyProviderImpl>(options);
}

}  // namespace libwebrtc
