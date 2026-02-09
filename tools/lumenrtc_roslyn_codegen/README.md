# lumenrtc_roslyn_codegen

Roslyn source generator that converts LumenRTC ABI metadata into C# interop code
during compilation.

## Input

- ABI IDL JSON (for example `abi/generated/lumenrtc/lumenrtc.idl.json`).
- Managed handle metadata JSON (for example `abi/bindings/lumenrtc.managed.json`).
- MSBuild properties passed through `CompilerVisibleProperty`:
  - `LumenRtcAbiIdlPath`
  - `LumenRtcAbiManagedMetadataPath`
  - `LumenRtcAbiNamespace`
  - `LumenRtcAbiClassName`
  - `LumenRtcAbiAccessModifier`
  - `LumenRtcAbiCallingConvention`
  - `LumenRtcAbiLibraryExpression`

## Output

- Generated `NativeMethods` (`DllImport`) source.
- Generated `NativeTypes` source (enums/structs/delegates/constants).
- Generated `NativeHandles` source (`SafeHandle` partial methods for release/lifetime).
- All generated sources are added directly to compilation (no checked-in `*.g.cs` required).

## Handle Contracts

For each handle entry in managed metadata, project source must declare a matching
type (`namespace` + `cs_type`) as:

- `partial class`
- inheriting `System.Runtime.InteropServices.SafeHandle`
- accessibility matching metadata (`public` or `internal`)

Violations are reported as source-generator diagnostics (`LRTCABI008`-`LRTCABI012`).

## Integration

`src/LumenRTC/LumenRTC.csproj` wires the generator as an analyzer project reference
and passes IDL + managed metadata as `AdditionalFiles`.

Validation command:

```bash
dotnet build src/LumenRTC/LumenRTC.csproj
```
