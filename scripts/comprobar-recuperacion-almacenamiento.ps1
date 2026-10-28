$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $sgolRoot
. "$PSScriptRoot/secret-store.ps1"
$parent = Join-Path $sgolRoot 'artifacts/security-audit/implementation'
$recovery = Get-ChildItem -LiteralPath $parent -Directory -Filter 'recovery-*' | Sort-Object Name -Descending | Select-Object -First 1
if (!$recovery -or !(Test-Path -LiteralPath (Join-Path $recovery.FullName 'storage'))) { throw 'Primero ensaya recuperación DB/storage.' }
$sourceSecrets = Read-SgolSecrets (Join-Path $sgolRoot '.local')
$sourceAccessKey = 'sgol-maintenance'
if (!$sourceSecrets.StoragePassword) {
    $legacy = Get-Content -LiteralPath (Join-Path $sgolRoot '.local/config.json') -Raw | ConvertFrom-Json
    $sourceSecrets.StoragePassword = $legacy.StoragePassword
    $sourceAccessKey = 'sgol-local'
}
$testSecrets = Read-SgolSecrets $parent
$identities = Write-SgolStorageIdentities $recovery.FullName $testSecrets security-maintenance security-runtime -MaintenanceName admin-sgol-loc
$container = 'sgol-security-recovered-storage'
if (docker ps -aq --filter "name=^/${container}$") {
    $label = docker inspect --format '{{index .Config.Labels "sgol.security.recovery"}}' $container
    if ($label -ne $recovery.Name) { throw 'El contenedor existente no identifica esta copia. Conserva la evidencia y prepara un contenedor de recuperación separado.' }
    docker restart $container | Out-Null
    docker restart sgol-security-recovered-gateway | Out-Null
} else {
$image = 'chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5'
docker run -d --name $container --label "sgol.security.recovery=$($recovery.Name)" --network sgol-security-checks_checks -p 127.0.0.1:8349:8080 `
    --mount "type=bind,source=$($recovery.FullName)/storage,target=/data" `
    --mount "type=bind,source=$identities,target=/run/secrets/identities.json,readonly" `
    $image mini '-dir=/data' '-ip=127.0.0.1' '-ip.bind=127.0.0.1' '-admin.ui=false' '-webdav=false' '-s3.port.iceberg=0' '-s3.port.lance=0' '-s3.iam=false' '-s3.config=/run/secrets/identities.json'  | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'No arrancó copia storage.' }
docker run -d --name sgol-security-recovered-gateway --network "container:$container" --user 101:101 --read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges:true `
    --mount "type=bind,source=$sgolRoot/scripts/storage-gateway.conf,target=/etc/nginx/nginx.conf,readonly" `
    sgol-storage-gateway:security-20261028 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'No arrancó gateway de copia.' }
}
Start-Sleep -Seconds 3
try {
    $env:SGOL_STORAGE_ENDPOINT = 'http://127.0.0.1:8333'
    $env:SGOL_STORAGE_ACCESS_KEY = $sourceAccessKey
    $env:SGOL_STORAGE_SECRET_KEY = $sourceSecrets.StoragePassword
    $env:SGOL_STORAGE_INVENTORY_PATH = Join-Path $recovery.FullName 'source-objects.json'
    dotnet tools/Sgol.StorageSetup/bin/Debug/net10.0/Sgol.StorageSetup.dll --inventory
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer inventario fuente.' }
    $env:SGOL_STORAGE_ENDPOINT = 'http://127.0.0.1:8349'
    $env:SGOL_STORAGE_ACCESS_KEY = 'security-maintenance'
    $env:SGOL_STORAGE_SECRET_KEY = $testSecrets.StoragePassword
    $env:SGOL_STORAGE_INVENTORY_PATH = Join-Path $recovery.FullName 'restored-objects.json'
    dotnet tools/Sgol.StorageSetup/bin/Debug/net10.0/Sgol.StorageSetup.dll --inventory
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer almacenamiento restaurado.' }
    if ((Get-FileHash (Join-Path $recovery.FullName 'source-objects.json')).Hash -ne (Get-FileHash (Join-Path $recovery.FullName 'restored-objects.json')).Hash) {
        throw 'Los objetos restaurados no coinciden con el origen.'
    }
    $resultPath = Join-Path $recovery.FullName 'result.json'
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    $anonymous = Invoke-WebRequest 'http://127.0.0.1:8349/sgol-clean?list-type=2' -SkipHttpErrorCheck
    if ([int]$anonymous.StatusCode -ne 403) { throw 'La recuperación permite acceso anónimo; no activar.' }
    $result.storageRestoredAndServed = $true
    $result | ConvertTo-Json | Set-Content -LiteralPath $resultPath
    Write-Host 'PASS almacenamiento restaurado arranca con barrera de red; inventario y SHA256 de objetos iguales al origen.'
} finally {
    foreach ($key in @('SGOL_STORAGE_ENDPOINT','SGOL_STORAGE_ACCESS_KEY','SGOL_STORAGE_SECRET_KEY','SGOL_STORAGE_INVENTORY_PATH')) {
        [Environment]::SetEnvironmentVariable($key, $null, 'Process')
    }
}
