#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/bootstrap.sh --lumenrtc_bridge-build-dir <path> [options]

Options:
  --lumenrtc_bridge-build-dir <path>   Required. Path to lumenrtc_bridge build output (out/...)
  --cmake-build-dir <path>       CMake build output directory (default: native/build)
  --build-type <Release|Debug>   Build type (default: Release)
  -h, --help                     Show help

Environment:
  LUMENRTC_BRIDGE_BUILD_DIR            Used if --lumenrtc_bridge-build-dir is not set
  LUMENRTC_BRIDGE_ROOT                 Optional. Overrides auto-detect of lumenrtc_bridge repo
EOF
}

lumenrtc_bridge_build_dir=""
cmake_build_dir="native/build"
build_type="Release"

detect_lumenrtc_bridge_build_dir() {
  local repo_root
  repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

  local candidates=()
  if [[ -n "${LUMENRTC_BRIDGE_BUILD_DIR:-}" ]]; then
    candidates+=("${LUMENRTC_BRIDGE_BUILD_DIR}")
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
    if [[ -f "${dir}/lumenrtc_bridge.so" ]] || \
       [[ -f "${dir}/lumenrtc_bridge.dylib" ]] || \
       [[ -f "${dir}/lumenrtc_bridge.dll" ]] || \
       [[ -f "${dir}/lumenrtc_bridge.lib" ]]; then
      echo "$dir"
      return 0
    fi
  done

  return 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --lumenrtc_bridge-build-dir)
      lumenrtc_bridge_build_dir="$2"
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

if [[ -z "$lumenrtc_bridge_build_dir" ]]; then
  lumenrtc_bridge_build_dir="${LUMENRTC_BRIDGE_BUILD_DIR:-}"
fi

if [[ -z "$lumenrtc_bridge_build_dir" || "$lumenrtc_bridge_build_dir" == "auto" ]]; then
  if detected="$(detect_lumenrtc_bridge_build_dir)"; then
    lumenrtc_bridge_build_dir="$detected"
  fi
fi

if [[ -z "$lumenrtc_bridge_build_dir" ]]; then
  echo "LUMENRTC_BRIDGE_BUILD_DIR is not set. Pass --lumenrtc_bridge-build-dir or set the LUMENRTC_BRIDGE_BUILD_DIR environment variable." >&2
  echo "Tip: you can pass --lumenrtc_bridge-build-dir auto for auto-detection." >&2
  echo "If you have not built lumenrtc_bridge yet, run scripts/setup.sh to fetch and build it." >&2
  exit 1
fi

cmake_args=(
  -DLUMENRTC_BRIDGE_BUILD_DIR="$lumenrtc_bridge_build_dir"
  -DCMAKE_BUILD_TYPE="$build_type"
)

if [[ -n "${LUMENRTC_BRIDGE_ROOT:-}" ]]; then
  cmake_args+=( -DLUMENRTC_BRIDGE_ROOT="$LUMENRTC_BRIDGE_ROOT" )
fi

cmake -S native -B "$cmake_build_dir" "${cmake_args[@]}"
cmake --build "$cmake_build_dir" -j

dotnet build src/LumenRTC/LumenRTC.csproj -c "$build_type"
