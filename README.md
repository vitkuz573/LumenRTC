# LumenRTC

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/vitkuz573/LumenRTC)

LumenRTC is a native C ABI + .NET (`net10.0`) wrapper over `lumenrtc_bridge`.
Canonical upstream for WebRTC is `https://webrtc.googlesource.com/src.git`.

The repository uses [abi-forge](https://github.com/vitkuz573/abi-forge) (installed
via pip) to govern, snapshot, and generate bindings for the C ABI. From a single
IDL snapshot the framework generates:
- C# P/Invoke + managed wrapper (via `AbiForge.RoslynGenerator` NuGet)
- Python ctypes module
- Rust FFI (`extern "C"` block)
- TypeScript/Node.js module (ffi-napi)
- Go cgo package

Source-of-truth runbooks:
- Upstream sync: `docs/WEBRTC_UPSTREAM_SYNC.md`
- ABI governance workflow: `abi/README.md`

## Supported Platforms

Build scripts and CMake wiring in this repo target:
- Windows
- Linux

macOS is not currently wired as a first-class build target in this repository.

## Repository Structure

- `native/`: C ABI shared library (`lumenrtc`).
- `src/LumenRTC/`: managed wrapper and public API.
- `src/LumenRTC.Rendering.Sdl/`: optional SDL2 renderer helper.
- `samples/`: local camera, screen share, streaming, signaling examples.
- `abi/`: ABI config, baselines, generated IDL, bindings metadata, governance docs.
- `tests/abi/`: ABI governance test suite (Python unittest).
- `tools/abi_codegen_core/`: target-agnostic codegen primitives shared by generators.
- `tools/lumenrtc_codegen/`: LumenRTC-specific codegen tests.
- `tools/abi_roslyn_codegen/`: local build of the Roslyn source generator (project uses `AbiForge.RoslynGenerator` NuGet).

## Prerequisites

- .NET SDK 10
- CMake + Ninja
- Python 3 + `pip install abi-forge` (for ABI governance tooling)
- clang (for ABI header parsing)
- Git
- `depot_tools` (`gclient`, `gn`, `ninja`) for setup/sync scripts

`bridge/lumenrtc_bridge` is the in-repo first-party bridge source used when
`LUMENRTC_BRIDGE_ROOT` is not explicitly set.

## First Clone Setup (Recommended)

This pulls official WebRTC, syncs `bridge/lumenrtc_bridge` into the checkout,
builds `lumenrtc_bridge`, then builds LumenRTC native + managed layers.

Windows:

```powershell
scripts\setup.cmd -BuildType Release
```

Linux:

```bash
scripts/setup.sh --build-type Release
```

Notes:
- Default upstream ref is `branch-heads/7151`.
- Use `--webrtc-root`/`-WebRtcRoot` to reuse an existing checkout.
- Use `--webrtc-branch`/`-WebRtcBranch` to pin another official ref.
- Setup scripts auto-clone `depot_tools` when missing.

## Build Manually

Native build:

```bash
cmake -S native -B native/build \
  -DLUMENRTC_BRIDGE_ROOT=/path/to/lumenrtc_bridge \
  -DLUMENRTC_BRIDGE_BUILD_DIR=/path/to/webrtc/out/Release
cmake --build native/build -j
```

Managed build:

```bash
dotnet build src/LumenRTC/LumenRTC.csproj -c Release
```

Desktop capture support is always compiled in for `lumenrtc`.

## One-Command Bootstrap

Linux:

```bash
scripts/bootstrap.sh --lumenrtc_bridge-build-dir /path/to/webrtc/out/Release
```

Windows:

```powershell
scripts\bootstrap.ps1 -LumenRtcBridgeBuildDir C:\path\to\webrtc\out\Release
```

If build dir is omitted (or set to `auto`), bootstrap tries common output paths
and env vars (`LUMENRTC_BRIDGE_BUILD_DIR`).

## ABI Governance

Native ABI version macros are in `native/include/lumenrtc.h`:
- `LUMENRTC_ABI_VERSION_MAJOR`
- `LUMENRTC_ABI_VERSION_MINOR`
- `LUMENRTC_ABI_VERSION_PATCH`

Runtime ABI probes exported by native lib:
- `lrtc_abi_version_major`
- `lrtc_abi_version_minor`
- `lrtc_abi_version_patch`
- `lrtc_abi_version_string`

The managed layer validates ABI major compatibility before first P/Invoke.

### ABI Tooling

