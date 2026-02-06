#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/setup.sh [options]

Options:
  --depot-tools-dir <path>    Depot_tools directory (default: ../depot_tools)
  --webrtc-root <path>         Root directory for WebRTC checkout (default: ../webrtc_build)
  --webrtc-branch <branch>     WebRTC branch (default: m137_release)
  --target-cpu <cpu>           Target CPU (default: x64)
  --build-type <Release|Debug> Build type (default: Release)
  --desktop-capture <ON|OFF>   Enable desktop capture (default: ON)
  --skip-sync                  Skip gclient sync
  --skip-patch                 Skip applying custom audio patch
  --skip-bootstrap             Skip building LumenRTC after libwebrtc
  -h, --help                   Show help
USAGE
}

webrtc_root=""
webrtc_branch="m137_release"
target_cpu="x64"
build_type="Release"
desktop_capture="ON"
skip_sync=false
skip_patch=false
skip_bootstrap=false
depot_tools_dir=""

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing '$1' in PATH. Install depot_tools (for gclient/gn/ninja) and ensure it is on PATH." >&2
    exit 1
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --webrtc-root)
      webrtc_root="$2"
      shift 2
      ;;
    --depot-tools-dir)
      depot_tools_dir="$2"
      shift 2
      ;;
    --webrtc-branch)
      webrtc_branch="$2"
      shift 2
      ;;
    --target-cpu)
      target_cpu="$2"
      shift 2
      ;;
    --build-type)
      build_type="$2"
      shift 2
      ;;
    --desktop-capture)
      desktop_capture="$2"
      shift 2
      ;;
    --skip-sync)
      skip_sync=true
      shift
      ;;
    --skip-patch)
      skip_patch=true
      shift
      ;;
    --skip-bootstrap)
      skip_bootstrap=true
      shift
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

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -z "$webrtc_root" ]]; then
  webrtc_root="${repo_root}/../webrtc_build"
fi

if [[ -z "$depot_tools_dir" ]]; then
  depot_tools_dir="${repo_root}/../depot_tools"
fi

require_cmd git
require_cmd python3

if ! command -v gclient >/dev/null 2>&1; then
  if [[ ! -d "$depot_tools_dir" ]]; then
    echo "Cloning depot_tools into $depot_tools_dir"
    git clone https://chromium.googlesource.com/chromium/tools/depot_tools.git "$depot_tools_dir"
  fi
  export PATH="$depot_tools_dir:$PATH"
fi

require_cmd gclient
require_cmd gn
require_cmd ninja

mkdir -p "$webrtc_root"
if [[ ! -f "${webrtc_root}/.gclient" ]]; then
  cat > "${webrtc_root}/.gclient" <<GCLIENT_EOF
solutions = [
  {
    "name"        : 'src',
    "url"         : 'https://github.com/webrtc-sdk/webrtc.git@${webrtc_branch}',
    "deps_file"   : 'DEPS',
    "managed"     : False,
    "custom_deps" : {},
    "custom_vars" : {},
  },
]

target_os = ['linux']
GCLIENT_EOF
fi

pushd "$webrtc_root" >/dev/null
if [[ "$skip_sync" == false ]]; then
  gclient sync
fi

src_dir="${webrtc_root}/src"
if [[ ! -d "$src_dir" ]]; then
  echo "Expected src directory at ${src_dir}. gclient sync may have failed." >&2
  exit 1
fi

if [[ ! -d "${src_dir}/libwebrtc" ]]; then
  git clone https://github.com/webrtc-sdk/libwebrtc "${src_dir}/libwebrtc"
fi

if [[ "$skip_patch" == false ]]; then
  patch_path="${src_dir}/libwebrtc/patchs/custom_audio_source_m137.patch"
  if [[ -f "$patch_path" ]]; then
    if git apply --check "$patch_path" >/dev/null 2>&1; then
      git apply "$patch_path"
      echo "Applied patch: custom_audio_source_m137.patch"
    else
      echo "Patch already applied or not applicable; skipping."
    fi
  else
    echo "Patch not found: $patch_path" >&2
  fi

  repo_patch="${repo_root}/scripts/patches/libwebrtc_ice_candidate_status.patch"
  if [[ -f "$repo_patch" ]]; then
    pushd "${src_dir}/libwebrtc" >/dev/null
    if git apply --check "$repo_patch" >/dev/null 2>&1; then
      git apply "$repo_patch"
      echo "Applied patch: libwebrtc_ice_candidate_status.patch"
    else
      echo "Patch already applied or not applicable; skipping: libwebrtc_ice_candidate_status.patch"
    fi
    popd >/dev/null
  else
    echo "Patch not found: $repo_patch" >&2
  fi
fi

build_gn_path="${src_dir}/BUILD.gn"
python3 - <<PY
from pathlib import Path
import re
path = Path(r"$build_gn_path")
if not path.exists():
    print("WARN: BUILD.gn not found.")
    raise SystemExit(0)
text = path.read_text()
if "//libwebrtc" in text:
    raise SystemExit(0)
pattern = r"deps\\s*=\\s*\\[\\s*\\\":webrtc\\\"\\s*\\]"
m = re.search(pattern, text)
if not m:
    print("WARN: Could not auto-update BUILD.gn. Please add //libwebrtc to group(\"default\").")
    raise SystemExit(0)
text = re.sub(pattern, 'deps = [ ":webrtc", "//libwebrtc", ]', text, count=1)
path.write_text(text)
print("Updated BUILD.gn to include //libwebrtc in default group.")
PY

out_dir="${src_dir}/out/${build_type}"
if [[ "$build_type" == "Debug" ]]; then
  is_debug=true
else
  is_debug=false
fi
if [[ "$desktop_capture" == "ON" ]]; then
  desktop_capture_flag=true
else
  desktop_capture_flag=false
fi

gn gen "$out_dir" --args="target_os=\"linux\" target_cpu=\"$target_cpu\" is_debug=$is_debug rtc_include_tests=false rtc_use_h264=true ffmpeg_branding=\"Chrome\" is_component_build=false rtc_build_examples=false use_rtti=true use_custom_libcxx=false rtc_enable_protobuf=false libwebrtc_desktop_capture=$desktop_capture_flag"

ninja -C "$out_dir" libwebrtc

if [[ "$skip_bootstrap" == false ]]; then
  export LIBWEBRTC_ROOT="${src_dir}/libwebrtc"
  export LIBWEBRTC_BUILD_DIR="$out_dir"
  "${repo_root}/scripts/bootstrap.sh" --libwebrtc-build-dir "$out_dir" --build-type "$build_type" --desktop-capture "$desktop_capture"
fi

popd >/dev/null

cat <<NEXT_EOF

Next steps:
  export LIBWEBRTC_BUILD_DIR="$out_dir"
  export LumenRtcNativeDir="${repo_root}/native/build"
  dotnet run --project ./samples/LumenRTC.Sample.LocalCamera/LumenRTC.Sample.LocalCamera.csproj
NEXT_EOF
