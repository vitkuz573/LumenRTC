Param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  [Parameter(Mandatory = $false)]
  [ValidateSet("Release", "Debug")]
  [string]$BuildType = "Release",

  [Parameter(Mandatory = $false)]
  [string]$WinLibWebRtcBuildDir = "",

  [Parameter(Mandatory = $false)]
  [string]$LinuxLibWebRtcBuildDir = "",

  [Parameter(Mandatory = $false)]
  [string]$WinLumenRtcNativeDir = "",

  [Parameter(Mandatory = $false)]
  [string]$LinuxLumenRtcNativeDir = "",

  [Parameter(Mandatory = $false)]
  [string]$WinCMakeBuildDir = "native/build-win",

  [Parameter(Mandatory = $false)]
  [string]$LinuxCMakeBuildDir = "native/build-linux",

  [Parameter(Mandatory = $false)]
  [string]$Output = "artifacts/nuget",

  [Parameter(Mandatory = $false)]
  [string]$NuGetSource = "https://api.nuget.org/v3/index.json",

  [Parameter(Mandatory = $false)]
  [string]$NuGetApiKey = $env:NUGET_API_KEY,

  [Parameter(Mandatory = $false)]
  [string]$WslDistro = "",

  [Parameter(Mandatory = $false)]
  [switch]$SkipWindowsBuild,

  [Parameter(Mandatory = $false)]
  [switch]$SkipLinuxBuild,

  [Parameter(Mandatory = $false)]
  [switch]$SkipNuGetPush
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Escape-BashSingleQuoted {
  param([string]$Value)
  $replacement = "'" + '"' + "'" + '"' + "'"
  return ($Value -replace "'", $replacement)
}

function Test-PathContainsFile {
  param(
    [string]$Directory,
    [string[]]$Files
  )

  if ([string]::IsNullOrWhiteSpace($Directory)) { return $false }
  if (-not (Test-Path $Directory)) { return $false }

  foreach ($file in $Files) {
    if (Test-Path (Join-Path $Directory $file)) {
      return $true
    }
  }

  return $false
}

function Invoke-Wsl {
  param([string]$Command)

  $args = @()
  if (-not [string]::IsNullOrWhiteSpace($WslDistro)) {
    $args += @("-d", $WslDistro)
  }
  $args += @("bash", "-lc", $Command)

  & wsl @args
  if ($LASTEXITCODE -ne 0) {
    throw "WSL command failed (exit code $LASTEXITCODE): $Command"
  }
}

function Invoke-WslCapture {
  param([string]$Command)

  $args = @()
  if (-not [string]::IsNullOrWhiteSpace($WslDistro)) {
    $args += @("-d", $WslDistro)
  }
  $args += @("bash", "-lc", $Command)

  $output = & wsl @args
  if ($LASTEXITCODE -ne 0) {
    return $null
  }

  return ($output | Select-Object -First 1).Trim()
}

function Convert-WindowsPathToWslPath {
  param([string]$Path)

  if ([string]::IsNullOrWhiteSpace($Path)) {
    return ""
  }

  if ($Path.StartsWith("/")) {
    return $Path
  }

  $sourcePath = $Path
  if (Test-Path $Path) {
    $sourcePath = (Resolve-Path $Path).Path
  }

  $args = @()
  if (-not [string]::IsNullOrWhiteSpace($WslDistro)) {
    $args += @("-d", $WslDistro)
  }
  $args += @("wslpath", "-a", "-u", $sourcePath)

  $converted = & wsl @args
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to convert Windows path to WSL path: $Path"
  }

  return ($converted | Select-Object -First 1).Trim()
}

function Convert-WslPathToWindowsPath {
  param([string]$Path)

  if ([string]::IsNullOrWhiteSpace($Path)) {
    return ""
  }

  if ($Path -match '^[A-Za-z]:\\' -or $Path.StartsWith('\\')) {
    return $Path
  }

  $args = @()
  if (-not [string]::IsNullOrWhiteSpace($WslDistro)) {
    $args += @("-d", $WslDistro)
  }
  $args += @("wslpath", "-a", "-w", $Path)

  $converted = & wsl @args
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to convert WSL path to Windows path: $Path"
  }

  return ($converted | Select-Object -First 1).Trim()
}

