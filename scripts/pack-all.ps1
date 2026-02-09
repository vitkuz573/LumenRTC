param(
  [string]$Configuration = "Release",
  [string[]]$Rids = @("win-x64", "linux-x64"),
  [string]$Output = "artifacts\\nuget",
  [string]$PackageVersion,
  [string]$LibWebRtcBuildDir,
  [string]$LumenRtcNativeDir,
  [string]$WinLibWebRtcBuildDir,
  [string]$WinLumenRtcNativeDir,
  [string]$LinuxLibWebRtcBuildDir,
  [string]$LinuxLumenRtcNativeDir,
  [switch]$NoNative,
  [switch]$PackSdl
)

$ErrorActionPreference = "Stop"

if ($NoNative) {
  $packArgs = @(
    "pack",
    "src\\LumenRTC\\LumenRTC.csproj",
    "-c", $Configuration,
    "-o", $Output
  )
  if ($PackageVersion) { $packArgs += "-p:PackageVersion=$PackageVersion" }
  & dotnet @packArgs
  if ($PackSdl) {
    & dotnet pack "src\\LumenRTC.Rendering.Sdl\\LumenRTC.Rendering.Sdl.csproj" -c $Configuration -o $Output
  }
  exit $LASTEXITCODE
}

# Backward-compatible fallback for callers that pass only generic directories.
if (-not $WinLibWebRtcBuildDir -and $LibWebRtcBuildDir) { $WinLibWebRtcBuildDir = $LibWebRtcBuildDir }
if (-not $LinuxLibWebRtcBuildDir -and $LibWebRtcBuildDir) { $LinuxLibWebRtcBuildDir = $LibWebRtcBuildDir }
if (-not $WinLumenRtcNativeDir -and $LumenRtcNativeDir) { $WinLumenRtcNativeDir = $LumenRtcNativeDir }
if (-not $LinuxLumenRtcNativeDir -and $LumenRtcNativeDir) { $LinuxLumenRtcNativeDir = $LumenRtcNativeDir }

$includeWinX64 = $Rids -contains "win-x64"
$includeLinuxX64 = $Rids -contains "linux-x64"

$packArgs = @(
  "pack",
  "src\\LumenRTC\\LumenRTC.csproj",
  "-c", $Configuration,
  "-o", $Output,
  "-p:LumenRtcPackNativeMultiRid=true",
  "-p:LumenRtcPackIncludeWinX64=$($includeWinX64.ToString().ToLowerInvariant())",
  "-p:LumenRtcPackIncludeLinuxX64=$($includeLinuxX64.ToString().ToLowerInvariant())"
)

if ($PackageVersion) { $packArgs += "-p:PackageVersion=$PackageVersion" }
if ($WinLumenRtcNativeDir) { $packArgs += "-p:LumenRtcWinNativeDir=$WinLumenRtcNativeDir" }
if ($WinLibWebRtcBuildDir) { $packArgs += "-p:LibWebRtcWinBuildDir=$WinLibWebRtcBuildDir" }
if ($LinuxLumenRtcNativeDir) { $packArgs += "-p:LumenRtcLinuxNativeDir=$LinuxLumenRtcNativeDir" }
if ($LinuxLibWebRtcBuildDir) { $packArgs += "-p:LibWebRtcLinuxBuildDir=$LinuxLibWebRtcBuildDir" }

& dotnet @packArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($PackSdl) {
  $sdlArgs = @(
    "pack",
    "src\\LumenRTC.Rendering.Sdl\\LumenRTC.Rendering.Sdl.csproj",
    "-c", $Configuration,
    "-o", $Output
  )
  if ($PackageVersion) { $sdlArgs += "-p:PackageVersion=$PackageVersion" }
  & dotnet @sdlArgs
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