```bash
abi_framework generate-baseline              # snapshot current IDL as new baseline
abi_framework verify-all --skip-binary       # check for ABI regressions
abi_framework generate --skip-binary         # regenerate IDL from header
abi_framework codegen --skip-binary          # re-run all downstream code generators
abi_framework sync --skip-binary             # sync generated artifacts
abi_framework release-prepare --skip-binary --release-tag vX.Y.Z
```

Binding generation (multi-language, LumenRTC target):

```bash
# Regenerate IDL from header
abi_framework generate --skip-binary

# Re-run all downstream code generators (Python, Rust, TypeScript, Go, C++ headers)
abi_framework codegen --skip-binary

# Watch mode: re-run codegen automatically on header/metadata changes
abi_framework watch

# Snapshot current IDL as the new baseline (after a deliberate ABI change)
abi_framework generate-baseline
```

For full command list: `abi_framework --help`

Current ABI facts:
- ABI IDL schema: v1 (`abi/generated/lumenrtc/lumenrtc.idl.json`)
- Native artifacts generated from IDL:
  `native/include/lumenrtc.h`, `native/lumenrtc.map` (do not edit manually)
- LumenRTC target uses `clang_preprocess` parser backend; CI asserts no fallback.

## Runtime

Ensure `lumenrtc` and `lumenrtc_bridge` are discoverable by the loader:
- Windows: on `PATH` or next to app binaries.
- Linux: on `LD_LIBRARY_PATH` (or via rpath).

Sample builds can copy native libs into output automatically. Override via
`LumenRtcNativeDir` and `LumenRtcBridgeBuildDir` (or `LUMENRTC_BRIDGE_BUILD_DIR`).

## Quickstart (Convenience API)

Minimal camera preview:

```csharp
using LumenRTC;
using LumenRTC.Rendering.Sdl;

using var rtc = ConvenienceApi.CreateContext();
using var camera = rtc.CreateCameraTrack(new CameraTrackOptions
{
    Width = 1280,
    Height = 720,
    Fps = 30,
});

using var renderer = new SdlVideoRenderer("LumenRTC Camera", 1280, 720);
camera.Track.AddSink(renderer.Sink);
renderer.Run();
```

Local track lifecycle helpers:

```csharp
if (!camera.IsRunning)
{
    camera.TryStart(out _);
}

camera.Mute();
camera.Unmute();
camera.TryStop(out _);
```

Async offer/answer flow:

```csharp
using var rtc = ConvenienceApi.CreateContext();
var config = new RtcConfiguration()
    .WithStun("stun:stun.l.google.com:19302")
    .EnableDscp();

using var pc = rtc.CreatePeerConnection(builder => builder
    .WithConfiguration(config)
    .OnIceCandidate(candidate => Console.WriteLine(candidate.Candidate)));

var offer = await pc.CreateOfferAsync();
await pc.SetLocalDescriptionAsync(offer);
```

DTMF on an audio sender:

```csharp
if (sender.TryInsertDtmf("123#", out var err))
{
    Console.WriteLine("DTMF queued");
}
else
{
    Console.WriteLine(err);
}

sender.SetDtmfToneChangeHandler(change =>
{
    Console.WriteLine($"Tone={change.Tone}, Remaining={change.RemainingBuffer}");
});
```

RTP transceiver direction orchestration:

```csharp
foreach (var tx in pc.GetTransceivers(MediaType.Video))
{
    tx.TrySetSendEnabled(false, out _);   // stop upstream video
    tx.TrySetReceiveEnabled(true, out _); // keep downstream video
}

pc.TryPauseMedia(MediaType.Audio, out _);
pc.TryResumeMedia(MediaType.Audio, out _);
```

DataChannel high-level handlers:

```csharp
var dc = pc.CreateDataChannel("chat");
dc.StateChanged += state => Console.WriteLine($"DC state: {state}");
dc.SetTextMessageHandler(text => Console.WriteLine($"Incoming: {text}"));
dc.SendText("{\"type\":\"ping\"}");
```

Timeout-aware peer connection async:

```csharp
var offer = await pc.CreateOfferAsync(TimeSpan.FromSeconds(10), cancellationToken);
await pc.SetLocalDescriptionAsync(offer, TimeSpan.FromSeconds(10), cancellationToken);
var stats = await pc.GetStatsReportAsync(TimeSpan.FromSeconds(5), cancellationToken);

var selectedPair = stats.GetFirst(new RtcStatQuery(
    Type: RtcStatTypes.CandidatePair,
    Predicate: stat => stat.GetBoolOrNull("selected") == true));
```

## Quickstart (Core API)

Minimal camera preview with explicit factory/device primitives:

