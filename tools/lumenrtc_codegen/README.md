# lumenrtc_codegen

Project-specific ABI generators and metadata validation for LumenRTC.

This folder intentionally contains LumenRTC-specific codegen plugins that run via
`abi_framework` external generator hooks.

- `lumenrtc_native_exports.py`: renders C ABI export forwarding units from ABI IDL.
- `lumenrtc_managed_api_metadata_codegen.py`: generates normalized
  `abi/bindings/lumenrtc.managed_api.json` from metadata + ABI IDL.
- `lumenrtc_managed_api_codegen.py`: validates generated `managed_api` metadata and renders
  native impl handle boilerplate.
- `abi/bindings/lumenrtc.managed_api.source.json`: metadata source.
- `abi/bindings/lumenrtc.managed_api.json`: normalized metadata consumed by
  Roslyn source generator (for C#) and by `lumenrtc_managed_api_codegen.py` (for native handle boilerplate).
  `required_native_functions` is derived from metadata + IDL.
- `abi/bindings/lumenrtc.symbol_contract.sources.json`: declarative source spec for
  the universal symbol contract generator (`tools/abi_framework/generator_sdk/symbol_contract_generator.py`).
- `tests/`: LumenRTC metadata integrity tests (`interop`/`managed` JSON files).

`abi_framework` stays target-agnostic and executes these via configured external commands.
