Param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("server", "viewer", "sender")]
  [string]$Mode,

  [Parameter(Mandatory = $true)]
  [string]$RepoRoot,

  [Parameter(Mandatory = $true)]
  [string]$SignalUrl,

  [Parameter(Mandatory = $false)]
  [string]$Room = "demo",

  [Parameter(Mandatory = $false)]
  [ValidateSet("screen", "window")]
  [string]$Capture = "screen",

  [Parameter(Mandatory = $false)]
  [int]$Source = 0,

  [Parameter(Mandatory = $false)]
  [int]$Fps = 30,

  [Parameter(Mandatory = $false)]
  [ValidateSet("true", "false")]
  [string]$Cursor = "true",

  [Parameter(Mandatory = $false)]
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Debug",

  [Parameter(Mandatory = $false)]
  [string]$LibWebRtcBuildDir = "",

  [Parameter(Mandatory = $false)]
  [string]$LumenRtcNativeDir = "",

  [Parameter(Mandatory = $false)]
  [switch]$TraceSignaling,

  [Parameter(Mandatory = $false)]
  [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$resolvedRepoRoot = (Resolve-Path $RepoRoot).Path
Set-Location $resolvedRepoRoot

if (-not [string]::IsNullOrWhiteSpace($LibWebRtcBuildDir)) {
  $env:LIBWEBRTC_BUILD_DIR = $LibWebRtcBuildDir
}
if (-not [string]::IsNullOrWhiteSpace($LumenRtcNativeDir)) {
  $env:LumenRtcNativeDir = $LumenRtcNativeDir
}

$projectPath = ""
$modeArgs = @()

switch ($Mode) {
  "server" {
    $Host.UI.RawUI.WindowTitle = "LumenRTC Signaling Server"
    $projectPath = Join-Path $resolvedRepoRoot "samples\LumenRTC.Sample.SignalingServer\LumenRTC.Sample.SignalingServer.csproj"
    $modeArgs = @("--url", $SignalUrl)
  }
  "viewer" {
    $Host.UI.RawUI.WindowTitle = "LumenRTC Streaming Viewer"
    $projectPath = Join-Path $resolvedRepoRoot "samples\LumenRTC.Sample.Streaming.Core\LumenRTC.Sample.Streaming.Core.csproj"
    $modeArgs = @("--role", "viewer", "--server", $SignalUrl, "--room", $Room)
  }
  "sender" {
    $Host.UI.RawUI.WindowTitle = "LumenRTC Streaming Sender"
    $projectPath = Join-Path $resolvedRepoRoot "samples\LumenRTC.Sample.Streaming.Core\LumenRTC.Sample.Streaming.Core.csproj"
    $modeArgs = @(
      "--role", "sender",
      "--server", $SignalUrl,
      "--room", $Room,
      "--capture", $Capture,
      "--source", "$Source",
      "--fps", "$Fps",
      "--cursor", $Cursor
    )
  }
}

if ($TraceSignaling -and $Mode -ne "server") {
  $modeArgs += @("--trace-signaling", "true")
}

if (-not (Test-Path $projectPath)) {
  throw "Project not found: $projectPath"
}

$runArgs = @("run")
if ($NoBuild) {
  $runArgs += "--no-build"
}
$runArgs += @("--configuration", $Configuration, "--project", $projectPath, "--")
$runArgs += $modeArgs

Write-Host "Starting $Mode..."
& dotnet @runArgs
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}