```csharp
using LumenRTC;
using LumenRTC.Rendering.Sdl;

using var core = CoreApi.CreateSession();
using var videoDevice = core.Factory.GetVideoDevice();
if (videoDevice.NumberOfDevices() == 0)
{
    throw new InvalidOperationException("No camera devices available.");
}

var firstDevice = videoDevice.GetDeviceName(0);
using var capturer = videoDevice.CreateCapturer(firstDevice.Name, 0, 1280, 720, 30);
if (!capturer.Start())
{
    throw new InvalidOperationException("Failed to start camera capture.");
}

using var source = core.Factory.CreateVideoSource(capturer, "camera");
using var track = core.Factory.CreateVideoTrack(source, "camera0");
using var renderer = new SdlVideoRenderer("LumenRTC Camera", 1280, 720);
track.AddSink(renderer.Sink);
renderer.Run();
capturer.Stop();
```

## Packaging

The repo is ready for NuGet packaging but does not publish packages. Use the
pack scripts to produce local `.nupkg` files.

Windows (PowerShell):

```powershell
scripts\pack.ps1 -Configuration Release -Rid win-x64 -LumenRtcBridgeBuildDir C:\path\to\webrtc\out\Release
```

Linux:

```bash
RID=linux-x64 LUMENRTC_BRIDGE_BUILD_DIR=/path/to/webrtc/out/Release scripts/pack.sh
```

To pack without native libraries, use `-NoNative` (PowerShell) or `NO_NATIVE=true`.

To build a single package containing multiple runtime folders:

```powershell
scripts\pack-all.ps1 `
  -Rids win-x64,linux-x64 `
  -WinLumenRtcNativeDir C:\path\to\native\build `
  -WinLumenRtcBridgeBuildDir C:\path\to\webrtc\out\Release `
  -LinuxLumenRtcNativeDir /path/to/native/build `
  -LinuxLumenRtcBridgeBuildDir /path/to/webrtc/out/Release `
  -PackageVersion 1.0.1
```

```bash
RIDS=win-x64,linux-x64 \
WIN_LUMENRTC_NATIVE_DIR=/path/to/win/native/build \
WIN_LUMENRTC_BRIDGE_BUILD_DIR=/path/to/win/webrtc/out/Release \
LINUX_LUMENRTC_NATIVE_DIR=/path/to/linux/native/build \
LINUX_LUMENRTC_BRIDGE_BUILD_DIR=/path/to/linux/webrtc/out/Release \
PACKAGE_VERSION=1.0.1 \
scripts/pack-all.sh
```

One-command local release (build win+linux, pack multi-RID, push to NuGet):

```powershell
$env:NUGET_API_KEY = "<nuget-api-key>"
pwsh -File .\scripts\release-local.ps1 -Version 1.0.2 -BuildType Release
```

Note: Windows native asset is packed as `runtimes/win-x64/native/lumenrtc_native.dll` to avoid
publish collisions on case-insensitive filesystems (`LumenRTC.dll` vs `lumenrtc.dll`). Local native
build outputs may still be named `lumenrtc.dll`; the managed resolver supports both names.

Useful options:

- `-SkipNuGetPush` to only create `.nupkg` locally
- `-WslDistro <name>` if you need a specific distro
- `-WinLumenRtcBridgeBuildDir`, `-LinuxLumenRtcBridgeBuildDir` for explicit lumenrtc_bridge paths
- `-WinLumenRtcNativeDir`, `-LinuxLumenRtcNativeDir` to reuse prebuilt native outputs

SDL renderer runtime (optional):

- Windows: `SDL2.dll` must be on `PATH` or next to the app.
- Linux: `libSDL2-2.0.so.0` must be discoverable by the loader.

## AppVeyor (Hosted, Quota-Friendly)

Building `lumenrtc_bridge` from source on free hosted CI usually exceeds quotas.
Use AppVeyor only for ABI checks in this repository.

AppVeyor runs:

- ABI governance tests from `tests/abi/` (Python unittest)
- `scripts/abi_guardrails.sh`
- ABI generate/codegen checks with `--check --fail-on-sync`
- managed build validation (`dotnet build src/LumenRTC/LumenRTC.csproj`)
- ABI verification/audit reports into `artifacts/abi`

## SDL Renderer

The `LumenRTC.Rendering.Sdl` project provides a minimal video renderer using SDL2.

```csharp
using LumenRTC.Rendering.Sdl;

using var renderer = new SdlVideoRenderer("LumenRTC", 1280, 720);
track.AddSink(renderer.Sink);
renderer.Run();
```

## Codec Preferences

