param(
    [switch]$RepairStaleLock
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$browserPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts\cv02\browsers'))
$lockPath = [IO.Path]::GetFullPath((Join-Path $browserPath '__dirlock'))
$expectedPrefix = $browserPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$playwrightScript = Join-Path $repositoryRoot 'tests\Sgol.Cv02Demo\bin\Release\net10.0\playwright.ps1'

if (-not $lockPath.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'CV02_BROWSER_INSTALL_LOCK_PATH_REJECTED'
}

if (Test-Path -LiteralPath $lockPath) {
    if (-not $RepairStaleLock) {
        throw 'CV02_BROWSER_INSTALL_LOCK_PRESENT'
    }

    Remove-Item -LiteralPath $lockPath -Recurse -Force
}

if (-not (Test-Path -LiteralPath $playwrightScript -PathType Leaf)) {
    throw 'CV02_BROWSER_INSTALLER_NOT_BUILT'
}

$env:PLAYWRIGHT_BROWSERS_PATH = $browserPath
rtk powershell -NoProfile -ExecutionPolicy Bypass -File $playwrightScript install chromium webkit
if ($LASTEXITCODE -ne 0) {
    throw 'CV02_BROWSER_INSTALL_FAILED'
}
