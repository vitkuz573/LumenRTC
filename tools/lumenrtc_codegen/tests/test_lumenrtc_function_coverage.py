import json
import re
import subprocess
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
IDL_PATH = REPO_ROOT / "abi" / "generated" / "lumenrtc" / "lumenrtc.idl.json"
MANAGED_API_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed_api.json"
MANAGED_HANDLES_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed.json"
SYMBOL_CONTRACT_SPEC_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.symbol_contract.sources.json"
SYMBOL_CONTRACT_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.symbol_contract.json"
SRC_ROOT = REPO_ROOT / "src" / "LumenRTC"
SYMBOL_CONTRACT_GENERATOR_PATH = (
    REPO_ROOT / "tools" / "abi_framework" / "generator_sdk" / "symbol_contract_generator.py"
)


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def collect_manual_native_refs() -> set[str]:
    pattern = re.compile(r"NativeMethods\.(lrtc_[A-Za-z0-9_]+)\b")
    refs: set[str] = set()
    for path in SRC_ROOT.rglob("*.cs"):
        normalized = str(path).replace("\\", "/")
        if "/obj/" in normalized or "/bin/" in normalized:
            continue
        text = path.read_text(encoding="utf-8")
        refs.update(pattern.findall(text))
    return refs


class FunctionCoverageTests(unittest.TestCase):
    def test_symbol_contract_codegen_check_is_clean(self) -> None:
        command = [
            "python3",
            str(SYMBOL_CONTRACT_GENERATOR_PATH),
            "--idl",
            str(IDL_PATH),
            "--spec",
            str(SYMBOL_CONTRACT_SPEC_PATH),
            "--out",
            str(SYMBOL_CONTRACT_PATH),
            "--check",
        ]
        result = subprocess.run(command, cwd=REPO_ROOT, capture_output=True, text=True)
        if result.returncode != 0:
            combined = (result.stdout + "\n" + result.stderr).strip()
            self.fail(f"symbol contract codegen --check failed:\n{combined}")

    def test_idl_functions_are_covered_by_managed_surface(self) -> None:
        idl = load_json(IDL_PATH)
        managed_api = load_json(MANAGED_API_PATH)
        managed_handles = load_json(MANAGED_HANDLES_PATH)
        symbol_contract = load_json(SYMBOL_CONTRACT_PATH)

        idl_functions = {
            item.get("name")
            for item in idl.get("functions", [])
            if isinstance(item, dict) and isinstance(item.get("name"), str)
        }

        required_native = {
            item
            for item in managed_api.get("required_native_functions", [])
            if isinstance(item, str) and item
        }

        handle_release_methods = {
            entry.get("release")
            for entry in managed_handles.get("handles", [])
            if isinstance(entry, dict) and isinstance(entry.get("release"), str) and entry.get("release")
        }

        manual_refs = collect_manual_native_refs()

        covered = required_native | handle_release_methods | manual_refs
        missing = sorted(name for name in idl_functions if name not in covered)
        contract_symbols = {
            item
            for item in symbol_contract.get("symbols", [])
            if isinstance(item, str) and item
        }
        expected_missing = sorted(name for name in idl_functions if name not in contract_symbols)
        expected_extra = sorted(name for name in contract_symbols if name not in idl_functions)

        self.assertFalse(
            missing,
            "IDL functions missing managed coverage: " + ", ".join(missing),
        )
        self.assertFalse(
            expected_missing,
            "Symbol contract is missing IDL symbols: " + ", ".join(expected_missing),
        )
        self.assertFalse(
            expected_extra,
            "Symbol contract has non-ABI symbols: " + ", ".join(expected_extra),
        )


if __name__ == "__main__":
    unittest.main()
