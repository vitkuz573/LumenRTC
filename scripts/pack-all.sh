#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
output="${OUTPUT:-artifacts/nuget}"
package_version="${PACKAGE_VERSION:-}"
no_native="${NO_NATIVE:-false}"
pack_sdl="${PACK_SDL:-false}"

rids_input="${RIDS:-win-x64,linux-x64}"
IFS=',' read -r -a rids <<< "$rids_input"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
win_lumenrtc_native_dir="${WIN_LUMENRTC_NATIVE_DIR:-${LUMENRTC_NATIVE_DIR:-}}"
win_libwebrtc_build_dir="${WIN_LIBWEBRTC_BUILD_DIR:-${LIBWEBRTC_BUILD_DIR:-${WEBRTC_BUILD_DIR:-${WEBRTC_OUT_DIR:-${WEBRTC_OUT:-}}}}}"
linux_lumenrtc_native_dir="${LINUX_LUMENRTC_NATIVE_DIR:-${LUMENRTC_NATIVE_DIR:-}}"
linux_libwebrtc_build_dir="${LINUX_LIBWEBRTC_BUILD_DIR:-${LIBWEBRTC_BUILD_DIR:-${WEBRTC_BUILD_DIR:-${WEBRTC_OUT_DIR:-${WEBRTC_OUT:-}}}}}"

include_win_x64=false
include_linux_x64=false
for rid in "${rids[@]}"; do
  if [[ "$rid" == "win-x64" ]]; then
    include_win_x64=true
  elif [[ "$rid" == "linux-x64" ]]; then
    include_linux_x64=true
  fi
done

pack_args=(
  pack
  "${repo_root}/src/LumenRTC/LumenRTC.csproj"
  -c "$configuration"
  -o "$output"
)

if [[ -n "$package_version" ]]; then
  pack_args+=("-p:PackageVersion=${package_version}")
fi

if [[ "$no_native" != "true" ]]; then
  pack_args+=("-p:LumenRtcPackNativeMultiRid=true")
  pack_args+=("-p:LumenRtcPackIncludeWinX64=${include_win_x64}")
  pack_args+=("-p:LumenRtcPackIncludeLinuxX64=${include_linux_x64}")
  if [[ -n "$win_lumenrtc_native_dir" ]]; then
    pack_args+=("-p:LumenRtcWinNativeDir=${win_lumenrtc_native_dir}")
  fi
  if [[ -n "$win_libwebrtc_build_dir" ]]; then
    pack_args+=("-p:LibWebRtcWinBuildDir=${win_libwebrtc_build_dir}")
  fi
  if [[ -n "$linux_lumenrtc_native_dir" ]]; then
    pack_args+=("-p:LumenRtcLinuxNativeDir=${linux_lumenrtc_native_dir}")
  fi
  if [[ -n "$linux_libwebrtc_build_dir" ]]; then
    pack_args+=("-p:LibWebRtcLinuxBuildDir=${linux_libwebrtc_build_dir}")
  fi
fi

dotnet "${pack_args[@]}"

if [[ "$pack_sdl" == "true" ]]; then
  sdl_args=(
    pack
    "${repo_root}/src/LumenRTC.Rendering.Sdl/LumenRTC.Rendering.Sdl.csproj"
    -c "$configuration"
    -o "$output"
  )
  if [[ -n "$package_version" ]]; then
    sdl_args+=("-p:PackageVersion=${package_version}")
  fi
  dotnet "${sdl_args[@]}"
fi
