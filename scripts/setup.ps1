Param(
  [Parameter(Mandatory = $false)]
  [string]$DepotToolsDir = $env:DEPOT_TOOLS,

  [Parameter(Mandatory = $false)]
  [string]$WebRtcRoot = "",

  [Parameter(Mandatory = $false)]
  [string]$RepoRoot = "",

  [Parameter(Mandatory = $false)]
  [string]$WebRtcBranch = "branch-heads/7151",

  [Parameter(Mandatory = $false)]
  [ValidateSet("x64", "x86", "arm", "arm64")]
  [string]$TargetCpu = "x64",

  [Parameter(Mandatory = $false)]
  [ValidateSet("Release", "Debug")]
  [string]$BuildType = "Release",

  [Parameter(Mandatory = $false)]
  [ValidateSet("ON", "OFF")]
  [string]$DesktopCapture = "ON",

  [Parameter(Mandatory = $false)]
  [switch]$SkipSync,

  [Parameter(Mandatory = $false)]
  [switch]$SkipBootstrap
)

$ErrorActionPreference = "Stop"

function Join-PathSafe {
  param(
    [string]$BasePath,
    [string]$ChildPath
  )

  if ([string]::IsNullOrWhiteSpace($BasePath)) {
    return $ChildPath
  }
  if ([string]::IsNullOrWhiteSpace($ChildPath)) {
    return $BasePath
  }
  return (Join-Path $BasePath $ChildPath)
}

$repoRoot = $RepoRoot
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
  $repoRoot = $PSCommandPath
  if (-not [string]::IsNullOrWhiteSpace($repoRoot)) {
    $repoRoot = Split-Path -Parent $repoRoot
  }
}
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
  $repoRoot = $PWD.ProviderPath
}
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
  $repoRoot = (Get-Location).ProviderPath
}
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
  $repoRoot = [Environment]::CurrentDirectory
}
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
  $repoRoot = "."
}

$scriptRoot = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) { $PSScriptRoot } else { Join-PathSafe $repoRoot "scripts" }
$lumenRoot = if (-not [string]::IsNullOrWhiteSpace($scriptRoot)) { Split-Path -Parent $scriptRoot } else { $repoRoot }

function Require-Command {
  param([string]$Name)
  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    throw "Missing '$Name' in PATH. Install depot_tools (for gclient/gn/ninja) and ensure it is on PATH."
  }
}

