# LumenRTC

LumenRTC is a C ABI + C# binding layer for a local `libwebrtc` bridge.
WebRTC upstream: `https://webrtc.googlesource.com/src`.
The goal is a stable native ABI for .NET while keeping the heavy lifting in
`libwebrtc`.

Upstream sync runbook: `docs/WEBRTC_UPSTREAM_SYNC.md`.

## Layout

- `native/` C ABI layer built as `lumenrtc` shared library.
- `src/LumenRTC/` .NET wrapper (P/Invoke + managed API).
- `src/LumenRTC.Rendering.Sdl/` SDL2-based renderer helper (optional).

## Build prerequisites

1. Ensure bridge sources are present in `vendor/libwebrtc`.
   This is used by default if
   `LIBWEBRTC_ROOT` is not set.

2. Build `libwebrtc` in an official WebRTC checkout (`webrtc.googlesource.com/src`).
   The setup scripts handle this automatically and sync the local bridge into
   `src/libwebrtc`.
3. Note the build output directory (webrtc `out` folder).

### First clone quickstart (recommended)

The setup scripts sync official WebRTC, copy `vendor/libwebrtc` into the
checkout, build `libwebrtc`, then build `lumenrtc` and the .NET wrapper. This
can take time and disk space.

The scripts require `depot_tools`. If `gclient` is missing, the setup scripts will
clone `depot_tools` into `../depot_tools` automatically.

Windows (run from Developer PowerShell for VS 2026):

```powershell
scripts\\setup.cmd -BuildType Release
```

Linux:

```bash
scripts/setup.sh --build-type Release
```

If you already have a WebRTC checkout, you can pass `-WebRtcRoot` (PowerShell) or
`--webrtc-root` (bash) to reuse it.
Use `-WebRtcBranch` / `--webrtc-branch` to override the default ref
(`branch-heads/7151`).

## Build native (C ABI)

```bash
cmake -S native -B native/build \
  -DLIBWEBRTC_ROOT=/path/to/libwebrtc \
  -DLIBWEBRTC_BUILD_DIR=/path/to/webrtc/out-debug/Linux-x64 \
  -DLUMENRTC_ENABLE_DESKTOP_CAPTURE=ON

cmake --build native/build -j
```

If you use the bridge source at `vendor/libwebrtc`, you can omit
`LIBWEBRTC_ROOT` and only pass `LIBWEBRTC_BUILD_DIR`.

If your `libwebrtc` was built without desktop capture, set
`LUMENRTC_ENABLE_DESKTOP_CAPTURE=OFF` to avoid ABI mismatches.

## ABI Governance

The native ABI is versioned directly in `native/include/lumenrtc.h` via:

- `LUMENRTC_ABI_VERSION_MAJOR`
- `LUMENRTC_ABI_VERSION_MINOR`
- `LUMENRTC_ABI_VERSION_PATCH`

The native library exports runtime probes:

- `lrtc_abi_version_major`
- `lrtc_abi_version_minor`
- `lrtc_abi_version_patch`
- `lrtc_abi_version_string`

The managed loader validates ABI major compatibility before the first P/Invoke
call, so incompatible native binaries fail fast with a clear error.

### ABI Tooling

Use the config-driven ABI framework:

```bash
# Create/update baseline from current header + type surface.
scripts/abi.sh baseline

# Verify current state against baseline.
scripts/abi.sh check --skip-binary

# Verify including native export surface (after native build).
scripts/abi.sh check --binary native/build/liblumenrtc.so

# Verify all configured ABI targets (multi-target repos).
scripts/abi.sh check-all --skip-binary

# Generate ABI IDL from target config.
scripts/abi.sh generate --skip-binary

# Generate ABI IDL + run configured language generators (plugin host).
scripts/abi.sh codegen --skip-binary

# Migrate IDL payload to schema v2 (with metadata + availability/docs fields).
scripts/abi.sh idl-migrate --input abi/generated/lumenrtc/lumenrtc.idl.json --to-version 2

# Sync generated ABI artifacts (and optionally baselines).
scripts/abi.sh sync --skip-binary

# List/configure targets quickly.
scripts/abi.sh list-targets
scripts/abi.sh init-target ...

# Benchmark ABI pipeline latency and output JSON metrics.
scripts/abi.sh benchmark --skip-binary --iterations 3 --output artifacts/abi/benchmark.report.json

# Generate release-ready ABI changelog.
scripts/abi.sh changelog --skip-binary --release-tag vX.Y.Z --output abi/CHANGELOG.md

# One-shot release ABI pipeline (doctor + sync + codegen + verify-all + changelog + benchmark + HTML report).
scripts/abi.sh release-prepare --skip-binary --release-tag vX.Y.Z
```

PowerShell equivalent:

```powershell
scripts\\abi.ps1 baseline
scripts\\abi.ps1 check --skip-binary
scripts\\abi.ps1 check --binary native\\build\\lumenrtc.dll
scripts\\abi.ps1 check-all --skip-binary
scripts\\abi.ps1 generate --skip-binary
scripts\\abi.ps1 codegen --skip-binary
scripts\\abi.ps1 sync --skip-binary
scripts\\abi.ps1 benchmark --skip-binary --iterations 3 --output artifacts\\abi\\benchmark.report.json
scripts\\abi.ps1 changelog --skip-binary --release-tag vX.Y.Z --output abi\\CHANGELOG.md
scripts\\abi.ps1 release-prepare --skip-binary --release-tag vX.Y.Z
```

