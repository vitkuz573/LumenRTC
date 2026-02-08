# abi_guard

`abi_guard` is a config-driven ABI governance framework that can be reused across repositories.

## Key capabilities

- Captures C ABI function surface from headers (`api_macro` + `call_macro` + `symbol_prefix`).
- Parses ABI-relevant type surface from headers:
  - `typedef enum ...` (members + numeric values when evaluable)
  - `typedef struct ...` (field order + declarations)
- Validates C# P/Invoke declarations (`DllImport` / `LibraryImport`) against header exports.
- Optionally validates native binary exports (`.so` / `.dylib` / `.dll`).
- Computes ABI change classification:
  - `breaking`
  - `additive`
  - `none`
- Enforces semantic versioning policy from ABI macros:
  - required bump (`major` / `minor` / `none`)
  - recommended next version
- Supports multi-target verification in one config.
- Bootstraps new targets with generated config + baseline.

## Commands

```bash
# Snapshot one target
python3 tools/abi_guard/abi_guard.py snapshot \
  --repo-root . \
  --config abi/config.json \
  --target lumenrtc \
  --skip-binary \
  --output abi/current.json

# Verify one target against baseline
python3 tools/abi_guard/abi_guard.py verify \
  --repo-root . \
  --config abi/config.json \
  --target lumenrtc \
  --baseline abi/baselines/lumenrtc.json \
  --skip-binary

# Verify all targets in config
python3 tools/abi_guard/abi_guard.py verify-all \
  --repo-root . \
  --config abi/config.json \
  --skip-binary \
  --output-dir artifacts/abi

# Compare two snapshot files
python3 tools/abi_guard/abi_guard.py diff \
  --baseline abi/baselines/lumenrtc.json \
  --current abi/current.json

# Initialize a new target
python3 tools/abi_guard/abi_guard.py init-target \
  --repo-root . \
  --config abi/config.json \
  --target myabi \
  --header-path native/include/myabi.h \
  --api-macro MYABI_API \
  --call-macro MYABI_CALL \
  --symbol-prefix myabi_ \
  --version-major-macro MYABI_ABI_VERSION_MAJOR \
  --version-minor-macro MYABI_ABI_VERSION_MINOR \
  --version-patch-macro MYABI_ABI_VERSION_PATCH \
  --pinvoke-path src/MyAbi/Interop \
  --binary-path native/build/libmyabi.so
```

## Config format

```json
{
  "targets": {
    "my_target": {
      "baseline_path": "abi/baselines/my_target.json",
      "header": {
        "path": "native/include/my_api.h",
        "api_macro": "MY_API",
        "call_macro": "MY_CALL",
        "symbol_prefix": "my_",
        "version_macros": {
          "major": "MY_ABI_VERSION_MAJOR",
          "minor": "MY_ABI_VERSION_MINOR",
          "patch": "MY_ABI_VERSION_PATCH"
        },
        "types": {
          "enable_enums": true,
          "enable_structs": true,
          "enum_name_pattern": "^my_",
          "struct_name_pattern": "^my_",
          "ignore_enums": [],
          "ignore_structs": [],
          "struct_tail_addition_is_breaking": true
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

## Wrapper scripts

- Bash: `scripts/abi.sh`
- PowerShell: `scripts/abi.ps1`

Both wrappers expose the same high-level commands:

- `snapshot`
- `baseline`
- `baseline-all`
- `verify` / `check`
- `verify-all` / `check-all`
- `list-targets`
- `init-target`
- `diff`
