#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/setup.sh [options]

Options:
  --depot-tools-dir <path>    Depot_tools directory (default: ../depot_tools)
  --webrtc-root <path>         Root directory for WebRTC checkout (default: ../webrtc_build)
  --webrtc-branch <branch>     WebRTC ref (default: branch-heads/7151)
  --target-cpu <cpu>           Target CPU (default: x64)
  --build-type <Release|Debug> Build type (default: Release)
  --sync-retries <count>       gclient sync retries on transient failures (default: 3)
  --sync-delay <seconds>       Delay between sync retries (default: 8)
  --sync-full-history          Disable --no-history for gclient sync
  --skip-sync                  Skip gclient sync
  --skip-bootstrap             Skip building LumenRTC after libwebrtc
  -h, --help                   Show help
USAGE
}

webrtc_root=""
webrtc_branch="branch-heads/7151"
target_cpu="x64"
build_type="Release"
skip_sync=false
skip_bootstrap=false
depot_tools_dir=""
sync_retries=3
sync_delay=8
sync_no_history=true

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing '$1' in PATH. Install depot_tools (for gclient/gn/ninja) and ensure it is on PATH." >&2
    exit 1
  fi
}

cleanup_partial_sync_artifacts() {
  local root="$1"
  local instr_dir="${root}/src/third_party/instrumented_libs"
  local status=0
  if [[ -d "$instr_dir" ]]; then
    rm -rf "${instr_dir}/binaries" || status=$?
    find "$instr_dir" -maxdepth 1 -type f \
      \( -name "*.tmp" -o -name "*.partial" -o -name "*.tar" -o -name "*.tar.*" \) \
      -delete 2>/dev/null || status=$?
  fi

  return "$status"
}

run_gclient_sync_with_retry() {
  local root="$1"
  local attempts="$2"
  local delay="$3"
  local use_no_history="$4"
  local attempt=1
  local status=1

  while (( attempt <= attempts )); do
    local cmd=(gclient sync)
    if [[ "$use_no_history" == true ]]; then
      cmd+=(--no-history)
    fi
    if (( attempt > 1 )); then
      cmd+=(--reset --delete_unversioned_trees)
    fi

    echo "[setup] gclient sync attempt ${attempt}/${attempts}..."
    set +e
    "${cmd[@]}"
    status=$?
    set -e

    if (( status == 0 )); then
      return 0
    fi

    echo "[setup] gclient sync failed with exit code ${status}."
    if (( status >= 128 )); then
      echo "[setup] gclient sync terminated by signal $((status - 128))."
    fi
    echo "[setup] Cleaning partial sync artifacts..."
    if ! cleanup_partial_sync_artifacts "$root"; then
      echo "[setup] Failed to clean partial sync artifacts due to filesystem I/O errors." >&2
      echo "[setup] Abort retries, restart WSL and verify host disk health." >&2
      return 90
    fi

    if (( attempt < attempts )); then
      echo "[setup] Retrying in ${delay}s..."
      if ! sleep "$delay"; then
        echo "[setup] sleep command failed due to filesystem/runtime I/O errors." >&2
        echo "[setup] Abort retries, restart WSL and verify host disk health." >&2
        return 91
      fi
    fi
    ((attempt++))
  done

  return "$status"
}

sync_libwebrtc_wrapper() {
  local src_wrapper="${repo_root}/vendor/libwebrtc"
  local dst_wrapper="$1"

  if [[ ! -d "${src_wrapper}/include" ]]; then
    echo "Wrapper source not found at ${src_wrapper}." >&2
    exit 1
  fi

  mkdir -p "$dst_wrapper"
  find "$dst_wrapper" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
  (
    cd "$src_wrapper"
    tar --exclude=.git --exclude=build --exclude=out --exclude=.conan -cf - .
  ) | (
    cd "$dst_wrapper"
    tar -xf -
  )
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
    --sync-retries)
      sync_retries="$2"
      shift 2
      ;;
    --sync-delay)
      sync_delay="$2"
      shift 2
      ;;
    --sync-full-history)
      sync_no_history=false
      shift
      ;;
    --skip-sync)
      skip_sync=true
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

if ! [[ "$sync_retries" =~ ^[0-9]+$ ]] || (( sync_retries < 1 )); then
  echo "--sync-retries must be a positive integer." >&2
  exit 1
fi

if ! [[ "$sync_delay" =~ ^[0-9]+$ ]] || (( sync_delay < 0 )); then
  echo "--sync-delay must be a non-negative integer." >&2
  exit 1
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -z "$webrtc_root" ]]; then
  webrtc_root="${repo_root}/../webrtc_build"
fi

if [[ -z "$depot_tools_dir" ]]; then
  depot_tools_dir="${repo_root}/../depot_tools"
fi

require_cmd git
require_cmd python3
require_cmd tar

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
webrtc_url="https://webrtc.googlesource.com/src.git"
if [[ -n "$webrtc_branch" ]]; then
  webrtc_url="${webrtc_url}@${webrtc_branch}"
fi
cat > "${webrtc_root}/.gclient" <<GCLIENT_EOF
solutions = [
  {
    "name"        : 'src',
    "url"         : '${webrtc_url}',
    "deps_file"   : 'DEPS',
    "managed"     : False,
    "custom_deps" : {},
    "custom_vars" : {},
  },
]

target_os = ['linux']
GCLIENT_EOF

pushd "$webrtc_root" >/dev/null
if [[ "$skip_sync" == false ]]; then
  if ! run_gclient_sync_with_retry "$webrtc_root" "$sync_retries" "$sync_delay" "$sync_no_history"; then
    echo "gclient sync failed after ${sync_retries} attempt(s)." >&2
    exit 1
  fi
fi

src_dir="${webrtc_root}/src"
if [[ ! -d "$src_dir" ]]; then
  echo "Expected src directory at ${src_dir}. gclient sync may have failed." >&2
  exit 1
fi

sync_libwebrtc_wrapper "${src_dir}/libwebrtc"

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
pattern = r'deps\\s*=\\s*\\[\\s*":webrtc"\\s*\\]'
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

pushd "$src_dir" >/dev/null
gn gen "$out_dir" --args="target_os=\"linux\" target_cpu=\"$target_cpu\" is_debug=$is_debug rtc_include_tests=false rtc_use_h264=true ffmpeg_branding=\"Chrome\" is_component_build=false rtc_build_examples=false use_rtti=true use_custom_libcxx=false rtc_enable_protobuf=false libwebrtc_desktop_capture=true"
ninja -C "$out_dir" libwebrtc
popd >/dev/null

if [[ "$skip_bootstrap" == false ]]; then
  export LIBWEBRTC_ROOT="${src_dir}/libwebrtc"
  export LIBWEBRTC_BUILD_DIR="$out_dir"
  pushd "$repo_root" >/dev/null
  "${repo_root}/scripts/bootstrap.sh" --libwebrtc-build-dir "$out_dir" --build-type "$build_type"
  popd >/dev/null
fi

popd >/dev/null

cat <<NEXT_EOF

Next steps:
  export LIBWEBRTC_BUILD_DIR="$out_dir"
  export LumenRtcNativeDir="${repo_root}/native/build"
  dotnet run --project ./samples/LumenRTC.Sample.LocalCamera.Convenience/LumenRTC.Sample.LocalCamera.Convenience.csproj
NEXT_EOF
