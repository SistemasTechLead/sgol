[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(?:[a-z0-9./:_-]+@sha256:[0-9a-f]{64}|sha256:[0-9a-f]{64})$')]
    [string]$ImageRef,

    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$RuntimeEnvironmentFile,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^s3://[^/]+/.+\.manifest\.json$')]
    [string]$BackupManifestUri,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^s3://[^/]+/.+\.manifest\.json$')]
    [string]$ReplicaManifestUri,

    [ValidateScript({ -not (Test-Path -LiteralPath $_ -PathType Leaf) })]
    [string]$EvidenceDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runtimeEnvironmentPath = (Resolve-Path -LiteralPath $RuntimeEnvironmentFile).Path
if ($runtimeEnvironmentPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The synthetic runtime environment file must remain outside the repository.'
}
$runtimeEnvironment = Get-Content -LiteralPath $runtimeEnvironmentPath
if ($runtimeEnvironment -notcontains 'SGOL_SYNTHETIC_ONLY=true') {
    throw 'The external gate accepts only an environment explicitly marked SGOL_SYNTHETIC_ONLY=true.'
}
function Test-AbsolutePath([string]$value) {
    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        return $value -match '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+)'
    }
    return $value.StartsWith('/', [StringComparison]::Ordinal)
}
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $temporaryRoot ("sgol-tech-ops-evidence-" + [guid]::NewGuid().ToString('N'))
}
elseif (-not (Test-AbsolutePath $EvidenceDirectory)) {
    throw 'EvidenceDirectory must be an absolute path outside the repository.'
}
$evidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
if ($evidenceDirectory.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'External TECH-OPS evidence must remain outside the repository.'
}
if (Test-Path -LiteralPath $evidenceDirectory) {
    if (-not (Test-Path -LiteralPath $evidenceDirectory -PathType Container) -or
        (Get-ChildItem -LiteralPath $evidenceDirectory -Force | Select-Object -First 1)) {
        throw 'EvidenceDirectory must be a new or empty directory to prevent overwriting evidence.'
    }
}
else {
    New-Item -ItemType Directory -Path $evidenceDirectory | Out-Null
}

