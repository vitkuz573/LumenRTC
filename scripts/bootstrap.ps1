Param(
  [Parameter(Mandatory = $false)]
  [string]$LibWebRtcBuildDir = $env:LIBWEBRTC_BUILD_DIR,

  [Parameter(Mandatory = $false)]
  [string]$CMakeBuildDir = "native/build",

  [Parameter(Mandatory = $false)]
  [ValidateSet("ON", "OFF")]
  [string]$DesktopCapture = "ON",

  [Parameter(Mandatory = $false)]
  [ValidateSet("Release", "Debug")]
  [string]$BuildType = "Release"
)

$ErrorActionPreference = "Stop"

function Test-LibWebRtcBuildDir {
  param([string]$Path)
  if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
  $candidates = @(
    (Join-Path $Path "libwebrtc.dll"),
    (Join-Path $Path "libwebrtc.lib"),
    (Join-Path $Path "libwebrtc.so"),
    (Join-Path $Path "libwebrtc.dylib")
  )
  foreach ($candidate in $candidates) {
    if (Test-Path $candidate) { return $true }
  }
  return $false
}

function Find-LibWebRtcBuildDir {
  $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
  $candidates = @()

  if (-not [string]::IsNullOrWhiteSpace($env:LIBWEBRTC_BUILD_DIR)) { $candidates += $env:LIBWEBRTC_BUILD_DIR }
  if (-not [string]::IsNullOrWhiteSpace($env:WEBRTC_BUILD_DIR)) { $candidates += $env:WEBRTC_BUILD_DIR }
  if (-not [string]::IsNullOrWhiteSpace($env:WEBRTC_OUT_DIR)) { $candidates += $env:WEBRTC_OUT_DIR }
  if (-not [string]::IsNullOrWhiteSpace($env:WEBRTC_OUT)) { $candidates += $env:WEBRTC_OUT }

  $candidates += @(
    (Join-Path $repoRoot "..\\webrtc_build\\src\\out-debug\\Linux-x64"),
    (Join-Path $repoRoot "..\\webrtc_build\\src\\out\\Debug"),
    (Join-Path $repoRoot "..\\webrtc_build\\src\\out\\Release"),
    (Join-Path $repoRoot "..\\webrtc\\src\\out\\Debug"),
    (Join-Path $repoRoot "..\\webrtc\\src\\out\\Release"),
    (Join-Path $repoRoot "..\\webrtc\\src\\out\\Default"),
    (Join-Path $repoRoot "..\\webrtc\\src\\out-default")
  )

  foreach ($dir in $candidates) {
    if (Test-LibWebRtcBuildDir $dir) { return $dir }
  }

  return $null
}

if ([string]::IsNullOrWhiteSpace($LibWebRtcBuildDir) -or $LibWebRtcBuildDir -eq "auto") {
  $LibWebRtcBuildDir = Find-LibWebRtcBuildDir
}

if ([string]::IsNullOrWhiteSpace($LibWebRtcBuildDir)) {
  Write-Error "LIBWEBRTC_BUILD_DIR is not set. Pass -LibWebRtcBuildDir or set the LIBWEBRTC_BUILD_DIR environment variable."
  Write-Error "Tip: you can pass -LibWebRtcBuildDir auto for auto-detection."
  Write-Error "If you have not built libwebrtc yet, run scripts\\setup.ps1 to fetch and build it."
}

$cmakeArgs = @(
  "-DLIBWEBRTC_BUILD_DIR=$LibWebRtcBuildDir",
  "-DLUMENRTC_ENABLE_DESKTOP_CAPTURE=$DesktopCapture"
)

if (-not [string]::IsNullOrWhiteSpace($env:LIBWEBRTC_ROOT)) {
  $cmakeArgs += "-DLIBWEBRTC_ROOT=$env:LIBWEBRTC_ROOT"
}

cmake -S native -B $CMakeBuildDir @cmakeArgs
cmake --build $CMakeBuildDir --config $BuildType

dotnet build src/LumenRTC/LumenRTC.csproj -c $BuildType
