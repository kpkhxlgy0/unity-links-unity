param(
    [Parameter(Mandatory = $true)]
    [string]$EditorRoot
)

$ErrorActionPreference = "Stop"

$mono = Join-Path $EditorRoot "Editor\Data\MonoBleedingEdge\bin\mono.exe"
$compiler = Join-Path $EditorRoot "Editor\Data\MonoBleedingEdge\lib\mono\4.5\csc.exe"
$monoProfile = Join-Path $EditorRoot "Editor\Data\MonoBleedingEdge\lib\mono\unityjit-win32"
if (-not (Test-Path -LiteralPath $mono)) {
    throw "Unity/Tuanjie Mono was not found: $mono"
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "Unity/Tuanjie C# compiler was not found: $compiler"
}
if (-not (Test-Path -LiteralPath $monoProfile)) {
    throw "Unity/Tuanjie Editor Mono profile was not found: $monoProfile"
}

$componentRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$testRoot = Join-Path ([IO.Path]::GetTempPath()) (
    "unity-links-reload-regression-" + [Guid]::NewGuid().ToString("N"))
$null = New-Item -ItemType Directory -Path $testRoot
$executable = Join-Path $testRoot "ReceiverReloadRegression.exe"
$stdout = Join-Path $testRoot "stdout.txt"
$stderr = Join-Path $testRoot "stderr.txt"
$previousMonoPath = $env:MONO_PATH

try {
    $env:MONO_PATH = $monoProfile
    $sources = @(
        (Join-Path $PSScriptRoot "UnityStubs.cs"),
        (Join-Path $PSScriptRoot "ReceiverReloadRegression.cs"),
        (Join-Path $componentRoot "Editor\UnityAssetLinkPath.cs"),
        (Join-Path $componentRoot "Editor\UnityAssetLinkProtocol.cs"),
        (Join-Path $componentRoot "Editor\UnityAssetLinkReceiver.cs")
    )
    & $mono $compiler `
        /nologo `
        /langversion:preview `
        /target:exe `
        "/nowarn:0649,0067" `
        "/out:$executable" `
        $sources
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to compile the reload regression test."
    }

    $process = Start-Process `
        -FilePath $mono `
        -ArgumentList @($executable) `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -WindowStyle Hidden `
        -PassThru
    if (-not $process.WaitForExit(30000)) {
        $process.Kill()
        throw "Reload regression test timed out."
    }

    $standardOutput = @(Get-Content -LiteralPath $stdout)
    $standardError = @(Get-Content -LiteralPath $stderr)
    $standardOutput | Where-Object {
        $_ -notmatch "^abort_threads: Failed aborting id:"
    }
    if ($process.ExitCode -ne 0) {
        $standardError
        throw "Reload regression test failed with exit code $($process.ExitCode)."
    }
    $unexpectedStandardError = @(
        $standardError | Where-Object {
            $_ -and $_ -notmatch "^abort_threads: Failed aborting id:"
        }
    )
    if ($unexpectedStandardError.Count -gt 0) {
        $unexpectedStandardError
        throw "Reload regression test wrote unexpected standard error."
    }
}
finally {
    $env:MONO_PATH = $previousMonoPath
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedTestRoot.StartsWith(
            $resolvedTempRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a test directory outside the system temp root."
    }
    if (Test-Path -LiteralPath $resolvedTestRoot) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
