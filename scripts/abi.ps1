param(
    [Parameter(Position = 0)]
    [ValidateSet("snapshot", "baseline", "verify", "check", "diff")]
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
$guard = Join-Path $repoRoot "tools/abi_guard/abi_guard.py"

function Invoke-Guard {
    param([string[]]$GuardArgs)

    if ($python -eq "py") {
        & $python -3 $guard @GuardArgs
    } else {
        & $python $guard @GuardArgs
    }

    if ($LASTEXITCODE -ne 0) {
        throw "abi_guard failed with exit code $LASTEXITCODE."
    }
}

switch ($Command) {
    "snapshot" {
        $guardArgs = @("snapshot", "--repo-root", $repoRoot, "--config", $config, "--target", $target) + $Arguments
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "baseline" {
        $guardArgs = @("snapshot", "--repo-root", $repoRoot, "--config", $config, "--target", $target, "--skip-binary", "--output", $baseline) + $Arguments
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "verify" {
        $guardArgs = @("verify", "--repo-root", $repoRoot, "--config", $config, "--target", $target, "--baseline", $baseline) + $Arguments
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "check" {
        $guardArgs = @("verify", "--repo-root", $repoRoot, "--config", $config, "--target", $target, "--baseline", $baseline) + $Arguments
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
    "diff" {
        $guardArgs = @("diff") + $Arguments
        Invoke-Guard -GuardArgs $guardArgs
        break
    }
}
