#include <lumenrtc_bridge.h>

using namespace lumenrtc_bridge;

int main()
{
    LumenRtcBridgeRuntime::Initialize();

    auto pPeerConnectionFactory = LumenRtcBridgeRuntime::CreateRTCPeerConnectionFactory();
    pPeerConnectionFactory->Initialize();

    pPeerConnectionFactory->Terminate();

    LumenRtcBridgeRuntime::Terminate();
}