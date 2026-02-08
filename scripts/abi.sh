#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

PYTHON_BIN="${PYTHON_BIN:-python3}"
ABI_CONFIG="${ABI_CONFIG:-${REPO_ROOT}/abi/config.json}"
ABI_TARGET="${ABI_TARGET:-lumenrtc}"
ABI_BASELINE="${ABI_BASELINE:-${REPO_ROOT}/abi/baselines/lumenrtc.json}"
ABI_BASELINE_ROOT="${ABI_BASELINE_ROOT:-${REPO_ROOT}/abi/baselines}"

usage() {
  cat <<'EOF'
Usage: scripts/abi.sh <command> [args...]

Commands:
  snapshot      Snapshot ABI for ABI_TARGET
  baseline      Write baseline snapshot for ABI_TARGET
  baseline-all  Write baseline snapshots for all configured targets
  regen         Regenerate baselines for all targets (supports --verify)
  regen-baselines Alias for regen
  doctor        Run ABI config/environment diagnostics
  changelog     Generate ABI changelog markdown
  verify        Verify ABI_TARGET against baseline
  check         Alias for verify
  verify-all    Verify all configured targets
  check-all     Alias for verify-all
  list-targets  List configured targets
  init-target   Initialize a new target in config
  diff          Compare two snapshot files (pass raw abi_guard diff args)
EOF
}

if [[ $# -lt 1 ]]; then
  usage
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
  baseline-all)
    while IFS= read -r target; do
      [[ -z "${target}" ]] && continue
      run_guard snapshot \
        --repo-root "${REPO_ROOT}" \
        --config "${ABI_CONFIG}" \
        --target "${target}" \
        --skip-binary \
        --output "${ABI_BASELINE_ROOT}/${target}.json" \
        "$@"
    done < <(run_guard list-targets --config "${ABI_CONFIG}")
    ;;
  regen|regen-baselines)
    run_guard regen-baselines \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
      "$@"
    ;;
  doctor)
    run_guard doctor \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
      "$@"
    ;;
  changelog)
    run_guard changelog \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
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
  verify-all|check-all)
    run_guard verify-all \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
      "$@"
    ;;
  list-targets)
    run_guard list-targets --config "${ABI_CONFIG}" "$@"
    ;;
  init-target)
    run_guard init-target \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
      "$@"
    ;;
  diff)
    run_guard diff "$@"
    ;;
  *)
    echo "Unknown command: ${COMMAND}"
    usage
    exit 2
    ;;
esac