```csharp
var codecs = factory.GetRtpSenderCodecMimeTypes(MediaType.Video);
Console.WriteLine(string.Join(", ", codecs));

// Prefer AV1, then VP9, VP8, H264 (if supported by both peers).
pc.SetCodecPreferences(MediaType.Video, new[]
{
    "video/AV1",
    "video/VP9",
    "video/VP8",
    "video/H264",
});
```

Apply codec preferences after adding tracks and before creating an offer.

## Logging

```csharp
RtcLogging.SetMinLevel(LogSeverity.Info);
RtcLogging.SetLogSink(LogSeverity.Info, message => Console.WriteLine(message));
```

Call `RtcLogging.RemoveLogSink()` to detach the callback.

## DTMF

```csharp
var sender = pc.GetSenders().First(s => s.MediaType == MediaType.Audio);
var dtmf = sender.DtmfSender;
if (dtmf?.CanInsert == true)
{
    dtmf.InsertDtmf("123#", duration: 100, interToneGap: 70);
}
```

## Samples

Local camera preview (requires SDL2 runtime):

```bash
# Convenience API
dotnet run --project samples/LumenRTC.Sample.LocalCamera.Convenience/LumenRTC.Sample.LocalCamera.Convenience.csproj

# Core API
dotnet run --project samples/LumenRTC.Sample.LocalCamera.Core/LumenRTC.Sample.LocalCamera.Core.csproj
```

Screen share preview (requires SDL2 runtime):

```bash
# Core API
dotnet run --project samples/LumenRTC.Sample.ScreenShare.Core/LumenRTC.Sample.ScreenShare.Core.csproj

# Convenience API
dotnet run --project samples/LumenRTC.Sample.ScreenShare.Convenience/LumenRTC.Sample.ScreenShare.Convenience.csproj
```

Signaling server (simple WebSocket relay):

```bash
dotnet run --project samples/LumenRTC.Sample.SignalingServer/LumenRTC.Sample.SignalingServer.csproj -- --url http://localhost:8080/ws/
```

Streaming demo (run in two terminals):

```bash
# Core API sender (captures screen)
dotnet run --project samples/LumenRTC.Sample.Streaming.Core/LumenRTC.Sample.Streaming.Core.csproj -- \
  --role sender --server ws://localhost:8080/ws/ --room demo --capture screen --source 0 --fps 30

# Core API viewer (renders remote track + opens data channel)
dotnet run --project samples/LumenRTC.Sample.Streaming.Core/LumenRTC.Sample.Streaming.Core.csproj -- \
  --role viewer --server ws://localhost:8080/ws/ --room demo

# Convenience API sender
dotnet run --project samples/LumenRTC.Sample.Streaming.Convenience/LumenRTC.Sample.Streaming.Convenience.csproj -- \
  --role sender --server ws://localhost:8080/ws/ --room demo --capture screen --source 0 --fps 30

# Convenience API viewer
dotnet run --project samples/LumenRTC.Sample.Streaming.Convenience/LumenRTC.Sample.Streaming.Convenience.csproj -- \
  --role viewer --server ws://localhost:8080/ws/ --room demo
```

The signaling sample emits `room_state` and `peer_joined` events, so
negotiation starts reliably even if the viewer starts before the sender.

Windows one-command launcher (opens signaling + viewer + sender in separate windows):

```powershell
scripts\run-streaming-demo.cmd -NoBuild
```

For signaling diagnostics:

```powershell
scripts\run-streaming-demo.cmd -NoBuild -TraceSignaling
```

Optional STUN:

```bash
--stun stun:stun.l.google.com:19302
```

## ABI notes

- Callbacks run on WebRTC worker/signaling threads.
- Strings passed to callbacks are valid only for the duration of the callback.
- `lrtc_video_frame_t` is a handle you must release. If you need to keep a frame
  beyond the callback, call `lrtc_video_frame_retain` to create a new handle.

## Capabilities

- Native C ABI and managed .NET wrapper (`net10.0`)
- Peer connection, ICE, data channels, RTP senders/receivers/transceivers
- Camera and desktop capture, plus optional SDL2 rendering
- ABI governance pipeline with generation, codegen, verify, sync, and release prep
- Multi-language binding generation from a single IDL snapshot:
  - Python ctypes (`abi/generated/lumenrtc/lumenrtc_ctypes.py`)
  - Rust FFI (`abi/generated/lumenrtc/lumenrtc_ffi.rs`)
  - TypeScript/Node.js ffi-napi (`abi/generated/lumenrtc/lumenrtc_ffi.ts`)
  - Go cgo (`abi/generated/lumenrtc/lumenrtc_ffi.go`)
- ABI governance via [abi-forge](https://github.com/vitkuz573/abi-forge) with incremental generator cache and watch mode