function Resolve-VisualStudio {
  $result = [ordered]@{
    Path = ""
    Version = ""
  }

  function Get-VersionFromPath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) {
      return ""
    }
    if ($Path -match '\\\\(20\\d{2})\\\\') {
      return $Matches[1]
    }
    return ""
  }

  function Try-ParseVswhereJson {
    param([string]$Json)
    if ([string]::IsNullOrWhiteSpace($Json)) {
      return $null
    }
    try {
      return ($Json | ConvertFrom-Json)
    } catch {
      return $null
    }
  }

  function Test-VisualStudioPath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) {
      return $false
    }
    if (-not (Test-Path $Path)) {
      return $false
    }
    $vcTools = Join-PathSafe $Path "VC\\Tools\\MSVC"
    return (Test-Path $vcTools)
  }

  if (-not [string]::IsNullOrWhiteSpace($env:GYP_MSVS_OVERRIDE_PATH)) {
    $candidate = $env:GYP_MSVS_OVERRIDE_PATH.TrimEnd('\\')
    if (Test-VisualStudioPath -Path $candidate) {
      $result.Path = $candidate
      $result.Version = Get-VersionFromPath -Path $candidate
    }
  }
  if (-not [string]::IsNullOrWhiteSpace($env:LUMENRTC_VS_YEAR)) {
    $result.Version = $env:LUMENRTC_VS_YEAR.Trim()
  }
  if (-not [string]::IsNullOrWhiteSpace($result.Path)) {
    return $result
  }

  if (-not [string]::IsNullOrWhiteSpace($env:VSINSTALLDIR)) {
    $candidate = $env:VSINSTALLDIR.TrimEnd('\\')
    if (Test-VisualStudioPath -Path $candidate) {
      $result.Path = $candidate
      $result.Version = Get-VersionFromPath -Path $candidate
      return $result
    }
  }

  $vswherePath = $null
  if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
    $vswherePath = Join-PathSafe ${env:ProgramFiles(x86)} "Microsoft Visual Studio\\Installer\\vswhere.exe"
  }
  if (-not (Test-Path $vswherePath) -and -not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    $vswherePath = Join-PathSafe $env:ProgramFiles "Microsoft Visual Studio\\Installer\\vswhere.exe"
  }

  if (Test-Path $vswherePath) {
    $vswhereJson = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json 2>$null
    $jsonData = Try-ParseVswhereJson -Json $vswhereJson
    if ($null -ne $jsonData) {
      $entry = $jsonData
      if ($entry -is [array]) {
        $entry = $entry | Select-Object -First 1
      }

      if ($null -ne $entry.installationPath) {
        $candidate = [string]$entry.installationPath
        if (Test-VisualStudioPath -Path $candidate) {
          $result.Path = $candidate.Trim()
        }
      }

      $versionCandidates = @()
      if ($null -ne $entry.catalog) {
        $versionCandidates += [string]$entry.catalog.productLineVersion
        $versionCandidates += [string]$entry.catalog.productDisplayVersion
        $versionCandidates += [string]$entry.catalog.productLine
        $versionCandidates += [string]$entry.catalog.productSemanticVersion
        $versionCandidates += [string]$entry.catalog.buildVersion
      }
      $versionCandidates += [string]$entry.installationVersion

      foreach ($candidate in $versionCandidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
          continue
        }
        if ($candidate -match '2026') {
          $result.Version = "2026"
          break
        }
      }

      if ([string]::IsNullOrWhiteSpace($result.Version)) {
        $installVersion = [string]$entry.installationVersion
        if ($installVersion -match '^18\.') {
          $result.Version = "2026"
        } elseif ($installVersion -match '^17\.') {
          $result.Version = "2022"
        } elseif ($installVersion -match '^16\.') {
          $result.Version = "2019"
        } elseif ($installVersion -match '^15\.') {
          $result.Version = "2017"
        }
      }
    }

    $installPath = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installPath)) {
      $candidate = $installPath.Trim()
      if (Test-VisualStudioPath -Path $candidate) {
        $result.Path = $candidate
      }
    }

    if ([string]::IsNullOrWhiteSpace($result.Version)) {
      $lineVersion = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property catalog_productLineVersion 2>$null
      if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($lineVersion)) {
        if ($lineVersion -match '2026') {
          $result.Version = "2026"
        }
      }

      $displayVersion = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property catalog_productDisplayVersion 2>$null
      if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($displayVersion)) {
        if ($displayVersion -match '2026') {
          $result.Version = "2026"
        }
      }

      $installVersion = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationVersion 2>$null
      if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installVersion)) {
        if ($installVersion -match '^18\.') {
          $result.Version = "2026"
        } elseif ($installVersion -match '^17\.') {
          $result.Version = "2022"
        } elseif ($installVersion -match '^16\.') {
          $result.Version = "2019"
        } elseif ($installVersion -match '^15\.') {
          $result.Version = "2017"
        }
      }
    }
  }

  return $result
}

function Ensure-GclientConfig {
  param(
    [string]$Root,
    [string]$Branch
  )

  $gclientPath = Join-PathSafe $Root ".gclient"
  $webrtcUrl = "https://webrtc.googlesource.com/src.git"
  if (-not [string]::IsNullOrWhiteSpace($Branch)) {
    $webrtcUrl = "$webrtcUrl@$Branch"
  }

  $content = @"
solutions = [
  {
    "name"        : 'src',
    "url"         : '$webrtcUrl',
    "deps_file"   : 'DEPS',
    "managed"     : False,
    "custom_deps" : {},
    "custom_vars": {},
  },
]

target_os = ['win']
"@

  Set-Content -Path $gclientPath -Value $content -Encoding ASCII
}

