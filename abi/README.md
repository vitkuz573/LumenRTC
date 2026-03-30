# ABI Governance

## Source of truth

- ABI IDL artifact: `abi/generated/lumenrtc/lumenrtc.idl.json`
- Generated native ABI artifacts:
  - `native/include/lumenrtc.h`
  - `native/lumenrtc.map`
- ABI semantic version macros:
  - `LUMENRTC_ABI_VERSION_MAJOR`
  - `LUMENRTC_ABI_VERSION_MINOR`
  - `LUMENRTC_ABI_VERSION_PATCH`
- Managed interop is generated at C# compile time from ABI IDL by the Roslyn source generator
  (`AbiForge.RoslynGenerator` NuGet package).
- Project-level managed/native convenience wrappers are generated from:
  `abi/bindings/lumenrtc.managed_api.source.json`
  (normalized metadata: `abi/bindings/lumenrtc.managed_api.json`)
- Generated bindings symbol contract lockfile:
  `abi/bindings/lumenrtc.symbol_contract.json`
  (spec source: `abi/bindings/lumenrtc.symbol_contract.sources.json`)
- Runtime resolver and ABI guard logic:
  `src/LumenRTC/Interop/NativeMethods.Core.cs`

## Pipeline

1. Edit/build ABI surface.
2. Fast static verification:
   - `abi_framework verify-all --skip-binary`
3. Export verification after native build:
   - `abi_framework verify-all --binary native/build/liblumenrtc.so`
4. Regenerate baseline when ABI change is intentional:
   - `abi_framework generate-baseline`
5. Generate ABI IDL from config:
   - `abi_framework generate --skip-binary`
   - Output: `abi/generated/lumenrtc/lumenrtc.idl.json`
6. Run language generators from ABI IDL:
   - `abi_framework codegen --skip-binary`
   - Outputs:
     - `abi/bindings/lumenrtc.symbol_contract.json`
     - `native/src/lumenrtc.exports.cpp`
     - `native/src/lumenrtc_impl.h`
     - `native/src/lumenrtc_impl_handles.generated.h`
7. Build C# project with source generation:
   - `dotnet build src/LumenRTC/LumenRTC.csproj`
   - `LumenRTC.csproj` passes IDL + managed metadata as `AdditionalFiles`;
     `AbiForge.RoslynGenerator` emits `NativeMethods`, `NativeTypes`, `NativeHandles`,
     and managed API wrappers at compile time.
8. Sync generated artifacts and optionally baselines:
   - `abi_framework sync --skip-binary`
9. Benchmark ABI pipeline:
   - `abi_framework benchmark --skip-binary --iterations 3 --output artifacts/abi/benchmark.report.json`
10. Enforce benchmark budgets:
    - `abi_framework benchmark-gate --report artifacts/abi/benchmark.report.json --budget abi/benchmark_budget.json`
11. Audit waiver lifecycle and metadata:
    - `abi_framework waiver-audit --fail-on-expired --fail-on-missing-metadata`
12. Generate ABI changelog:
    - `abi_framework changelog --skip-binary --release-tag vX.Y.Z --output abi/CHANGELOG.md`
13. Run full release preparation pipeline:
    - `abi_framework release-prepare --skip-binary --release-tag vX.Y.Z --emit-sbom --emit-attestation`

`native/include/lumenrtc.h` and `native/lumenrtc.map` are generated artifacts.
Do not edit them manually.

## Compatibility policy

- Removing/changing existing ABI symbols is breaking and requires a major bump.
- Adding ABI symbols is additive and requires at least a minor bump.
- Enum value changes/removals are breaking.
- Struct layout changes are breaking by default (`struct_tail_addition_is_breaking`).
- Binary exports must match header ABI symbols.
- Optional bindings symbol contract (`bindings.symbol_contract`) can enforce parity between ABI IDL and language binding expectations.
- Policy rules and waivers are supported (`policy.rules`, `policy.waivers`).
- Waiver governance is policy-driven through `policy.waiver_requirements`.
- For strict mode, waivers should include `created_utc`, `expires_utc`, `owner`, `approved_by`, `ticket`, and `reason`.
- Header parsing uses `header.parser.backend=clang_preprocess` for LumenRTC
  with fallback for local environments; CI validates clang parser is active and
  fallback is not used.
- `header.parser.compiler_candidates` and environment override `ABI_CLANG`
  allow deterministic clang selection on Linux/macOS/Windows.

See also:

- `abi/GOVERNANCE.md`
- `abi/RFC_TEMPLATE.md`

## Reusing for another ABI

`abi-forge` is target-driven. Add another target in `abi/config.json` with:

- header path + API/CALL macros + symbol prefix
- type policy (`enum`/`struct` patterns and exceptions)
- optional `bindings.symbol_contract`
- optional baseline path override
- optional binary export path

Bootstrap command:

- `abi_framework bootstrap --target mylib --header-path include/mylib.h --namespace MyLib`
