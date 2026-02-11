import json
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
IDL_PATH = REPO_ROOT / "abi" / "generated" / "lumenrtc" / "lumenrtc.idl.json"
META_PATH = REPO_ROOT / "abi" / "bindings" / "lumenrtc.interop.json"


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


class InteropMetadataTests(unittest.TestCase):
    def test_callback_typedef_and_struct_conventions_declared(self) -> None:
        meta = load_json(META_PATH)

        call_tokens = meta.get("callback_typedef_call_tokens", [])
        self.assertIsInstance(call_tokens, list)
        self.assertTrue(call_tokens, "callback_typedef_call_tokens must declare at least one token")
        self.assertTrue(all(isinstance(item, str) and item for item in call_tokens))

        callback_suffixes = meta.get("callback_struct_suffixes", [])
        self.assertIsInstance(callback_suffixes, list)
        self.assertTrue(callback_suffixes, "callback_struct_suffixes must declare at least one suffix")
        self.assertTrue(all(isinstance(item, str) and item for item in callback_suffixes))

        output_hints = meta.get("output_hints", {})
        self.assertIsInstance(output_hints, dict)
        pattern = output_hints.get("pattern")
        self.assertIsInstance(pattern, str)
        self.assertIn("{class}", pattern)
        self.assertIn("{section_pascal}", pattern)
        self.assertIn("{target}", pattern)

    def test_callback_metadata_settings_are_embedded_in_idl(self) -> None:
        idl = load_json(IDL_PATH)
        interop = ((idl.get("bindings") or {}).get("interop") or {})

        self.assertIn("callback_typedef_call_tokens", interop)
        self.assertIn("callback_struct_suffixes", interop)
        self.assertIn("output_hints", interop)

    def test_interop_metadata_covers_opaque_types(self) -> None:
        idl = load_json(IDL_PATH)
        meta = load_json(META_PATH)

        opaque_types = set(idl.get("header_types", {}).get("opaque_types", []))
        meta_opaque = meta.get("opaque_types", {})

        missing = sorted(opaque_types - set(meta_opaque.keys()))
        self.assertFalse(missing, f"Missing opaque type metadata: {missing}")

        without_release = sorted(
            name for name, payload in meta_opaque.items() if isinstance(payload, dict) and not payload.get("release")
        )
        self.assertFalse(without_release, f"Opaque types missing release: {without_release}")

    def test_interop_metadata_override_targets_exist(self) -> None:
        idl = load_json(IDL_PATH)
        meta = load_json(META_PATH)

        structs = idl.get("header_types", {}).get("structs", {})
        overrides = meta.get("struct_field_overrides", {})
        missing = []
        for key in overrides.keys():
            if "." not in key:
                missing.append(key)
                continue
            struct_name, field_name = key.split(".", 1)
            struct = structs.get(struct_name)
            if not struct:
                missing.append(key)
                continue
            fields = {item.get("name") for item in struct.get("fields", [])}
            if field_name not in fields:
                missing.append(key)
        self.assertFalse(missing, f"Invalid struct_field_overrides entries: {missing}")

    def test_callback_field_overrides_reference_fields(self) -> None:
        idl = load_json(IDL_PATH)
        meta = load_json(META_PATH)

        structs = idl.get("header_types", {}).get("structs", {})
        callback_suffixes = meta.get("callback_struct_suffixes", ["_callbacks_t"])
        callback_structs = [
            payload
            for name, payload in structs.items()
            if isinstance(name, str) and any(name.endswith(suffix) for suffix in callback_suffixes)
        ]
        callback_fields = set()
        for struct in callback_structs:
            for field in struct.get("fields", []):
                callback_fields.add(field.get("name"))

        overrides = meta.get("callback_field_overrides", {})
        missing = sorted(name for name in overrides.keys() if name not in callback_fields)
        self.assertFalse(missing, f"Invalid callback_field_overrides entries: {missing}")

    def test_function_parameter_overrides_reference_existing_items(self) -> None:
        idl = load_json(IDL_PATH)
        meta = load_json(META_PATH)

        functions = {
            item.get("name"): {param.get("name") for param in item.get("parameters", [])}
            for item in idl.get("functions", [])
            if isinstance(item, dict) and isinstance(item.get("name"), str)
        }

        overrides = meta.get("functions", {})
        missing_functions = []
        missing_parameters = []

        for function_name, payload in overrides.items():
            if function_name not in functions:
                missing_functions.append(function_name)
                continue

            if not isinstance(payload, dict):
                continue

            parameters = payload.get("parameters", {})
            if not isinstance(parameters, dict):
                continue

            known_parameters = functions[function_name]
            for parameter_name in parameters.keys():
                if parameter_name not in known_parameters:
                    missing_parameters.append(f"{function_name}.{parameter_name}")

        self.assertFalse(missing_functions, f"Function overrides reference unknown functions: {missing_functions}")
        self.assertFalse(missing_parameters, f"Function overrides reference unknown parameters: {missing_parameters}")

    def test_struct_layout_overrides_reference_existing_structs(self) -> None:
        idl = load_json(IDL_PATH)
        meta = load_json(META_PATH)

        structs = idl.get("header_types", {}).get("structs", {})
        overrides = meta.get("struct_layout_overrides", {})

        missing_structs = sorted(name for name in overrides.keys() if name not in structs)
        self.assertFalse(missing_structs, f"Invalid struct_layout_overrides entries: {missing_structs}")

        invalid_pack = []
        for name, payload in overrides.items():
            pack_value = None
            if isinstance(payload, int):
                pack_value = payload
            elif isinstance(payload, dict):
                pack_value = payload.get("pack")

            if pack_value is not None and (not isinstance(pack_value, int) or pack_value <= 0):
                invalid_pack.append(name)

        self.assertFalse(invalid_pack, f"struct_layout_overrides.pack must be positive integer: {invalid_pack}")


if __name__ == "__main__":
    unittest.main()
