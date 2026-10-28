param([switch]$ActivateLocal)
$ErrorActionPreference = 'Stop'
if (!$ActivateLocal) { throw 'La rotación habitual requiere preparación aislada aprobada y -ActivateLocal.' }
$sgolRoot = Split-Path -Parent $PSScriptRoot
$directory = Join-Path $sgolRoot '.local'
. "$PSScriptRoot/secret-store.ps1"
$secrets = Read-SgolSecrets $directory
if (!$secrets.DatabasePassword) { throw 'Primero migra los secretos locales.' }
if (!$secrets.PendingDatabasePassword) {
    $secrets.PendingDatabasePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Write-SgolSecrets $directory $secrets
}
try {
    $env:SGOL_NEW_MAINTENANCE_PASSWORD = $secrets.PendingDatabasePassword
    # Retry after an interrupted commit: try the persisted pending credential first.
    $verified = $false
    foreach ($password in @($secrets.PendingDatabasePassword, $secrets.DatabasePassword)) {
        $env:SGOL_MAINTENANCE_CONNECTION = "Host=127.0.0.1;Port=5440;Database=sgol_local;Username=sgol_local;Password=$password"
        dotnet "$sgolRoot/tools/Sgol.DatabaseSecurity/bin/Debug/net10.0/Sgol.DatabaseSecurity.dll" --activate-local --rotate-maintenance-only *> (Join-Path $directory 'maintenance-rotation.log')
        if ($LASTEXITCODE -eq 0) { $verified = $true; break }
    }
    if (!$verified) { throw 'No se verificó rotación. Se conservan ambas credenciales DPAPI para recuperación.' }
    $secrets.DatabasePassword = $secrets.PendingDatabasePassword
    $secrets.Remove('PendingDatabasePassword') | Out-Null
    Write-SgolSecrets $directory $secrets
    Write-Host 'Mantenimiento PostgreSQL rotado y verificado; claves de aplicación y datos conservados.'
} finally {
    $env:SGOL_MAINTENANCE_CONNECTION = $null
    $env:SGOL_NEW_MAINTENANCE_PASSWORD = $null
}
