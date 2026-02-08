#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

PYTHON_BIN="${PYTHON_BIN:-python3}"
ABI_CONFIG="${ABI_CONFIG:-${REPO_ROOT}/abi/config.json}"
ABI_TARGET="${ABI_TARGET:-lumenrtc}"
ABI_BASELINE="${ABI_BASELINE:-${REPO_ROOT}/abi/baselines/lumenrtc.json}"

if [[ $# -lt 1 ]]; then
  echo "Usage: scripts/abi.sh <snapshot|baseline|verify|check|diff> [args...]"
  exit 2
fi

COMMAND="$1"
shift || true

run_guard() {
  "${PYTHON_BIN}" "${REPO_ROOT}/tools/abi_guard/abi_guard.py" "$@"
}

case "${COMMAND}" in
  snapshot)
    run_guard snapshot \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
      --target "${ABI_TARGET}" \
      "$@"
    ;;
  baseline)
    run_guard snapshot \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
      --target "${ABI_TARGET}" \
      --skip-binary \
      --output "${ABI_BASELINE}" \
      "$@"
    ;;
  verify|check)
    run_guard verify \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
      --target "${ABI_TARGET}" \
      --baseline "${ABI_BASELINE}" \
      "$@"
    ;;
  diff)
    run_guard diff "$@"
    ;;
  *)
    echo "Unknown command: ${COMMAND}"
    echo "Usage: scripts/abi.sh <snapshot|baseline|verify|check|diff> [args...]"
    exit 2
    ;;
esac
