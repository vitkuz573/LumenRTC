# Repository Guidelines

## Project Structure & Module Organization
- `src/LumenRTC/`: main .NET API and runtime interop layer.
- `src/LumenRTC.Rendering.Sdl/`: optional SDL2 video renderer helpers.
- `native/`: C ABI bridge (`src/lumenrtc.cpp`) and CMake build files.
- `abi/`: ABI config, baselines, governance docs, and generated IDL.
- `tools/abi_framework/`: Python ABI governance CLI plus unit tests.
- `tools/abi_roslyn_codegen/`: Roslyn source generator used by `src/LumenRTC`.
- `samples/`: runnable demos (camera, screen share, signaling, streaming).
- `scripts/`: bootstrap, ABI, packaging, and demo launcher scripts.

Generated files are part of the ABI pipeline. Do not hand-edit `native/include/lumenrtc.h`, `native/lumenrtc.map`, or `abi/generated/lumenrtc/lumenrtc.idl.json`; regenerate via ABI scripts.

## Build, Test, and Development Commands
- `scripts/setup.sh --build-type Release` (or `scripts/setup.ps1` / `scripts/setup.cmd`): full bootstrap for WebRTC + native + managed build.
- `cmake -S native -B native/build -DLIBWEBRTC_BUILD_DIR=/path/to/out` then `cmake --build native/build -j`: build native `lumenrtc`.
- `dotnet build src/LumenRTC/LumenRTC.csproj`: build managed library and validate source-generator integration.
- `dotnet run --project samples/LumenRTC.Sample.LocalCamera.Core/LumenRTC.Sample.LocalCamera.Core.csproj`: run a sample locally.
- `scripts/abi.sh check --skip-binary`: fast ABI drift check.
- `scripts/abi.sh codegen --skip-binary --check --fail-on-sync`: validate generated ABI/codegen artifacts are in sync.
- `python3 -m unittest discover -s tools/abi_framework/tests -p "test_*.py"`: run ABI framework unit tests.

## Coding Style & Naming Conventions
- Follow `.editorconfig`: UTF-8, LF, 4-space indentation, trim trailing whitespace.
- C# preferences: file-scoped namespaces, braces on new lines, `System` usings first.
- Keep nullable annotations and implicit usings enabled (project defaults).
- Naming: PascalCase for public C# APIs, camelCase for locals/parameters, `lrtc_*` prefix for exported C ABI symbols.

## Testing Guidelines
- Main automated tests live in `tools/abi_framework/tests/`.
- For ABI-affecting changes, run: `check-all`, `generate --check --fail-on-sync`, `codegen --check --fail-on-sync`, and `dotnet build src/LumenRTC/LumenRTC.csproj`.
- Add or update regression tests when touching parser, policy, diff classification, or codegen behavior.

## Commit & Pull Request Guidelines
- Use Conventional Commit style seen in history: `feat:`, `fix:`, `refactor(scope):`, `docs:`, `abi:`.
- Keep commits focused and imperative (example: `fix(loopback): retry ICE apply when remote not ready`).
- PRs should include summary, rationale, risk level, and validation output.
- For ABI-related PRs, complete the checklist in `.github/pull_request_template.md` and state ABI classification (`none`, `additive`, `breaking`) with required version bump.
