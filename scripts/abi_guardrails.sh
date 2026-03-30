#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${REPO_ROOT}"

echo "[abi-guardrails] checking tracked generated C# sources"
tracked_generated="$(git ls-files | grep -E '^src/LumenRTC/.+\.g\.cs$' || true)"
if [[ -n "${tracked_generated}" ]]; then
  echo "ERROR: tracked generated C# files are not allowed under src/LumenRTC."
  echo "${tracked_generated}"
  exit 1
fi

echo "[abi-guardrails] all checks passed"