function Resolve-WindowsPath {
  param(
    [string]$Path,
    [string]$BaseDirectory
  )

  if ([string]::IsNullOrWhiteSpace($Path)) {
    return ""
  }

  if ($Path.StartsWith("/")) {
    return (Convert-WslPathToWindowsPath $Path)
  }

  if ([System.IO.Path]::IsPathRooted($Path)) {
    return [System.IO.Path]::GetFullPath($Path)
  }

  return [System.IO.Path]::GetFullPath((Join-Path $BaseDirectory $Path))
}

function Resolve-WindowsLibWebRtcDir {
  param([string]$ExplicitDir)

  if (-not [string]::IsNullOrWhiteSpace($ExplicitDir)) {
    $resolved = Resolve-WindowsPath -Path $ExplicitDir -BaseDirectory $repoRoot
    if (Test-PathContainsFile -Directory $resolved -Files @("libwebrtc.dll", "libwebrtc.dll.lib", "libwebrtc.lib")) {
      return $resolved
    }
    throw "Windows libwebrtc directory does not contain expected files: $resolved"
  }

  $candidates = @(
    $env:LIBWEBRTC_BUILD_DIR,
    $env:WEBRTC_BUILD_DIR,
    $env:WEBRTC_OUT_DIR,
    $env:WEBRTC_OUT,
    (Join-Path $repoRoot "webrtc_build\src\out\Release"),
    (Join-Path $repoRoot "webrtc_build\src\out\Default"),
    (Join-Path $repoRoot "..\webrtc_build\src\out\Release"),
    (Join-Path $repoRoot "..\webrtc_build\src\out\Default")
  )

  foreach ($candidate in $candidates) {
    if (Test-PathContainsFile -Directory $candidate -Files @("libwebrtc.dll", "libwebrtc.dll.lib", "libwebrtc.lib")) {
      return (Resolve-Path $candidate).Path
    }
  }

  throw "Windows libwebrtc build directory not found. Pass -WinLibWebRtcBuildDir."
}

function Resolve-LinuxLibWebRtcDirWsl {
  param([string]$ExplicitDir)

  if (-not [string]::IsNullOrWhiteSpace($ExplicitDir)) {
    $explicitWsl = Convert-WindowsPathToWslPath $ExplicitDir
    $escaped = Escape-BashSingleQuoted $explicitWsl
    $probe = "if [ -f '$escaped/libwebrtc.so' ]; then printf '%s' '$escaped'; else exit 1; fi"
    $result = Invoke-WslCapture -Command $probe
    if ($result) {
      return $result
    }
    throw "Linux libwebrtc directory does not contain libwebrtc.so: $explicitWsl"
  }

  $repoWsl = Convert-WindowsPathToWslPath $repoRoot
  $repoWslEscaped = Escape-BashSingleQuoted $repoWsl

  $probe = @"
set -euo pipefail
repo='$repoWslEscaped'
for d in \
  "$repo/webrtc_build/src/out/Release" \
  "$repo/webrtc_build/src/out-debug/Linux-x64" \
  "$repo/../webrtc_build/src/out/Release" \
  "$repo/../webrtc_build/src/out-debug/Linux-x64" \
  "$HOME/webrtc_build/src/out/Release" \
  "$HOME/webrtc_build/src/out-debug/Linux-x64"
do
  if [ -f "$d/libwebrtc.so" ]; then
    printf '%s' "$d"
    exit 0
  fi
done
exit 1
"@

  $resolved = Invoke-WslCapture -Command $probe
  if (-not $resolved) {
    throw "Linux libwebrtc build directory not found. Pass -LinuxLibWebRtcBuildDir."
  }

  return $resolved
}

