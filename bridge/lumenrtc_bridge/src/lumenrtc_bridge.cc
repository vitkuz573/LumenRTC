#include "lumenrtc_bridge.h"

#include <atomic>
#include <mutex>

#include "api/scoped_refptr.h"
#include "rtc_base/ssl_adapter.h"
#include "rtc_base/thread.h"
#include "rtc_peerconnection_factory_impl.h"

namespace lumenrtc_bridge {
namespace {

std::mutex g_runtime_mutex;
std::atomic<bool> g_runtime_initialized = false;

}  // namespace

bool LumenRtcBridgeRuntime::Initialize() {
  if (g_runtime_initialized.load(std::memory_order_acquire)) {
    return true;
  }

  std::lock_guard<std::mutex> lock(g_runtime_mutex);
  if (g_runtime_initialized.load(std::memory_order_relaxed)) {
    return true;
  }

  if (!webrtc::InitializeSSL()) {
    return false;
  }

  g_runtime_initialized.store(true, std::memory_order_release);
  return true;
}

void LumenRtcBridgeRuntime::Terminate() {
  std::lock_guard<std::mutex> lock(g_runtime_mutex);
  if (!g_runtime_initialized.exchange(false, std::memory_order_acq_rel)) {
    return;
  }

  webrtc::ThreadManager::Instance()->SetCurrentThread(nullptr);
  webrtc::CleanupSSL();
}

scoped_refptr<RTCPeerConnectionFactory>
LumenRtcBridgeRuntime::CreateRTCPeerConnectionFactory() {
  return scoped_refptr<RTCPeerConnectionFactory>(
      new RefCountedObject<RTCPeerConnectionFactoryImpl>());
}

}  // namespace lumenrtc_bridge
