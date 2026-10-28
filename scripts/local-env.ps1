$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
$sgolLocal = Join-Path $sgolRoot '.local'
$sgolConfigPath = Join-Path $sgolLocal 'config.json'
if (!(Test-Path -LiteralPath $sgolConfigPath)) { throw 'Primero ejecuta scripts/iniciar-local.ps1.' }
. "$PSScriptRoot/secret-store.ps1"
if (!(Test-Path -LiteralPath (Join-Path $sgolLocal 'secrets.dpapi.json'))) { throw 'Primero prepara el almacén protegido con scripts/preparar-seguridad-local.ps1.' }
$sgolSecrets = Read-SgolSecrets $sgolLocal -Names @('RuntimePassword','StorageRuntimePassword')
$sgolConfig = Get-Content -LiteralPath $sgolConfigPath -Raw | ConvertFrom-Json
if (!$sgolSecrets.RuntimePassword) { throw 'Falta preparar el rol runtime con scripts/preparar-seguridad-local.ps1.' }
$env:DOTNET_ENVIRONMENT = 'Development'
$env:RequestLimits__TimeoutSeconds = '300'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
# Reset LAN overrides when returning to the local mode in the same terminal.
$env:ReverseProxy__KnownProxy = $null
$env:LocalShared__SecurityHeaders = $null
$env:LocalShared__EvidenceOrigin = $null
$env:ConnectionStrings__Sgol = "Host=127.0.0.1;Port=5440;Database=sgol_local;Username=sgol_runtime;Password=$($sgolSecrets.RuntimePassword)"
$env:ASPNETCORE_URLS = 'https://localhost:7240'
$env:SgolApi__TrustedOrigin = 'https://localhost:7240'
$env:Evidence__Storage__Endpoint = 'http://localhost:8333'
$env:Evidence__Storage__Region = 'us-east-1'
$env:Evidence__Storage__QuarantineBucket = 'sgol-quarantine'
$env:Evidence__Storage__CleanBucket = 'sgol-clean'
$env:Evidence__Storage__AccessKey = 'sgol-runtime'
$env:Evidence__Storage__SecretKey = $sgolSecrets.StorageRuntimePassword
$env:Evidence__Storage__AllowedUploadOrigins__0 = 'https://localhost:7240'
$env:Evidence__Storage__AllowInsecureTransport = 'true'
$env:Evidence__Scanner__Host = '127.0.0.1'
$env:Evidence__Scanner__Port = '3310'
$env:SGOL_BOOTSTRAP_INITIAL_PASSWORD = $null
$env:DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER = '1'
$env:DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH = '1'
$env:DOTNET_WATCH_RESTART_ON_RUDE_EDIT = '1'
# Enable only after the local process migrations and recovery checks have been completed.
$env:Processes__OperationsEnabled = if ($sgolConfig.PSObject.Properties['OperationalProcessesEnabled'] -and $sgolConfig.OperationalProcessesEnabled -eq $true) { 'true' } else { 'false' }
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Microsoft.EntityFrameworkCore', 'Warning', 'Process')
