#!/usr/bin/env python3
from __future__ import annotations

import argparse
import sys
from pathlib import Path

TOOL_PATH = "tools/lumenrtc_codegen/lumenrtc_native_exports.py"

CORE_SRC = Path(__file__).resolve().parents[1] / "abi_codegen_core" / "src"
if str(CORE_SRC) not in sys.path:
    sys.path.insert(0, str(CORE_SRC))

from abi_codegen_core.common import load_json_object, write_if_changed
from abi_codegen_core.native_exports import (
    NativeExportRenderOptions,
    render_exports,
    render_impl_header,
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--idl", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--impl-header", required=True)
    parser.add_argument("--header-include", default="lumenrtc.h")
    parser.add_argument("--impl-header-include", default="lumenrtc_impl.h")
    parser.add_argument("--api-macro", default="LUMENRTC_API")
    parser.add_argument("--call-macro", default="LUMENRTC_CALL")
    parser.add_argument("--impl-prefix", default="lrtc_impl_")
    parser.add_argument("--symbol-prefix", default="lrtc_")
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    idl_path = Path(args.idl)
    out_path = Path(args.out)
    header_path = Path(args.impl_header)

    idl = load_json_object(idl_path)
    functions = idl.get("functions")
    if not isinstance(functions, list):
        raise SystemExit("IDL missing 'functions' array")

    options = NativeExportRenderOptions(
        header_include=args.header_include,
        impl_header_include=args.impl_header_include,
        api_macro=args.api_macro,
        call_macro=args.call_macro,
        impl_prefix=args.impl_prefix,
        symbol_prefix=args.symbol_prefix,
    )

    exports = render_exports(functions, options, TOOL_PATH)
    header = render_impl_header(functions, options)

    status = 0
    status |= write_if_changed(out_path, exports, args.check, args.dry_run)
    status |= write_if_changed(header_path, header, args.check, args.dry_run)
    return status


if __name__ == "__main__":
    raise SystemExit(main())
