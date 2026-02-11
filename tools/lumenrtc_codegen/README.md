# lumenrtc_codegen

Project-specific ABI generators and metadata validation for LumenRTC.

This folder intentionally contains LumenRTC-specific codegen plugins that run via
`abi_framework` external generator hooks.
Shared, target-agnostic primitives are factored into:
`tools/abi_codegen_core/`.

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
- `plugin.manifest.json`: plugin contract metadata for this package.

`abi_framework` stays target-agnostic and executes these via
`bindings.generators[].manifest` + `bindings.generators[].plugin` bindings in `abi/config.json`.

Validate manifest contract:

```bash
python3 tools/abi_framework/abi_framework.py validate-plugin-manifest \
  --manifest tools/lumenrtc_codegen/plugin.manifest.json

# or discover from configured generators:
python3 tools/abi_framework/abi_framework.py validate-plugin-manifest \
  --repo-root . \
  --config abi/config.json \
  --target lumenrtc
```
