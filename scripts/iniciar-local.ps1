param([switch]$NoBrowser, [switch]$Red, [switch]$WithWorker)
$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $sgolRoot
$sgolLocal = Join-Path $sgolRoot '.local'
New-Item -ItemType Directory -Force -Path $sgolLocal | Out-Null
$existingRuntime = Join-Path $sgolLocal 'processes.json'
if (Test-Path -LiteralPath $existingRuntime) {
    # Restart applies saved changes and switches between local and LAN settings.
    & "$PSScriptRoot/detener-local.ps1"
}
if (!(Test-Path -LiteralPath "$sgolLocal/config.json")) {
    $config = [ordered]@{
        DatabasePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
        StoragePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
        InitialPassword = 'Local!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(12))
    }
    $config | ConvertTo-Json | Set-Content -LiteralPath "$sgolLocal/config.json" -Encoding utf8
    "URL: https://localhost:7240`nUsuario: direccion.local`nContraseña temporal: $($config.InitialPassword)`nEn el primer acceso cambia la contraseña y configura MFA con tu aplicación de autenticación." | Set-Content -LiteralPath "$sgolLocal/acceso.txt" -Encoding utf8
}
. "$PSScriptRoot/local-env.ps1"
$baseUrl = 'https://localhost:7240'
if ($Red) {
    . "$PSScriptRoot/red-env.ps1"
    $baseUrl = $env:SgolApi__TrustedOrigin
}
"SGOL_LOCAL_DB_PASSWORD=$($sgolConfig.DatabasePassword)`nSGOL_LOCAL_S3_PASSWORD=$($sgolConfig.StoragePassword)" | Set-Content -LiteralPath "$sgolLocal/compose.env" -Encoding utf8
docker info --format '{{.ServerVersion}}' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Abre Docker Desktop y vuelve a ejecutar este comando.' }
if (!$Red) {
    dotnet dev-certs https --trust
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar el certificado HTTPS local.' }
}
docker compose --env-file .local/compose.env -f compose.local.yaml up -d
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron iniciar las dependencias.' }
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    docker compose --env-file .local/compose.env -f compose.local.yaml exec -T postgres pg_isready -U sgol_local -d sgol_local | Out-Null
    if ($LASTEXITCODE -eq 0) { break }
    Start-Sleep -Seconds 2
}
if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL no está disponible.' }
dotnet run --project tools/Sgol.LocalSetup/Sgol.LocalSetup.csproj
if ($LASTEXITCODE -ne 0) { throw 'Falló la preparación de datos locales; revisa la salida anterior.' }
$env:SGOL_BOOTSTRAP_INITIAL_PASSWORD = $null
$runtimePath = Join-Path $sgolLocal 'processes.json'
$runtime = if (Test-Path -LiteralPath $runtimePath) { Get-Content -LiteralPath $runtimePath -Raw | ConvertFrom-Json } else { $null }
$processes = [ordered]@{}
$sgolServices = if ($Red) { @('proxy', 'web') } else { @('web') }
# Operational tasks run in the web process; only legacy outbox processing needs a persistent worker.
# Portable backup jobs remain available through Sgol.Worker run-job.
if ($WithWorker -or $env:LegacyWorkflow__Enabled -eq 'true') { $sgolServices += 'worker' }
foreach ($service in $sgolServices) {
    $old = if ($runtime) { $runtime.$service } else { $null }
    $running = if ($old) { Get-Process -Id $old -ErrorAction SilentlyContinue } else { $null }
    $commandLine = if ($running) { (Get-CimInstance Win32_Process -Filter "ProcessId = $old").CommandLine } else { '' }
    if ($running -and $commandLine.Contains("$PSScriptRoot/run-local.ps1")) { $processes[$service] = $old; continue }
    $shell = (Get-Process -Id $PID).Path
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSScriptRoot/run-local.ps1`"", '-Service', $service)
    if ($Red) { $arguments += '-Red' }
    $process = Start-Process -FilePath $shell -WindowStyle Hidden -PassThru -ArgumentList $arguments -RedirectStandardOutput "$sgolLocal/$service.log" -RedirectStandardError "$sgolLocal/$service.error.log"
    $processes[$service] = $process.Id
}
$processes | ConvertTo-Json | Set-Content -LiteralPath $runtimePath -Encoding utf8
for ($attempt = 0; $attempt -lt 60; $attempt++) {
    try { $response = Invoke-RestMethod "$baseUrl/health/live"; if ($response.status -eq 'alive') { break } } catch { }
    Start-Sleep -Seconds 2
}
if ($attempt -eq 60) { throw "La aplicación no respondió. Revisa $sgolLocal/web.log y web.error.log." }
Write-Host "SGOL disponible en $baseUrl · Credenciales: $sgolLocal/acceso.txt"
if (!$NoBrowser) { Start-Process $baseUrl }
