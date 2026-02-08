param(
    [Parameter(Position = 0)]
    [ValidateSet("snapshot", "baseline", "baseline-all", "regen", "regen-baselines", "doctor", "generate", "roslyn", "sync", "release-prepare", "changelog", "verify", "check", "verify-all", "check-all", "list-targets", "init-target", "diff")]
    [string]$Command = "check",

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$scriptDir = Split-Path -Path $PSCommandPath -Parent
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

$python = if ($env:PYTHON_BIN) {
    $env:PYTHON_BIN
} elseif (Get-Command python3 -ErrorAction SilentlyContinue) {
    "python3"
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    "python"
} elseif (Get-Command py -ErrorAction SilentlyContinue) {
    "py"
} else {
    throw "Python interpreter was not found. Set PYTHON_BIN or install python3/python."
}

$config = if ($env:ABI_CONFIG) { $env:ABI_CONFIG } else { Join-Path $repoRoot "abi/config.json" }
$target = if ($env:ABI_TARGET) { $env:ABI_TARGET } else { "lumenrtc" }
$baseline = if ($env:ABI_BASELINE) { $env:ABI_BASELINE } else { Join-Path $repoRoot "abi/baselines/lumenrtc.json" }
$baselineRoot = if ($env:ABI_BASELINE_ROOT) { $env:ABI_BASELINE_ROOT } else { Join-Path $repoRoot "abi/baselines" }
$guard = Join-Path $repoRoot "tools/abi_framework/abi_framework.py"
$dotnet = if ($env:DOTNET_BIN) { $env:DOTNET_BIN } else { "dotnet" }
$roslynProject = if ($env:ABI_ROSLYN_PROJECT) { $env:ABI_ROSLYN_PROJECT } else { Join-Path $repoRoot "tools/lumenrtc_roslyn_codegen/LumenRTC.Abi.RoslynGenerator.csproj" }
$idlPath = if ($env:ABI_IDL) { $env:ABI_IDL } else { Join-Path $repoRoot "abi/generated/lumenrtc/lumenrtc.idl.json" }
$roslynOutput = if ($env:ABI_ROSLYN_OUTPUT) { $env:ABI_ROSLYN_OUTPUT } else { Join-Path $repoRoot "abi/generated/lumenrtc/NativeMethods.g.cs" }
$roslynNamespace = if ($env:ABI_ROSLYN_NAMESPACE) { $env:ABI_ROSLYN_NAMESPACE } else { "LumenRTC.Interop" }
$roslynClassName = if ($env:ABI_ROSLYN_CLASS_NAME) { $env:ABI_ROSLYN_CLASS_NAME } else { "NativeMethods" }
$roslynAccessModifier = if ($env:ABI_ROSLYN_ACCESS_MODIFIER) { $env:ABI_ROSLYN_ACCESS_MODIFIER } else { "internal" }
$roslynCallingConvention = if ($env:ABI_ROSLYN_CALLING_CONVENTION) { $env:ABI_ROSLYN_CALLING_CONVENTION } else { "Cdecl" }
$roslynLibraryExpression = if ($env:ABI_ROSLYN_LIBRARY_EXPRESSION) { $env:ABI_ROSLYN_LIBRARY_EXPRESSION } else { "LibName" }

function Resolve-RepoPath {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Value)) {
        return $Value
    }

    return Join-Path $repoRoot $Value
}

if (Test-Path $config) {
    try {
        $parsedConfig = Get-Content -Raw -Path $config | ConvertFrom-Json
        $targetConfigProperty = $parsedConfig.targets.PSObject.Properties[$target]
        $targetConfig = if ($targetConfigProperty) { $targetConfigProperty.Value } else { $null }
        $roslynConfig = if ($targetConfig -and $targetConfig.bindings -and $targetConfig.bindings.csharp) { $targetConfig.bindings.csharp } else { $null }

        if ($roslynConfig) {
            if (-not $env:ABI_IDL -and $roslynConfig.idl_path) {
                $resolved = Resolve-RepoPath -Value $roslynConfig.idl_path
                if ($resolved) { $idlPath = $resolved }
            }
            if (-not $env:ABI_ROSLYN_OUTPUT -and $roslynConfig.output_path) {
                $resolved = Resolve-RepoPath -Value $roslynConfig.output_path
                if ($resolved) { $roslynOutput = $resolved }
            }
            if (-not $env:ABI_ROSLYN_NAMESPACE -and $roslynConfig.namespace) {
                $roslynNamespace = $roslynConfig.namespace
            }
            if (-not $env:ABI_ROSLYN_CLASS_NAME -and $roslynConfig.class_name) {
                $roslynClassName = $roslynConfig.class_name
            }
            if (-not $env:ABI_ROSLYN_ACCESS_MODIFIER -and $roslynConfig.access_modifier) {
                $roslynAccessModifier = $roslynConfig.access_modifier
            }
            if (-not $env:ABI_ROSLYN_CALLING_CONVENTION -and $roslynConfig.calling_convention) {
                $roslynCallingConvention = $roslynConfig.calling_convention
            }
            if (-not $env:ABI_ROSLYN_LIBRARY_EXPRESSION -and $roslynConfig.library_expression) {
                $roslynLibraryExpression = $roslynConfig.library_expression
            }
        }
    }
    catch {
        # Ignore config parse issues here; abi_framework command will report config problems in its own flow.
    }
}
$extraArgs = @()
if ($Arguments) {
    foreach ($arg in $Arguments) {
        if (-not [string]::IsNullOrWhiteSpace($arg)) {
            $extraArgs += $arg
        }
    }
}

