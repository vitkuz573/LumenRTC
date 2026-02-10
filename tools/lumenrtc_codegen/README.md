# lumenrtc_codegen

Project-specific ABI generators and metadata validation for LumenRTC.

This folder intentionally contains LumenRTC-specific codegen plugins that run via
`abi_framework` external generator hooks.

- `lumenrtc_native_exports.py`: renders C ABI export forwarding units from ABI IDL.
- `lumenrtc_managed_api_codegen.py`: validates `managed_api` metadata and renders
  native impl handle boilerplate.
- `abi/bindings/lumenrtc.managed_api.json`: data-only specification consumed by
  Roslyn source generator (for C#) and by `lumenrtc_managed_api_codegen.py` (for native handle boilerplate).
- `tests/`: LumenRTC metadata integrity tests (`interop`/`managed` JSON files).

`abi_framework` stays target-agnostic and executes these via configured external commands.