function Resolve-NativeBinaryDirectory {
  param(
    [string]$BuildDirectory,
    [string]$FileName
  )

  if ([string]::IsNullOrWhiteSpace($BuildDirectory)) {
    throw "Build directory is empty while searching for '$FileName'."
  }

  $candidates = @(
    $BuildDirectory,
    (Join-Path $BuildDirectory "Release")
  )

  foreach ($candidate in $candidates) {
    if (Test-Path (Join-Path $candidate $FileName)) {
      return $candidate
    }
  }

  throw "Native binary '$FileName' not found under: $($candidates -join ', ')"
}

function Resolve-NativeDirectoryFromInput {
  param(
    [string]$ExplicitDir,
    [string]$BuildDirectory,
    [string]$FileName,
    [string]$BaseDirectory,
    [string]$DisplayName
  )

  if (-not [string]::IsNullOrWhiteSpace($ExplicitDir)) {
    $resolvedExplicit = Resolve-WindowsPath -Path $ExplicitDir -BaseDirectory $BaseDirectory
    return Resolve-NativeBinaryDirectory -BuildDirectory $resolvedExplicit -FileName $FileName
  }

  return Resolve-NativeBinaryDirectory -BuildDirectory $BuildDirectory -FileName $FileName
}

function Assert-PackageContainsEntries {
  param(
    [string]$PackagePath,
    [string[]]$ExpectedEntries
  )

  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
  try {
    $allEntries = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) {
      [void]$allEntries.Add($entry.FullName)
    }

    foreach ($expected in $ExpectedEntries) {
      if (-not $allEntries.Contains($expected)) {
        throw "NuGet package is missing expected entry: $expected"
      }
    }
  } finally {
    $archive.Dispose()
  }
}

if ($env:OS -ne "Windows_NT") {
  throw "Run this script on Windows (PowerShell/pwsh). It orchestrates Windows build tools and WSL."
}

