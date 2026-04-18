/*
 * Copyright 2022 LiveKit
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#include "rtc_desktop_media_list_impl.h"

#include <unordered_map>
#include <unordered_set>

#include "internal/jpeg_util.h"
#include "rtc_base/checks.h"
#include "rtc_base/logging.h"
#include "third_party/libyuv/include/libyuv.h"

#ifdef WEBRTC_WIN
#include "modules/desktop_capture/win/window_capture_utils.h"
#include <windows.h>
#endif

namespace lumenrtc_bridge {

#ifdef WEBRTC_WIN
namespace {
bool TryBindCurrentThreadToInputDesktop() {
  HDESK input_desktop =
      ::OpenInputDesktop(0, FALSE, DESKTOP_READOBJECTS | DESKTOP_SWITCHDESKTOP);
  if (input_desktop == nullptr) {
    RTC_LOG(LS_WARNING) << "OpenInputDesktop failed: " << ::GetLastError();
    return false;
  }

  const BOOL set_result = ::SetThreadDesktop(input_desktop);
  const DWORD set_error = set_result ? ERROR_SUCCESS : ::GetLastError();
  ::CloseDesktop(input_desktop);

  if (!set_result) {
    RTC_LOG(LS_WARNING) << "SetThreadDesktop failed: " << set_error;
    return false;
  }

  return true;
}
}  // namespace
#endif

namespace {
constexpr int kThumbnailMaxEdge = 320;
}

RTCDesktopMediaListImpl::RTCDesktopMediaListImpl(DesktopType type,
                                                 webrtc::Thread* signaling_thread)
    : thread_(webrtc::Thread::Create()),
      type_(type),
      signaling_thread_(signaling_thread) {
  RTC_DCHECK(thread_);
  thread_->Start();
  options_ = webrtc::DesktopCaptureOptions::CreateDefault();
  options_.set_detect_updated_region(true);
#ifdef WEBRTC_WIN
  options_.set_allow_directx_capturer(true);
#endif
#ifdef WEBRTC_LINUX
  if (type == kScreen) {
    options_.set_allow_pipewire(true);
  }
#endif
  callback_ = std::make_unique<CallbackProxy>();
  callback_->SetCallback(
      [this](webrtc::DesktopCapturer::Result result,
             std::unique_ptr<webrtc::DesktopFrame> frame) {
        OnThumbnailCaptureResult(result, std::move(frame));
      });
  thread_->BlockingCall([this, type] {
#ifdef WEBRTC_WIN
    const bool desktop_bound = TryBindCurrentThreadToInputDesktop();
    if (!desktop_bound) {
      options_.set_allow_directx_capturer(false);
      RTC_LOG(LS_WARNING)
          << "Desktop media list forcing GDI path (allow_directx_capturer=false). "
          << "desktop_bound=" << desktop_bound;
    }
#endif
    if (type == kScreen) {
      capturer_ = webrtc::DesktopCapturer::CreateScreenCapturer(options_);
    } else {
      capturer_ = webrtc::DesktopCapturer::CreateWindowCapturer(options_);
    }
    if (!capturer_) {
      RTC_LOG(LS_ERROR) << "Failed to create desktop media list capturer.";
      return;
    }

    capturer_->Start(callback_.get());
  });
}

RTCDesktopMediaListImpl::~RTCDesktopMediaListImpl() {
  if (callback_) {
    callback_->SetCallback(nullptr);
  }
  thread_->Stop();
}

int32_t RTCDesktopMediaListImpl::UpdateSourceList(bool force_reload,
                                                  bool get_thumbnail) {
  if (!capturer_) {
    RTC_LOG(LS_ERROR) << "Desktop media list capturer is not initialized.";
    return -1;
  }

  if (force_reload) {
    for (const auto& source : sources_) {
      if (observer_) {
        auto* source_ptr = source.get();
        signaling_thread_->BlockingCall(
            [&, source_ptr]() { observer_->OnMediaSourceRemoved(source_ptr); });
      }
    }
    sources_.clear();
  }

  webrtc::DesktopCapturer::SourceList new_sources;
  bool source_list_retrieved = false;
  thread_->BlockingCall([this, &new_sources, &source_list_retrieved] {
    if (!capturer_) {
      return;
    }
    source_list_retrieved = capturer_->GetSourceList(&new_sources);
  });

  if (!source_list_retrieved) {
    RTC_LOG(LS_WARNING) << "Failed to retrieve desktop source list.";
    return -1;
  }

  for (size_t i = 0; i < new_sources.size(); ++i) {
    if (type_ == kScreen && new_sources[i].title.empty()) {
      new_sources[i].title = std::string("Screen " + std::to_string(i + 1));
    }
  }

  std::unordered_map<webrtc::DesktopCapturer::SourceId,
                     scoped_refptr<MediaSourceImpl>> existing_by_id;
  existing_by_id.reserve(sources_.size());
  for (const auto& source : sources_) {
    existing_by_id.emplace(source->source_id(), source);
  }

  std::unordered_set<webrtc::DesktopCapturer::SourceId> new_ids;
  new_ids.reserve(new_sources.size());

  std::vector<scoped_refptr<MediaSourceImpl>> reordered_sources;
  reordered_sources.reserve(new_sources.size());

  for (const auto& new_source : new_sources) {
    new_ids.insert(new_source.id);
    auto existing = existing_by_id.find(new_source.id);
    if (existing == existing_by_id.end()) {
      auto source = scoped_refptr<MediaSourceImpl>(
          new RefCountedObject<MediaSourceImpl>(this, new_source, type_));
      reordered_sources.push_back(source);
      GetThumbnail(source, get_thumbnail);
      if (observer_) {
        signaling_thread_->BlockingCall(
            [&, source]() { observer_->OnMediaSourceAdded(source); });
      }
      continue;
    }

    auto source = existing->second;
    if (source->source.title != new_source.title) {
      source->source.title = new_source.title;
      if (observer_) {
        signaling_thread_->BlockingCall(
            [&, source]() { observer_->OnMediaSourceNameChanged(source); });
      }
    }

    reordered_sources.push_back(source);
  }

  for (const auto& source : sources_) {
    if (new_ids.find(source->source_id()) != new_ids.end()) {
      continue;
    }

    if (observer_) {
      auto* source_ptr = source.get();
      signaling_thread_->BlockingCall(
          [&, source_ptr]() { observer_->OnMediaSourceRemoved(source_ptr); });
    }
  }

  sources_ = std::move(reordered_sources);

  if (get_thumbnail) {
    for (const auto& source : sources_) {
      GetThumbnail(source.get(), true);
    }
  }

  return static_cast<int32_t>(sources_.size());
}

bool RTCDesktopMediaListImpl::GetThumbnail(scoped_refptr<MediaSource> source,
                                           bool notify) {
  thread_->PostTask([this, source, notify] {
    if (!capturer_) {
      return;
    }

    MediaSourceImpl* source_impl = static_cast<MediaSourceImpl*>(source.get());
    if (!source_impl) {
      return;
    }

    pending_thumbnail_requests_.push_back(
        {scoped_refptr<MediaSourceImpl>(source_impl), notify});
    TryStartNextThumbnailCapture();
  });
  return true;
}

int RTCDesktopMediaListImpl::GetSourceCount() const { return sources_.size(); }

scoped_refptr<MediaSource> RTCDesktopMediaListImpl::GetSource(int index) {
  if (index < 0 || index >= static_cast<int>(sources_.size())) {
    return nullptr;
  }
  return sources_[index];
}

void RTCDesktopMediaListImpl::TryStartNextThumbnailCapture() {
  if (!capturer_ || thumbnail_capture_in_flight_ ||
      pending_thumbnail_requests_.empty()) {
    return;
  }

  while (!pending_thumbnail_requests_.empty()) {
    auto& request = pending_thumbnail_requests_.front();
    if (!request.source.get()) {
      pending_thumbnail_requests_.pop_front();
      continue;
    }

    if (!capturer_->SelectSource(request.source->source_id())) {
      pending_thumbnail_requests_.pop_front();
      continue;
    }

    thumbnail_capture_in_flight_ = true;
    capturer_->CaptureFrame();
    return;
  }
}

void RTCDesktopMediaListImpl::OnThumbnailCaptureResult(
    webrtc::DesktopCapturer::Result result,
    std::unique_ptr<webrtc::DesktopFrame> frame) {
  if (pending_thumbnail_requests_.empty()) {
    thumbnail_capture_in_flight_ = false;
    return;
  }

  PendingThumbnailRequest request = pending_thumbnail_requests_.front();
  pending_thumbnail_requests_.pop_front();
  thumbnail_capture_in_flight_ = false;

  if (request.source.get()) {
    request.source->SaveCaptureResult(result, std::move(frame));

    if (observer_ && request.notify) {
      MediaSourceImpl* source = request.source.get();
      signaling_thread_->BlockingCall([this, source]() {
        if (observer_) {
          observer_->OnMediaSourceThumbnailChanged(source);
        }
      });
    }
  }

  TryStartNextThumbnailCapture();
}

bool MediaSourceImpl::UpdateThumbnail() {
  return mediaList_->GetThumbnail(this);
}

#ifdef WEBRTC_WIN
extern int filterException(int code, PEXCEPTION_POINTERS ex);
#endif

void MediaSourceImpl::SaveCaptureResult(
    webrtc::DesktopCapturer::Result result,
    std::unique_ptr<webrtc::DesktopFrame> frame) {
  if (result != webrtc::DesktopCapturer::Result::SUCCESS || !frame) {
    return;
  }

  int width = frame->size().width();
  int height = frame->size().height();
#ifdef WEBRTC_WIN
  webrtc::DesktopRect rect_ = webrtc::DesktopRect::MakeWH(width, height);

  if (type_ != kScreen) {
    webrtc::GetWindowRect(reinterpret_cast<HWND>(source_id()), &rect_);
  }

  __try
#endif
  {

    if (!i420_buffer_ || !i420_buffer_.get() ||
        i420_buffer_->width() * i420_buffer_->height() != width * height) {
      i420_buffer_ = webrtc::I420Buffer::Create(width, height);
    }

    const int convert_result = libyuv::ConvertToI420(
        frame->data(), 0, i420_buffer_->MutableDataY(),
        i420_buffer_->StrideY(), i420_buffer_->MutableDataU(),
        i420_buffer_->StrideU(), i420_buffer_->MutableDataV(),
        i420_buffer_->StrideV(), 0, 0,
#ifdef WEBRTC_WIN
        rect_.width(), rect_.height(),
#else
        width, height,
#endif
        width, height, libyuv::kRotate0, libyuv::FOURCC_ARGB);

    if (convert_result < 0) {
      RTC_LOG(LS_WARNING) << "Failed to convert thumbnail frame to I420.";
      return;
    }

    webrtc::VideoFrame input_frame(i420_buffer_, 0, 0,
                                   webrtc::kVideoRotation_0);

    int thumbnail_width = input_frame.width();
    int thumbnail_height = input_frame.height();
    webrtc::scoped_refptr<webrtc::I420BufferInterface> thumbnail_buffer =
        i420_buffer_;

    const int source_max_edge =
        thumbnail_width > thumbnail_height ? thumbnail_width : thumbnail_height;
    if (source_max_edge > kThumbnailMaxEdge) {
      const double scale =
          static_cast<double>(kThumbnailMaxEdge) / source_max_edge;
      thumbnail_width = static_cast<int>(thumbnail_width * scale);
      thumbnail_height = static_cast<int>(thumbnail_height * scale);
      if (thumbnail_width < 1) {
        thumbnail_width = 1;
      }
      if (thumbnail_height < 1) {
        thumbnail_height = 1;
      }

      webrtc::scoped_refptr<webrtc::I420Buffer> scaled_buffer =
          webrtc::I420Buffer::Create(thumbnail_width, thumbnail_height);
      scaled_buffer->ScaleFrom(*i420_buffer_);
      thumbnail_buffer = scaled_buffer;
    }

    webrtc::VideoFrame thumbnail_frame(thumbnail_buffer, 0, 0,
                                       webrtc::kVideoRotation_0);

    const int kColorPlanes = 3;  // R, G and B.
    size_t rgb_len = thumbnail_frame.height() * thumbnail_frame.width() *
                     kColorPlanes;
    std::unique_ptr<uint8_t[]> rgb_buf(new uint8_t[rgb_len]);

    // kRGB24 actually corresponds to FourCC 24BG which is 24-bit BGR.
    if (ConvertFromI420(thumbnail_frame, webrtc::VideoType::kRGB24, 0,
                        rgb_buf.get()) < 0) {
      RTC_LOG(LS_ERROR) << "Could not convert input frame to RGB.";
      return;
    }

    // Create a thumbnail image from the captured frame.
    thumbnail_ = EncodeRGBToJpeg((const unsigned char*)rgb_buf.get(),
                                 thumbnail_frame.width(),
                                 thumbnail_frame.height(),
                                 kColorPlanes, 75);
  }
#ifdef WEBRTC_WIN
  __except (filterException(GetExceptionCode(), GetExceptionInformation())) {
  }
#endif
}

}  // namespace lumenrtc_bridge
