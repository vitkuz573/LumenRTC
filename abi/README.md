# ABI Governance

## Source of truth

- Native ABI declarations: `native/include/lumenrtc.h`
- ABI semantic version macros:
  - `LUMENRTC_ABI_VERSION_MAJOR`
  - `LUMENRTC_ABI_VERSION_MINOR`
  - `LUMENRTC_ABI_VERSION_PATCH`
- Managed interop surface: `src/LumenRTC/Interop/*.cs`

## Guard pipeline

1. Build or edit ABI surface.
2. Run `scripts/abi.sh check --skip-binary` for fast static checks.
3. After native build, run `scripts/abi.sh check --binary native/build/liblumenrtc.so`.
4. If ABI intentionally changed, update version macros and refresh baseline:
   - `scripts/abi.sh baseline`

## Compatibility policy

- Removing/changing existing ABI symbols is **breaking** and requires major version bump.
- Adding new ABI symbols is additive and should not regress version tuple.
- Header and C# P/Invoke surfaces must stay in sync.
- Binary exports must match header ABI symbols.

## Reusing for another ABI

`tools/abi_guard` is target-driven. Add another target entry into `abi/config.json` with:

- header path + API/CALL macros + symbol prefix
- P/Invoke file roots
- optional binary path for export validation
