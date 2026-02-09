import json
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
IDL_PATH = REPO_ROOT / "abi" / "generated" / "lumenrtc" / "lumenrtc.idl.json"
MANAGED_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.managed.json"


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


class ManagedMetadataTests(unittest.TestCase):
    def test_managed_handle_metadata_matches_idl(self) -> None:
        idl = load_json(IDL_PATH)
        managed = load_json(MANAGED_PATH)

        funcs = {f["name"]: f for f in idl.get("functions", [])}
        opaque = set(idl.get("header_types", {}).get("opaque_types", []))

        handles = managed.get("handles", [])
        self.assertIsInstance(handles, list)

        seen_cs = set()
        for entry in handles:
            self.assertIsInstance(entry, dict)
            cs_type = entry.get("cs_type")
            release = entry.get("release")
            c_handle_type = entry.get("c_handle_type")

            self.assertIsInstance(cs_type, str)
            self.assertTrue(cs_type)
            self.assertIsInstance(release, str)
            self.assertTrue(release)
            self.assertIsInstance(c_handle_type, str)
            self.assertTrue(c_handle_type)

            self.assertNotIn(cs_type, seen_cs)
            seen_cs.add(cs_type)

            opaque_name = c_handle_type[:-1] if c_handle_type.endswith("*") else c_handle_type
            self.assertIn(opaque_name, opaque)
            self.assertIn(release, funcs)
            params = funcs[release].get("parameters", [])
            self.assertTrue(params and params[0].get("c_type") == c_handle_type)


if __name__ == "__main__":
    unittest.main()
