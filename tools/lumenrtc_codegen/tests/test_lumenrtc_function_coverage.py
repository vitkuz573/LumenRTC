import json
import re
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
IDL_PATH = REPO_ROOT / "abi" / "generated" / "lumenrtc" / "lumenrtc.idl.json"
MANAGED_API_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed_api.json"
MANAGED_HANDLES_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed.json"
SRC_ROOT = REPO_ROOT / "src" / "LumenRTC"


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
    def test_idl_functions_are_covered_by_managed_surface(self) -> None:
        idl = load_json(IDL_PATH)
        managed_api = load_json(MANAGED_API_PATH)
        managed_handles = load_json(MANAGED_HANDLES_PATH)

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

        self.assertFalse(
            missing,
            "IDL functions missing managed coverage: " + ", ".join(missing),
        )


if __name__ == "__main__":
    unittest.main()
