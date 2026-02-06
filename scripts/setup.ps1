Param(
  [Parameter(Mandatory = $false)]
  [string]$DepotToolsDir = $env:DEPOT_TOOLS,

  [Parameter(Mandatory = $false)]
  [string]$WebRtcRoot = (Join-Path $PSScriptRoot "..\\webrtc_build"),

  [Parameter(Mandatory = $false)]
  [ValidateSet("m137_release")]
  [string]$WebRtcBranch = "m137_release",

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
  [switch]$SkipPatch,

  [Parameter(Mandatory = $false)]
  [switch]$SkipBootstrap
)

$ErrorActionPreference = "Stop"

function Require-Command {
  param([string]$Name)
  if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
    throw "Missing '$Name' in PATH. Install depot_tools (for gclient/gn/ninja) and ensure it is on PATH."
  }
}

function Ensure-GclientConfig {
  param(
    [string]$Root,
    [string]$Branch
  )

  $gclientPath = Join-Path $Root ".gclient"
  if (Test-Path $gclientPath) {
    return
  }

  $content = @'
solutions = [
  {
    "name"        : 'src',
    "url"         : 'https://github.com/webrtc-sdk/webrtc.git@m137_release',
    "deps_file"   : 'DEPS',
    "managed"     : False,
    "custom_deps" : {},
    "custom_vars": {},
  },
]

target_os = ['win']
'@

  $content = $content.Replace("m137_release", $Branch)
  Set-Content -Path $gclientPath -Value $content
}

function Ensure-LibWebRtcRepo {
  param([string]$SrcDir)

  $libWebRtcDir = Join-Path $SrcDir "libwebrtc"
  if (-not (Test-Path $libWebRtcDir)) {
    git clone https://github.com/webrtc-sdk/libwebrtc $libWebRtcDir
  }
  return $libWebRtcDir
}

function Apply-CustomPatch {
  param([string]$LibWebRtcDir)

  $patchPath = Join-Path $LibWebRtcDir "patchs\\custom_audio_source_m137.patch"
  if (-not (Test-Path $patchPath)) {
    Write-Warning "Patch not found: $patchPath"
    return
  }

  & git apply --check $patchPath 2>$null
  if ($LASTEXITCODE -eq 0) {
    git apply $patchPath
    Write-Host "Applied patch: custom_audio_source_m137.patch"
  } else {
    Write-Host "Patch already applied or not applicable; skipping."
  }
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
    $gclient = Join-Path $dir "gclient.bat"
    if (Test-Path $gclient) { return $dir }
    $gclient = Join-Path $dir "gclient"
    if (Test-Path $gclient) { return $dir }
  }
  return $null
}

function Ensure-DepotTools {
  param([string]$PreferredDir)

  $candidates = @()
  if (-not [string]::IsNullOrWhiteSpace($PreferredDir)) { $candidates += $PreferredDir }
  if (-not [string]::IsNullOrWhiteSpace($env:DEPOT_TOOLS)) { $candidates += $env:DEPOT_TOOLS }

  $candidates += @(
    (Join-Path $PSScriptRoot "..\\depot_tools"),
    (Join-Path $PSScriptRoot "..\\..\\depot_tools"),
    (Join-Path $env:USERPROFILE "depot_tools")
  )

  $found = Find-DepotTools -Candidates $candidates
  if ($found) { return $found }

  $target = $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
  if (-not $target) {
    $target = Join-Path $PSScriptRoot "..\\depot_tools"
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
if ([string]::IsNullOrWhiteSpace($env:GYP_MSVS_VERSION)) {
  $env:GYP_MSVS_VERSION = "2022"
}
if ([string]::IsNullOrWhiteSpace($env:GYP_MSVS_OVERRIDE_PATH) -and -not [string]::IsNullOrWhiteSpace($env:VSINSTALLDIR)) {
  $env:GYP_MSVS_OVERRIDE_PATH = $env:VSINSTALLDIR.TrimEnd('\\')
}

$webRtcRoot = Resolve-Path -LiteralPath $WebRtcRoot -ErrorAction SilentlyContinue
if (-not $webRtcRoot) {
  New-Item -ItemType Directory -Force -Path $WebRtcRoot | Out-Null
  $webRtcRoot = Resolve-Path -LiteralPath $WebRtcRoot
}

Ensure-GclientConfig -Root $webRtcRoot -Branch $WebRtcBranch

Push-Location $webRtcRoot
try {
  if (-not $SkipSync) {
    gclient sync
  }

  $srcDir = Join-Path $webRtcRoot "src"
  if (-not (Test-Path $srcDir)) {
    throw "Expected src directory at $srcDir. gclient sync may have failed."
  }

  $libWebRtcDir = Ensure-LibWebRtcRepo -SrcDir $srcDir
  if (-not $SkipPatch) {
    Apply-CustomPatch -LibWebRtcDir $libWebRtcDir
  }

  $buildGnPath = Join-Path $srcDir "BUILD.gn"
  Ensure-BuildGnIncludesLibWebRtc -BuildGnPath $buildGnPath

  $outDir = Join-Path $srcDir "out\\$BuildType"
  $isDebug = if ($BuildType -eq "Debug") { "true" } else { "false" }
  $desktopCaptureFlag = if ($DesktopCapture -eq "ON") { "true" } else { "false" }

  $gnArgs = @(
    'target_os="win"',
    "target_cpu=`"$TargetCpu`"",
    "is_component_build=false",
    "is_clang=true",
    "is_debug=$isDebug",
    "rtc_use_h264=true",
    'ffmpeg_branding="Chrome"',
    "rtc_include_tests=false",
    "rtc_build_examples=false",
    "libwebrtc_desktop_capture=$desktopCaptureFlag"
  ) -join " "

  gn gen $outDir --args="$gnArgs"
  ninja -C $outDir libwebrtc

  if (-not $SkipBootstrap) {
    $env:LIBWEBRTC_ROOT = $libWebRtcDir
    $env:LIBWEBRTC_BUILD_DIR = $outDir

    $bootstrapScript = Join-Path $PSScriptRoot "bootstrap.ps1"
    & $bootstrapScript -LibWebRtcBuildDir $outDir -BuildType $BuildType -DesktopCapture $DesktopCapture
  }
}
finally {
  Pop-Location
}

$nativeDefault = Join-Path $PSScriptRoot "..\\native\\build\\$BuildType"
if (-not (Test-Path $nativeDefault)) {
  $nativeDefault = Join-Path $PSScriptRoot "..\\native\\build"
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  `$env:LIBWEBRTC_BUILD_DIR=\"$($webRtcRoot)\\src\\out\\$BuildType\""
Write-Host "  `$env:LumenRtcNativeDir=\"$nativeDefault\""
Write-Host "  dotnet run --project .\\samples\\LumenRTC.Sample.LocalCamera\\LumenRTC.Sample.LocalCamera.csproj"
