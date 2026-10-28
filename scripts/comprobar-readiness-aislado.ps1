$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
$target = 'sgol-security-checks-gateway-1'
$project = docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' $target
if ($LASTEXITCODE -ne 0 -or $project -ne 'sgol-security-checks') { throw 'El contenedor no pertenece al stack aislado.' }
$uri = 'https://localhost:7287/health/ready'
$fixture = Get-Content -LiteralPath (Join-Path $sgolRoot 'artifacts/security-audit/implementation/browser-fixture.private.json') -Raw | ConvertFrom-Json
$listeners = @(Get-NetTCPConnection -LocalPort 7287 -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
if ($listeners.Count -ne 1 -or $listeners[0] -ne $fixture.processId) { throw 'El proceso HTTP no coincide con el fixture retenido.' }
$results = @()
$results += @{ scenario = 'before'; status = [int](Invoke-WebRequest $uri -SkipHttpErrorCheck).StatusCode }
try {
    docker stop $target | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo detener dependencia aislada.' }
    Start-Sleep -Seconds 6
    $results += @{ scenario = 'storage stopped'; status = [int](Invoke-WebRequest $uri -SkipHttpErrorCheck).StatusCode }
} finally {
    docker start $target | Out-Null
}
Start-Sleep -Seconds 6
$results += @{ scenario = 'storage recovered'; status = [int](Invoke-WebRequest $uri -SkipHttpErrorCheck).StatusCode }
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $sgolRoot 'artifacts/security-audit/implementation/readiness-real-outage.json')
if ($results[0].status -ne 200 -or $results[1].status -ne 503 -or $results[2].status -ne 200) { throw 'Readiness no refleja la dependencia real.' }
Write-Host 'PASS readiness real: 200, storage aislado detenido 503, recuperado 200.'
