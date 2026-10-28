$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $sgolRoot
. "$PSScriptRoot/secret-store.ps1"
$directory = Join-Path $sgolRoot ('artifacts/security-audit/implementation/recovery-' + (Get-Date -Format yyyyMMddHHmmss))
New-Item -ItemType Directory -Path $directory | Out-Null
Set-SgolPrivateAcl $directory
# Source is read-only. Destination is a physically different PostgreSQL container/volume.
$source = 'sgol-local-postgres-1'
$target = 'sgol-security-checks-postgres-1'
$sourceId = docker inspect --format '{{.Id}}' $source
$targetId = docker inspect --format '{{.Id}}' $target
if ($LASTEXITCODE -ne 0 -or !$sourceId -or !$targetId -or $sourceId -eq $targetId) { throw 'No se verificaron contenedores distintos.' }
$database = 'sgol_task_checks_security_recovery_' + (Get-Date -Format yyyyMMddHHmmss)
docker exec $source pg_dump -U sgol_local -d sgol_local -Fc -f /tmp/sgol-security-readonly.dump
if ($LASTEXITCODE -ne 0) { throw 'Falló el respaldo de lectura.' }
docker cp "${source}:/tmp/sgol-security-readonly.dump" (Join-Path $directory 'database.dump')
if ($LASTEXITCODE -ne 0) { throw 'Falló la copia de respaldo.' }
docker cp (Join-Path $directory 'database.dump') "${target}:/tmp/sgol-security-recovery.dump"
docker exec $target createdb -U postgres $database
if ($LASTEXITCODE -ne 0) { throw 'No se creó destino aislado.' }
docker exec $target pg_restore -U postgres -d $database --no-owner --exit-on-error /tmp/sgol-security-recovery.dump
if ($LASTEXITCODE -ne 0) { throw 'Falló restauración aislada.' }
$sql = @'
SELECT format('SELECT %L AS relation, count(*), md5(coalesce(string_agg(md5(row_to_json(t)::text), '''' ORDER BY md5(row_to_json(t)::text)), '''')) FROM %I.%I t;', tablename, schemaname, tablename)
FROM pg_tables WHERE schemaname='public' ORDER BY tablename
\gexec
'@
$before = $sql | docker exec -i $source psql -X -qAt -U sgol_local -d sgol_local
if ($LASTEXITCODE -ne 0) { throw 'No se obtuvo fingerprint fuente.' }
$restored = $sql | docker exec -i $target psql -X -qAt -U postgres -d $database
if ($LASTEXITCODE -ne 0) { throw 'No se obtuvo fingerprint restaurado.' }
$before | Set-Content -LiteralPath (Join-Path $directory 'source-fingerprints.txt')
$restored | Set-Content -LiteralPath (Join-Path $directory 'restored-fingerprints.txt')
if (Compare-Object $before $restored) { throw 'La restauración no conserva todas las tablas/filas/claves.' }
# Preserve storage files as a private recovery copy, without mounting or modifying the daily volume.
docker cp 'sgol-local-storage-1:/data' (Join-Path $directory 'storage')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo preservar almacenamiento.' }
$manifest = Get-ChildItem -LiteralPath (Join-Path $directory 'storage') -File -Recurse | ForEach-Object {
    [pscustomobject]@{ path = [IO.Path]::GetRelativePath((Join-Path $directory 'storage'), $_.FullName); size = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $directory 'storage-manifest.json')
$after = $sql | docker exec -i $source psql -X -qAt -U sgol_local -d sgol_local
if (Compare-Object $before $after) { throw 'El origen cambió durante la preparación; investigar antes de activar.' }
[ordered]@{ database = $database; sourceContainer = $sourceId; targetContainer = $targetId; allTableFingerprintsEqual = $true;
    sourceUnchanged = $true; storageFiles = @($manifest).Count; storageRestoredAndServed = $false;
    databaseSha256 = (Get-FileHash -LiteralPath (Join-Path $directory 'database.dump')).Hash } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'result.json')
Write-Host "Restauración DB verificada: $database. Almacenamiento copiado; su arranque y acceso aún requieren prueba. Evidencia: $directory"
