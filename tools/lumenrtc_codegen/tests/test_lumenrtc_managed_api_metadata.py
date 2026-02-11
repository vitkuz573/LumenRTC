import json
import subprocess
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
IDL_PATH = REPO_ROOT / "abi" / "generated" / "lumenrtc" / "lumenrtc.idl.json"
MANAGED_API_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed_api.json"
MANAGED_API_SOURCE_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed_api.source.json"
METADATA_GENERATOR_PATH = REPO_ROOT / "tools" / "lumenrtc_codegen" / "lumenrtc_managed_api_metadata_codegen.py"
GENERATOR_PATH = REPO_ROOT / "tools" / "lumenrtc_codegen" / "lumenrtc_managed_api_codegen.py"


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


class ManagedApiMetadataTests(unittest.TestCase):
    def test_managed_api_schema_and_required_functions(self) -> None:
        idl = load_json(IDL_PATH)
        managed_api = load_json(MANAGED_API_PATH)

        self.assertEqual(managed_api.get("schema_version"), 2)
        self.assertIsInstance(managed_api.get("namespace"), str)
        self.assertTrue(managed_api.get("namespace"))

        output_hints = managed_api.get("output_hints", {})
        self.assertIsInstance(output_hints, dict)
        self.assertTrue(output_hints)
        self.assertSetEqual(
            set(output_hints.keys()),
            {"pattern", "suffix"},
            "managed_api output_hints must use canonical keys only",
        )
        pattern = output_hints.get("pattern")
        self.assertIsInstance(pattern, str)
        self.assertIn("{section_pascal}", pattern)

        auto_abi_surface = managed_api.get("auto_abi_surface", {})
        self.assertIsInstance(auto_abi_surface, dict)
        self.assertTrue(auto_abi_surface)
        self.assertTrue(auto_abi_surface.get("enabled"))
        self.assertEqual(auto_abi_surface.get("method_prefix"), "Abi")
        self.assertEqual(auto_abi_surface.get("section_suffix"), "_abi_surface")
        self.assertIn("include_deprecated", auto_abi_surface)
        public_facade = auto_abi_surface.get("public_facade", {})
        self.assertIsInstance(public_facade, dict)
        self.assertTrue(public_facade)
        self.assertTrue(public_facade.get("enabled"))
        self.assertEqual(public_facade.get("class_suffix"), "_abi_facade")
        self.assertEqual(public_facade.get("method_prefix"), "Raw")
        self.assertEqual(public_facade.get("section_suffix"), "_abi_facade")
        self.assertTrue(public_facade.get("allow_int_ptr"))

        idl_function_names = {
            item.get("name")
            for item in idl.get("functions", [])
            if isinstance(item, dict) and isinstance(item.get("name"), str)
        }

        required_native = managed_api.get("required_native_functions", [])
        self.assertIsInstance(required_native, list)
        self.assertTrue(required_native)

        missing = sorted(name for name in required_native if name not in idl_function_names)
        self.assertFalse(missing, f"required_native_functions missing from IDL: {missing}")

    def test_managed_api_metadata_codegen_check_is_clean(self) -> None:
        command = [
            "python3",
            str(METADATA_GENERATOR_PATH),
            "--idl",
            str(IDL_PATH),
            "--source",
            str(MANAGED_API_SOURCE_PATH),
            "--out",
            str(MANAGED_API_PATH),
            "--check",
        ]
        result = subprocess.run(command, cwd=REPO_ROOT, capture_output=True, text=True)
        if result.returncode != 0:
            combined = (result.stdout + "\n" + result.stderr).strip()
            self.fail(f"managed API metadata codegen --check failed:\n{combined}")

    def test_managed_api_codegen_check_is_clean(self) -> None:
        command = [
            "python3",
            str(GENERATOR_PATH),
            "--idl",
            str(IDL_PATH),
            "--managed-api",
            str(MANAGED_API_PATH),
            "--out-native-handles",
            str(REPO_ROOT / "native" / "src" / "lumenrtc_impl_handles.generated.h"),
            "--check",
        ]
        result = subprocess.run(command, cwd=REPO_ROOT, capture_output=True, text=True)
        if result.returncode != 0:
            combined = (result.stdout + "\n" + result.stderr).strip()
            self.fail(f"managed API codegen --check failed:\n{combined}")


if __name__ == "__main__":
    unittest.main()
