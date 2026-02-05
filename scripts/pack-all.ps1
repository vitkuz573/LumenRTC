param(
  [string]$Configuration = "Release",
  [string[]]$Rids = @("win-x64", "linux-x64"),
  [string]$Output = "artifacts\\nuget",
  [string]$LibWebRtcBuildDir,
  [string]$LumenRtcNativeDir,
  [switch]$NoNative,
  [switch]$PackSdl
)

$ErrorActionPreference = "Stop"

$packScript = Join-Path $PSScriptRoot "pack.ps1"

if ($NoNative) {
  & $packScript -Configuration $Configuration -Output $Output -NoNative -PackSdl:$PackSdl
  exit $LASTEXITCODE
}

foreach ($rid in $Rids) {
  $ridOutput = Join-Path $Output $rid
  $args = @(
    "-Configuration", $Configuration,
    "-Rid", $rid,
    "-Output", $ridOutput
  )
  if ($LibWebRtcBuildDir) { $args += @("-LibWebRtcBuildDir", $LibWebRtcBuildDir) }
  if ($LumenRtcNativeDir) { $args += @("-LumenRtcNativeDir", $LumenRtcNativeDir) }
  if ($PackSdl) { $args += "-PackSdl" }
  & $packScript @args
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
