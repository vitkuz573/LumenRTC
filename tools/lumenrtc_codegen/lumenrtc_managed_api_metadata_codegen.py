#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


TOOL_PATH = "tools/lumenrtc_codegen/lumenrtc_managed_api_metadata_codegen.py"
DEFAULT_NATIVE_CALL_PATTERN = r"\bNativeMethods\.([A-Za-z_][A-Za-z0-9_]*)\b"

CORE_SRC = Path(__file__).resolve().parents[1] / "abi_codegen_core" / "src"
if str(CORE_SRC) not in sys.path:
    sys.path.insert(0, str(CORE_SRC))

from abi_codegen_core.common import load_json_object, write_if_changed
from abi_codegen_core.required_functions import derive_required_functions


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


def resolve_discovery_rules(
    payload: dict[str, Any],
    cli_patterns: list[str],
    cli_function_pattern: str | None,
) -> tuple[list[str], str | None]:
    patterns = list(cli_patterns)
    function_name_pattern = cli_function_pattern

    rules = payload.get("required_native_functions_rules")
    if isinstance(rules, dict):
        rules_patterns = rules.get("native_call_patterns")
        if isinstance(rules_patterns, list):
            for index, item in enumerate(rules_patterns):
                if not isinstance(item, str) or not item:
                    raise SystemExit(
                        f"managed_api.required_native_functions_rules.native_call_patterns[{index}] must be non-empty string"
                    )
                patterns.append(item)
        rules_function_pattern = rules.get("function_name_pattern")
        if function_name_pattern is None and isinstance(rules_function_pattern, str) and rules_function_pattern:
            function_name_pattern = rules_function_pattern

    if not patterns:
        patterns.append(DEFAULT_NATIVE_CALL_PATTERN)
    return patterns, function_name_pattern


def normalize_payload(
    payload: dict[str, Any],
    idl_names: set[str],
    native_call_patterns: list[str],
    function_name_pattern: str | None,
) -> dict[str, Any]:
    normalized = json.loads(json.dumps(payload))
    schema_version = normalized.get("schema_version")
    if schema_version != 2:
        raise SystemExit(f"managed_api.schema_version must be 2, got {schema_version!r}")
    namespace_name = normalized.get("namespace")
    if not isinstance(namespace_name, str) or not namespace_name:
        raise SystemExit("managed_api.namespace must be a non-empty string")

    required_native_functions = derive_required_functions(
        payload=normalized,
        idl_names=idl_names,
        native_call_patterns=native_call_patterns,
        function_name_pattern=function_name_pattern,
    )
    normalized["required_native_functions"] = required_native_functions
    return normalized


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--idl", required=True)
    parser.add_argument("--source", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--native-call-pattern", action="append", default=[])
    parser.add_argument("--function-name-pattern")
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    idl = load_json_object(Path(args.idl))
    source_payload = load_json_object(Path(args.source))
    idl_names = collect_idl_function_names(idl)
    discovery_patterns, function_name_pattern = resolve_discovery_rules(
        source_payload,
        cli_patterns=list(args.native_call_pattern),
        cli_function_pattern=args.function_name_pattern,
    )
    normalized_payload = normalize_payload(
        source_payload,
        idl_names,
        native_call_patterns=discovery_patterns,
        function_name_pattern=function_name_pattern,
    )

    output = json.dumps(normalized_payload, ensure_ascii=False, indent=2, sort_keys=False) + "\n"
    return write_if_changed(Path(args.out), output, args.check, args.dry_run)


if __name__ == "__main__":
    raise SystemExit(main())
