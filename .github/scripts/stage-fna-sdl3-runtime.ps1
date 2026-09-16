param(
    [string]$ClassicUOCommit = "986f9a7303a3dd747cb81a6da322e4994a169e93",
    [string]$FnaCommit = "ed62a28d91c6dcacc113b80d96570ae1f9600e03"
)

$ErrorActionPreference = "Stop"

$repository = Resolve-Path (Join-Path $PSScriptRoot "../..")
$fnaDirectory = Join-Path $repository "external/FNA"
$runtimeDirectory = Join-Path $repository "external/x64"
$classicUO = Join-Path ([System.IO.Path]::GetTempPath()) "ClassicUO-SDL3-$([Guid]::NewGuid())"

try {
    $actualFnaCommit = git -C $fnaDirectory rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $actualFnaCommit -ne $FnaCommit) {
        throw "Expected FNA $FnaCommit but checkout contains $actualFnaCommit"
    }

    git clone --filter=blob:none --no-checkout https://github.com/ClassicUO/ClassicUO.git $classicUO
    if ($LASTEXITCODE -ne 0) {
        throw "Could not clone the pinned ClassicUO dependency source"
    }

    git -C $classicUO checkout $ClassicUOCommit -- external/x64
    if ($LASTEXITCODE -ne 0) {
        throw "Could not check out the pinned ClassicUO native runtime"
    }

    Remove-Item (Join-Path $runtimeDirectory "SDL2.dll") -Force -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $classicUO "external/x64/*") $runtimeDirectory -Force

    if (!(Test-Path (Join-Path $runtimeDirectory "SDL3.dll"))) {
        throw "Pinned native stack does not contain SDL3.dll"
    }

    Get-Item (Join-Path $runtimeDirectory "*.dll") | Select-Object Name, Length
    Get-FileHash (Join-Path $runtimeDirectory "*.dll") -Algorithm SHA256
}
finally {
    if (Test-Path $classicUO) {
        Remove-Item $classicUO -Recurse -Force
    }
}
