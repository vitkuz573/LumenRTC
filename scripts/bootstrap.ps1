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

function Quote-CmdArg {
  param([string]$Value)
  if ([string]::IsNullOrWhiteSpace($Value)) { return '""' }
  if ($Value -match '[\s"]') {
    $escaped = $Value -replace '"', '\"'
    return '"' + $escaped + '"'
  }
  return $Value
}

function Resolve-VsDevCmd {
  $candidates = @()
  if (-not [string]::IsNullOrWhiteSpace($env:GYP_MSVS_OVERRIDE_PATH)) {
    $candidates += Join-Path $env:GYP_MSVS_OVERRIDE_PATH "Common7\\Tools\\VsDevCmd.bat"
  }
  if (-not [string]::IsNullOrWhiteSpace($env:VSINSTALLDIR)) {
    $candidates += Join-Path $env:VSINSTALLDIR "Common7\\Tools\\VsDevCmd.bat"
  }

  $vswherePath = $null
  if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
    $vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\\Installer\\vswhere.exe"
  }
  if (-not (Test-Path $vswherePath) -and -not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    $vswherePath = Join-Path $env:ProgramFiles "Microsoft Visual Studio\\Installer\\vswhere.exe"
  }
  if (Test-Path $vswherePath) {
    $installPath = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installPath)) {
      $candidates += Join-Path $installPath "Common7\\Tools\\VsDevCmd.bat"
    }
  }

  foreach ($candidate in $candidates) {
    if (Test-Path $candidate) { return $candidate }
  }
  return $null
}

function Invoke-VsDevCmd {
  param([string]$Command)
  $vsDevCmd = Resolve-VsDevCmd
  if (-not $vsDevCmd) {
    throw "Visual Studio Build Tools not found. Set GYP_MSVS_OVERRIDE_PATH or install VS with C++ workload."
  }
  $cmdLine = "call " + (Quote-CmdArg $vsDevCmd) + " -arch=x64 -host_arch=x64 -no_logo && " + $Command
  cmd /c $cmdLine
  if ($LASTEXITCODE -ne 0) {
    throw "Command failed: $Command"
  }
}

function Test-LibWebRtcBuildDir {
  param([string]$Path)
  if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
  $candidates = @(
    (Join-Path $Path "libwebrtc.dll"),
    (Join-Path $Path "libwebrtc.dll.lib"),
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

if ($env:OS -eq "Windows_NT") {
  $cmakeConfigureArgs = @("-S", "native", "-B", $CMakeBuildDir) + $cmakeArgs + @("-G", "Ninja", "-DCMAKE_C_COMPILER=cl", "-DCMAKE_CXX_COMPILER=cl")
  $cmakeBuildArgs = @("--build", $CMakeBuildDir, "--config", $BuildType)
  $configureCmd = "cmake " + (($cmakeConfigureArgs | ForEach-Object { Quote-CmdArg $_ }) -join " ")
  $buildCmd = "cmake " + (($cmakeBuildArgs | ForEach-Object { Quote-CmdArg $_ }) -join " ")

  Invoke-VsDevCmd $configureCmd
  Invoke-VsDevCmd $buildCmd
} else {
  cmake -S native -B $CMakeBuildDir @cmakeArgs
  cmake --build $CMakeBuildDir --config $BuildType
}

dotnet build src/LumenRTC/LumenRTC.csproj -c $BuildType
