Param(
  [Parameter(Mandatory = $false)]
  [int]$Source = 0,

  [Parameter(Mandatory = $false)]
  [int]$Fps = 30,

  [Parameter(Mandatory = $false)]
  [switch]$NoCursor,

  [Parameter(Mandatory = $false)]
  [switch]$TraceSignaling,

  [Parameter(Mandatory = $false)]
  [switch]$TraceIceNative,

  [Parameter(Mandatory = $false)]
  [int]$StatsIntervalMs = 2000,

  [Parameter(Mandatory = $false)]
  [string]$Stun = "",

  [Parameter(Mandatory = $false)]
  [ValidateSet("inproc", "ws")]
  [string]$SignalingMode = "inproc",

  [Parameter(Mandatory = $false)]
  [ValidateSet("nontrickle", "trickle")]
  [string]$IceExchange = "nontrickle",

  [Parameter(Mandatory = $false)]
  [string]$Server = "ws://localhost:8080/ws/",

  [Parameter(Mandatory = $false)]
  [string]$Room = "demo",

  [Parameter(Mandatory = $false)]
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Debug",

  [Parameter(Mandatory = $false)]
  [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
  $root = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Split-Path -Parent $PSScriptRoot
  } else {
    Split-Path -Parent $PSCommandPath
  }
  return (Resolve-Path $root).Path
}

function Resolve-LibWebRtcBuildDir {
  param([string]$RepoRoot)

  $candidates = @()
  if (-not [string]::IsNullOrWhiteSpace($env:LIBWEBRTC_BUILD_DIR)) { $candidates += $env:LIBWEBRTC_BUILD_DIR }
  if (-not [string]::IsNullOrWhiteSpace($env:WEBRTC_BUILD_DIR)) { $candidates += $env:WEBRTC_BUILD_DIR }
  if (-not [string]::IsNullOrWhiteSpace($env:WEBRTC_OUT_DIR)) { $candidates += $env:WEBRTC_OUT_DIR }
  if (-not [string]::IsNullOrWhiteSpace($env:WEBRTC_OUT)) { $candidates += $env:WEBRTC_OUT }

  $candidates += @(
    (Join-Path $RepoRoot "webrtc_build\src\out\Release"),
    (Join-Path $RepoRoot "webrtc_build\src\out\Default")
  )

  foreach ($candidate in $candidates) {
    if ([string]::IsNullOrWhiteSpace($candidate)) {
      continue
    }

    $full = Resolve-Path $candidate -ErrorAction SilentlyContinue
    if (-not $full) {
      continue
    }

    $dllPath = Join-Path $full.Path "libwebrtc.dll"
    if (Test-Path $dllPath) {
      return $full.Path
    }
  }

  return $null
}

function Resolve-LumenRtcNativeDir {
  param([string]$RepoRoot)

  $candidates = @()
  if (-not [string]::IsNullOrWhiteSpace($env:LumenRtcNativeDir)) { $candidates += $env:LumenRtcNativeDir }
  if (-not [string]::IsNullOrWhiteSpace($env:LUMENRTC_NATIVE_DIR)) { $candidates += $env:LUMENRTC_NATIVE_DIR }

  $candidates += @(
    (Join-Path $RepoRoot "native\build"),
    (Join-Path $RepoRoot "native\build\Release")
  )

  foreach ($candidate in $candidates) {
    if ([string]::IsNullOrWhiteSpace($candidate)) {
      continue
    }

    $full = Resolve-Path $candidate -ErrorAction SilentlyContinue
    if (-not $full) {
      continue
    }

    $dllPath = Join-Path $full.Path "lumenrtc.dll"
    if (Test-Path $dllPath) {
      return $full.Path
    }
  }

  return $null
}

$repoRoot = Resolve-RepoRoot
$projectPath = Join-Path $repoRoot "samples\LumenRTC.Sample.ScreenShareLoopback.Core\LumenRTC.Sample.ScreenShareLoopback.Core.csproj"
if (-not (Test-Path $projectPath)) {
  throw "Loopback project not found: $projectPath"
}

$libWebRtcBuildDir = Resolve-LibWebRtcBuildDir -RepoRoot $repoRoot
if ([string]::IsNullOrWhiteSpace($libWebRtcBuildDir)) {
  throw "libwebrtc.dll not found. Run scripts\setup.ps1 first or set LIBWEBRTC_BUILD_DIR."
}

$lumenRtcNativeDir = Resolve-LumenRtcNativeDir -RepoRoot $repoRoot
if ([string]::IsNullOrWhiteSpace($lumenRtcNativeDir)) {
  throw "lumenrtc.dll not found. Run scripts\setup.ps1 first or set LumenRtcNativeDir."
}

$env:LIBWEBRTC_BUILD_DIR = $libWebRtcBuildDir
$env:LumenRtcNativeDir = $lumenRtcNativeDir

if (-not $NoBuild) {
  dotnet build $projectPath -c $Configuration
}

$cursorValue = if ($NoCursor) { "false" } else { "true" }
$effectiveIceExchange = $IceExchange

$runArgs = @("run", "--configuration", $Configuration, "--project", $projectPath, "--")
$runArgs += @("--source", "$Source", "--fps", "$Fps", "--cursor", $cursorValue)
$runArgs += @("--stats-interval-ms", "$StatsIntervalMs")
$runArgs += @("--signaling-mode", $SignalingMode)
$runArgs += @("--ice-exchange", $effectiveIceExchange)

if (-not [string]::IsNullOrWhiteSpace($Stun)) {
  $runArgs += @("--stun", $Stun)
}

if ($SignalingMode -eq "ws") {
  $runArgs += @("--server", $Server, "--room", $Room)
}

if ($TraceSignaling) {
  $runArgs += @("--trace-signaling", "true")
}

if ($TraceIceNative) {
  $runArgs += @("--trace-ice-native", "true")
}

Write-Host "Running loopback demo..."
Write-Host "  Project:   $projectPath"
Write-Host "  Source:    $Source"
Write-Host "  FPS:       $Fps"
Write-Host "  Cursor:    $cursorValue"
Write-Host "  Signaling: $SignalingMode"
Write-Host "  ICE Xchg:  $effectiveIceExchange"
Write-Host "  ICE Trace: $(if ($TraceIceNative) { 'native' } else { 'off' })"
if ($SignalingMode -eq "ws") {
  Write-Host "  Server:    $Server"
  Write-Host "  Room:      $Room"
}

& dotnet @runArgs
exit $LASTEXITCODE
