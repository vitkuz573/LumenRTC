#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/bootstrap.sh --libwebrtc-build-dir <path> [options]

Options:
  --libwebrtc-build-dir <path>   Required. Path to libwebrtc build output (out/...)
  --cmake-build-dir <path>       CMake build output directory (default: native/build)
  --desktop-capture <ON|OFF>     Enable desktop capture (default: ON)
  --build-type <Release|Debug>   Build type (default: Release)
  -h, --help                     Show help

Environment:
  LIBWEBRTC_BUILD_DIR            Used if --libwebrtc-build-dir is not set
  LIBWEBRTC_ROOT                 Optional. Overrides auto-detect of libwebrtc repo
EOF
}

libwebrtc_build_dir=""
cmake_build_dir="native/build"
desktop_capture="ON"
build_type="Release"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --libwebrtc-build-dir)
      libwebrtc_build_dir="$2"
      shift 2
      ;;
    --cmake-build-dir)
      cmake_build_dir="$2"
      shift 2
      ;;
    --desktop-capture)
      desktop_capture="$2"
      shift 2
      ;;
    --build-type)
      build_type="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown аргумент: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "$libwebrtc_build_dir" ]]; then
  libwebrtc_build_dir="${LIBWEBRTC_BUILD_DIR:-}"
fi

if [[ -z "$libwebrtc_build_dir" ]]; then
  echo "LIBWEBRTC_BUILD_DIR не задан. Передайте --libwebrtc-build-dir или переменную окружения LIBWEBRTC_BUILD_DIR." >&2
  exit 1
fi

cmake_args=(
  -DLIBWEBRTC_BUILD_DIR="$libwebrtc_build_dir"
  -DLUMENRTC_ENABLE_DESKTOP_CAPTURE="$desktop_capture"
  -DCMAKE_BUILD_TYPE="$build_type"
)

if [[ -n "${LIBWEBRTC_ROOT:-}" ]]; then
  cmake_args+=( -DLIBWEBRTC_ROOT="$LIBWEBRTC_ROOT" )
fi

cmake -S native -B "$cmake_build_dir" "${cmake_args[@]}"
cmake --build "$cmake_build_dir" -j

dotnet build src/LumenRTC/LumenRTC.csproj -c "$build_type"
