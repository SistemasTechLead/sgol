[CmdletBinding()]
param([ValidateSet('Start', 'Stop', 'Status')][string]$Action = 'Start')
$ErrorActionPreference = 'Stop'
$server = Join-Path $PSScriptRoot 'servidor-grafo-boveda.cjs'
$html = Join-Path $PSScriptRoot '../.local/graphify/visualizacion/graph.html'
function Test-Viewer {
    try { return (Invoke-WebRequest 'http://127.0.0.1:7389/health' -TimeoutSec 2).Content -eq 'sgol-graphify-viewer-v1' }
    catch { return $false }
}
if ($Action -eq 'Stop') {
    $processes = @(Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($server) })
    foreach ($process in $processes) { Stop-Process -Id $process.ProcessId }
    Write-Output "Visor detenido: $($processes.Count) proceso(s)."
    return
}
if (Test-Viewer) { Write-Output 'Visor disponible: http://127.0.0.1:7389/graph.html'; return }
if ($Action -eq 'Status') { throw 'El visor no esta disponible. Ejecutar este script con -Action Start.' }
if (!(Test-Path -LiteralPath $html)) { & (Join-Path $PSScriptRoot 'generar-grafo-boveda.ps1') }
$node = (Get-Command node -ErrorAction Stop).Source
Start-Process -FilePath $node -ArgumentList @('"' + $server + '"') -WindowStyle Hidden -RedirectStandardError (Join-Path (Split-Path $html) 'visor-error.log') | Out-Null
for ($attempt = 0; $attempt -lt 10; $attempt++) {
    if (Test-Viewer) { Write-Output 'Visor disponible: http://127.0.0.1:7389/graph.html'; return }
    Start-Sleep -Milliseconds 300
}
throw 'No se pudo iniciar el visor. Revisar visor-error.log y el puerto 7389.'
