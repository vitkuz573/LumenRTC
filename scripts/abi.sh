#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

PYTHON_BIN="${PYTHON_BIN:-python3}"
ABI_CONFIG="${ABI_CONFIG:-${REPO_ROOT}/abi/config.json}"
ABI_TARGET="${ABI_TARGET:-lumenrtc}"
ABI_BASELINE="${ABI_BASELINE:-${REPO_ROOT}/abi/baselines/lumenrtc.json}"
ABI_BASELINE_ROOT="${ABI_BASELINE_ROOT:-${REPO_ROOT}/abi/baselines}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
ABI_IDL_FROM_ENV="${ABI_IDL+x}"
ABI_ROSLYN_OUTPUT_FROM_ENV="${ABI_ROSLYN_OUTPUT+x}"
ABI_ROSLYN_NAMESPACE_FROM_ENV="${ABI_ROSLYN_NAMESPACE+x}"
ABI_ROSLYN_CLASS_NAME_FROM_ENV="${ABI_ROSLYN_CLASS_NAME+x}"
ABI_ROSLYN_ACCESS_MODIFIER_FROM_ENV="${ABI_ROSLYN_ACCESS_MODIFIER+x}"
ABI_ROSLYN_CALLING_CONVENTION_FROM_ENV="${ABI_ROSLYN_CALLING_CONVENTION+x}"
ABI_ROSLYN_LIBRARY_EXPRESSION_FROM_ENV="${ABI_ROSLYN_LIBRARY_EXPRESSION+x}"
ABI_ROSLYN_PROJECT="${ABI_ROSLYN_PROJECT:-${REPO_ROOT}/tools/lumenrtc_roslyn_codegen/LumenRTC.Abi.RoslynGenerator.csproj}"
ABI_IDL="${ABI_IDL:-${REPO_ROOT}/abi/generated/lumenrtc/lumenrtc.idl.json}"
ABI_ROSLYN_OUTPUT="${ABI_ROSLYN_OUTPUT:-${REPO_ROOT}/abi/generated/lumenrtc/NativeMethods.g.cs}"
ABI_ROSLYN_NAMESPACE="${ABI_ROSLYN_NAMESPACE:-LumenRTC.Interop}"
ABI_ROSLYN_CLASS_NAME="${ABI_ROSLYN_CLASS_NAME:-NativeMethods}"
ABI_ROSLYN_ACCESS_MODIFIER="${ABI_ROSLYN_ACCESS_MODIFIER:-internal}"
ABI_ROSLYN_CALLING_CONVENTION="${ABI_ROSLYN_CALLING_CONVENTION:-Cdecl}"
ABI_ROSLYN_LIBRARY_EXPRESSION="${ABI_ROSLYN_LIBRARY_EXPRESSION:-LibName}"

