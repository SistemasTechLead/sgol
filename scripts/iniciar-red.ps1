param([string]$Direccion, [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
$sgolLocal = Join-Path $sgolRoot '.local'
New-Item -ItemType Directory -Force -Path $sgolLocal | Out-Null
. "$PSScriptRoot/secret-store.ps1"
$null = Move-SgolLegacySecrets $sgolLocal
if (!$Direccion) {
    $candidates = @(Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway } |
        ForEach-Object { $_.IPv4Address.IPAddress } | Where-Object { $_ -match '^(10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[01])\.)' })
    if ($candidates.Count -ne 1) { throw 'Indica la IPv4 de tu red de trabajo: .\scripts\iniciar-red.ps1 -Direccion 192.168.1.50' }
    $Direccion = $candidates[0]
}
$address = $null
if (![Net.IPAddress]::TryParse($Direccion, [ref]$address) -or
    $address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or
    $Direccion -notmatch '^(10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[01])\.)' -or
    !(Get-NetIPAddress -AddressFamily IPv4 | Where-Object IPAddress -eq $Direccion)) {
    throw 'Usa una IPv4 privada asignada a esta computadora.'
}
$configPath = Join-Path $sgolLocal 'red.json'
$lanConfig = if (Test-Path -LiteralPath $configPath) { Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json } else { $null }
$certificate = if ($lanConfig) { Get-Item "Cert:\CurrentUser\My\$($lanConfig.Thumbprint)" -ErrorAction SilentlyContinue } else { $null }
if (!$certificate -or $lanConfig.Address -ne $Direccion -or $certificate.NotAfter -lt (Get-Date).AddDays(7)) {
    $certificate = New-SelfSignedCertificate -Type SSLServerAuthentication -Subject 'CN=SGOL pruebas en red' `
        -FriendlyName 'SGOL pruebas en red' -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears(1) `
        -TextExtension @("2.5.29.17={text}IPAddress=$Direccion")
    $lanConfig = [ordered]@{ Address = $Direccion; Thumbprint = $certificate.Thumbprint }
    $lanConfig | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding utf8
}
$publicCertificate = Join-Path $sgolLocal 'SGOL-red.cer'
Export-Certificate -Cert $certificate -FilePath $publicCertificate | Out-Null
if (!(Test-Path -LiteralPath "Cert:\CurrentUser\Root\$($certificate.Thumbprint)")) {
    certutil -user -addstore Root $publicCertificate | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo confiar en el certificado de pruebas.' }
}
Write-Host "Certificado publico para los compañeros: $publicCertificate"
Write-Host "Huella: $($certificate.Thumbprint)"
& "$PSScriptRoot/iniciar-local.ps1" -Red -NoBrowser:$NoBrowser
if (!(Get-NetFirewallRule -Name 'SGOL-Pruebas-Red' -ErrorAction SilentlyContinue)) {
    Write-Warning 'Falta la regla de firewall. Ejecuta scripts\habilitar-red.ps1 una vez desde PowerShell como administrador.'
}
