#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import glob
import json
import os
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

TOOL_VERSION = "1.0.0"


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


def parse_c_header(header_path: Path, api_macro: str, call_macro: str, symbol_prefix: str, version_macros: dict[str, str]) -> tuple[dict[str, Any], AbiVersion]:
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

    header_payload = {
        "path": str(header_path),
        "function_count": len(functions),
        "symbols": sorted(functions.keys()),
        "functions": {name: functions[name] for name in sorted(functions.keys())},
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
    export_line = re.compile(r"^\\s+\\d+\\s+[0-9A-Fa-f]+\\s+[0-9A-Fa-f]+\\s+(\\S+)$")
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
        }
    else:
        binary_cfg = require_dict(target.get("binary"), "binary")
        configured_path = binary_override or require_str(binary_cfg.get("path"), "binary.path")
        allow_non_prefixed = bool(binary_cfg.get("allow_non_prefixed_exports", False))
        binary_path = ensure_relative_path(repo_root, configured_path).resolve()
        binary_payload = extract_binary_exports(
            binary_path=binary_path,
            symbol_prefix=symbol_prefix,
            allow_non_prefixed_exports=allow_non_prefixed,
        )
        binary_payload["path"] = to_repo_relative(binary_path, repo_root)
        binary_payload["skipped"] = False

    return {
        "tool": {
            "name": "abi_guard",
            "version": TOOL_VERSION,
        },
        "target": target_name,
        "generated_at_utc": dt.datetime.now(tz=dt.timezone.utc).isoformat(),
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

    if current_version.as_tuple() < baseline_version.as_tuple():
        errors.append(
            f"ABI version regressed: baseline {baseline_version.as_tuple()} -> current {current_version.as_tuple()}."
        )

    breaking = bool(removed or changed)
    if breaking and current_version.major <= baseline_version.major:
        errors.append(
            "Breaking ABI changes detected (removed/changed symbols) but ABI major version was not increased "
            f"(baseline {baseline_version.major}, current {current_version.major})."
        )

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

    status = "pass" if not errors else "fail"
    return {
        "status": status,
        "baseline_abi_version": baseline_version.as_dict(),
        "current_abi_version": current_version.as_dict(),
        "removed_symbols": removed,
        "added_symbols": added,
        "changed_signatures": changed,
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
    lines.append(f"- Removed symbols: `{len(report.get('removed_symbols', []))}`")
    lines.append(f"- Added symbols: `{len(report.get('added_symbols', []))}`")
    lines.append(f"- Changed signatures: `{len(report.get('changed_signatures', []))}`")
    lines.append("")

    warnings = report.get("warnings", [])
    errors = report.get("errors", [])

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
        f"{snapshot['header']['function_count']} header symbols and "
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


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="abi_guard",
        description="Config-driven ABI snapshot and compatibility checker.",
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

    diff = sub.add_parser("diff", help="Compare two snapshot files.")
    diff.add_argument("--baseline", required=True, help="Path to baseline snapshot JSON.")
    diff.add_argument("--current", required=True, help="Path to current snapshot JSON.")
    diff.add_argument("--report", help="Write diff report JSON to path.")
    diff.add_argument("--markdown-report", help="Write diff report as Markdown.")
    diff.set_defaults(func=command_diff)

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return int(args.func(args))
    except AbiGuardError as exc:
        print(f"abi_guard error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