function Invoke-Guard {
    param([string[]]$GuardArgs)

    if ($python -eq "py") {
        & $python -3 $guard @GuardArgs
    } else {
        & $python $guard @GuardArgs
    }

    if ($LASTEXITCODE -ne 0) {
        throw "abi_framework failed with exit code $LASTEXITCODE."
    }
}

function Invoke-GuardCapture {
    param([string[]]$GuardArgs)

    if ($python -eq "py") {
        $output = & $python -3 $guard @GuardArgs
    } else {
        $output = & $python $guard @GuardArgs
    }

    if ($LASTEXITCODE -ne 0) {
        throw "abi_framework failed with exit code $LASTEXITCODE."
    }

    return $output
}

function Invoke-Roslyn {
    param([string[]]$RoslynArgs)

    $baseArgs = @(
        "run",
        "--project", $roslynProject,
        "--",
        "--idl", $idlPath,
        "--output", $roslynOutput,
        "--namespace", $roslynNamespace,
        "--class-name", $roslynClassName,
        "--access-modifier", $roslynAccessModifier,
        "--calling-convention", $roslynCallingConvention,
        "--library-expression", $roslynLibraryExpression
    ) + $RoslynArgs

    & $dotnet @baseArgs
    if ($LASTEXITCODE -ne 0) {
        throw "lumenrtc_roslyn_codegen failed with exit code $LASTEXITCODE."
    }
}

switch ($Command) {
    "snapshot" {
        $guardArgs = @("snapshot", "--repo-root", $repoRoot, "--config", $config, "--target", $target) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "baseline" {
        $guardArgs = @("snapshot", "--repo-root", $repoRoot, "--config", $config, "--target", $target, "--skip-binary", "--output", $baseline) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "baseline-all" {
        $targets = Invoke-GuardCapture -GuardArgs @("list-targets", "--config", $config)

        foreach ($line in $targets) {
            $t = $line.Trim()
            if ([string]::IsNullOrWhiteSpace($t)) {
                continue
            }

            $out = Join-Path $baselineRoot "$t.json"
            $guardArgs = @("snapshot", "--repo-root", $repoRoot, "--config", $config, "--target", $t, "--skip-binary", "--output", $out) + $extraArgs
            Invoke-Guard -GuardArgs $guardArgs
        }
        break
    }
    "regen" {
        $guardArgs = @("regen-baselines", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "regen-baselines" {
        $guardArgs = @("regen-baselines", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "doctor" {
        $guardArgs = @("doctor", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "generate" {
        $guardArgs = @("generate", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "roslyn" {
        Invoke-Roslyn -RoslynArgs $extraArgs
        break
    }
    "sync" {
        $guardArgs = @("sync", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "release-prepare" {
        $guardArgs = @("release-prepare", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "changelog" {
        $guardArgs = @("changelog", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "verify" {
        $guardArgs = @("verify", "--repo-root", $repoRoot, "--config", $config, "--target", $target, "--baseline", $baseline) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "check" {
        $guardArgs = @("verify", "--repo-root", $repoRoot, "--config", $config, "--target", $target, "--baseline", $baseline) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "verify-all" {
        $guardArgs = @("verify-all", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "check-all" {
        $guardArgs = @("verify-all", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "list-targets" {
        $guardArgs = @("list-targets", "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "init-target" {
        $guardArgs = @("init-target", "--repo-root", $repoRoot, "--config", $config) + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "diff" {
        $guardArgs = @("diff") + $extraArgs
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
}
