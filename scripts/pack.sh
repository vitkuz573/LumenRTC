#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
rid="${RID:-linux-x64}"
output="${OUTPUT:-artifacts/nuget}"
libwebrtc_build_dir="${LIBWEBRTC_BUILD_DIR:-${WEBRTC_BUILD_DIR:-${WEBRTC_OUT_DIR:-${WEBRTC_OUT:-}}}}"
lumenrtc_native_dir="${LUMENRTC_NATIVE_DIR:-}"
no_native="${NO_NATIVE:-false}"
pack_sdl="${PACK_SDL:-false}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -z "$lumenrtc_native_dir" ]]; then
  if [[ -f "${repo_root}/native/build/Release/liblumenrtc.so" ]]; then
    lumenrtc_native_dir="${repo_root}/native/build/Release"
  elif [[ -f "${repo_root}/native/build/liblumenrtc.so" ]]; then
    lumenrtc_native_dir="${repo_root}/native/build"
  fi
fi

pack_args=(
  pack
  "${repo_root}/src/LumenRTC/LumenRTC.csproj"
  -c "$configuration"
  -o "$output"
)

if [[ "$no_native" != "true" ]]; then
  pack_args+=("-p:LumenRtcPackNative=true")
  pack_args+=("-p:LumenRtcPackRid=${rid}")
  if [[ -n "$lumenrtc_native_dir" ]]; then
    pack_args+=("-p:LumenRtcNativeDir=${lumenrtc_native_dir}")
  fi
  if [[ -n "$libwebrtc_build_dir" ]]; then
    pack_args+=("-p:LibWebRtcBuildDir=${libwebrtc_build_dir}")
  fi
fi

dotnet "${pack_args[@]}"

if [[ "$pack_sdl" == "true" ]]; then
  dotnet pack "${repo_root}/src/LumenRTC.Rendering.Sdl/LumenRTC.Rendering.Sdl.csproj" -c "$configuration" -o "$output"
fi
