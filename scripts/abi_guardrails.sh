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

echo "[abi-guardrails] checking generator manifest paths exist"
python3 - <<'PY'
import json
import sys
from pathlib import Path

repo_root = Path.cwd()
config_path = repo_root / "abi" / "config.json"
payload = json.loads(config_path.read_text(encoding="utf-8"))


def resolve_manifest_path(token: str) -> Path:
    """Resolve a manifest path token, expanding {repo_root} and handling relative paths."""
    expanded = token.replace("{repo_root}", str(repo_root))
    p = Path(expanded)
    return p if p.is_absolute() else (repo_root / p)


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
            manifest = generator.get("manifest")
            if not isinstance(manifest, str) or not manifest:
                continue
            resolved = resolve_manifest_path(manifest)
            if not resolved.exists():
                violations.append(
                    f"{target_name}.bindings.generators[{idx}].manifest: "
                    f"file not found at '{manifest}'"
                )

if violations:
    print("ERROR: ABI generator manifest path guardrails failed:")
    for item in violations:
        print(" - " + item)
    sys.exit(1)

print("abi generator manifest paths: all present")
PY

echo "[abi-guardrails] checking baseline snapshot files exist"
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
        baseline_path = target.get("baseline_path")
        if not isinstance(baseline_path, str) or not baseline_path:
            continue
        path = Path(baseline_path)
        if not path.is_absolute():
            path = Path(baseline_path)
        if not path.exists():
            violations.append(
                f"{target_name}: baseline snapshot not found at '{baseline_path}'"
            )

if violations:
    print("ERROR: ABI baseline snapshot guardrails failed:")
    for item in violations:
        print(" - " + item)
    sys.exit(1)

print("abi baseline snapshots: all present")
PY

echo "[abi-guardrails] all checks passed"
