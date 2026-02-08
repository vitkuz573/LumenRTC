# lumenrtc_roslyn_codegen

Roslyn source generator that converts LumenRTC ABI IDL JSON into C# `DllImport`
interop declarations during compilation.

## Input

- ABI IDL JSON (for example `abi/generated/lumenrtc/lumenrtc.idl.json`).
- MSBuild properties passed through `CompilerVisibleProperty`:
  - `LumenRtcAbiIdlPath`
  - `LumenRtcAbiNamespace`
  - `LumenRtcAbiClassName`
  - `LumenRtcAbiAccessModifier`
  - `LumenRtcAbiCallingConvention`
  - `LumenRtcAbiLibraryExpression`

## Output

- Generated `NativeMethods` source added directly to the compilation (no checked-in `NativeMethods.g.cs` file).

## Integration

`src/LumenRTC/LumenRTC.csproj` wires the generator as an analyzer project reference
and passes the IDL file as `AdditionalFiles`.

Validation command:

```bash
dotnet build src/LumenRTC/LumenRTC.csproj
```
