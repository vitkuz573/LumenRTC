Param(
  [Parameter(Mandatory = $false)]
  [string]$Room = "demo",

  [Parameter(Mandatory = $false)]
  [string]$SignalUrl = "http://localhost:8080/ws/",

  [Parameter(Mandatory = $false)]
  [ValidateSet("screen", "window")]
  [string]$Capture = "screen",

  [Parameter(Mandatory = $false)]
  [int]$Source = 0,

  [Parameter(Mandatory = $false)]
  [int]$Fps = 30,

  [Parameter(Mandatory = $false)]
  [switch]$NoCursor,

  [Parameter(Mandatory = $false)]
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Debug",

  [Parameter(Mandatory = $false)]
  [switch]$NoBuild,

  [Parameter(Mandatory = $false)]
  [switch]$SkipServer,

  [Parameter(Mandatory = $false)]
  [switch]$KillExisting,

  [Parameter(Mandatory = $false)]
  [switch]$VerboseLaunch,

  [Parameter(Mandatory = $false)]
  [switch]$TraceSignaling
)

$ErrorActionPreference = "Stop"

function Ensure-TrailingSlash {
  param([string]$Url)
  if ([string]::IsNullOrWhiteSpace($Url)) {
    return $Url
  }
  if ($Url.EndsWith("/")) {
    return $Url
  }
  return "$Url/"
}

function Convert-ToClientServerUrl {
  param([string]$Url)
  $trimmed = Ensure-TrailingSlash $Url
  if ($trimmed -match "^http://") {
    return "ws://" + $trimmed.Substring(7)
  }
  if ($trimmed -match "^https://") {
    return "wss://" + $trimmed.Substring(8)
  }
  return $trimmed
}

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
    $full = (Resolve-Path $candidate -ErrorAction SilentlyContinue)
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
    $full = (Resolve-Path $candidate -ErrorAction SilentlyContinue)
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

function Resolve-PowerShellExe {
  $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
  if ($pwsh) {
    return $pwsh.Source
  }
  $ps = Get-Command powershell.exe -ErrorAction SilentlyContinue
  if ($ps) {
    return $ps.Source
  }
  throw "PowerShell executable not found (pwsh/powershell.exe)."
}

function Stop-RunningSampleProcesses {
  param([string]$Pattern)

  $processes = Get-CimInstance Win32_Process -Filter "name='dotnet.exe'" -ErrorAction SilentlyContinue
  if (-not $processes) {
    return
  }

  foreach ($proc in $processes) {
    if ($proc.ProcessId -eq $PID) {
      continue
    }
    if ($proc.CommandLine -and $proc.CommandLine.Contains($Pattern)) {
      try {
        Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
      } catch {
      }
    }
  }
}

function Start-PowerShellWindow {
  param(
    [string]$PowerShellExe,
    [string]$ScriptPath,
    [string[]]$ScriptArgs,
    [switch]$VerboseLaunch
  )

  $argList = @()
  $exeName = [System.IO.Path]::GetFileNameWithoutExtension($PowerShellExe)
  if ($exeName -ieq "pwsh") {
    $argList += @("-NoExit", "-File", $ScriptPath)
  } else {
    $argList += @("-NoExit", "-ExecutionPolicy", "Bypass", "-File", $ScriptPath)
  }
  if ($ScriptArgs -and $ScriptArgs.Count -gt 0) {
    $argList += $ScriptArgs
  }
  if ($VerboseLaunch) {
    Write-Host "Launch:" $PowerShellExe ($argList -join " ")
  }
  return Start-Process -FilePath $PowerShellExe -ArgumentList $argList -PassThru
}

$repoRoot = Resolve-RepoRoot

$signalingProject = Join-Path $repoRoot "samples\LumenRTC.Sample.SignalingServer\LumenRTC.Sample.SignalingServer.csproj"
$streamingProject = Join-Path $repoRoot "samples\LumenRTC.Sample.Streaming.Core\LumenRTC.Sample.Streaming.Core.csproj"
$childScript = Join-Path $repoRoot "scripts\run-streaming-child.ps1"