function Sync-LibWebRtcWrapper {
  param(
    [string]$SrcDir,
    [string]$WrapperSourceDir
  )

  if ([string]::IsNullOrWhiteSpace($WrapperSourceDir) -or -not (Test-Path $WrapperSourceDir)) {
    throw "Wrapper source directory not found: $WrapperSourceDir"
  }
  if (-not (Test-Path (Join-PathSafe $WrapperSourceDir "include"))) {
    throw "Wrapper source is invalid (missing include/): $WrapperSourceDir"
  }

  $libWebRtcDir = Join-PathSafe $SrcDir "libwebrtc"
  if (Test-Path $libWebRtcDir) {
    Remove-Item -LiteralPath $libWebRtcDir -Recurse -Force
  }
  New-Item -ItemType Directory -Force -Path $libWebRtcDir | Out-Null

  $exclude = @(".git", "build", "out", ".conan")
  Get-ChildItem -LiteralPath $WrapperSourceDir -Force | ForEach-Object {
    if ($exclude -contains $_.Name) {
      return
    }
    Copy-Item -LiteralPath $_.FullName -Destination $libWebRtcDir -Recurse -Force
  }

  return $libWebRtcDir
}

function Ensure-BuildGnIncludesLibWebRtc {
  param([string]$BuildGnPath)

  if (-not (Test-Path $BuildGnPath)) {
    Write-Warning "BUILD.gn not found: $BuildGnPath"
    return
  }

  $content = Get-Content $BuildGnPath -Raw
  if ($content -match "//libwebrtc") {
    return
  }

  $pattern = 'deps\s*=\s*\[\s*":webrtc"\s*\]'
  if ($content -match $pattern) {
    $updated = [regex]::Replace($content, $pattern, 'deps = [ ":webrtc", "//libwebrtc", ]', 1)
    Set-Content -Path $BuildGnPath -Value $updated
    Write-Host "Updated BUILD.gn to include //libwebrtc in default group."
  } else {
    Write-Warning "Could not auto-update BUILD.gn. Please add //libwebrtc to group(\"default\")."
  }
}

function Find-DepotTools {
  param([string[]]$Candidates)
  foreach ($dir in $Candidates) {
    if ([string]::IsNullOrWhiteSpace($dir)) { continue }
    $gclient = Join-PathSafe $dir "gclient.bat"
    if (Test-Path $gclient) { return $dir }
    $gclient = Join-PathSafe $dir "gclient"
    if (Test-Path $gclient) { return $dir }
  }
  return $null
}

function Ensure-DepotTools {
  param([string]$PreferredDir)

  $candidates = @()
  if (-not [string]::IsNullOrWhiteSpace($PreferredDir)) { $candidates += $PreferredDir }
  if (-not [string]::IsNullOrWhiteSpace($env:DEPOT_TOOLS)) { $candidates += $env:DEPOT_TOOLS }

  $candidates += (Join-PathSafe $repoRoot "depot_tools")
  $candidates += (Join-PathSafe $repoRoot "..\\depot_tools")
  if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
    $candidates += (Join-PathSafe $env:USERPROFILE "depot_tools")
  }

  $found = Find-DepotTools -Candidates $candidates
  if ($found) { return $found }

  $target = $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
  if (-not $target) {
    $target = Join-PathSafe $repoRoot "depot_tools"
  }

  if (-not (Test-Path $target)) {
    New-Item -ItemType Directory -Force -Path $target | Out-Null
  }

  Write-Host "Cloning depot_tools into $target"
  git clone https://chromium.googlesource.com/chromium/tools/depot_tools.git $target
  return $target
}

Require-Command git

$resolvedDepotTools = Ensure-DepotTools -PreferredDir $DepotToolsDir
if (-not [string]::IsNullOrWhiteSpace($resolvedDepotTools)) {
  $env:DEPOT_TOOLS = $resolvedDepotTools
  if (-not $env:PATH.Contains($resolvedDepotTools)) {
    $env:PATH = "$resolvedDepotTools;$env:PATH"
  }
}

