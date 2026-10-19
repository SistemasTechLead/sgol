param([switch]$Cerrar)
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (!(New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Abre PowerShell como administrador y ejecuta este script para cambiar la regla del firewall.'
}
$name = 'SGOL-Pruebas-Red'
if ($Cerrar) {
    Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Write-Host 'Acceso de red de SGOL cerrado.'
    return
}
$configPath = Join-Path (Split-Path -Parent $PSScriptRoot) '.local/red.json'
if (!(Test-Path -LiteralPath $configPath)) { throw 'Primero ejecuta scripts\iniciar-red.ps1.' }
$lanConfig = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -Name $name -DisplayName 'SGOL - pruebas en red local' -Direction Inbound `
    -Action Allow -Protocol TCP -LocalPort 7440,7443 -LocalAddress $lanConfig.Address `
    -RemoteAddress LocalSubnet -Profile Any -Program (Get-Command dotnet).Source | Out-Null
Write-Host "Acceso habilitado desde la subred local: https://$($lanConfig.Address):7440"
