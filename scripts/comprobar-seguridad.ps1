param([switch]$SkipBuild, [switch]$Browser)
$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $sgolRoot
. "$PSScriptRoot/secret-store.ps1"
$buildRoot = Join-Path $sgolRoot 'artifacts/security-audit/check-build'
$directory = Join-Path $sgolRoot 'artifacts/security-audit/implementation'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
Set-SgolPrivateAcl $directory
$secrets = Read-SgolSecrets $directory
foreach ($key in @('DatabasePassword','StoragePassword','StorageRuntimePassword','MigrationPassword','RuntimePassword')) {
    if (!$secrets[$key]) { $secrets[$key] = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
}
Write-SgolSecrets $directory $secrets
try {
    $env:SGOL_SECURITY_STORAGE_CONFIG = Write-SgolStorageIdentities $directory $secrets security-maintenance security-runtime
    $env:SGOL_SECURITY_RUNTIME_S3_PASSWORD = $secrets.StorageRuntimePassword
    $env:SGOL_SECURITY_DB_PASSWORD = $secrets.DatabasePassword
    $env:SGOL_SECURITY_S3_PASSWORD = $secrets.StoragePassword
    $env:SGOL_MIGRATION_PASSWORD = $secrets.MigrationPassword
    $env:SGOL_RUNTIME_PASSWORD = $secrets.RuntimePassword
    $env:SGOL_SECURITY_ROOT = $sgolRoot
    $env:SGOL_SECURITY_BUILD_ROOT = $buildRoot
    $env:SGOL_MAINTENANCE_CONNECTION = "Host=127.0.0.1;Port=5549;Database=postgres;Username=postgres;Password=$($secrets.DatabasePassword)"
    docker compose -f tools/Sgol.SecurityChecks/compose.yaml up -d
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar el stack aislado.' }
    for ($i = 0; $i -lt 30; $i++) {
        docker compose -f tools/Sgol.SecurityChecks/compose.yaml exec -T postgres pg_isready -U postgres | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL aislado no está listo.' }
    if (!$SkipBuild) {
        foreach ($project in @('src/Sgol.Web','tools/Sgol.DatabaseSecurity','tools/Sgol.SecurityChecks','tools/Sgol.SecurityMediaChecks')) {
            dotnet build $project "-p:ArtifactsPath=$buildRoot" -p:UseSharedCompilation=false --nologo -v:q
            if ($LASTEXITCODE -ne 0) { throw "No compiló $project" }
        }
    }
    if ($Browser) {
        dotnet "$buildRoot/bin/Sgol.SecurityChecks/debug/Sgol.SecurityChecks.dll" --hold-browser
    } else {
        dotnet "$buildRoot/bin/Sgol.SecurityChecks/debug/Sgol.SecurityChecks.dll"
    }
    if ($LASTEXITCODE -ne 0) { throw 'Falló una regresión integrada; consulta la evidencia aislada.' }
    dotnet "$buildRoot/bin/Sgol.SecurityMediaChecks/debug/Sgol.SecurityMediaChecks.dll"
    if ($LASTEXITCODE -ne 0) { throw 'Falló una regresión de archivos.' }
} finally {
    foreach ($name in @('SGOL_SECURITY_BUILD_ROOT','SGOL_SECURITY_STORAGE_CONFIG','SGOL_SECURITY_RUNTIME_S3_PASSWORD','SGOL_SECURITY_DB_PASSWORD','SGOL_SECURITY_S3_PASSWORD','SGOL_MIGRATION_PASSWORD','SGOL_RUNTIME_PASSWORD','SGOL_MAINTENANCE_CONNECTION','SGOL_SECURITY_ROOT')) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
}