if (-not (Test-Path $signalingProject)) {
  throw "Signaling project not found: $signalingProject"
}
if (-not (Test-Path $streamingProject)) {
  throw "Streaming project not found: $streamingProject"
}
if (-not (Test-Path $childScript)) {
  throw "Child script not found: $childScript"
}

if ($KillExisting) {
  Stop-RunningSampleProcesses -Pattern "LumenRTC.Sample.SignalingServer"
  Stop-RunningSampleProcesses -Pattern "LumenRTC.Sample.Streaming"
}

$libWebRtcBuildDir = Resolve-LibWebRtcBuildDir -RepoRoot $repoRoot
if ([string]::IsNullOrWhiteSpace($libWebRtcBuildDir)) {
  throw "libwebrtc.dll not found. Run scripts\setup.ps1 first or set LIBWEBRTC_BUILD_DIR."
}

$lumenRtcNativeDir = Resolve-LumenRtcNativeDir -RepoRoot $repoRoot
if ([string]::IsNullOrWhiteSpace($lumenRtcNativeDir)) {
  throw "lumenrtc.dll not found. Run scripts\setup.ps1 first or set LumenRtcNativeDir."
}

if (-not $NoBuild) {
  dotnet build $signalingProject -c $Configuration
  dotnet build $streamingProject -c $Configuration
}

$normalizedSignalUrl = Ensure-TrailingSlash $SignalUrl
$clientServerUrl = Convert-ToClientServerUrl $normalizedSignalUrl
$cursorValue = if ($NoCursor) { "false" } else { "true" }
$psExe = Resolve-PowerShellExe

$commonChildArgs = @(
  "-RepoRoot", $repoRoot,
  "-Configuration", $Configuration,
  "-LibWebRtcBuildDir", $libWebRtcBuildDir,
  "-LumenRtcNativeDir", $lumenRtcNativeDir,
  "-NoBuild"
)
if ($TraceSignaling) {
  $commonChildArgs += "-TraceSignaling"
}

$serverArgs = @(
  "-Mode", "server",
  "-SignalUrl", $normalizedSignalUrl
) + $commonChildArgs

$viewerArgs = @(
  "-Mode", "viewer",
  "-SignalUrl", $clientServerUrl,
  "-Room", $Room
) + $commonChildArgs

$senderArgs = @(
  "-Mode", "sender",
  "-SignalUrl", $clientServerUrl,
  "-Room", $Room,
  "-Capture", $Capture,
  "-Source", "$Source",
  "-Fps", "$Fps",
  "-Cursor", $cursorValue
) + $commonChildArgs

$serverProcess = $null
if (-not $SkipServer) {
  $serverProcess = Start-PowerShellWindow -PowerShellExe $psExe -ScriptPath $childScript -ScriptArgs $serverArgs -VerboseLaunch:$VerboseLaunch
  Start-Sleep -Seconds 1
}

$viewerProcess = Start-PowerShellWindow -PowerShellExe $psExe -ScriptPath $childScript -ScriptArgs $viewerArgs -VerboseLaunch:$VerboseLaunch
Start-Sleep -Seconds 1
$senderProcess = Start-PowerShellWindow -PowerShellExe $psExe -ScriptPath $childScript -ScriptArgs $senderArgs -VerboseLaunch:$VerboseLaunch

Write-Host "Started streaming demo."
if ($serverProcess) {
  Write-Host "  Signaling PID: $($serverProcess.Id)"
}
Write-Host "  Viewer PID:    $($viewerProcess.Id)"
Write-Host "  Sender PID:    $($senderProcess.Id)"
Write-Host "Room: $Room"
Write-Host "Server URL: $normalizedSignalUrl"
Write-Host "Client URL: $clientServerUrl"
