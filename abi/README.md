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
5. For multi-target repos, verify all targets at once:
   - `scripts/abi.sh check-all --skip-binary`
6. Generate/update ABI IDL + managed stubs from config:
   - `scripts/abi.sh generate --skip-binary`
   - Outputs (for `lumenrtc`): `abi/generated/lumenrtc/lumenrtc.idl.json` and `abi/generated/lumenrtc/NativeMethods.g.cs`
7. Sync generated ABI artifacts and optionally refresh baselines:
   - `scripts/abi.sh sync --skip-binary`
8. Generate changelog for release notes:
   - `scripts/abi.sh changelog --skip-binary --release-tag vX.Y.Z --output abi/CHANGELOG.md`
9. Run full release preparation pipeline:
   - `scripts/abi.sh release-prepare --skip-binary --release-tag vX.Y.Z`

## Compatibility policy

- Removing/changing existing ABI symbols is **breaking** and requires major version bump.
- Adding new ABI symbols is additive and should not regress version tuple.
- Enum value changes/removals are treated as **breaking** changes.
- Struct layout changes are treated as **breaking** by default
  (`struct_tail_addition_is_breaking` is configurable per target).
- Header and C# P/Invoke surfaces must stay in sync.
- Binary exports must match header ABI symbols.

## Reusing for another ABI

`tools/abi_guard` is target-driven. Add another target entry into `abi/config.json` with:

- header path + API/CALL macros + symbol prefix
- type policy (`enum`/`struct` patterns and exceptions)
- P/Invoke file roots
- optional baseline path override
- optional binary path for export validation

Bootstrap command for new targets:

- `scripts/abi.sh init-target ...` (or `scripts/abi.ps1 init-target ...`)
