$ErrorActionPreference = 'Stop'
$screenSession = (Get-Process -Id $PID).SessionId
$screenRunning = @(Get-Process -Name Sgol.ScreenClient -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $screenSession })
if ($screenRunning.Count -eq 0) {
    Start-Process -FilePath (Join-Path $PSScriptRoot 'Sgol.ScreenClient.exe') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden
}
