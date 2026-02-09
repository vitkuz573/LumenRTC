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

echo "[abi-guardrails] checking abi_framework generator isolation"
unexpected_generators="$(find tools/abi_framework/generators -mindepth 1 -type f ! -name '.gitkeep' -print 2>/dev/null || true)"
if [[ -n "${unexpected_generators}" ]]; then
  echo "ERROR: tools/abi_framework/generators must remain target-agnostic and empty."
  echo "${unexpected_generators}"
  exit 1
fi

echo "[abi-guardrails] checking external generator commands for managed-source writes"
python3 - <<'PY'
import json
import sys
from pathlib import Path

config_path = Path("abi/config.json")
payload = json.loads(config_path.read_text(encoding="utf-8"))

violations = []
targets = payload.get("targets")
if isinstance(targets, dict):
    for target_name in sorted(targets.keys()):
        target = targets.get(target_name)
        if not isinstance(target, dict):
            continue
        bindings = target.get("bindings")
        if not isinstance(bindings, dict):
            continue
        generators = bindings.get("generators")
        if not isinstance(generators, list):
            continue
        for idx, generator in enumerate(generators):
            if not isinstance(generator, dict):
                continue
            command = generator.get("command")
            if not isinstance(command, list):
                continue
            for token in command:
                if not isinstance(token, str):
                    continue
                normalized = token.replace("\\", "/").lower()
                if normalized.endswith(".cs") or ".g.cs" in normalized:
                    violations.append(
                        f"{target_name}.bindings.generators[{idx}] references C# output token: {token!r}"
                    )
                if (
                    normalized.startswith("{repo_root}/src/lumenrtc/")
                    or normalized.startswith("src/lumenrtc/")
                    or normalized.startswith("./src/lumenrtc/")
                ):
                    violations.append(
                        f"{target_name}.bindings.generators[{idx}] references managed source path token: {token!r}"
                    )

if violations:
    print("ERROR: ABI generator guardrails failed:")
    for item in violations:
        print(" - " + item)
    sys.exit(1)

print("abi generator guardrails: clean")
PY

echo "[abi-guardrails] all checks passed"
