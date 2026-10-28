param([switch]$ActivateLocal)
$ErrorActionPreference = 'Stop'
if (!$ActivateLocal) { throw 'Usa -ActivateLocal solo después de verificar la preparación y recuperación aisladas.' }
$sgolRoot = Split-Path -Parent $PSScriptRoot
$sgolLocal = Join-Path $sgolRoot '.local'
. "$PSScriptRoot/secret-store.ps1"
$secrets = Move-SgolLegacySecrets $sgolLocal
foreach ($key in @('RuntimePassword','MigrationPassword','StorageRuntimePassword')) {
    if (!$secrets[$key]) { $secrets[$key] = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
}
Write-SgolSecrets $sgolLocal $secrets
$null = Write-SgolStorageIdentities $sgolLocal $secrets -MaintenanceName admin-sgol-loc
try {
    $env:SGOL_MAINTENANCE_CONNECTION = "Host=127.0.0.1;Port=5440;Database=sgol_local;Username=sgol_local;Password=$($secrets.DatabasePassword)"
    $env:SGOL_MIGRATION_PASSWORD = $secrets.MigrationPassword
    $env:SGOL_RUNTIME_PASSWORD = $secrets.RuntimePassword
    dotnet run --project "$sgolRoot/tools/Sgol.DatabaseSecurity" -- --activate-local
    if ($LASTEXITCODE -ne 0) { throw 'Falló la preparación de privilegios; Web no debe arrancar con la cuenta de mantenimiento.' }
} finally {
    $env:SGOL_MAINTENANCE_CONNECTION = $null
    $env:SGOL_MIGRATION_PASSWORD = $null
    $env:SGOL_RUNTIME_PASSWORD = $null
}