Require-Command gclient
Require-Command gn
Require-Command ninja

if ([string]::IsNullOrWhiteSpace($env:DEPOT_TOOLS_WIN_TOOLCHAIN)) {
  $env:DEPOT_TOOLS_WIN_TOOLCHAIN = "0"
}
$vsInfo = Resolve-VisualStudio
if ([string]::IsNullOrWhiteSpace($env:GYP_MSVS_VERSION)) {
  $env:GYP_MSVS_VERSION = "2022"
}
if ([string]::IsNullOrWhiteSpace($env:GYP_MSVS_OVERRIDE_PATH) -and -not [string]::IsNullOrWhiteSpace($vsInfo.Path)) {
  $env:GYP_MSVS_OVERRIDE_PATH = $vsInfo.Path
}
if ([string]::IsNullOrWhiteSpace($env:GYP_MSVS_OVERRIDE_PATH)) {
  throw "Visual Studio 2026 with C++ build tools not found. Install VS 2026 (Desktop development with C++) or set GYP_MSVS_OVERRIDE_PATH."
}
$vsPathCheck = $env:GYP_MSVS_OVERRIDE_PATH.TrimEnd('\\')
$detectedVersion = $vsInfo.Version
if ([string]::IsNullOrWhiteSpace($detectedVersion)) {
  if ($vsPathCheck -match '\\\\(20\\d{2})\\\\') {
    $detectedVersion = $Matches[1]
  } else {
    $detectedVersion = "unknown"
  }
}
if ($detectedVersion -ne "2026") {
  throw "Only Visual Studio 2026 is supported. Detected: $detectedVersion. Set LUMENRTC_VS_YEAR=2026 if detection fails."
}
if (-not (Test-Path $vsPathCheck)) {
  throw "GYP_MSVS_OVERRIDE_PATH points to a missing folder: $vsPathCheck"
}
$vsToolchainDir = Join-PathSafe $vsPathCheck "VC\\Tools\\MSVC"
if (-not (Test-Path $vsToolchainDir)) {
  throw "GYP_MSVS_OVERRIDE_PATH does not contain VC\\Tools\\MSVC. Install C++ build tools or fix the path."
}
if ([string]::IsNullOrWhiteSpace($env:vs2022_install)) {
  $env:vs2022_install = $vsPathCheck
}

if ([string]::IsNullOrWhiteSpace($WebRtcRoot)) {
  if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    $repoRoot = "."
  }
  $WebRtcRoot = [System.IO.Path]::Combine($repoRoot, "webrtc_build")
  if ([string]::IsNullOrWhiteSpace($WebRtcRoot)) {
    $WebRtcRoot = "webrtc_build"
  }
}

if ($env:LUMENRTC_SETUP_DEBUG -eq "1") {
  Write-Host "setup.ps1 debug:"
  Write-Host "  RepoRoot: $repoRoot"
  Write-Host "  ScriptRoot: $scriptRoot"
  Write-Host "  WebRtcRoot: $WebRtcRoot"
  Write-Host "  DepotToolsDir: $DepotToolsDir"
}

if ([string]::IsNullOrWhiteSpace($WebRtcRoot)) {
  $WebRtcRoot = "webrtc_build"
}

$webRtcRoot = Resolve-Path -LiteralPath $WebRtcRoot -ErrorAction SilentlyContinue
if (-not $webRtcRoot) {
  $WebRtcRoot = ($WebRtcRoot ?? "").Trim()
  if ([string]::IsNullOrWhiteSpace($WebRtcRoot)) {
    $WebRtcRoot = "webrtc_build"
  }
  New-Item -ItemType Directory -Force -Path $WebRtcRoot | Out-Null
  $webRtcRoot = Resolve-Path -LiteralPath $WebRtcRoot
}

Ensure-GclientConfig -Root $webRtcRoot -Branch $WebRtcBranch

Push-Location $webRtcRoot
try {
  if (-not $SkipSync) {
    gclient sync
    if ($LASTEXITCODE -ne 0) {
      throw "gclient sync failed. Check depot_tools and network access."
    }
  }
}
finally {
  Pop-Location
}

