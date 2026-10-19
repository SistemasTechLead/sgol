$ErrorActionPreference = 'Stop'
try {
    if (![Environment]::Is64BitOperatingSystem) { throw 'Este paquete requiere Windows de 64 bits.' }
    $screenIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $screenPrincipal = [Security.Principal.WindowsPrincipal]::new($screenIdentity)
    if ($screenPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Abre Instalar.cmd sin Ejecutar como administrador, desde la cuenta de Windows que usará la pantalla.'
    }
    $screenConfig = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'pantalla.json') -Raw | ConvertFrom-Json
    $screenCertificate = Get-PfxCertificate -FilePath (Join-Path $PSScriptRoot 'SGOL-red.cer')
    if ($screenCertificate.Thumbprint -ne $screenConfig.CertificateThumbprint) { throw 'El certificado no coincide con el paquete de SGOL.' }
    if ($screenCertificate.NotAfter -le (Get-Date)) { throw 'El certificado de SGOL está vencido. Solicita un paquete actualizado.' }
    $screenDestination = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'SGOL\Pantalla'))
    New-Item -ItemType Directory -Path $screenDestination -Force | Out-Null
    foreach ($screenFile in @('Sgol.ScreenClient.exe', 'pantalla.json', 'Iniciar.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $screenFile) -Destination (Join-Path $screenDestination $screenFile) -Force
    }
    # Trust only the public SGOL certificate, for this Windows user.
    if (!(Test-Path -LiteralPath ('Cert:\CurrentUser\Root\' + $screenCertificate.Thumbprint))) {
        Import-Certificate -FilePath (Join-Path $PSScriptRoot 'SGOL-red.cer') -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    }
    & (Join-Path $screenDestination 'Sgol.ScreenClient.exe') --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Falló la comprobación de protección de credenciales de Windows.' }
    $screenShell = New-Object -ComObject WScript.Shell
    $screenShortcut = $screenShell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Startup')) 'Pantalla SGOL.lnk'))
    $screenShortcut.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $screenShortcut.Arguments = '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + (Join-Path $screenDestination 'Iniciar.ps1') + '"'
    $screenShortcut.WorkingDirectory = $screenDestination
    $screenShortcut.WindowStyle = 7
    $screenShortcut.Save()
    & (Join-Path $screenDestination 'Iniciar.ps1')
    Write-Host 'Instalado para esta cuenta de Windows. Edge mostrará un código de vinculación.'
    Write-Host 'Comparte ese código en la conversación de preparación de SGOL.'
    Write-Host 'El inicio automático de la sesión de Windows se configura por separado.'
}
catch {
    Write-Host ('No se pudo instalar: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
