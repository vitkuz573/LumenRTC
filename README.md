# LumenRTC

LumenRTC is a native C ABI + .NET (`net10.0`) wrapper over `libwebrtc`.
Canonical upstream for WebRTC is `https://webrtc.googlesource.com/src.git`.

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
- `abi/`: ABI config, baselines, generated IDL, changelog/governance docs.
- `tools/abi_framework/`: reusable ABI framework.
- `tools/lumenrtc_roslyn_codegen/`: Roslyn source generator for managed interop.

## Prerequisites

- .NET SDK 10
- CMake + Ninja
- Python 3
- Git
- `depot_tools` (`gclient`, `gn`, `ninja`) for setup/sync scripts

`vendor/libwebrtc` is the local bridge wrapper source used when
`LIBWEBRTC_ROOT` is not explicitly set.

## First Clone Setup (Recommended)

This pulls official WebRTC, syncs `vendor/libwebrtc` into the checkout,
builds `libwebrtc`, then builds LumenRTC native + managed layers.

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
  -DLIBWEBRTC_ROOT=/path/to/libwebrtc \
  -DLIBWEBRTC_BUILD_DIR=/path/to/webrtc/out/Release \
  -DLUMENRTC_ENABLE_DESKTOP_CAPTURE=ON
cmake --build native/build -j
```

Managed build:

```bash
dotnet build src/LumenRTC/LumenRTC.csproj -c Release
```

If `libwebrtc` was built without desktop capture, use
`-DLUMENRTC_ENABLE_DESKTOP_CAPTURE=OFF`.

## One-Command Bootstrap

Linux:

```bash
scripts/bootstrap.sh --libwebrtc-build-dir /path/to/webrtc/out/Release
```

Windows:

```powershell
scripts\bootstrap.ps1 -LibWebRtcBuildDir C:\path\to\webrtc\out\Release
```

If build dir is omitted (or set to `auto`), bootstrap tries common output paths
and env vars (`LIBWEBRTC_BUILD_DIR`, `WEBRTC_BUILD_DIR`, etc.).

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

Bash:

```bash
scripts/abi.sh baseline
scripts/abi.sh check --skip-binary
scripts/abi.sh check-all --skip-binary
scripts/abi.sh generate --skip-binary
scripts/abi.sh codegen --skip-binary
scripts/abi.sh sync --skip-binary
scripts/abi.sh release-prepare --skip-binary --release-tag vX.Y.Z
```

PowerShell:

```powershell
scripts\abi.ps1 baseline
scripts\abi.ps1 check --skip-binary
scripts\abi.ps1 check-all --skip-binary
scripts\abi.ps1 generate --skip-binary
scripts\abi.ps1 codegen --skip-binary
scripts\abi.ps1 sync --skip-binary
scripts\abi.ps1 release-prepare --skip-binary --release-tag vX.Y.Z
```

For full command list:
- `scripts/abi.sh`
- `scripts/abi.ps1`
- `python3 tools/abi_framework/abi_framework.py --help`

Current ABI facts:
- ABI IDL schema: v1 (`abi/generated/lumenrtc/lumenrtc.idl.json`)
- Native artifacts generated from IDL:
  `native/include/lumenrtc.h`, `native/lumenrtc.map` (do not edit manually)
- LumenRTC target uses `clang_preprocess` parser backend; CI asserts no fallback.

## Runtime

Ensure `lumenrtc` and `libwebrtc` are discoverable by the loader:
- Windows: on `PATH` or next to app binaries.
- Linux: on `LD_LIBRARY_PATH` (or via rpath).

Sample builds can copy native libs into output automatically. Override via
`LumenRtcNativeDir` and `LibWebRtcBuildDir` (or `LIBWEBRTC_BUILD_DIR`).

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
scripts\pack.ps1 -Configuration Release -Rid win-x64 -LibWebRtcBuildDir C:\path\to\webrtc\out\Release
```

Linux:

```bash
RID=linux-x64 LIBWEBRTC_BUILD_DIR=/path/to/webrtc/out/Release scripts/pack.sh
```

To pack without native libraries, use `-NoNative` (PowerShell) or `NO_NATIVE=true`.

To build multiple RID-specific packages in one go (each written to its own
output subfolder):

```powershell
scripts\pack-all.ps1 -Rids win-x64,linux-x64 -LibWebRtcBuildDir C:\path\to\webrtc\out\Release
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
