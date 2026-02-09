#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/bootstrap.sh --libwebrtc-build-dir <path> [options]

Options:
  --libwebrtc-build-dir <path>   Required. Path to libwebrtc build output (out/...)
  --cmake-build-dir <path>       CMake build output directory (default: native/build)
  --build-type <Release|Debug>   Build type (default: Release)
  -h, --help                     Show help

Environment:
  LIBWEBRTC_BUILD_DIR            Used if --libwebrtc-build-dir is not set
  LIBWEBRTC_ROOT                 Optional. Overrides auto-detect of libwebrtc repo
EOF
}

libwebrtc_build_dir=""
cmake_build_dir="native/build"
build_type="Release"

detect_libwebrtc_build_dir() {
  local repo_root
  repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

  local candidates=()
  if [[ -n "${LIBWEBRTC_BUILD_DIR:-}" ]]; then
    candidates+=("${LIBWEBRTC_BUILD_DIR}")
  fi
  if [[ -n "${WEBRTC_BUILD_DIR:-}" ]]; then
    candidates+=("${WEBRTC_BUILD_DIR}")
  fi
  if [[ -n "${WEBRTC_OUT_DIR:-}" ]]; then
    candidates+=("${WEBRTC_OUT_DIR}")
  fi
  if [[ -n "${WEBRTC_OUT:-}" ]]; then
    candidates+=("${WEBRTC_OUT}")
  fi

  candidates+=(
    "${repo_root}/../webrtc_build/src/out-debug/Linux-x64"
    "${repo_root}/../webrtc_build/src/out/Debug"
    "${repo_root}/../webrtc_build/src/out/Release"
    "${repo_root}/../webrtc/src/out/Debug"
    "${repo_root}/../webrtc/src/out/Release"
    "${repo_root}/../webrtc/src/out/Default"
    "${repo_root}/../webrtc/src/out-default"
  )

  for dir in "${candidates[@]}"; do
    [[ -z "$dir" ]] && continue
    if [[ -f "${dir}/libwebrtc.so" ]] || \
       [[ -f "${dir}/libwebrtc.dylib" ]] || \
       [[ -f "${dir}/libwebrtc.dll" ]] || \
       [[ -f "${dir}/libwebrtc.lib" ]]; then
      echo "$dir"
      return 0
    fi
  done

  return 1
}

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
    --build-type)
      build_type="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "$libwebrtc_build_dir" ]]; then
  libwebrtc_build_dir="${LIBWEBRTC_BUILD_DIR:-}"
fi

if [[ -z "$libwebrtc_build_dir" || "$libwebrtc_build_dir" == "auto" ]]; then
  if detected="$(detect_libwebrtc_build_dir)"; then
    libwebrtc_build_dir="$detected"
  fi
fi

if [[ -z "$libwebrtc_build_dir" ]]; then
  echo "LIBWEBRTC_BUILD_DIR is not set. Pass --libwebrtc-build-dir or set the LIBWEBRTC_BUILD_DIR environment variable." >&2
  echo "Tip: you can pass --libwebrtc-build-dir auto for auto-detection." >&2
  echo "If you have not built libwebrtc yet, run scripts/setup.sh to fetch and build it." >&2
  exit 1
fi

cmake_args=(
  -DLIBWEBRTC_BUILD_DIR="$libwebrtc_build_dir"
  -DCMAKE_BUILD_TYPE="$build_type"
)

if [[ -n "${LIBWEBRTC_ROOT:-}" ]]; then
  cmake_args+=( -DLIBWEBRTC_ROOT="$LIBWEBRTC_ROOT" )
fi

cmake -S native -B "$cmake_build_dir" "${cmake_args[@]}"
cmake --build "$cmake_build_dir" -j

dotnet build src/LumenRTC/LumenRTC.csproj -c "$build_type"
