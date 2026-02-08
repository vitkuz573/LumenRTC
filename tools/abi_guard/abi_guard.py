#!/usr/bin/env python3
from __future__ import annotations

import argparse
import ast
import datetime as dt
import glob
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

TOOL_VERSION = "2.0.0"


class AbiGuardError(Exception):
    pass


@dataclass(frozen=True)
class AbiVersion:
    major: int
    minor: int
    patch: int

    def as_tuple(self) -> tuple[int, int, int]:
        return (self.major, self.minor, self.patch)

    def as_dict(self) -> dict[str, int]:
        return {
            "major": self.major,
            "minor": self.minor,
            "patch": self.patch,
        }


@dataclass(frozen=True)
class TypePolicy:
    enable_enums: bool
    enable_structs: bool
    enum_name_pattern: str
    struct_name_pattern: str
    ignore_enums: tuple[str, ...]
    ignore_structs: tuple[str, ...]
    struct_tail_addition_is_breaking: bool

    def as_dict(self) -> dict[str, Any]:
        return {
            "enable_enums": self.enable_enums,
            "enable_structs": self.enable_structs,
            "enum_name_pattern": self.enum_name_pattern,
            "struct_name_pattern": self.struct_name_pattern,
            "ignore_enums": list(self.ignore_enums),
            "ignore_structs": list(self.ignore_structs),
            "struct_tail_addition_is_breaking": self.struct_tail_addition_is_breaking,
        }


