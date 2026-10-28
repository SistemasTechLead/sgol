$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $sgolRoot
. "$PSScriptRoot/secret-store.ps1"
$directory = Join-Path $sgolRoot 'artifacts/security-audit/implementation'
$secrets = Read-SgolSecrets $directory
$database = (Get-Content -LiteralPath (Join-Path $directory 'results.json') -Raw | ConvertFrom-Json).database
if ($database -notmatch '^sgol_task_checks_security_[0-9]+$') { throw 'Destino de rotación no aislado.' }
$role = 'sgol_security_rotation_' + (Get-Date -Format yyyyMMddHHmmss)
$old = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$new = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
"CREATE ROLE $role LOGIN PASSWORD '$old'; GRANT CONNECT ON DATABASE $database TO $role;" | docker exec -i sgol-security-checks-postgres-1 psql -X -q -v ON_ERROR_STOP=1 -U postgres -d $database
if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar rol de rotación aislado.' }
try {
    $env:SGOL_MAINTENANCE_CONNECTION = "Host=127.0.0.1;Port=5549;Database=$database;Username=$role;Password=$old"
    $env:SGOL_NEW_MAINTENANCE_PASSWORD = $new
    dotnet tools/Sgol.DatabaseSecurity/bin/Debug/net10.0/Sgol.DatabaseSecurity.dll --rotate-maintenance-only
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo rotar contraseña aislada.' }
    # Old credential must now fail through TCP authentication, not Unix socket trust.
    $env:SGOL_NEW_MAINTENANCE_PASSWORD = $old
    dotnet tools/Sgol.DatabaseSecurity/bin/Debug/net10.0/Sgol.DatabaseSecurity.dll --rotate-maintenance-only *> (Join-Path $directory 'rotation-old-credential-rejected.log')
    if ($LASTEXITCODE -eq 0) { throw 'La credencial retirada aún funciona.' }
    @{ database = $database; newCredentialVerified = $true; retiredCredentialRejected = $true } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'rotation-result.json')
    Write-Host 'PASS rotación DB y rechazo de credencial antigua en instancia aislada.'
} finally {
    $env:SGOL_MAINTENANCE_CONNECTION = $null
    $env:SGOL_NEW_MAINTENANCE_PASSWORD = $null
}
