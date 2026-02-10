# ABI Governance

## Scope

ABI governance applies to:

- `native/include/lumenrtc.h`
- `src/LumenRTC/Interop/*`
- `tools/abi_framework/*`
- `tools/abi_roslyn_codegen/*`
- `abi/*`

## Required Process

1. Run static ABI checks (`check` / `check-all`) before merge.
2. Run IDL/codegen consistency checks (`generate --check`, `codegen --check`).
3. If ABI changes are intentional:
   - update baselines,
   - regenerate changelog,
   - document classification (`none/additive/breaking`) and version bump.
4. Avoid permanent waivers. Every waiver must include:
   - owner,
   - reason,
   - `expires_utc`.

## Compatibility Policy

- Removing or changing existing ABI symbols is breaking (major bump).
- Adding ABI symbols is additive (minor bump).
- Enum value removal/change is breaking.
- Struct layout changes are breaking by default.

## Review Gates

- ABI-related pull requests must include ABI checklist completion.
- CODEOWNERS approval is required for ABI scope.
- CI ABI workflow must pass.

## Incident Handling

If ABI regression is found post-merge:

1. Freeze ABI-affecting merges.
2. Produce `verify-all` + SARIF + changelog evidence.
3. Revert or ship forward with explicit major bump and migration notes.
