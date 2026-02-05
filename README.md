# LumenRTC

LumenRTC is a C ABI + C# binding layer for the `webrtc-sdk/libwebrtc` wrapper.
Upstream: `https://github.com/webrtc-sdk/libwebrtc`.
The goal is a stable native ABI for .NET while keeping the heavy lifting in
`libwebrtc`.

## Layout

- `native/` C ABI layer built as `lumenrtc` shared library.
- `src/LumenRTC/` .NET wrapper (P/Invoke + managed API).
- `src/LumenRTC.Rendering.Sdl/` SDL2-based renderer helper (optional).

## Build prerequisites

1. Fetch the upstream wrapper (submodule):

```bash
git submodule update --init --recursive
```

This populates `external/libwebrtc` and is used by default if
`LIBWEBRTC_ROOT` is not set.

2. Build `libwebrtc` as described in `external/libwebrtc/README.md` (in your WebRTC
   checkout). You need a `libwebrtc` shared library (`.dll` or `.so`).
3. Note the build output directory (webrtc `out` folder).

## Build native (C ABI)

```bash
cmake -S native -B native/build \
  -DLIBWEBRTC_ROOT=/path/to/libwebrtc \
  -DLIBWEBRTC_BUILD_DIR=/path/to/webrtc/out-debug/Linux-x64 \
  -DLUMENRTC_ENABLE_DESKTOP_CAPTURE=ON

cmake --build native/build -j
```

If you use the submodule at `external/libwebrtc`, you can omit
`LIBWEBRTC_ROOT` and only pass `LIBWEBRTC_BUILD_DIR`.

If your `libwebrtc` was built without desktop capture, set
`LUMENRTC_ENABLE_DESKTOP_CAPTURE=OFF` to avoid ABI mismatches.

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
dotnet run --project samples/LumenRTC.Sample.LocalCamera/LumenRTC.Sample.LocalCamera.csproj
```

Screen share preview (requires SDL2 runtime and desktop capture enabled):

```bash
dotnet run --project samples/LumenRTC.Sample.ScreenShare/LumenRTC.Sample.ScreenShare.csproj
```

Screen share loopback (offer/answer in-process, codec preferences applied):

```bash
dotnet run --project samples/LumenRTC.Sample.ScreenShareLoopback/LumenRTC.Sample.ScreenShareLoopback.csproj
```

Signaling server (simple WebSocket relay):

```bash
dotnet run --project samples/LumenRTC.Sample.SignalingServer/LumenRTC.Sample.SignalingServer.csproj -- --url http://localhost:8080/ws/
```

Streaming demo (run in two terminals):

```bash
# Sender (captures screen)
dotnet run --project samples/LumenRTC.Sample.Streaming/LumenRTC.Sample.Streaming.csproj -- \\
  --role sender --server ws://localhost:8080/ws/ --room demo --capture screen --source 0 --fps 30

# Viewer (renders remote track + opens data channel)
dotnet run --project samples/LumenRTC.Sample.Streaming/LumenRTC.Sample.Streaming.csproj -- \\
  --role viewer --server ws://localhost:8080/ws/ --room demo
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
renderer integration. Stats marshaling and higher-level helpers are next.
