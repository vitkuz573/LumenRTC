param(
  [string]$Configuration = "Release",
  [string]$Rid = "win-x64",
  [string]$Output = "artifacts\\nuget",
  [string]$LibWebRtcBuildDir,
  [string]$LumenRtcNativeDir,
  [switch]$NoNative,
  [switch]$PackSdl
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if (-not $LumenRtcNativeDir) {
  $candidate = Join-Path $repoRoot "native\\build\\Release"
  if (Test-Path (Join-Path $candidate "lumenrtc.dll")) {
    $LumenRtcNativeDir = $candidate
  } else {
    $candidate = Join-Path $repoRoot "native\\build"
    if (Test-Path (Join-Path $candidate "lumenrtc.dll")) {
      $LumenRtcNativeDir = $candidate
    }
  }
}

if (-not $LibWebRtcBuildDir) {
  if ($env:LIBWEBRTC_BUILD_DIR) { $LibWebRtcBuildDir = $env:LIBWEBRTC_BUILD_DIR }
  elseif ($env:WEBRTC_BUILD_DIR) { $LibWebRtcBuildDir = $env:WEBRTC_BUILD_DIR }
  elseif ($env:WEBRTC_OUT_DIR) { $LibWebRtcBuildDir = $env:WEBRTC_OUT_DIR }
  elseif ($env:WEBRTC_OUT) { $LibWebRtcBuildDir = $env:WEBRTC_OUT }
}

$packArgs = @(
  "pack",
  "src\\LumenRTC\\LumenRTC.csproj",
  "-c", $Configuration,
  "-o", $Output
)

if (-not $NoNative) {
  $packArgs += "-p:LumenRtcPackNative=true"
  $packArgs += "-p:LumenRtcPackRid=$Rid"
  if ($LumenRtcNativeDir) { $packArgs += "-p:LumenRtcNativeDir=$LumenRtcNativeDir" }
  if ($LibWebRtcBuildDir) { $packArgs += "-p:LibWebRtcBuildDir=$LibWebRtcBuildDir" }
}

& dotnet @packArgs

if ($PackSdl) {
  & dotnet pack "src\\LumenRTC.Rendering.Sdl\\LumenRTC.Rendering.Sdl.csproj" -c $Configuration -o $Output
}
