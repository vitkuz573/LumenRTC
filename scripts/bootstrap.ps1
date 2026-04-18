Param(
  [Parameter(Mandatory = $false)]
  [string]$LumenRtcBridgeBuildDir = $env:LUMENRTC_BRIDGE_BUILD_DIR,

  [Parameter(Mandatory = $false)]
  [string]$CMakeBuildDir = "native/build",

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

function Test-LumenRtcBridgeBuildDir {
  param([string]$Path)
  if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
  $candidates = @(
    (Join-Path $Path "lumenrtc_bridge.dll"),
    (Join-Path $Path "lumenrtc_bridge.dll.lib"),
    (Join-Path $Path "lumenrtc_bridge.lib"),
    (Join-Path $Path "lumenrtc_bridge.so"),
    (Join-Path $Path "lumenrtc_bridge.dylib")
  )
  foreach ($candidate in $candidates) {
    if (Test-Path $candidate) { return $true }
  }
  return $false
}

function Resolve-WindowsNinjaForCMake {
  if ($env:OS -ne "Windows_NT") { return $null }

  $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..") -ErrorAction SilentlyContinue
  $repoRootPath = if ($repoRoot) { $repoRoot.Path } else { $null }
  $candidates = @()

  if (-not [string]::IsNullOrWhiteSpace($env:DEPOT_TOOLS)) {
    $candidates += (Join-Path $env:DEPOT_TOOLS "ninja.bat")
    $candidates += (Join-Path $env:DEPOT_TOOLS "ninja.exe")
  }

  if (-not [string]::IsNullOrWhiteSpace($repoRootPath)) {
    $candidates += (Join-Path $repoRootPath "depot_tools\\ninja.bat")
    $candidates += (Join-Path $repoRootPath "depot_tools\\ninja.exe")
    $candidates += (Join-Path $repoRootPath "..\\depot_tools\\ninja.bat")
    $candidates += (Join-Path $repoRootPath "..\\depot_tools\\ninja.exe")
  }

  foreach ($candidate in $candidates) {
    if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate)) {
      return (Resolve-Path $candidate).Path
    }
  }

  foreach ($commandName in @("ninja.bat", "ninja.exe", "ninja")) {
    $command = Get-Command $commandName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) {
      if (-not [string]::IsNullOrWhiteSpace($command.Source)) { return $command.Source }
      if (-not [string]::IsNullOrWhiteSpace($command.Path)) { return $command.Path }
      return $commandName
    }
  }

  return "ninja"
}

function Find-LumenRtcBridgeBuildDir {
  $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
  $candidates = @()

  if (-not [string]::IsNullOrWhiteSpace($env:LUMENRTC_BRIDGE_BUILD_DIR)) { $candidates += $env:LUMENRTC_BRIDGE_BUILD_DIR }

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
    if (Test-LumenRtcBridgeBuildDir $dir) { return $dir }
  }

  return $null
}

if ([string]::IsNullOrWhiteSpace($LumenRtcBridgeBuildDir) -or $LumenRtcBridgeBuildDir -eq "auto") {
  $LumenRtcBridgeBuildDir = Find-LumenRtcBridgeBuildDir
}

if ([string]::IsNullOrWhiteSpace($LumenRtcBridgeBuildDir)) {
  Write-Error "LUMENRTC_BRIDGE_BUILD_DIR is not set. Pass -LumenRtcBridgeBuildDir or set the LUMENRTC_BRIDGE_BUILD_DIR environment variable."
  Write-Error "Tip: you can pass -LumenRtcBridgeBuildDir auto for auto-detection."
  Write-Error "If you have not built lumenrtc_bridge yet, run scripts\\setup.ps1 to fetch and build it."
}

$cmakeArgs = @(
  "-DLUMENRTC_BRIDGE_BUILD_DIR=$LumenRtcBridgeBuildDir"
)

if (-not [string]::IsNullOrWhiteSpace($env:LUMENRTC_BRIDGE_ROOT)) {
  $cmakeArgs += "-DLUMENRTC_BRIDGE_ROOT=$env:LUMENRTC_BRIDGE_ROOT"
}

if ($env:OS -eq "Windows_NT") {
  $resolvedNinja = Resolve-WindowsNinjaForCMake
  $cmakeConfigureArgs = @("-S", "native", "-B", $CMakeBuildDir) + $cmakeArgs + @("-G", "Ninja", "-DCMAKE_C_COMPILER=cl", "-DCMAKE_CXX_COMPILER=cl", "-DCMAKE_BUILD_TYPE=$BuildType")
  if (-not [string]::IsNullOrWhiteSpace($resolvedNinja)) {
    $cmakeConfigureArgs += "-DCMAKE_MAKE_PROGRAM=$resolvedNinja"
    Write-Host "Using Ninja for CMake: $resolvedNinja"
  }
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
