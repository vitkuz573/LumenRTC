param(
    [Parameter(Position = 0)]
    [ValidateSet("snapshot", "baseline", "baseline-all", "regen", "regen-baselines", "doctor", "changelog", "verify", "check", "verify-all", "check-all", "list-targets", "init-target", "diff")]
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
$guard = Join-Path $repoRoot "tools/abi_guard/abi_guard.py"
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
        throw "abi_guard failed with exit code $LASTEXITCODE."
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
        throw "abi_guard failed with exit code $LASTEXITCODE."
    }

    return $output
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
