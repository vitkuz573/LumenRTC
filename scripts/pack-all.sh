#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
output="${OUTPUT:-artifacts/nuget}"
no_native="${NO_NATIVE:-false}"
pack_sdl="${PACK_SDL:-false}"

rids_input="${RIDS:-win-x64,linux-x64}"
IFS=',' read -r -a rids <<< "$rids_input"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
pack_script="${repo_root}/scripts/pack.sh"

if [[ "$no_native" == "true" ]]; then
  CONFIGURATION="$configuration" OUTPUT="$output" PACK_SDL="$pack_sdl" NO_NATIVE=true "$pack_script"
  exit 0
fi

for rid in "${rids[@]}"; do
  rid_output="${output}/${rid}"
  CONFIGURATION="$configuration" OUTPUT="$rid_output" RID="$rid" PACK_SDL="$pack_sdl" "$pack_script"
done