$srcDirRoot = Join-PathSafe $webRtcRoot "src"
if (-not (Test-Path $srcDirRoot)) {
  throw "Expected src directory at $srcDirRoot. gclient sync may have failed."
}

$wrapperSourceDir = Join-PathSafe $lumenRoot "vendor\\libwebrtc"
$libWebRtcDir = Sync-LibWebRtcWrapper -SrcDir $srcDirRoot -WrapperSourceDir $wrapperSourceDir

Push-Location $srcDirRoot
try {
  $buildGnPath = Join-PathSafe $srcDirRoot "BUILD.gn"
  Ensure-BuildGnIncludesLibWebRtc -BuildGnPath $buildGnPath

  $outDir = Join-PathSafe $srcDirRoot "out\\$BuildType"
  $isDebug = if ($BuildType -eq "Debug") { "true" } else { "false" }
  $desktopCaptureFlag = if ($DesktopCapture -eq "ON") { "true" } else { "false" }

  New-Item -ItemType Directory -Force -Path $outDir | Out-Null
  $argsGnPath = Join-PathSafe $outDir "args.gn"
  $argsContent = @(
    'target_os = "win"',
    ('target_cpu = "{0}"' -f $TargetCpu),
    "is_component_build = false",
    "is_clang = true",
    "is_debug = $isDebug",
    "rtc_use_h264 = true",
    'ffmpeg_branding = "Chrome"',
    "rtc_include_tests = false",
    "rtc_build_examples = false",
    "libwebrtc_desktop_capture = $desktopCaptureFlag"
  ) -join "`n"

  Set-Content -Path $argsGnPath -Value $argsContent -Encoding ASCII
  gn gen $outDir
  if ($LASTEXITCODE -ne 0) {
    throw "gn gen failed. Ensure Visual Studio with C++ build tools is installed and GYP_MSVS_OVERRIDE_PATH is set."
  }
  ninja -C $outDir libwebrtc
  if ($LASTEXITCODE -ne 0) {
    throw "ninja build failed. See the errors above for details."
  }

  if (-not $SkipBootstrap) {
    $env:LIBWEBRTC_ROOT = $libWebRtcDir
    $env:LIBWEBRTC_BUILD_DIR = $outDir

    $bootstrapScript = Join-PathSafe $scriptRoot "bootstrap.ps1"
    Push-Location $lumenRoot
    try {
      & $bootstrapScript -LibWebRtcBuildDir $outDir -BuildType $BuildType -DesktopCapture $DesktopCapture
    }
    finally {
      Pop-Location
    }
  }
}
finally {
  Pop-Location
}

if ($scriptRoot -is [System.Management.Automation.PathInfo]) {
  $scriptRoot = $scriptRoot.Path
}

$nativeDefault = Join-PathSafe $scriptRoot "..\\native\\build\\$BuildType"
if (-not (Test-Path $nativeDefault)) {
  $nativeDefault = Join-PathSafe $scriptRoot "..\\native\\build"
}

Write-Host ""
Write-Host "Next steps:"
if ($srcDirRoot -is [System.Management.Automation.PathInfo]) {
  $srcDirRoot = $srcDirRoot.Path
}
$libWebRtcOutDir = Join-PathSafe $srcDirRoot "out\\$BuildType"
if (Test-Path $libWebRtcOutDir) {
  $libWebRtcOutDir = (Resolve-Path -LiteralPath $libWebRtcOutDir).Path
}
if (Test-Path $nativeDefault) {
  $nativeDefault = (Resolve-Path -LiteralPath $nativeDefault).Path
}
Write-Host ('  $env:LIBWEBRTC_BUILD_DIR="{0}"' -f $libWebRtcOutDir)
Write-Host ('  $env:LumenRtcNativeDir="{0}"' -f $nativeDefault)
Write-Host "  dotnet run --project .\\samples\\LumenRTC.Sample.LocalCamera.Convenience\\LumenRTC.Sample.LocalCamera.Convenience.csproj"
