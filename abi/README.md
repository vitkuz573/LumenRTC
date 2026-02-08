# ABI Governance

## Source of truth

- Native ABI declarations: `native/include/lumenrtc.h`
- ABI semantic version macros:
  - `LUMENRTC_ABI_VERSION_MAJOR`
  - `LUMENRTC_ABI_VERSION_MINOR`
  - `LUMENRTC_ABI_VERSION_PATCH`
- Managed interop is generated at C# compile time from ABI IDL by the Roslyn source generator:
  `tools/lumenrtc_roslyn_codegen/`
- Runtime resolver and ABI guard logic:
  `src/LumenRTC/Interop/NativeMethods.Core.cs`

## Pipeline

1. Edit/build ABI surface.
2. Fast static verification:
   - `scripts/abi.sh check --skip-binary`
3. Export verification after native build:
   - `scripts/abi.sh check --binary native/build/liblumenrtc.so`
4. Regenerate baseline when ABI change is intentional:
   - `scripts/abi.sh baseline`
5. Multi-target verification:
   - `scripts/abi.sh check-all --skip-binary`
6. Generate ABI IDL from config:
   - `scripts/abi.sh generate --skip-binary`
   - Output: `abi/generated/lumenrtc/lumenrtc.idl.json`
7. Run language generators from ABI IDL (plugin host):
   - `scripts/abi.sh codegen --skip-binary`
8. Build C# project with source generation:
   - `dotnet build src/LumenRTC/LumenRTC.csproj`
   - `src/LumenRTC/LumenRTC.csproj` passes IDL as `AdditionalFiles` and receives generated `NativeMethods` in compilation.
9. Sync generated artifacts and optionally baselines:
   - `scripts/abi.sh sync --skip-binary`
10. Benchmark ABI pipeline:
   - `scripts/abi.sh benchmark --skip-binary --iterations 3 --output artifacts/abi/benchmark.report.json`
11. Generate ABI changelog:
   - `scripts/abi.sh changelog --skip-binary --release-tag vX.Y.Z --output abi/CHANGELOG.md`
12. Run full release preparation pipeline:
   - `scripts/abi.sh release-prepare --skip-binary --release-tag vX.Y.Z`

## Compatibility policy

- Removing/changing existing ABI symbols is breaking and requires a major bump.
- Adding ABI symbols is additive and requires at least a minor bump.
- Enum value changes/removals are breaking.
- Struct layout changes are breaking by default (`struct_tail_addition_is_breaking`).
- Binary exports must match header ABI symbols.
- Optional bindings symbol policy (`bindings.expected_symbols`) can enforce parity between ABI IDL and language binding expectations.
- Policy rules and waivers are supported (`policy.rules`, `policy.waivers`).
- Waivers should be temporary and include `owner`, `reason`, and `expires_utc`.
- Header parsing uses `header.parser.backend=clang_preprocess` for LumenRTC
  with fallback for local environments; CI validates clang parser is active and
  fallback is not used.
- `header.parser.compiler_candidates` and environment override `ABI_CLANG`
  allow deterministic clang selection on Linux/macOS/Windows.

See also:

- `abi/GOVERNANCE.md`
- `abi/RFC_TEMPLATE.md`

## Reusing for another ABI

`tools/abi_framework` is target-driven. Add another target in `abi/config.json` with:

- header path + API/CALL macros + symbol prefix
- type policy (`enum`/`struct` patterns and exceptions)
- optional `bindings.expected_symbols`
- optional baseline path override
- optional binary export path

Bootstrap command:

- `scripts/abi.sh init-target ...` (or `scripts/abi.ps1 init-target ...`)
