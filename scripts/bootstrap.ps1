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

if ([string]::IsNullOrWhiteSpace($LibWebRtcBuildDir)) {
  Write-Error "LIBWEBRTC_BUILD_DIR не задан. Укажите -LibWebRtcBuildDir или переменную окружения LIBWEBRTC_BUILD_DIR."
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
