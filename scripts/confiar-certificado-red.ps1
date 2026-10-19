param([Parameter(Mandatory)][string]$Certificado, [Parameter(Mandatory)][string]$Huella)
$ErrorActionPreference = 'Stop'
$path = (Resolve-Path -LiteralPath $Certificado).Path
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($path)
if ($certificate.HasPrivateKey -or $certificate.Thumbprint -ne $Huella.Replace(' ', '') -or
    $certificate.NotAfter -lt (Get-Date)) { throw 'El certificado no coincide con la huella entregada o ha expirado.' }
certutil -user -addstore Root $path | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'No se pudo instalar el certificado de SGOL.' }
Write-Host 'Certificado de SGOL instalado para este usuario. Vuelve a abrir el navegador.'
