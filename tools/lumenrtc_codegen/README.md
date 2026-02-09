# lumenrtc_codegen

Project-specific ABI generators and metadata validation for LumenRTC.

This folder intentionally contains LumenRTC-specific codegen plugins that run via
`abi_framework` external generator hooks.

- `lumenrtc_native_exports.py`: renders C ABI export forwarding units from ABI IDL.
- `tests/`: LumenRTC metadata integrity tests (`interop`/`managed` JSON files).

`abi_framework` stays target-agnostic and executes these via configured external commands.
