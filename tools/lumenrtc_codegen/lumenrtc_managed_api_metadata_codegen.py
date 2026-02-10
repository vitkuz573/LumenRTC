#!/usr/bin/env python3
from __future__ import annotations

import argparse
import difflib
import json
import re
from pathlib import Path
from typing import Any


TOOL_PATH = "tools/lumenrtc_codegen/lumenrtc_managed_api_metadata_codegen.py"
NATIVE_CALL_PATTERN = re.compile(r"\bNativeMethods\.(lrtc_[a-z0-9_]+)\b")


def load_json(path: Path) -> dict[str, Any]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise SystemExit(f"JSON root in '{path}' must be an object")
    return data


def write_if_changed(path: Path, content: str, check: bool, dry_run: bool) -> int:
    existing = path.read_text(encoding="utf-8") if path.exists() else ""
    if existing == content:
        return 0
    if check:
        diff = difflib.unified_diff(
            existing.splitlines(),
            content.splitlines(),
            fromfile=f"a/{path}",
            tofile=f"b/{path}",
            lineterm="",
        )
        print("\n".join(diff))
        return 1
    if not dry_run:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
    return 0


def collect_idl_function_names(idl: dict[str, Any]) -> set[str]:
    functions = idl.get("functions")
    if not isinstance(functions, list):
        raise SystemExit("IDL missing array 'functions'")

    names: set[str] = set()
    for index, item in enumerate(functions):
        if not isinstance(item, dict):
            raise SystemExit(f"IDL function at index {index} must be object")
        name = item.get("name")
        if not isinstance(name, str) or not name:
            raise SystemExit(f"IDL function at index {index} missing non-empty 'name'")
        names.add(name)
    return names


def iter_strings(value: Any) -> list[str]:
    if isinstance(value, str):
        return [value]
    if isinstance(value, list):
        result: list[str] = []
        for item in value:
            result.extend(iter_strings(item))
        return result
    if isinstance(value, dict):
        result: list[str] = []
        for item in value.values():
            result.extend(iter_strings(item))
        return result
    return []


def derive_required_native_functions(payload: dict[str, Any], idl_names: set[str]) -> list[str]:
    discovered: set[str] = set()
    for text in iter_strings(payload):
        for match in NATIVE_CALL_PATTERN.findall(text):
            if match in idl_names:
                discovered.add(match)
    return sorted(discovered)


def normalize_payload(payload: dict[str, Any], idl_names: set[str]) -> dict[str, Any]:
    normalized = json.loads(json.dumps(payload))
    schema_version = normalized.get("schema_version")
    if schema_version != 2:
        raise SystemExit(f"managed_api.schema_version must be 2, got {schema_version!r}")
    namespace_name = normalized.get("namespace")
    if not isinstance(namespace_name, str) or not namespace_name:
        raise SystemExit("managed_api.namespace must be a non-empty string")

    required_native_functions = derive_required_native_functions(normalized, idl_names)
    normalized["required_native_functions"] = required_native_functions
    return normalized


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--idl", required=True)
    parser.add_argument("--source", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    idl = load_json(Path(args.idl))
    source_payload = load_json(Path(args.source))
    idl_names = collect_idl_function_names(idl)
    normalized_payload = normalize_payload(source_payload, idl_names)

    output = json.dumps(normalized_payload, ensure_ascii=False, indent=2, sort_keys=False) + "\n"
    return write_if_changed(Path(args.out), output, args.check, args.dry_run)


if __name__ == "__main__":
    raise SystemExit(main())
