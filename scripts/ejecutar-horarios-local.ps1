$ErrorActionPreference = 'Stop'
$sgolScheduleRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'local-env.ps1')
. (Join-Path $PSScriptRoot 'red-env.ps1')
$env:SGOL_BOOTSTRAP_INITIAL_PASSWORD = $null
Set-Location -LiteralPath (Join-Path $sgolScheduleRoot 'src/Sgol.Web')
& dotnet (Join-Path $sgolScheduleRoot '.local/cross-role-web-final/Sgol.Web.dll')
