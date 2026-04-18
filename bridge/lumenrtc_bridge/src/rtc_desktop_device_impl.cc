#include "rtc_desktop_device_impl.h"

#include "rtc_base/thread.h"
#include "rtc_desktop_capturer.h"
#include "rtc_desktop_media_list.h"
#include "rtc_video_device_impl.h"

namespace lumenrtc_bridge {

RTCDesktopDeviceImpl::RTCDesktopDeviceImpl(webrtc::Thread* signaling_thread)
    : signaling_thread_(signaling_thread) {}

RTCDesktopDeviceImpl::~RTCDesktopDeviceImpl() = default;

scoped_refptr<RTCDesktopCapturer> RTCDesktopDeviceImpl::CreateDesktopCapturer(
    scoped_refptr<MediaSource> source, bool showCursor) {
  if (!source) {
    return nullptr;
  }

  auto* source_impl = static_cast<MediaSourceImpl*>(source.get());
  return new RefCountedObject<RTCDesktopCapturerImpl>(
      source_impl->type(), source_impl->source_id(), signaling_thread_, source,
      showCursor);
}

scoped_refptr<RTCDesktopMediaList> RTCDesktopDeviceImpl::GetDesktopMediaList(
    DesktopType type) {
  auto it = desktop_media_lists_.find(type);
  if (it != desktop_media_lists_.end()) {
    return it->second;
  }

  auto media_list = scoped_refptr<RTCDesktopMediaListImpl>(
      new RefCountedObject<RTCDesktopMediaListImpl>(type, signaling_thread_));
  desktop_media_lists_.emplace(type, media_list);
  return media_list;
}

}  // namespace lumenrtc_bridge
