param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Automated', 'Interactive')]
    [string]$Mode
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = Join-Path $repositoryRoot 'tests\Sgol.Cv02Demo\Sgol.Cv02Demo.csproj'
$browserPath = Join-Path $repositoryRoot '.artifacts\cv02\browsers'
$browserLockPath = Join-Path $browserPath '__dirlock'

$externalDatabaseVariables = @(
    'ConnectionStrings__Sgol',
    'DATABASE_URL',
    'PGHOST',
    'PGPORT',
    'PGDATABASE',
    'PGUSER',
    'PGPASSWORD'
)
if ($externalDatabaseVariables | Where-Object { [Environment]::GetEnvironmentVariable($_) }) {
    throw 'CV02_EXTERNAL_DATABASE_CONFIGURATION_REJECTED'
}

if (Test-Path -LiteralPath $browserLockPath) {
    throw 'CV02_BROWSER_INSTALL_LOCK_PRESENT'
}

$env:PLAYWRIGHT_BROWSERS_PATH = $browserPath
$env:CV02_COMMIT = (rtk git -C $repositoryRoot rev-parse HEAD).Trim()

Push-Location $repositoryRoot
try {
    rtk dotnet run --project $projectPath --configuration Release --no-build -- --mode $Mode
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