if (-not (Get-Command wsl -ErrorAction SilentlyContinue)) {
  throw "WSL is required. Install WSL and ensure 'wsl.exe' is available in PATH."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$scriptsDir = Join-Path $repoRoot "scripts"

$winLibWebRtcDir = Resolve-WindowsLibWebRtcDir -ExplicitDir $WinLibWebRtcBuildDir
$linuxLibWebRtcDirWsl = Resolve-LinuxLibWebRtcDirWsl -ExplicitDir $LinuxLibWebRtcBuildDir

$winCMakeBuildDirWindows = Resolve-WindowsPath -Path $WinCMakeBuildDir -BaseDirectory $repoRoot
$linuxCMakeBuildDirWindows = Resolve-WindowsPath -Path $LinuxCMakeBuildDir -BaseDirectory $repoRoot

$linuxCMakeBuildDirWslArg = if ($LinuxCMakeBuildDir.StartsWith("/")) {
  $LinuxCMakeBuildDir
} elseif ([System.IO.Path]::IsPathRooted($LinuxCMakeBuildDir)) {
  Convert-WindowsPathToWslPath $LinuxCMakeBuildDir
} else {
  $LinuxCMakeBuildDir
}

Write-Host "[release] Repo root: $repoRoot"
Write-Host "[release] Version: $Version"
Write-Host "[release] Windows libwebrtc: $winLibWebRtcDir"
Write-Host "[release] Linux libwebrtc (WSL): $linuxLibWebRtcDirWsl"
Write-Host "[release] Output directory: $Output"

Push-Location $repoRoot
try {
  if (-not $SkipWindowsBuild) {
    Write-Host "[release] Building Windows native..."
    & (Join-Path $scriptsDir "bootstrap.ps1") `
      -LibWebRtcBuildDir $winLibWebRtcDir `
      -CMakeBuildDir $WinCMakeBuildDir `
      -BuildType $BuildType

    if ($LASTEXITCODE -ne 0) {
      throw "Windows native build failed."
    }
  } else {
    Write-Host "[release] Skipping Windows native build."
  }

  if (-not $SkipLinuxBuild) {
    Write-Host "[release] Building Linux native via WSL..."
    $repoWsl = Convert-WindowsPathToWslPath $repoRoot
    $repoWslEscaped = Escape-BashSingleQuoted $repoWsl
    $linuxLibWebRtcEscaped = Escape-BashSingleQuoted $linuxLibWebRtcDirWsl
    $linuxBuildDirEscaped = Escape-BashSingleQuoted $linuxCMakeBuildDirWslArg
    $buildTypeEscaped = Escape-BashSingleQuoted $BuildType

    $linuxCommand = "set -euo pipefail; cd '$repoWslEscaped'; ./scripts/bootstrap.sh --libwebrtc-build-dir '$linuxLibWebRtcEscaped' --cmake-build-dir '$linuxBuildDirEscaped' --build-type '$buildTypeEscaped'"
    Invoke-Wsl -Command $linuxCommand
  } else {
    Write-Host "[release] Skipping Linux native build."
  }

  $winNativeDir = Resolve-NativeDirectoryFromInput `
    -ExplicitDir $WinLumenRtcNativeDir `
    -BuildDirectory $winCMakeBuildDirWindows `
    -FileName "lumenrtc.dll" `
    -BaseDirectory $repoRoot `
    -DisplayName "Windows native"

  $linuxNativeDir = Resolve-NativeDirectoryFromInput `
    -ExplicitDir $LinuxLumenRtcNativeDir `
    -BuildDirectory $linuxCMakeBuildDirWindows `
    -FileName "liblumenrtc.so" `
    -BaseDirectory $repoRoot `
    -DisplayName "Linux native"

  $linuxLibWebRtcDirWindows = Convert-WslPathToWindowsPath $linuxLibWebRtcDirWsl
  $outputDirWindows = Resolve-WindowsPath -Path $Output -BaseDirectory $repoRoot

  Write-Host "[release] Windows native dir: $winNativeDir"
  Write-Host "[release] Linux native dir: $linuxNativeDir"
  Write-Host "[release] Linux libwebrtc (Windows path): $linuxLibWebRtcDirWindows"

  Write-Host "[release] Packing NuGet (multi-RID)..."
  & (Join-Path $scriptsDir "pack-all.ps1") `
    -Configuration $BuildType `
    -Rids @("win-x64", "linux-x64") `
    -Output $outputDirWindows `
    -PackageVersion $Version `
    -WinLibWebRtcBuildDir $winLibWebRtcDir `
    -WinLumenRtcNativeDir $winNativeDir `
    -LinuxLibWebRtcBuildDir $linuxLibWebRtcDirWindows `
    -LinuxLumenRtcNativeDir $linuxNativeDir

  if ($LASTEXITCODE -ne 0) {
    throw "NuGet pack failed."
  }
} finally {
  Pop-Location
}

$outputDirWindows = Resolve-WindowsPath -Path $Output -BaseDirectory $repoRoot
$nupkgPath = Join-Path $outputDirWindows "LumenRTC.$Version.nupkg"
if (-not (Test-Path $nupkgPath)) {
  throw "Expected package was not created: $nupkgPath"
}

Assert-PackageContainsEntries -PackagePath $nupkgPath -ExpectedEntries @(
  "runtimes/win-x64/native/lumenrtc_native.dll"
  "runtimes/win-x64/native/libwebrtc.dll"
  "runtimes/linux-x64/native/liblumenrtc.so"
  "runtimes/linux-x64/native/libwebrtc.so"
)

Write-Host "[release] Package created: $nupkgPath"
Write-Host "[release] Verified multi-RID native entries in package."

if ($SkipNuGetPush) {
  Write-Host "[release] Skipping NuGet push."
  exit 0
}

if ([string]::IsNullOrWhiteSpace($NuGetApiKey)) {
  throw "NuGet API key is required. Set -NuGetApiKey or NUGET_API_KEY."
}

Write-Host "[release] Pushing to NuGet..."
dotnet nuget push $nupkgPath `
  --source $NuGetSource `
  --api-key $NuGetApiKey `
  --skip-duplicate

if ($LASTEXITCODE -ne 0) {
  throw "NuGet push failed."
}

Write-Host "[release] Done."
