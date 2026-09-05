param(
    [switch]$RepairStaleBrowserLock
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$installScript = Join-Path $PSScriptRoot 'install-cv02-browsers.ps1'
$demoScript = Join-Path $PSScriptRoot 'run-cv02.ps1'

rtk dotnet test (Join-Path $repositoryRoot 'tests\Sgol.IntegrationTests\Sgol.IntegrationTests.csproj') `
    --no-build --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw 'CV02_POSTGRESQL_SUITE_FAILED'
}

if ($RepairStaleBrowserLock) {
    rtk powershell -NoProfile -ExecutionPolicy Bypass -File $installScript -RepairStaleLock
}
else {
    rtk powershell -NoProfile -ExecutionPolicy Bypass -File $installScript
}

if ($LASTEXITCODE -ne 0) {
    throw 'CV02_BROWSER_INSTALL_FAILED'
}

rtk powershell -NoProfile -ExecutionPolicy Bypass -File $demoScript -Mode Automated
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
