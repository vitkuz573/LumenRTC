param(
  [string]$Configuration = "Release",
  [string]$Rid = "win-x64",
  [string]$Output = "artifacts\\nuget",
  [string]$LumenRtcBridgeBuildDir,
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

if (-not $LumenRtcBridgeBuildDir) {
  if ($env:LUMENRTC_BRIDGE_BUILD_DIR) { $LumenRtcBridgeBuildDir = $env:LUMENRTC_BRIDGE_BUILD_DIR }
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
  if ($LumenRtcBridgeBuildDir) { $packArgs += "-p:LumenRtcBridgeBuildDir=$LumenRtcBridgeBuildDir" }
}

& dotnet @packArgs

if ($PackSdl) {
  & dotnet pack "src\\LumenRTC.Rendering.Sdl\\LumenRTC.Rendering.Sdl.csproj" -c $Configuration -o $Output
}