def normalize_ws(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()


def strip_c_comments(content: str) -> str:
    content = re.sub(r"/\*.*?\*/", "", content, flags=re.S)
    content = re.sub(r"//.*?$", "", content, flags=re.M)
    return content


def load_json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except OSError as exc:
        raise AbiGuardError(f"Unable to read JSON file '{path}': {exc}") from exc
    except json.JSONDecodeError as exc:
        raise AbiGuardError(f"Invalid JSON in '{path}': {exc}") from exc


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def resolve_target(config: dict[str, Any], target_name: str) -> dict[str, Any]:
    targets = config.get("targets")
    if not isinstance(targets, dict):
        raise AbiGuardError("Config is missing required object: 'targets'.")
    target = targets.get(target_name)
    if not isinstance(target, dict):
        known = ", ".join(sorted(targets.keys()))
        raise AbiGuardError(f"Unknown target '{target_name}'. Known targets: {known or '<none>'}")
    return target


def ensure_relative_path(root: Path, value: str) -> Path:
    path = Path(value)
    if path.is_absolute():
        return path
    return root / path


def to_repo_relative(path: Path, repo_root: Path) -> str:
    try:
        return str(path.resolve().relative_to(repo_root.resolve()))
    except ValueError:
        return str(path.resolve())


def iter_files_from_entries(root: Path, entries: list[str], suffix: str) -> list[Path]:
    paths: list[Path] = []
    seen: set[Path] = set()

    for entry in entries:
        expanded: list[Path] = []
        entry_path = ensure_relative_path(root, entry)

        if any(ch in entry for ch in "*?[]"):
            for match in glob.glob(str(entry_path), recursive=True):
                expanded.append(Path(match))
        elif entry_path.is_dir():
            expanded.extend(entry_path.rglob(f"*{suffix}"))
        elif entry_path.is_file():
            expanded.append(entry_path)

        for candidate in expanded:
            if not candidate.is_file() or candidate.suffix.lower() != suffix:
                continue
            if "bin" in candidate.parts or "obj" in candidate.parts:
                continue
            resolved = candidate.resolve()
            if resolved not in seen:
                seen.add(resolved)
                paths.append(resolved)

    return sorted(paths)


def extract_define_int(content: str, macro_name: str) -> int:
    match = re.search(rf"^\s*#\s*define\s+{re.escape(macro_name)}\s+([0-9]+)\b", content, flags=re.M)
    if not match:
        raise AbiGuardError(f"Required macro '{macro_name}' was not found.")
    return int(match.group(1))


def normalize_identifier_list(value: Any, key: str) -> tuple[str, ...]:
    if value is None:
        return tuple()
    if not isinstance(value, list):
        raise AbiGuardError(f"Target field '{key}' must be an array when specified.")
    out: list[str] = []
    for idx, item in enumerate(value):
        if not isinstance(item, str) or not item:
            raise AbiGuardError(f"Target field '{key}[{idx}]' must be a non-empty string.")
        out.append(item)
    return tuple(out)


def build_type_policy(header_cfg: dict[str, Any], symbol_prefix: str) -> TypePolicy:
    raw_policy = header_cfg.get("types")
    if raw_policy is None:
        raw_policy = {}
    if not isinstance(raw_policy, dict):
        raise AbiGuardError("Target field 'header.types' must be an object when specified.")

    default_pattern = f"^{re.escape(symbol_prefix)}"

    enable_enums = bool(raw_policy.get("enable_enums", True))
    enable_structs = bool(raw_policy.get("enable_structs", True))
    enum_name_pattern = str(raw_policy.get("enum_name_pattern", default_pattern))
    struct_name_pattern = str(raw_policy.get("struct_name_pattern", default_pattern))

    ignore_enums = normalize_identifier_list(raw_policy.get("ignore_enums"), "header.types.ignore_enums")
    ignore_structs = normalize_identifier_list(raw_policy.get("ignore_structs"), "header.types.ignore_structs")

    struct_tail_addition_is_breaking = bool(raw_policy.get("struct_tail_addition_is_breaking", True))

    try:
        re.compile(enum_name_pattern)
    except re.error as exc:
        raise AbiGuardError(f"Invalid regex in header.types.enum_name_pattern: {exc}") from exc

    try:
        re.compile(struct_name_pattern)
    except re.error as exc:
        raise AbiGuardError(f"Invalid regex in header.types.struct_name_pattern: {exc}") from exc

    return TypePolicy(
        enable_enums=enable_enums,
        enable_structs=enable_structs,
        enum_name_pattern=enum_name_pattern,
        struct_name_pattern=struct_name_pattern,
        ignore_enums=ignore_enums,
        ignore_structs=ignore_structs,
        struct_tail_addition_is_breaking=struct_tail_addition_is_breaking,
    )


def stable_hash(value: Any) -> str:
    payload = json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def sanitize_c_int_expr(expr: str) -> str:
    compact = normalize_ws(expr)
    compact = re.sub(r"\b(0[xX][0-9A-Fa-f]+)([uUlL]+)\b", r"\1", compact)
    compact = re.sub(r"\b([0-9]+)([uUlL]+)\b", r"\1", compact)
    return compact


def eval_c_int_expr(expr: str) -> int | None:
    sanitized = sanitize_c_int_expr(expr)
    try:
        tree = ast.parse(sanitized, mode="eval")
    except SyntaxError:
        return None

    def _eval(node: ast.AST) -> int:
        if isinstance(node, ast.Expression):
            return _eval(node.body)
        if isinstance(node, ast.Constant):
            if isinstance(node.value, int):
                return int(node.value)
            raise ValueError("non-int literal")
        if isinstance(node, ast.UnaryOp):
            value = _eval(node.operand)
            if isinstance(node.op, ast.UAdd):
                return +value
            if isinstance(node.op, ast.USub):
                return -value
            if isinstance(node.op, ast.Invert):
                return ~value
            raise ValueError("unsupported unary op")
        if isinstance(node, ast.BinOp):
            left = _eval(node.left)
            right = _eval(node.right)
            if isinstance(node.op, ast.Add):
                return left + right
            if isinstance(node.op, ast.Sub):
                return left - right
            if isinstance(node.op, ast.Mult):
                return left * right
            if isinstance(node.op, ast.FloorDiv) or isinstance(node.op, ast.Div):
                return left // right
            if isinstance(node.op, ast.Mod):
                return left % right
            if isinstance(node.op, ast.LShift):
                return left << right
            if isinstance(node.op, ast.RShift):
                return left >> right
            if isinstance(node.op, ast.BitOr):
                return left | right
            if isinstance(node.op, ast.BitAnd):
                return left & right
            if isinstance(node.op, ast.BitXor):
                return left ^ right
            raise ValueError("unsupported binary op")
        raise ValueError("unsupported expression")

    try:
        return _eval(tree)
    except Exception:
        return None


def parse_enum_blocks(content: str, policy: TypePolicy) -> dict[str, Any]:
    if not policy.enable_enums:
        return {}

    enum_pattern = re.compile(
        r"typedef\s+enum(?:\s+[A-Za-z_][A-Za-z0-9_]*)?\s*\{(?P<body>.*?)\}\s*(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*;",
        flags=re.S,
    )
    name_re = re.compile(policy.enum_name_pattern)

    enums: dict[str, Any] = {}

    for match in enum_pattern.finditer(content):
        enum_name = match.group("name")
        if enum_name in policy.ignore_enums:
            continue
        if not name_re.search(enum_name):
            continue

        body = match.group("body")
        raw_items = [normalize_ws(item) for item in body.split(",")]

        members: list[dict[str, Any]] = []
        last_value: int | None = None
        next_from_last = True

        for raw_item in raw_items:
            item = raw_item.strip()
            if not item:
                continue

            m = re.match(r"^(?P<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*=\s*(?P<expr>.+))?$", item)
            if not m:
                continue

            member_name = m.group("name")
            expr = m.group("expr")

            value_expr: str | None = None
            value: int | None
            if expr is None:
                if next_from_last and last_value is not None:
                    value = last_value + 1
                elif not members:
                    value = 0
                else:
                    value = None
                value_expr = None
            else:
                value_expr = sanitize_c_int_expr(expr)
                value = eval_c_int_expr(value_expr)

            if value is not None:
                last_value = value
                next_from_last = True
            else:
                next_from_last = False

            members.append(
                {
                    "name": member_name,
                    "value": value,
                    "value_expr": value_expr,
                }
            )

        enums[enum_name] = {
            "member_count": len(members),
            "members": members,
            "fingerprint": stable_hash(members),
        }

    return {name: enums[name] for name in sorted(enums.keys())}


def split_struct_declarations(body: str) -> list[str]:
    declarations: list[str] = []
    buffer = ""

    for line in body.splitlines():
        stripped = normalize_ws(line)
        if not stripped or stripped.startswith("#"):
            continue

        buffer = f"{buffer} {stripped}".strip() if buffer else stripped

        while ";" in buffer:
            before, after = buffer.split(";", 1)
            decl = normalize_ws(before)
            if decl:
                declarations.append(decl)
            buffer = normalize_ws(after)

    return declarations


def parse_struct_field(decl: str, index: int) -> dict[str, str]:
    function_ptr = re.search(r"\(\s*\*\s*(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)\s*\(", decl)
    if function_ptr:
        name = function_ptr.group("name")
        return {
            "name": name,
            "declaration": normalize_ws(decl),
        }

    bitfield = re.match(r"^(?P<left>.+?)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?P<bits>.+)$", decl)
    if bitfield:
        return {
            "name": bitfield.group("name"),
            "declaration": normalize_ws(decl),
        }

    array_field = re.match(
        r"^(?P<left>.+?)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)(?P<array>(?:\s*\[[^\]]+\])+)\s*$",
        decl,
    )
    if array_field:
        return {
            "name": array_field.group("name"),
            "declaration": normalize_ws(decl),
        }

    regular = re.match(r"^(?P<left>.+?)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*$", decl)
    if regular:
        return {
            "name": regular.group("name"),
            "declaration": normalize_ws(decl),
        }

    return {
        "name": f"__unnamed_{index}",
        "declaration": normalize_ws(decl),
    }


def parse_struct_blocks(content: str, policy: TypePolicy) -> dict[str, Any]:
    if not policy.enable_structs:
        return {}

    struct_pattern = re.compile(
        r"typedef\s+struct(?:\s+[A-Za-z_][A-Za-z0-9_]*)?\s*\{(?P<body>.*?)\}\s*(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*;",
        flags=re.S,
    )
    name_re = re.compile(policy.struct_name_pattern)

    structs: dict[str, Any] = {}

    for match in struct_pattern.finditer(content):
        struct_name = match.group("name")
        if struct_name in policy.ignore_structs:
            continue
        if not name_re.search(struct_name):
            continue

        body = match.group("body")
        declarations = split_struct_declarations(body)
        fields = [parse_struct_field(decl, idx) for idx, decl in enumerate(declarations)]

        structs[struct_name] = {
            "field_count": len(fields),
            "fields": fields,
            "fingerprint": stable_hash(fields),
        }

    return {name: structs[name] for name in sorted(structs.keys())}


def parse_c_header(
    header_path: Path,
    api_macro: str,
    call_macro: str,
    symbol_prefix: str,
    version_macros: dict[str, str],
    type_policy: TypePolicy,
) -> tuple[dict[str, Any], AbiVersion]:
    try:
        raw = header_path.read_text(encoding="utf-8")
    except OSError as exc:
        raise AbiGuardError(f"Unable to read header '{header_path}': {exc}") from exc

    content = strip_c_comments(raw)
    pattern = re.compile(
        rf"{re.escape(api_macro)}\s+(?P<ret>.*?)\s+{re.escape(call_macro)}\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?P<params>.*?)\)\s*;",
        flags=re.S,
    )

    functions: dict[str, dict[str, str]] = {}
    for match in pattern.finditer(content):
        name = match.group("name")
        if symbol_prefix and not name.startswith(symbol_prefix):
            continue
        return_type = normalize_ws(match.group("ret"))
        params = normalize_ws(match.group("params"))
        signature = f"{return_type} ({params})"
        functions[name] = {
            "return_type": return_type,
            "parameters": params,
            "signature": signature,
        }

    if not functions:
        raise AbiGuardError(
            f"No ABI functions were found in '{header_path}' with macros '{api_macro}'/'{call_macro}'."
        )

    major = extract_define_int(content, version_macros["major"])
    minor = extract_define_int(content, version_macros["minor"])
    patch = extract_define_int(content, version_macros["patch"])

    enums = parse_enum_blocks(content=content, policy=type_policy)
    structs = parse_struct_blocks(content=content, policy=type_policy)

    header_payload = {
        "path": str(header_path),
        "function_count": len(functions),
        "symbols": sorted(functions.keys()),
        "functions": {name: functions[name] for name in sorted(functions.keys())},
        "enum_count": len(enums),
        "enums": enums,
        "struct_count": len(structs),
        "structs": structs,
    }
    return header_payload, AbiVersion(major=major, minor=minor, patch=patch)


def parse_entry_point(attr_args: str) -> str | None:
    match = re.search(r"\bEntryPoint\s*=\s*\"([^\"]+)\"", attr_args)
    if match:
        return match.group(1)
    return None


def parse_csharp_pinvoke(file_paths: list[Path], symbol_prefix: str) -> dict[str, Any]:
    attribute_method_pattern = re.compile(
        r"\[(?:DllImport|LibraryImport)\((?P<attr>.*?)\)\]\s*"
        r"(?P<decl>(?:public|internal|private|protected)\s+static\s+extern\s+[^;]+;)",
        flags=re.S,
    )
    method_name_pattern = re.compile(
        r"\bextern\s+[^\(;]+\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        flags=re.S,
    )

    symbols: set[str] = set()
    declarations: dict[str, list[str]] = {}

    for path in file_paths:
        try:
            text = path.read_text(encoding="utf-8")
        except OSError as exc:
            raise AbiGuardError(f"Unable to read C# file '{path}': {exc}") from exc

        for match in attribute_method_pattern.finditer(text):
            attr = match.group("attr")
            decl = match.group("decl")
            name_match = method_name_pattern.search(decl)
            if not name_match:
                continue
            method_name = name_match.group("name")
            symbol_name = parse_entry_point(attr) or method_name

            if symbol_prefix and not symbol_name.startswith(symbol_prefix):
                continue

            symbols.add(symbol_name)
            declarations.setdefault(symbol_name, []).append(str(path))

    if not symbols:
        raise AbiGuardError("No P/Invoke symbols were found in configured C# sources.")

    return {
        "file_count": len(file_paths),
        "symbols": sorted(symbols),
        "symbol_count": len(symbols),
        "declarations": {k: sorted(set(v)) for k, v in sorted(declarations.items())},
    }


def run_first_command(commands: list[list[str]]) -> tuple[str, list[str]]:
    errors: list[str] = []
    for command in commands:
        exe = command[0]
        if shutil.which(exe) is None:
            continue
        try:
            proc = subprocess.run(command, check=True, capture_output=True, text=True)
            return proc.stdout, command
        except subprocess.CalledProcessError as exc:
            errors.append(f"{' '.join(command)}: {exc.stderr.strip() or exc.stdout.strip()}")

    if errors:
        raise AbiGuardError("Failed to query binary exports. " + " | ".join(errors))
    raise AbiGuardError("No export listing tool found. Install 'nm', 'llvm-nm', or equivalent.")


def parse_nm_exports(output: str) -> list[str]:
    exports: set[str] = set()
    for raw_line in output.splitlines():
        line = raw_line.strip()
        if not line or line.endswith(":"):
            continue
        parts = line.split()
        if len(parts) < 2:
            continue
        symbol = parts[-1]
        if symbol in {"|", "<"}:
            continue
        exports.add(symbol)
    return sorted(exports)


def parse_dumpbin_exports(output: str) -> list[str]:
    exports: set[str] = set()
    export_line = re.compile(r"^\s+\d+\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+(\S+)$")
    for raw_line in output.splitlines():
        line = raw_line.rstrip()
        match = export_line.match(line)
        if match:
            exports.add(match.group(1))
    return sorted(exports)


def canonicalize_prefixed_symbol(symbol: str, symbol_prefix: str) -> str | None:
    raw = symbol
    if raw.startswith("_"):
        raw = raw[1:]
    base = raw
    if "@" in base:
        left, right = base.rsplit("@", 1)
        if right.isdigit():
            base = left
    if symbol_prefix and not base.startswith(symbol_prefix):
        return None
    return base


def extract_binary_exports(binary_path: Path, symbol_prefix: str, allow_non_prefixed_exports: bool) -> dict[str, Any]:
    if not binary_path.exists():
        return {
            "available": False,
            "path": str(binary_path),
            "tool": None,
            "symbol_count": 0,
            "symbols": [],
            "raw_export_count": 0,
            "non_prefixed_export_count": 0,
            "non_prefixed_exports": [],
            "allow_non_prefixed_exports": allow_non_prefixed_exports,
        }

    commands: list[list[str]] = []
    if sys.platform.startswith("linux"):
        commands.append(["nm", "-D", "--defined-only", str(binary_path)])
        commands.append(["llvm-nm", "-D", "--defined-only", str(binary_path)])
    elif sys.platform == "darwin":
        commands.append(["nm", "-gU", str(binary_path)])
        commands.append(["llvm-nm", "-gU", str(binary_path)])
    elif os.name == "nt":
        commands.append(["dumpbin", "/exports", str(binary_path)])
        commands.append(["llvm-nm", "--defined-only", str(binary_path)])
    else:
        commands.append(["nm", "--defined-only", str(binary_path)])
        commands.append(["llvm-nm", "--defined-only", str(binary_path)])

    raw_output, used_tool = run_first_command(commands)
    if used_tool and used_tool[0].lower() == "dumpbin":
        raw_exports = parse_dumpbin_exports(raw_output)
    else:
        raw_exports = parse_nm_exports(raw_output)

    canonical_symbols: set[str] = set()
    non_prefixed: list[str] = []
    for raw_symbol in raw_exports:
        canonical = canonicalize_prefixed_symbol(raw_symbol, symbol_prefix)
        if canonical is None:
            non_prefixed.append(raw_symbol)
            continue
        canonical_symbols.add(canonical)

    return {
        "available": True,
        "path": str(binary_path),
        "tool": " ".join(used_tool),
        "symbol_count": len(canonical_symbols),
        "symbols": sorted(canonical_symbols),
        "raw_export_count": len(raw_exports),
        "non_prefixed_export_count": len(non_prefixed),
        "non_prefixed_exports": non_prefixed,
        "allow_non_prefixed_exports": allow_non_prefixed_exports,
    }


def require_dict(value: Any, key: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise AbiGuardError(f"Target is missing required object '{key}'.")
    return value


def require_str(value: Any, key: str) -> str:
    if not isinstance(value, str) or not value:
        raise AbiGuardError(f"Target field '{key}' must be a non-empty string.")
    return value


def build_snapshot(config: dict[str, Any], target_name: str, repo_root: Path, binary_override: str | None, skip_binary: bool) -> dict[str, Any]:
    target = resolve_target(config, target_name)

    header_cfg = require_dict(target.get("header"), "header")
    header_path = ensure_relative_path(repo_root, require_str(header_cfg.get("path"), "header.path")).resolve()
    api_macro = require_str(header_cfg.get("api_macro"), "header.api_macro")
    call_macro = require_str(header_cfg.get("call_macro"), "header.call_macro")
    symbol_prefix = require_str(header_cfg.get("symbol_prefix"), "header.symbol_prefix")
    type_policy = build_type_policy(header_cfg=header_cfg, symbol_prefix=symbol_prefix)

    version_macros_cfg = require_dict(header_cfg.get("version_macros"), "header.version_macros")
    version_macros = {
        "major": require_str(version_macros_cfg.get("major"), "header.version_macros.major"),
        "minor": require_str(version_macros_cfg.get("minor"), "header.version_macros.minor"),
        "patch": require_str(version_macros_cfg.get("patch"), "header.version_macros.patch"),
    }

    header_payload, abi_version = parse_c_header(
        header_path=header_path,
        api_macro=api_macro,
        call_macro=call_macro,
        symbol_prefix=symbol_prefix,
        version_macros=version_macros,
        type_policy=type_policy,
    )
    header_payload["path"] = to_repo_relative(header_path, repo_root)

    pinvoke_cfg = require_dict(target.get("pinvoke"), "pinvoke")
    pinvoke_entries = pinvoke_cfg.get("paths")
    if not isinstance(pinvoke_entries, list) or not pinvoke_entries:
        raise AbiGuardError("Target field 'pinvoke.paths' must be a non-empty array.")
    pinvoke_paths = iter_files_from_entries(repo_root, [str(x) for x in pinvoke_entries], ".cs")
    pinvoke_payload = parse_csharp_pinvoke(pinvoke_paths, symbol_prefix=symbol_prefix)
    pinvoke_payload["files"] = [to_repo_relative(path, repo_root) for path in pinvoke_paths]
    for symbol_name, locations in list(pinvoke_payload["declarations"].items()):
        pinvoke_payload["declarations"][symbol_name] = [
            to_repo_relative(Path(location), repo_root) for location in locations
        ]

    binary_payload: dict[str, Any]
    if skip_binary:
        binary_payload = {
            "available": False,
            "path": None,
            "tool": None,
            "symbol_count": 0,
            "symbols": [],
            "raw_export_count": 0,
            "non_prefixed_export_count": 0,
            "non_prefixed_exports": [],
            "allow_non_prefixed_exports": True,
            "skipped": True,
            "reason": "explicit_skip",
        }
    else:
        binary_cfg_obj = target.get("binary")
        if binary_override:
            allow_non_prefixed = False
            binary_path = ensure_relative_path(repo_root, binary_override).resolve()
            binary_payload = extract_binary_exports(
                binary_path=binary_path,
                symbol_prefix=symbol_prefix,
                allow_non_prefixed_exports=allow_non_prefixed,
            )
            binary_payload["path"] = to_repo_relative(binary_path, repo_root)
            binary_payload["skipped"] = False
        elif isinstance(binary_cfg_obj, dict):
            configured_path = require_str(binary_cfg_obj.get("path"), "binary.path")
            allow_non_prefixed = bool(binary_cfg_obj.get("allow_non_prefixed_exports", False))
            binary_path = ensure_relative_path(repo_root, configured_path).resolve()
            binary_payload = extract_binary_exports(
                binary_path=binary_path,
                symbol_prefix=symbol_prefix,
                allow_non_prefixed_exports=allow_non_prefixed,
            )
            binary_payload["path"] = to_repo_relative(binary_path, repo_root)
            binary_payload["skipped"] = False
        else:
            binary_payload = {
                "available": False,
                "path": None,
                "tool": None,
                "symbol_count": 0,
                "symbols": [],
                "raw_export_count": 0,
                "non_prefixed_export_count": 0,
                "non_prefixed_exports": [],
                "allow_non_prefixed_exports": True,
                "skipped": True,
                "reason": "not_configured",
            }

    return {
        "tool": {
            "name": "abi_guard",
            "version": TOOL_VERSION,
        },
        "target": target_name,
        "generated_at_utc": dt.datetime.now(tz=dt.timezone.utc).isoformat(),
        "policy": {
            "type_policy": type_policy.as_dict(),
            "strict_semver": True,
        },
        "abi_version": abi_version.as_dict(),
        "header": header_payload,
        "pinvoke": pinvoke_payload,
        "binary": binary_payload,
    }


def load_snapshot(path: Path) -> dict[str, Any]:
    return load_json(path)


def parse_snapshot_version(snapshot: dict[str, Any], label: str) -> AbiVersion:
    version_obj = snapshot.get("abi_version")
    if not isinstance(version_obj, dict):
        raise AbiGuardError(f"Snapshot '{label}' is missing abi_version.")
    try:
        major = int(version_obj["major"])
        minor = int(version_obj["minor"])
        patch = int(version_obj["patch"])
    except (KeyError, TypeError, ValueError) as exc:
        raise AbiGuardError(f"Snapshot '{label}' has invalid abi_version format.") from exc
    return AbiVersion(major=major, minor=minor, patch=patch)


def as_symbol_set(snapshot: dict[str, Any], section: str) -> set[str]:
    payload = snapshot.get(section)
    if not isinstance(payload, dict):
        raise AbiGuardError(f"Snapshot is missing section '{section}'.")
    symbols = payload.get("symbols")
    if not isinstance(symbols, list):
        raise AbiGuardError(f"Snapshot section '{section}' is missing symbols array.")
    return {str(x) for x in symbols}


def get_header_types(snapshot: dict[str, Any]) -> tuple[dict[str, Any], dict[str, Any]]:
    header = snapshot.get("header")
    if not isinstance(header, dict):
        return {}, {}

    enums = header.get("enums")
    structs = header.get("structs")

    out_enums = enums if isinstance(enums, dict) else {}
    out_structs = structs if isinstance(structs, dict) else {}
    return out_enums, out_structs


def compare_enum_sets(base_enums: dict[str, Any], curr_enums: dict[str, Any]) -> dict[str, Any]:
    base_names = set(base_enums.keys())
    curr_names = set(curr_enums.keys())

    removed_enums = sorted(base_names - curr_names)
    added_enums = sorted(curr_names - base_names)

    changed_enums: dict[str, Any] = {}
    breaking_changes: list[str] = []
    additive_changes: list[str] = []

    for name in sorted(base_names & curr_names):
        base_members = base_enums[name].get("members")
        curr_members = curr_enums[name].get("members")
        if not isinstance(base_members, list) or not isinstance(curr_members, list):
            changed_enums[name] = {
                "kind": "unknown",
                "reason": "enum members payload malformed",
            }
            breaking_changes.append(f"enum {name} malformed")
            continue

        base_map = {str(item.get("name")): item for item in base_members if isinstance(item, dict)}
        curr_map = {str(item.get("name")): item for item in curr_members if isinstance(item, dict)}

        removed_members = sorted(set(base_map.keys()) - set(curr_map.keys()))
        added_members = sorted(set(curr_map.keys()) - set(base_map.keys()))

        value_changed: list[str] = []
        for member_name in sorted(set(base_map.keys()) & set(curr_map.keys())):
            b = base_map[member_name]
            c = curr_map[member_name]
            if (b.get("value"), b.get("value_expr")) != (c.get("value"), c.get("value_expr")):
                value_changed.append(member_name)

        if removed_members or value_changed:
            changed_enums[name] = {
                "kind": "breaking",
                "removed_members": removed_members,
                "added_members": added_members,
                "value_changed": value_changed,
            }
            if removed_members:
                breaking_changes.append(f"enum {name} removed members: {', '.join(removed_members)}")
            if value_changed:
                breaking_changes.append(f"enum {name} changed values: {', '.join(value_changed)}")
            continue

        if added_members:
            changed_enums[name] = {
                "kind": "additive",
                "removed_members": [],
                "added_members": added_members,
                "value_changed": [],
            }
            additive_changes.append(f"enum {name} added members: {', '.join(added_members)}")

    if removed_enums:
        breaking_changes.append("removed enums: " + ", ".join(removed_enums))
    if added_enums:
        additive_changes.append("added enums: " + ", ".join(added_enums))

    return {
        "removed_enums": removed_enums,
        "added_enums": added_enums,
        "changed_enums": changed_enums,
        "breaking_changes": breaking_changes,
        "additive_changes": additive_changes,
    }


def compare_struct_sets(base_structs: dict[str, Any], curr_structs: dict[str, Any], struct_tail_addition_is_breaking: bool) -> dict[str, Any]:
    base_names = set(base_structs.keys())
    curr_names = set(curr_structs.keys())

    removed_structs = sorted(base_names - curr_names)
    added_structs = sorted(curr_names - base_names)

    changed_structs: dict[str, Any] = {}
    breaking_changes: list[str] = []
    additive_changes: list[str] = []

    for name in sorted(base_names & curr_names):
        base_fields = base_structs[name].get("fields")
        curr_fields = curr_structs[name].get("fields")
        if not isinstance(base_fields, list) or not isinstance(curr_fields, list):
            changed_structs[name] = {
                "kind": "unknown",
                "reason": "struct fields payload malformed",
            }
            breaking_changes.append(f"struct {name} malformed")
            continue

        base_decls = [normalize_ws(str(item.get("declaration"))) for item in base_fields if isinstance(item, dict)]
        curr_decls = [normalize_ws(str(item.get("declaration"))) for item in curr_fields if isinstance(item, dict)]

        if base_decls == curr_decls:
            continue

        base_names_seq = [str(item.get("name")) for item in base_fields if isinstance(item, dict)]
        curr_names_seq = [str(item.get("name")) for item in curr_fields if isinstance(item, dict)]

        removed_fields = sorted(set(base_names_seq) - set(curr_names_seq))
        added_fields = sorted(set(curr_names_seq) - set(base_names_seq))

        common = set(base_names_seq) & set(curr_names_seq)
        changed_fields: list[str] = []
        for field_name in sorted(common):
            b_idx = base_names_seq.index(field_name)
            c_idx = curr_names_seq.index(field_name)
            if base_decls[b_idx] != curr_decls[c_idx] or b_idx != c_idx:
                changed_fields.append(field_name)

        base_is_prefix = len(curr_decls) >= len(base_decls) and curr_decls[: len(base_decls)] == base_decls
        additive_tail = base_is_prefix and not struct_tail_addition_is_breaking

        if additive_tail:
            changed_structs[name] = {
                "kind": "additive",
                "removed_fields": removed_fields,
                "added_fields": added_fields,
                "changed_fields": changed_fields,
                "base_is_prefix": base_is_prefix,
            }
            additive_changes.append(f"struct {name} tail extended")
        else:
            changed_structs[name] = {
                "kind": "breaking",
                "removed_fields": removed_fields,
                "added_fields": added_fields,
                "changed_fields": changed_fields,
                "base_is_prefix": base_is_prefix,
            }
            breaking_changes.append(f"struct {name} layout changed")

    if removed_structs:
        breaking_changes.append("removed structs: " + ", ".join(removed_structs))
    if added_structs:
        additive_changes.append("added structs: " + ", ".join(added_structs))

    return {
        "removed_structs": removed_structs,
        "added_structs": added_structs,
        "changed_structs": changed_structs,
        "breaking_changes": breaking_changes,
        "additive_changes": additive_changes,
    }


def classify_change(has_breaking: bool, has_additive: bool) -> tuple[str, str]:
    if has_breaking:
        return "breaking", "major"
    if has_additive:
        return "additive", "minor"
    return "none", "none"


def recommended_version(baseline: AbiVersion, required_bump: str) -> AbiVersion:
    if required_bump == "major":
        return AbiVersion(baseline.major + 1, 0, 0)
    if required_bump == "minor":
        return AbiVersion(baseline.major, baseline.minor + 1, 0)
    return AbiVersion(baseline.major, baseline.minor, baseline.patch + 1)


def validate_version_policy(
    baseline_version: AbiVersion,
    current_version: AbiVersion,
    required_bump: str,
) -> tuple[bool, list[str]]:
    errors: list[str] = []

    if current_version.as_tuple() < baseline_version.as_tuple():
        errors.append(
            f"ABI version regressed: baseline {baseline_version.as_tuple()} -> current {current_version.as_tuple()}."
        )
        return False, errors

    if required_bump == "major":
        if current_version.major <= baseline_version.major:
            errors.append(
                "Breaking ABI changes detected but ABI major version was not increased "
                f"(baseline {baseline_version.major}, current {current_version.major})."
            )
            return False, errors
    elif required_bump == "minor":
        if current_version.major == baseline_version.major and current_version.minor <= baseline_version.minor:
            errors.append(
                "Additive ABI changes detected but ABI minor version was not increased "
                f"(baseline {baseline_version.major}.{baseline_version.minor}, "
                f"current {current_version.major}.{current_version.minor})."
            )
            return False, errors

    return True, errors


def compare_snapshots(baseline: dict[str, Any], current: dict[str, Any]) -> dict[str, Any]:
    errors: list[str] = []
    warnings: list[str] = []

    base_header = baseline.get("header", {})
    curr_header = current.get("header", {})
    base_funcs = base_header.get("functions")
    curr_funcs = curr_header.get("functions")
    if not isinstance(base_funcs, dict) or not isinstance(curr_funcs, dict):
        raise AbiGuardError("Snapshots must include header.functions objects.")

    base_names = set(base_funcs.keys())
    curr_names = set(curr_funcs.keys())
    removed = sorted(base_names - curr_names)
    added = sorted(curr_names - base_names)
    changed = sorted(
        name
        for name in (base_names & curr_names)
        if base_funcs[name].get("signature") != curr_funcs[name].get("signature")
    )

    if removed:
        warnings.append(f"Header symbols removed since baseline: {', '.join(removed)}")
    if changed:
        warnings.append(f"Header signatures changed since baseline: {', '.join(changed)}")

    baseline_version = parse_snapshot_version(baseline, "baseline")
    current_version = parse_snapshot_version(current, "current")

    curr_header_symbols = as_symbol_set(current, "header")
    curr_pinvoke_symbols = as_symbol_set(current, "pinvoke")

    missing_in_pinvoke = sorted(curr_header_symbols - curr_pinvoke_symbols)
    extra_in_pinvoke = sorted(curr_pinvoke_symbols - curr_header_symbols)

    if missing_in_pinvoke:
        errors.append(
            "Header symbols missing in C# P/Invoke declarations: " + ", ".join(missing_in_pinvoke)
        )
    if extra_in_pinvoke:
        errors.append(
            "C# P/Invoke symbols not present in header: " + ", ".join(extra_in_pinvoke)
        )

    binary_payload = current.get("binary", {})
    binary_available = bool(binary_payload.get("available"))
    binary_skipped = bool(binary_payload.get("skipped"))
    if binary_available:
        curr_binary_symbols = as_symbol_set(current, "binary")
        missing_in_binary = sorted(curr_header_symbols - curr_binary_symbols)
        extra_prefixed_binary = sorted(curr_binary_symbols - curr_header_symbols)
        if missing_in_binary:
            errors.append(
                "Header symbols missing in native binary exports: " + ", ".join(missing_in_binary)
            )
        if extra_prefixed_binary:
            errors.append(
                "Native binary exports prefixed ABI symbols not present in header: " + ", ".join(extra_prefixed_binary)
            )

        allow_non_prefixed = bool(binary_payload.get("allow_non_prefixed_exports", False))
        non_prefixed = binary_payload.get("non_prefixed_exports")
        if isinstance(non_prefixed, list) and non_prefixed and not allow_non_prefixed:
            max_preview = 25
            preview = ", ".join(non_prefixed[:max_preview])
            if len(non_prefixed) > max_preview:
                preview += ", ..."
            errors.append(
                "Native binary exports non-ABI symbols. "
                f"Count={len(non_prefixed)}. Examples: {preview}"
            )
    elif not binary_skipped:
        warnings.append(
            "Binary export checks were not executed because the binary path does not exist yet."
        )

    base_enums, base_structs = get_header_types(baseline)
    curr_enums, curr_structs = get_header_types(current)

    struct_tail_breaking = True
    current_policy = current.get("policy")
    if isinstance(current_policy, dict):
        type_policy = current_policy.get("type_policy")
        if isinstance(type_policy, dict):
            struct_tail_breaking = bool(type_policy.get("struct_tail_addition_is_breaking", True))

    enum_diff = compare_enum_sets(base_enums=base_enums, curr_enums=curr_enums)
    struct_diff = compare_struct_sets(
        base_structs=base_structs,
        curr_structs=curr_structs,
        struct_tail_addition_is_breaking=struct_tail_breaking,
    )

    function_breaking = bool(removed or changed)
    function_additive = bool(added)

    breaking_reasons: list[str] = []
    additive_reasons: list[str] = []

    if function_breaking:
        if removed:
            breaking_reasons.append("removed function symbols")
        if changed:
            breaking_reasons.append("changed function signatures")
    if function_additive:
        additive_reasons.append("added function symbols")

    breaking_reasons.extend(enum_diff["breaking_changes"])
    additive_reasons.extend(enum_diff["additive_changes"])
    breaking_reasons.extend(struct_diff["breaking_changes"])
    additive_reasons.extend(struct_diff["additive_changes"])

    change_classification, required_bump = classify_change(
        has_breaking=bool(breaking_reasons),
        has_additive=bool(additive_reasons),
    )

    version_ok, version_errors = validate_version_policy(
        baseline_version=baseline_version,
        current_version=current_version,
        required_bump=required_bump,
    )
    errors.extend(version_errors)

    recommended = recommended_version(baseline=baseline_version, required_bump=required_bump)

    status = "pass" if not errors else "fail"
    return {
        "status": status,
        "change_classification": change_classification,
        "required_bump": required_bump,
        "baseline_abi_version": baseline_version.as_dict(),
        "current_abi_version": current_version.as_dict(),
        "recommended_next_version": recommended.as_dict(),
        "version_policy_satisfied": version_ok,
        "removed_symbols": removed,
        "added_symbols": added,
        "changed_signatures": changed,
        "enum_diff": enum_diff,
        "struct_diff": struct_diff,
        "breaking_reasons": breaking_reasons,
        "additive_reasons": additive_reasons,
        "errors": errors,
        "warnings": warnings,
    }


def print_report(report: dict[str, Any]) -> None:
    status = report.get("status", "unknown")
    print(f"ABI check status: {status}")

    removed = report.get("removed_symbols", [])
    added = report.get("added_symbols", [])
    changed = report.get("changed_signatures", [])
    print(f"Removed symbols: {len(removed)}")
    print(f"Added symbols: {len(added)}")
    print(f"Changed signatures: {len(changed)}")

    classification = report.get("change_classification")
    required_bump = report.get("required_bump")
    recommended = report.get("recommended_next_version")
    print(f"Change classification: {classification}")
    print(f"Required bump: {required_bump}")
    print(f"Recommended next version: {recommended}")

    warnings = report.get("warnings", [])
    errors = report.get("errors", [])

    if warnings:
        print("Warnings:")
        for warning in warnings:
            print(f"  - {warning}")
    if errors:
        print("Errors:")
        for error in errors:
            print(f"  - {error}")


def write_markdown_report(path: Path, report: dict[str, Any]) -> None:
    lines: list[str] = []
    lines.append(f"# ABI Report ({report.get('status', 'unknown')})")
    lines.append("")
    lines.append(f"- Baseline ABI version: `{report.get('baseline_abi_version')}`")
    lines.append(f"- Current ABI version: `{report.get('current_abi_version')}`")
    lines.append(f"- Change classification: `{report.get('change_classification')}`")
    lines.append(f"- Required bump: `{report.get('required_bump')}`")
    lines.append(f"- Recommended next version: `{report.get('recommended_next_version')}`")
    lines.append(f"- Removed symbols: `{len(report.get('removed_symbols', []))}`")
    lines.append(f"- Added symbols: `{len(report.get('added_symbols', []))}`")
    lines.append(f"- Changed signatures: `{len(report.get('changed_signatures', []))}`")
    lines.append("")

    breaking_reasons = report.get("breaking_reasons", [])
    additive_reasons = report.get("additive_reasons", [])
    warnings = report.get("warnings", [])
    errors = report.get("errors", [])

    if breaking_reasons:
        lines.append("## Breaking Reasons")
        for reason in breaking_reasons:
            lines.append(f"- {reason}")
        lines.append("")

    if additive_reasons:
        lines.append("## Additive Reasons")
        for reason in additive_reasons:
            lines.append(f"- {reason}")
        lines.append("")

    if warnings:
        lines.append("## Warnings")
        for warning in warnings:
            lines.append(f"- {warning}")
        lines.append("")

    if errors:
        lines.append("## Errors")
        for error in errors:
            lines.append(f"- {error}")
        lines.append("")

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def command_snapshot(args: argparse.Namespace) -> int:
    repo_root = Path(args.repo_root).resolve()
    config = load_json(Path(args.config).resolve())
    snapshot = build_snapshot(
        config=config,
        target_name=args.target,
        repo_root=repo_root,
        binary_override=args.binary,
        skip_binary=args.skip_binary,
    )

    if args.output:
        write_json(Path(args.output).resolve(), snapshot)
    else:
        print(json.dumps(snapshot, indent=2, sort_keys=True))

    print(
        f"Snapshot created for target '{args.target}' with "
        f"{snapshot['header']['function_count']} header symbols, "
        f"{snapshot['header']['enum_count']} enums, "
        f"{snapshot['header']['struct_count']} structs, and "
        f"{snapshot['pinvoke']['symbol_count']} P/Invoke symbols.",
        file=sys.stderr,
    )
    return 0


def command_verify(args: argparse.Namespace) -> int:
    repo_root = Path(args.repo_root).resolve()
    config = load_json(Path(args.config).resolve())

    current = build_snapshot(
        config=config,
        target_name=args.target,
        repo_root=repo_root,
        binary_override=args.binary,
        skip_binary=args.skip_binary,
    )
    baseline = load_snapshot(Path(args.baseline).resolve())

    report = compare_snapshots(baseline=baseline, current=current)

    if args.current_output:
        write_json(Path(args.current_output).resolve(), current)
    if args.report:
        write_json(Path(args.report).resolve(), report)
    if args.markdown_report:
        write_markdown_report(Path(args.markdown_report).resolve(), report)

    print_report(report)
    return 0 if report.get("status") == "pass" else 1


def command_diff(args: argparse.Namespace) -> int:
    baseline = load_snapshot(Path(args.baseline).resolve())
    current = load_snapshot(Path(args.current).resolve())
    report = compare_snapshots(baseline=baseline, current=current)

    if args.report:
        write_json(Path(args.report).resolve(), report)
    if args.markdown_report:
        write_markdown_report(Path(args.markdown_report).resolve(), report)

    print_report(report)
    return 0 if report.get("status") == "pass" else 1


def command_list_targets(args: argparse.Namespace) -> int:
    config = load_json(Path(args.config).resolve())
    targets = config.get("targets")
    if not isinstance(targets, dict):
        raise AbiGuardError("Config is missing required object: 'targets'.")

    for name in sorted(targets.keys()):
        print(name)
    return 0


def resolve_baseline_for_target(repo_root: Path, config: dict[str, Any], target_name: str, baseline_root: str | None) -> Path:
    target = resolve_target(config, target_name)
    if baseline_root:
        return ensure_relative_path(repo_root, f"{baseline_root.rstrip('/')}/{target_name}.json").resolve()

    baseline_path = target.get("baseline_path")
    if isinstance(baseline_path, str) and baseline_path:
        return ensure_relative_path(repo_root, baseline_path).resolve()

    return ensure_relative_path(repo_root, f"abi/baselines/{target_name}.json").resolve()


def command_verify_all(args: argparse.Namespace) -> int:
    repo_root = Path(args.repo_root).resolve()
    config = load_json(Path(args.config).resolve())

    targets = config.get("targets")
    if not isinstance(targets, dict) or not targets:
        raise AbiGuardError("Config has no targets to verify.")

    output_dir = Path(args.output_dir).resolve() if args.output_dir else None
    final_status = 0
    aggregate: dict[str, Any] = {
        "status": "pass",
        "generated_at_utc": dt.datetime.now(tz=dt.timezone.utc).isoformat(),
        "results": {},
    }

    for target_name in sorted(targets.keys()):
        baseline_path = resolve_baseline_for_target(
            repo_root=repo_root,
            config=config,
            target_name=target_name,
            baseline_root=args.baseline_root,
        )
        if not baseline_path.exists():
            raise AbiGuardError(f"Baseline does not exist for target '{target_name}': {baseline_path}")

        current = build_snapshot(
            config=config,
            target_name=target_name,
            repo_root=repo_root,
            binary_override=args.binary,
            skip_binary=args.skip_binary,
        )
        baseline = load_snapshot(baseline_path)
        report = compare_snapshots(baseline=baseline, current=current)

        aggregate["results"][target_name] = report

        print(f"[{target_name}] {report.get('status')}")
        if report.get("status") != "pass":
            final_status = 1

        if output_dir:
            output_dir.mkdir(parents=True, exist_ok=True)
            write_json(output_dir / f"{target_name}.current.json", current)
            write_json(output_dir / f"{target_name}.report.json", report)
            write_markdown_report(output_dir / f"{target_name}.report.md", report)

    if final_status != 0:
        aggregate["status"] = "fail"

    if output_dir:
        write_json(output_dir / "aggregate.report.json", aggregate)

    return final_status


def command_init_target(args: argparse.Namespace) -> int:
    repo_root = Path(args.repo_root).resolve()
    config_path = Path(args.config).resolve()

    if config_path.exists():
        config = load_json(config_path)
    else:
        config = {"targets": {}}

    targets = config.get("targets")
    if not isinstance(targets, dict):
        raise AbiGuardError("Config root must contain object 'targets'.")

    if args.target in targets and not args.force:
        raise AbiGuardError(
            f"Target '{args.target}' already exists in config. Use --force to overwrite."
        )

    if not args.pinvoke_path:
        raise AbiGuardError("At least one --pinvoke-path must be provided.")

    baseline_rel = args.baseline_path or f"abi/baselines/{args.target}.json"

    target_entry: dict[str, Any] = {
        "baseline_path": baseline_rel,
        "header": {
            "path": args.header_path,
            "api_macro": args.api_macro,
            "call_macro": args.call_macro,
            "symbol_prefix": args.symbol_prefix,
            "version_macros": {
                "major": args.version_major_macro,
                "minor": args.version_minor_macro,
                "patch": args.version_patch_macro,
            },
            "types": {
                "enable_enums": True,
                "enable_structs": True,
                "enum_name_pattern": f"^{re.escape(args.symbol_prefix)}",
                "struct_name_pattern": f"^{re.escape(args.symbol_prefix)}",
                "ignore_enums": [],
                "ignore_structs": [],
                "struct_tail_addition_is_breaking": True,
            },
        },
        "pinvoke": {
            "paths": args.pinvoke_path,
        },
    }

    if args.binary_path:
        target_entry["binary"] = {
            "path": args.binary_path,
            "allow_non_prefixed_exports": False,
        }

    targets[args.target] = target_entry
    config["targets"] = targets
    write_json(config_path, config)

    if args.create_baseline:
        snapshot = build_snapshot(
            config=config,
            target_name=args.target,
            repo_root=repo_root,
            binary_override=None,
            skip_binary=True,
        )
        baseline_path = ensure_relative_path(repo_root, baseline_rel).resolve()
        write_json(baseline_path, snapshot)
        print(f"Created baseline: {baseline_path}")

    print(f"Target '{args.target}' initialized in {config_path}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="abi_guard",
        description="Config-driven ABI governance framework (snapshot/verify/diff/bootstrap).",
    )

    sub = parser.add_subparsers(dest="command", required=True)

    snapshot = sub.add_parser("snapshot", help="Generate current ABI snapshot.")
    snapshot.add_argument(
        "--repo-root",
        default=".",
        help="Repository root used to resolve relative paths (default: current directory).",
    )
    snapshot.add_argument("--config", required=True, help="Path to ABI config JSON.")
    snapshot.add_argument("--target", required=True, help="Target name from config targets map.")
    snapshot.add_argument("--binary", help="Override binary path for export checks.")
    snapshot.add_argument("--skip-binary", action="store_true", help="Skip binary export extraction.")
    snapshot.add_argument("--output", help="Write snapshot JSON to path.")
    snapshot.set_defaults(func=command_snapshot)

    verify = sub.add_parser("verify", help="Compare current ABI state with baseline snapshot.")
    verify.add_argument(
        "--repo-root",
        default=".",
        help="Repository root used to resolve relative paths (default: current directory).",
    )
    verify.add_argument("--config", required=True, help="Path to ABI config JSON.")
    verify.add_argument("--target", required=True, help="Target name from config targets map.")
    verify.add_argument("--baseline", required=True, help="Path to baseline snapshot JSON.")
    verify.add_argument("--binary", help="Override binary path for export checks.")
    verify.add_argument("--skip-binary", action="store_true", help="Skip binary export extraction.")
    verify.add_argument("--current-output", help="Write current snapshot JSON to path.")
    verify.add_argument("--report", help="Write verify report JSON to path.")
    verify.add_argument("--markdown-report", help="Write verify report as Markdown.")
    verify.set_defaults(func=command_verify)

    verify_all = sub.add_parser("verify-all", help="Verify all targets from config.")
    verify_all.add_argument(
        "--repo-root",
        default=".",
        help="Repository root used to resolve relative paths (default: current directory).",
    )
    verify_all.add_argument("--config", required=True, help="Path to ABI config JSON.")
    verify_all.add_argument("--baseline-root", help="Baseline directory (default: target baseline_path or abi/baselines).")
    verify_all.add_argument("--binary", help="Override binary path for export checks (applies to each target).")
    verify_all.add_argument("--skip-binary", action="store_true", help="Skip binary export extraction for all targets.")
    verify_all.add_argument("--output-dir", help="Directory to write per-target current/report artifacts.")
    verify_all.set_defaults(func=command_verify_all)

    diff = sub.add_parser("diff", help="Compare two snapshot files.")
    diff.add_argument("--baseline", required=True, help="Path to baseline snapshot JSON.")
    diff.add_argument("--current", required=True, help="Path to current snapshot JSON.")
    diff.add_argument("--report", help="Write diff report JSON to path.")
    diff.add_argument("--markdown-report", help="Write diff report as Markdown.")
    diff.set_defaults(func=command_diff)

    list_targets = sub.add_parser("list-targets", help="List target names from config.")
    list_targets.add_argument("--config", required=True, help="Path to ABI config JSON.")
    list_targets.set_defaults(func=command_list_targets)

    init_target = sub.add_parser("init-target", help="Bootstrap a new ABI target in config and create baseline.")
    init_target.add_argument("--repo-root", default=".", help="Repository root for relative path resolution.")
    init_target.add_argument("--config", required=True, help="Path to ABI config JSON.")
    init_target.add_argument("--target", required=True, help="Target name to initialize.")
    init_target.add_argument("--header-path", required=True, help="Header path relative to repo root.")
    init_target.add_argument("--api-macro", default="LUMENRTC_API", help="Export macro used in ABI declarations.")
    init_target.add_argument("--call-macro", default="LUMENRTC_CALL", help="Calling convention macro used in ABI declarations.")
    init_target.add_argument("--symbol-prefix", default="lrtc_", help="ABI symbol prefix (for function export matching).")
    init_target.add_argument("--version-major-macro", required=True, help="ABI major version macro name.")
    init_target.add_argument("--version-minor-macro", required=True, help="ABI minor version macro name.")
    init_target.add_argument("--version-patch-macro", required=True, help="ABI patch version macro name.")
    init_target.add_argument("--pinvoke-path", action="append", help="P/Invoke root path (repeatable).")
    init_target.add_argument("--binary-path", help="Optional native binary path.")
    init_target.add_argument("--baseline-path", help="Baseline path (default abi/baselines/<target>.json).")
    init_target.add_argument("--no-create-baseline", action="store_true", help="Do not create baseline immediately.")
    init_target.add_argument("--force", action="store_true", help="Overwrite target if already exists.")
    init_target.set_defaults(func=command_init_target)

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    if hasattr(args, "no_create_baseline"):
        args.create_baseline = not bool(args.no_create_baseline)

    try:
        return int(args.func(args))
    except AbiGuardError as exc:
        print(f"abi_guard error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