if command -v jq >/dev/null 2>&1 && [[ -f "${ABI_CONFIG}" ]]; then
  cfg_idl="$(jq -r --arg t "${ABI_TARGET}" '.targets[$t].bindings.csharp.idl_path // empty' "${ABI_CONFIG}" 2>/dev/null || true)"
  cfg_output="$(jq -r --arg t "${ABI_TARGET}" '.targets[$t].bindings.csharp.output_path // empty' "${ABI_CONFIG}" 2>/dev/null || true)"
  cfg_namespace="$(jq -r --arg t "${ABI_TARGET}" '.targets[$t].bindings.csharp.namespace // empty' "${ABI_CONFIG}" 2>/dev/null || true)"
  cfg_class_name="$(jq -r --arg t "${ABI_TARGET}" '.targets[$t].bindings.csharp.class_name // empty' "${ABI_CONFIG}" 2>/dev/null || true)"
  cfg_access_modifier="$(jq -r --arg t "${ABI_TARGET}" '.targets[$t].bindings.csharp.access_modifier // empty' "${ABI_CONFIG}" 2>/dev/null || true)"
  cfg_calling_convention="$(jq -r --arg t "${ABI_TARGET}" '.targets[$t].bindings.csharp.calling_convention // empty' "${ABI_CONFIG}" 2>/dev/null || true)"
  cfg_library_expression="$(jq -r --arg t "${ABI_TARGET}" '.targets[$t].bindings.csharp.library_expression // empty' "${ABI_CONFIG}" 2>/dev/null || true)"

  resolve_repo_path() {
    local value="$1"
    if [[ -z "${value}" ]]; then
      echo ""
    elif [[ "${value}" = /* ]]; then
      echo "${value}"
    else
      echo "${REPO_ROOT}/${value}"
    fi
  }

  if [[ -z "${ABI_IDL_FROM_ENV}" && -n "${cfg_idl}" ]]; then
    ABI_IDL="$(resolve_repo_path "${cfg_idl}")"
  fi
  if [[ -z "${ABI_ROSLYN_OUTPUT_FROM_ENV}" && -n "${cfg_output}" ]]; then
    ABI_ROSLYN_OUTPUT="$(resolve_repo_path "${cfg_output}")"
  fi
  if [[ -z "${ABI_ROSLYN_NAMESPACE_FROM_ENV}" && -n "${cfg_namespace}" ]]; then
    ABI_ROSLYN_NAMESPACE="${cfg_namespace}"
  fi
  if [[ -z "${ABI_ROSLYN_CLASS_NAME_FROM_ENV}" && -n "${cfg_class_name}" ]]; then
    ABI_ROSLYN_CLASS_NAME="${cfg_class_name}"
  fi
  if [[ -z "${ABI_ROSLYN_ACCESS_MODIFIER_FROM_ENV}" && -n "${cfg_access_modifier}" ]]; then
    ABI_ROSLYN_ACCESS_MODIFIER="${cfg_access_modifier}"
  fi
  if [[ -z "${ABI_ROSLYN_CALLING_CONVENTION_FROM_ENV}" && -n "${cfg_calling_convention}" ]]; then
    ABI_ROSLYN_CALLING_CONVENTION="${cfg_calling_convention}"
  fi
  if [[ -z "${ABI_ROSLYN_LIBRARY_EXPRESSION_FROM_ENV}" && -n "${cfg_library_expression}" ]]; then
    ABI_ROSLYN_LIBRARY_EXPRESSION="${cfg_library_expression}"
  fi
fi

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
  generate      Generate ABI IDL for configured targets
  roslyn        Generate LumenRTC C# interop from ABI IDL via Roslyn
  sync          Sync generated ABI artifacts (and optional baselines)
  release-prepare Run end-to-end ABI release preparation pipeline
  changelog     Generate ABI changelog markdown
  verify        Verify ABI_TARGET against baseline
  check         Alias for verify
  verify-all    Verify all configured targets
  check-all     Alias for verify-all
  list-targets  List configured targets
  init-target   Initialize a new target in config
  diff          Compare two snapshot files (pass raw abi_framework diff args)
EOF
}

if [[ $# -lt 1 ]]; then
  usage
  exit 2
fi

COMMAND="$1"
shift || true

run_guard() {
  "${PYTHON_BIN}" "${REPO_ROOT}/tools/abi_framework/abi_framework.py" "$@"
}

run_roslyn() {
  "${DOTNET_BIN}" run --project "${ABI_ROSLYN_PROJECT}" -- \
    --idl "${ABI_IDL}" \
    --output "${ABI_ROSLYN_OUTPUT}" \
    --namespace "${ABI_ROSLYN_NAMESPACE}" \
    --class-name "${ABI_ROSLYN_CLASS_NAME}" \
    --access-modifier "${ABI_ROSLYN_ACCESS_MODIFIER}" \
    --calling-convention "${ABI_ROSLYN_CALLING_CONVENTION}" \
    --library-expression "${ABI_ROSLYN_LIBRARY_EXPRESSION}" \
    "$@"
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
  generate)
    run_guard generate \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
      "$@"
    ;;
  roslyn)
    run_roslyn "$@"
    ;;
  sync)
    run_guard sync \
      --repo-root "${REPO_ROOT}" \
      --config "${ABI_CONFIG}" \
      "$@"
    ;;
  release-prepare)
    run_guard release-prepare \
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
