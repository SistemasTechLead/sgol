param([switch]$KeepRunning, [switch]$ResetFixture)
$ErrorActionPreference = 'Stop'
$perfRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$perfState = Join-Path $perfRoot '.local/performance-environment.json'
$perfCompose = Join-Path $PSScriptRoot 'performance.compose.yaml'
New-Item -ItemType Directory -Force (Split-Path $perfState) | Out-Null
if (!(Test-Path -LiteralPath $perfState)) {
    $perfSecret = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    @{ Password=$perfSecret } | ConvertTo-Json | Set-Content -LiteralPath $perfState
}
$perfConfig = Get-Content -LiteralPath $perfState -Raw | ConvertFrom-Json
$env:SGOL_PERF_PASSWORD = $perfConfig.Password
if (!$env:SGOL_PERF_SIGNATURE_VOLUME) { $env:SGOL_PERF_SIGNATURE_VOLUME='sgol-local_scanner-data' }
$env:SGOL_TASK_CHECKS_CONNECTION = "Host=localhost;Port=5547;Database=sgol_task_checks_performance;Username=sgol_perf;Password=$($perfConfig.Password);Maximum Pool Size=40;Timeout=5;Command Timeout=15"
$env:SGOL_PERF_SECRET=$perfConfig.Password
try {
    docker compose -f $perfCompose up -d --wait --wait-timeout 90
    if ($LASTEXITCODE) { throw 'Isolated dependency startup failed.' }
    if ($ResetFixture) {
        if (Get-NetTCPConnection -State Listen -LocalPort 7267 -ErrorAction SilentlyContinue) { throw 'Stop the isolated test host before resetting its fixture.' }
        $perfProject = docker inspect sgol-performance-postgres-1 --format '{{index .Config.Labels "com.docker.compose.project"}}'
        if ($perfProject -ne 'sgol-performance') { throw 'Unexpected container owner; refusing fixture reset.' }
        docker exec sgol-performance-postgres-1 dropdb -U sgol_perf --if-exists sgol_task_checks_performance
        if ($LASTEXITCODE) { throw 'Dedicated database reset failed.' }
        docker exec sgol-performance-postgres-1 createdb -U sgol_perf sgol_task_checks_performance
        if ($LASTEXITCODE) { throw 'Dedicated database creation failed.' }
    }
    dotnet run --project (Join-Path $PSScriptRoot 'Sgol.TaskChecks.csproj') -c Release -- --performance-isolated
    if ($LASTEXITCODE) { throw 'Performance checks did not complete. Inspect local artifacts.' }
} finally {
    if (!$KeepRunning) { docker compose -f $perfCompose stop }
    $env:SGOL_PERF_PASSWORD=$null
    $env:SGOL_PERF_SECRET=$null
    $env:SGOL_TASK_CHECKS_CONNECTION=$null
}
