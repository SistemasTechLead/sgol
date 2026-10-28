$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
$runtimePath = Join-Path $sgolRoot '.local/processes.json'
if (Test-Path -LiteralPath $runtimePath) {
    $runtime = Get-Content -LiteralPath $runtimePath -Raw | ConvertFrom-Json
    foreach ($service in @('web', 'worker', 'proxy')) {
        $processId = $runtime.$service
        if (!$processId) { continue }
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        $commandLine = if ($process) { (Get-CimInstance Win32_Process -Filter "ProcessId = $processId").CommandLine } else { '' }
        $runnerPaths = @(
            (Join-Path $PSScriptRoot 'run-local.ps1').ToLowerInvariant(),
            (Join-Path $sgolRoot '.local/run-weekly-master-main.ps1').Replace('/', '\').ToLowerInvariant()
        )
        if ($process -and ($runnerPaths | Where-Object { $commandLine.Replace('/', '\').ToLowerInvariant().Contains($_) })) {
            taskkill /PID $processId /T /F | Out-Null
        }
    }
    Remove-Item -LiteralPath $runtimePath
}
Set-Location -LiteralPath $sgolRoot
. "$PSScriptRoot/secret-store.ps1"
$sgolSecrets = Read-SgolSecrets (Join-Path $sgolRoot '.local')
# Compose interpolation is required for stop, but no password file is written.
$env:SGOL_LOCAL_DB_PASSWORD = if ($sgolSecrets.DatabasePassword) { $sgolSecrets.DatabasePassword } else { 'unused-stop' }
$env:SGOL_LOCAL_S3_PASSWORD = if ($sgolSecrets.StoragePassword) { $sgolSecrets.StoragePassword } else { 'unused-stop' }
try { docker compose -f compose.local.yaml stop } finally { $env:SGOL_LOCAL_DB_PASSWORD = $null; $env:SGOL_LOCAL_S3_PASSWORD = $null }
Write-Host 'SGOL detenido. Los datos de prueba se conservan.'