The generic tooling lives in `tools/abi_framework/` and can be reused for other
ABI targets through `abi/config.json`. LumenRTC-specific C# interop generation is
implemented as a Roslyn source generator in `tools/lumenrtc_roslyn_codegen/`.
The ABI IDL now uses schema v2 and can drive multiple language generators via
`bindings.generators` plugin entries.
`src/LumenRTC` consumes ABI IDL (`abi/generated/lumenrtc/lumenrtc.idl.json`) as
an `AdditionalFiles` input and emits `NativeMethods` at compile time; no
committed `NativeMethods.g.cs` file is required.
The ABI pipeline also generates native ABI artifacts from IDL:
`native/include/lumenrtc.h` and `native/lumenrtc.map` (do not edit manually).
LumenRTC target is configured to parse headers with `clang_preprocess`
(`header.parser.backend`), with local fallback to regex enabled. CI enforces
that clang backend is active without fallback. Parser supports
`header.parser.compiler_candidates` and `ABI_CLANG` override for deterministic
clang selection across environments.

### One-command bootstrap

Linux/macOS:

```bash
scripts/bootstrap.sh --libwebrtc-build-dir /path/to/webrtc/out-debug/Linux-x64
```

Windows (PowerShell):

```powershell
scripts\\bootstrap.ps1 -LibWebRtcBuildDir C:\\path\\to\\webrtc\\out-debug\\Windows-x64
```

If `--libwebrtc-build-dir` is omitted (or set to `auto`), the scripts attempt
a small set of common output paths relative to the repo and environment
variables such as `LIBWEBRTC_BUILD_DIR` and `WEBRTC_BUILD_DIR`.

Windows example (PowerShell):

```powershell
cmake -S native -B native\build `
  -DLIBWEBRTC_ROOT=C:\path\to\libwebrtc `
  -DLIBWEBRTC_BUILD_DIR=C:\path\to\webrtc\src\out-debug\Windows-x64

cmake --build native\build --config Release
```

## Runtime

Make sure `lumenrtc` and `libwebrtc` are discoverable by the loader:

- Windows: add to `PATH` or place DLLs next to the app.
- Linux: add to `LD_LIBRARY_PATH` or use rpath.

For sample projects, the build will copy `lumenrtc` and `libwebrtc` into the
output folder when possible. You can override paths via MSBuild properties:
`LumenRtcNativeDir` and `LibWebRtcBuildDir` (or set `LIBWEBRTC_BUILD_DIR`).

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
scripts\\pack.ps1 -Configuration Release -Rid win-x64 -LibWebRtcBuildDir C:\\path\\to\\webrtc\\out\\Release
```

Linux/macOS:

```bash
RID=linux-x64 LIBWEBRTC_BUILD_DIR=/path/to/webrtc/out/Release scripts/pack.sh
```

To pack without native libraries, use `-NoNative` (PowerShell) or `NO_NATIVE=true`.

To build multiple RID-specific packages in one go (each written to its own
output subfolder):

```powershell
scripts\\pack-all.ps1 -Rids win-x64,linux-x64 -LibWebRtcBuildDir C:\\path\\to\\webrtc\\out\\Release
```

```bash
RIDS=win-x64,linux-x64 LIBWEBRTC_BUILD_DIR=/path/to/webrtc/out/Release scripts/pack-all.sh
```

SDL renderer runtime (optional):

- Windows: `SDL2.dll` must be on `PATH` or next to the app.
- Linux: `libSDL2-2.0.so.0` must be discoverable by the loader.

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

Screen share preview (requires SDL2 runtime and desktop capture enabled):

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
dotnet run --project samples/LumenRTC.Sample.Streaming.Core/LumenRTC.Sample.Streaming.Core.csproj -- \\
  --role sender --server ws://localhost:8080/ws/ --room demo --capture screen --source 0 --fps 30

# Core API viewer (renders remote track + opens data channel)
dotnet run --project samples/LumenRTC.Sample.Streaming.Core/LumenRTC.Sample.Streaming.Core.csproj -- \\
  --role viewer --server ws://localhost:8080/ws/ --room demo

# Convenience API sender
dotnet run --project samples/LumenRTC.Sample.Streaming.Convenience/LumenRTC.Sample.Streaming.Convenience.csproj -- \\
  --role sender --server ws://localhost:8080/ws/ --room demo --capture screen --source 0 --fps 30

# Convenience API viewer
dotnet run --project samples/LumenRTC.Sample.Streaming.Convenience/LumenRTC.Sample.Streaming.Convenience.csproj -- \\
  --role viewer --server ws://localhost:8080/ws/ --room demo
```

The signaling sample emits `room_state` and `peer_joined` events, so
negotiation starts reliably even if the viewer starts before the sender.

Windows one-command launcher (opens signaling + viewer + sender in separate windows):

```powershell
scripts\\run-streaming-demo.cmd -NoBuild
```

For signaling diagnostics:

```powershell
scripts\\run-streaming-demo.cmd -NoBuild -TraceSignaling
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

## Status

Current focus: core peer connection, data channel, audio/video devices, and
renderer integration. Convenience helpers and async wrappers are available.