try {
    if (-not $ImageRef.StartsWith('sha256:', [StringComparison]::Ordinal)) {
        & docker pull $ImageRef
        if ($LASTEXITCODE -ne 0) { throw 'Could not pull the authorized immutable image.' }
    }

    $inspectPath = Join-Path $evidenceDirectory 'image-inspect.json'
    $inspectionJson = @(& docker image inspect $ImageRef)
    if ($LASTEXITCODE -ne 0) { throw 'Image inspection failed.' }
    $utf8WithoutBom = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText(
        $inspectPath,
        ($inspectionJson -join [Environment]::NewLine),
        $utf8WithoutBom)
    $inspection = ($inspectionJson | ConvertFrom-Json)[0]
    if ($inspection.Config.User -ne '1654:1654') { throw 'Image is not configured as UID/GID 1654.' }
    if ($inspection.Config.ExposedPorts.PSObject.Properties.Name -notcontains '8080/tcp') { throw 'Image does not expose only the internal Web port.' }
    if (-not $inspection.Config.Healthcheck) { throw 'Image healthcheck is missing.' }

    & docker run --rm --entrypoint dotnet $ImageRef Sgol.Worker.dll --help
    if ($LASTEXITCODE -ne 0) { throw 'Worker command smoke failed.' }
    & docker run --rm --entrypoint /usr/bin/pg_dump $ImageRef --version
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump is missing.' }
    & docker run --rm --entrypoint /usr/bin/age $ImageRef --version
    if ($LASTEXITCODE -ne 0) { throw 'age is missing.' }

    $env:SGOL_IMAGE_REF = $ImageRef
    $env:SGOL_ENV_FILE = $runtimeEnvironmentPath
    $env:SGOL_EXPECTED_MIGRATION = '20260912213000_AddPortableDataProtectionKeyRing'
    $env:SGOL_BACKUP_MANIFEST_URI = $BackupManifestUri
    $env:SGOL_REPLICA_MANIFEST_URI = $ReplicaManifestUri
    & docker compose --env-file $runtimeEnvironmentPath `
        -f (Join-Path $repositoryRoot 'deploy/staging/compose.yaml') config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Staging Compose rendering failed.' }

    & dotnet test (Join-Path $repositoryRoot 'tests/Sgol.IntegrationTests/Sgol.IntegrationTests.csproj') `
        --configuration Release `
        --filter 'FullyQualifiedName~Migrations_CreateOnlyTheApprovedTables|FullyQualifiedName~PortableDataProtectionKeyRingIsEncryptedAndReadableAfterRedeploy|FullyQualifiedName~StableJobLockPreventsDifferentSlotsFromOverlapping' `
        --logger "trx;LogFileName=$(Join-Path $evidenceDirectory 'tech-ops-postgresql.trx')"
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL migration, key-ring or locking tests failed.' }

    $env:SGOL_TECH_OPS_EXTERNAL_TESTS = 'true'
    & dotnet test (Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
        --configuration Release --filter 'Category=TechOpsExternal' `
        --logger "trx;LogFileName=$(Join-Path $evidenceDirectory 'tech-ops-external.trx')"
    if ($LASTEXITCODE -ne 0) { throw 'S3-compatible external tests failed.' }

    $compose = Join-Path $repositoryRoot 'deploy/staging/compose.yaml'
    & docker compose --env-file $runtimeEnvironmentPath -f $compose run --rm migrate
    if ($LASTEXITCODE -ne 0) { throw 'Expand-only migration gate failed.' }

    $backupSlots = @($runtimeEnvironment | Where-Object { $_ -match '^SGOL_BACKUP_SCHEDULED_FOR=' })
    if ($backupSlots.Count -ne 1) { throw 'The synthetic environment must define SGOL_BACKUP_SCHEDULED_FOR once.' }
    $env:SGOL_SCHEDULED_FOR = $backupSlots[0].Split('=', 2)[1]
    if ($env:SGOL_SCHEDULED_FOR -notmatch '^\d{4}-\d{2}-\d{2}T02:15:00Z$') {
        throw 'SGOL_BACKUP_SCHEDULED_FOR must select a synthetic daily slot at 02:15:00Z.'
    }
    & docker compose --env-file $runtimeEnvironmentPath -f $compose run --rm backup
    if ($LASTEXITCODE -ne 0) { throw 'Encrypted PostgreSQL backup gate failed.' }
    & docker compose --env-file $runtimeEnvironmentPath -f $compose run --rm backup
    if ($LASTEXITCODE -ne 0) { throw 'Idempotent PostgreSQL backup retry gate failed.' }

    $replicaSlots = @($runtimeEnvironment | Where-Object { $_ -match '^SGOL_REPLICA_SCHEDULED_FOR=' })
    if ($replicaSlots.Count -ne 1) { throw 'The synthetic environment must define SGOL_REPLICA_SCHEDULED_FOR once.' }
    $env:SGOL_SCHEDULED_FOR = $replicaSlots[0].Split('=', 2)[1]
    if ($env:SGOL_SCHEDULED_FOR -notmatch '^\d{4}-\d{2}-\d{2}T\d{2}:05:00Z$') {
        throw 'SGOL_REPLICA_SCHEDULED_FOR must select a synthetic hourly slot at minute 05 UTC.'
    }
    & docker compose --env-file $runtimeEnvironmentPath -f $compose run --rm replica
    if ($LASTEXITCODE -ne 0) { throw 'Incremental object replica gate failed.' }
    & docker compose --env-file $runtimeEnvironmentPath -f $compose run --rm replica
    if ($LASTEXITCODE -ne 0) { throw 'Idempotent object replica retry gate failed.' }
    & docker compose --env-file $runtimeEnvironmentPath -f $compose run --rm verify-backup
    if ($LASTEXITCODE -ne 0) { throw 'Isolated PostgreSQL restore gate failed.' }
    & docker compose --env-file $runtimeEnvironmentPath -f $compose run --rm verify-replica
    if ($LASTEXITCODE -ne 0) { throw 'Independent object replica verification gate failed.' }

    Write-Output "PASS: external TECH-OPS image, PostgreSQL and S3-compatible gates. Evidence: $evidenceDirectory"
}
finally {
    @(
        'SGOL_IMAGE_REF','SGOL_ENV_FILE','SGOL_EXPECTED_MIGRATION','SGOL_SCHEDULED_FOR',
        'SGOL_BACKUP_MANIFEST_URI','SGOL_REPLICA_MANIFEST_URI',
        'SGOL_TECH_OPS_EXTERNAL_TESTS'
    ) | ForEach-Object { Remove-Item -Path "Env:$_" -ErrorAction SilentlyContinue }
}
