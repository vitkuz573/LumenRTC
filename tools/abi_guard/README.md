# abi_guard

`abi_guard` is a config-driven ABI governance CLI designed to be reusable across repositories.

## What it checks

- C header ABI function surface (`api_macro` + `call_macro` + `symbol_prefix`)
- C# P/Invoke declarations (`DllImport` / `LibraryImport`)
- Optional native binary exports (`.so` / `.dylib` / `.dll`)
- Baseline compatibility and semantic version discipline (`major/minor/patch` macros)

## Commands

```bash
python3 tools/abi_guard/abi_guard.py snapshot --config abi/config.json --target lumenrtc --skip-binary --output abi/current.json
python3 tools/abi_guard/abi_guard.py verify --config abi/config.json --target lumenrtc --baseline abi/baselines/lumenrtc.json --skip-binary
python3 tools/abi_guard/abi_guard.py diff --baseline abi/baselines/lumenrtc.json --current abi/current.json
```

## Config format

```json
{
  "targets": {
    "my_target": {
      "header": {
        "path": "native/include/my_api.h",
        "api_macro": "MY_API",
        "call_macro": "MY_CALL",
        "symbol_prefix": "my_",
        "version_macros": {
          "major": "MY_ABI_VERSION_MAJOR",
          "minor": "MY_ABI_VERSION_MINOR",
          "patch": "MY_ABI_VERSION_PATCH"
        }
      },
      "pinvoke": {
        "paths": ["src/MyProject/Interop"]
      },
      "binary": {
        "path": "native/build/libmyapi.so",
        "allow_non_prefixed_exports": false
      }
    }
  }
}
```

## Generic usage notes

- You can define multiple targets in one config and verify each independently.
- `pinvoke.paths` accepts directories, files, and glob patterns.
- Binary checks are optional (`--skip-binary`) when running in environments without a built native artifact.
