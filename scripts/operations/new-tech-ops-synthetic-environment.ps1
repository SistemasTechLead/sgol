[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^sha256:[0-9a-f]{64}$')]
    [string]$ImageRef,

    [ValidateScript({ -not (Test-Path -LiteralPath $_ -PathType Leaf) })]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
function Test-AbsolutePath([string]$value) {
    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        return $value -match '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+)'
    }
    return $value.StartsWith('/', [StringComparison]::Ordinal)
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $temporaryRoot ('sgol-tech-ops-runtime-' + [guid]::NewGuid().ToString('N'))
}
elseif (-not (Test-AbsolutePath $OutputDirectory)) {
    throw 'OutputDirectory must be an absolute path outside the repository.'
}
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if ($outputPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Synthetic credentials and configuration must remain outside the repository.'
}
if (Test-Path -LiteralPath $outputPath) {
    if (-not (Test-Path -LiteralPath $outputPath -PathType Container) -or
        (Get-ChildItem -LiteralPath $outputPath -Force | Select-Object -First 1)) {
        throw 'OutputDirectory must be a new or empty directory to prevent overwriting external evidence.'
    }
}
else {
    New-Item -ItemType Directory -Path $outputPath | Out-Null
}

$utf8WithoutBom = New-Object Text.UTF8Encoding($false)
$postgresContainer = 'sgol-tech-ops-postgres'
$sourceContainer = 'sgol-tech-ops-s3-source'
$destinationContainer = 'sgol-tech-ops-s3-destination'
$composeNetwork = 'sgol-staging_private'
$postgresImage = 'postgres:18.6-alpine3.23'
$seaweedImage = 'chrislusf/seaweedfs:4.45@sha256:fc9f76fa993ad69966ffeb2f65d0318fcae39c6f8e20cf68ef7b3a5cb97769e5'
$sourceQuarantine = 'sgol-staging-evidence-quarantine'
$sourceClean = 'sgol-staging-evidence-clean'
$destinationQuarantine = 'sgol-staging-evidence-quarantine-replica'
$destinationClean = 'sgol-staging-evidence-clean-replica'
$manifestBucket = 'sgol-staging-portable-backups'
$backupSlot = '2026-09-12T02:15:00Z'
$replicaSlot = '2026-09-12T23:05:00Z'

function New-HexSecret([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    return ([BitConverter]::ToString($buffer)).Replace('-', '').ToLowerInvariant()
}

function Assert-DockerSuccess([string]$message) {
    if ($LASTEXITCODE -ne 0) { throw $message }
}

function Write-Utf8File([string]$path, [string]$content) {
    [IO.File]::WriteAllText($path, $content, $utf8WithoutBom)
}

function Wait-TcpPort([int]$port, [string]$description) {
    foreach ($attempt in 1..30) {
        $client = New-Object Net.Sockets.TcpClient
        try {
            $result = $client.BeginConnect('127.0.0.1', $port, $null, $null)
            if ($result.AsyncWaitHandle.WaitOne(1000) -and $client.Connected) {
                $client.EndConnect($result)
                return
            }
        }
        catch { }
        finally { $client.Dispose() }
        Start-Sleep -Seconds 1
    }
    throw "$description did not become reachable."
}

function Get-PublishedPort([string]$container) {
    $binding = (& docker port $container '8333/tcp').Trim()
    Assert-DockerSuccess "Could not resolve the published S3 port for $container."
    if ($binding -notmatch ':(\d+)$') { throw "Invalid S3 port binding for $container." }
    return [int]$Matches[1]
}

function New-FreeLoopbackPort {
    $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally { $listener.Stop() }
}

function Get-ContainerAddress([string]$container, [string]$network) {
    $inspection = (& docker inspect $container | ConvertFrom-Json)[0]
    Assert-DockerSuccess "Could not inspect $container."
    $networkProperty = $inspection.NetworkSettings.Networks.PSObject.Properties[$network]
    if ($null -eq $networkProperty -or $networkProperty.Value.IPAddress -notmatch '^(?:10\.|192\.168\.|172\.(?:1[6-9]|2\d|3[01])\.)') {
        throw "$container does not have an RFC1918 address on the private Compose network."
    }
    return $networkProperty.Value.IPAddress
}

function New-S3Configuration([array]$identities, [string]$path) {
    Write-Utf8File $path ((@{ identities = $identities } | ConvertTo-Json -Depth 8 -Compress))
}

function Set-ProvisionEnvironment([string]$phase, [int]$sourcePort, [int]$destinationPort) {
    $env:SGOL_TECH_OPS_PROVISIONING_TESTS = 'true'
    $env:SGOL_PROVISION_PHASE = $phase
    $env:SGOL_PROVISION_SOURCE_ENDPOINT = "http://127.0.0.1:$sourcePort"
    $env:SGOL_PROVISION_DESTINATION_ENDPOINT = "http://127.0.0.1:$destinationPort"
    $env:SGOL_PROVISION_SOURCE_QUARANTINE_BUCKET = $sourceQuarantine
    $env:SGOL_PROVISION_SOURCE_CLEAN_BUCKET = $sourceClean
    $env:SGOL_PROVISION_DESTINATION_QUARANTINE_BUCKET = $destinationQuarantine
    $env:SGOL_PROVISION_DESTINATION_CLEAN_BUCKET = $destinationClean
    $env:SGOL_PROVISION_MANIFEST_BUCKET = $manifestBucket
    $env:SGOL_PROVISION_SOURCE_ADMIN_ACCESS_KEY = $sourceAdminKey
    $env:SGOL_PROVISION_SOURCE_ADMIN_SECRET_KEY = $sourceAdminSecret
    $env:SGOL_PROVISION_DESTINATION_ADMIN_ACCESS_KEY = $destinationAdminKey
    $env:SGOL_PROVISION_DESTINATION_ADMIN_SECRET_KEY = $destinationAdminSecret
    $env:SGOL_PROVISION_EVIDENCE_ACCESS_KEY = $evidenceKey
    $env:SGOL_PROVISION_EVIDENCE_SECRET_KEY = $evidenceSecret
    $env:SGOL_PROVISION_REPLICA_SOURCE_ACCESS_KEY = $replicaSourceKey
    $env:SGOL_PROVISION_REPLICA_SOURCE_SECRET_KEY = $replicaSourceSecret
    $env:SGOL_PROVISION_BACKUP_ACCESS_KEY = $backupKey
    $env:SGOL_PROVISION_BACKUP_SECRET_KEY = $backupSecret
    $env:SGOL_PROVISION_REPLICA_DESTINATION_ACCESS_KEY = $replicaDestinationKey
    $env:SGOL_PROVISION_REPLICA_DESTINATION_SECRET_KEY = $replicaDestinationSecret
}

foreach ($resource in @($postgresContainer, $sourceContainer, $destinationContainer)) {
    $existingContainer = @(& docker container ls --all --filter "name=^/$resource`$" --format '{{.Names}}')
    Assert-DockerSuccess "Could not inspect the reserved container name: $resource"
    if ($existingContainer -contains $resource) { throw "Refusing to replace existing container: $resource" }
}
$existingNetwork = @(& docker network ls --filter "name=^$composeNetwork`$" --format '{{.Name}}')
Assert-DockerSuccess "Could not inspect the reserved network name: $composeNetwork"
if ($existingNetwork -contains $composeNetwork) { throw "Refusing to reuse existing network: $composeNetwork" }
& docker image inspect $ImageRef *> $null
Assert-DockerSuccess 'The authorized local OCI image is not available.'

$postgresAdminPassword = New-HexSecret 24
$appPassword = New-HexSecret 24
$backupDatabasePassword = New-HexSecret 24
$restorePassword = New-HexSecret 24
$sourceAdminKey = New-HexSecret 12
$sourceAdminSecret = New-HexSecret 32
$destinationAdminKey = New-HexSecret 12
$destinationAdminSecret = New-HexSecret 32
$evidenceKey = New-HexSecret 12
$evidenceSecret = New-HexSecret 32
$replicaSourceKey = New-HexSecret 12
$replicaSourceSecret = New-HexSecret 32
$backupKey = New-HexSecret 12
$backupSecret = New-HexSecret 32
$replicaDestinationKey = New-HexSecret 12
$replicaDestinationSecret = New-HexSecret 32
$pfxPassword = New-HexSecret 24

$previousErrorPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    $ageOutput = @(& docker run --rm --platform linux/amd64 --entrypoint /usr/bin/age-keygen $ImageRef 2>&1)
    $ageExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorPreference
}
if ($ageExitCode -ne 0) { throw 'Could not generate the ephemeral age identity.' }
$ageIdentity = $ageOutput | Where-Object { $_ -match '^AGE-SECRET-KEY-' } | Select-Object -First 1
$ageRecipientLine = $ageOutput | Where-Object { $_ -match 'Public key:\s+(age1\S+)' } | Select-Object -First 1
if (-not $ageIdentity -or $ageRecipientLine -notmatch 'Public key:\s+(age1\S+)') {
    throw 'age-keygen did not return the expected identity and recipient.'
}
$ageRecipient = $Matches[1]

$rsa = [Security.Cryptography.RSA]::Create(2048)
try {
    $request = New-Object Security.Cryptography.X509Certificates.CertificateRequest(
        'CN=SGOL TECH-OPS-001 synthetic', $rsa,
        [Security.Cryptography.HashAlgorithmName]::SHA256,
        [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(2))
    try {
        $pfx = $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $pfxPassword)
        $pfxBase64 = [Convert]::ToBase64String($pfx)
    }
    finally { $certificate.Dispose() }
}
finally { $rsa.Dispose() }

$sourceConfigPath = Join-Path $outputPath 'source-s3.json'
$destinationConfigPath = Join-Path $outputPath 'destination-s3.json'
$runtimePath = Join-Path $outputPath 'runtime.env'
$sourceOperationalIdentities = @(
    @{ name='evidence-web'; credentials=@(@{ accessKey=$evidenceKey; secretKey=$evidenceSecret }); actions=@("Read:$sourceQuarantine", "Write:$sourceQuarantine", "List:$sourceQuarantine", "Tagging:$sourceQuarantine", "Read:$sourceClean", "Write:$sourceClean", "List:$sourceClean", "Tagging:$sourceClean") },
    @{ name='replica-source'; credentials=@(@{ accessKey=$replicaSourceKey; secretKey=$replicaSourceSecret }); actions=@("Read:$sourceQuarantine", "List:$sourceQuarantine", "Read:$sourceClean", "List:$sourceClean") }
)
$destinationOperationalIdentities = @(
    @{ name='backup'; credentials=@(@{ accessKey=$backupKey; secretKey=$backupSecret }); actions=@("Read:$manifestBucket", "Write:$manifestBucket", "List:$manifestBucket", "Tagging:$manifestBucket") },
    @{ name='replica-destination'; credentials=@(@{ accessKey=$replicaDestinationKey; secretKey=$replicaDestinationSecret }); actions=@("Read:$destinationQuarantine", "Write:$destinationQuarantine", "List:$destinationQuarantine", "Tagging:$destinationQuarantine", "Read:$destinationClean", "Write:$destinationClean", "List:$destinationClean", "Tagging:$destinationClean", "Read:$manifestBucket", "Write:$manifestBucket", "List:$manifestBucket", "Tagging:$manifestBucket") }
)
New-S3Configuration (@(@{ name='source-provisioner'; credentials=@(@{ accessKey=$sourceAdminKey; secretKey=$sourceAdminSecret }); actions=@('Admin','Read','Write','List','Tagging') }) + $sourceOperationalIdentities) $sourceConfigPath
New-S3Configuration (@(@{ name='destination-provisioner'; credentials=@(@{ accessKey=$destinationAdminKey; secretKey=$destinationAdminSecret }); actions=@('Admin','Read','Write','List','Tagging') }) + $destinationOperationalIdentities) $destinationConfigPath

function Write-RuntimeEnvironment([string]$sourceAddress, [string]$destinationAddress) {
    $revision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    Assert-DockerSuccess 'Could not resolve the Git revision.'
    $lines = @(
        'SGOL_SYNTHETIC_ONLY=true', 'ASPNETCORE_ENVIRONMENT=Staging', 'ASPNETCORE_URLS=http://+:8080',
        'TZ=Etc/UTC', 'DOTNET_EnableDiagnostics=0', "SGOL_REVISION=$revision", "SGOL_IMAGE_DIGEST=$ImageRef",
        "Evidence__Storage__Endpoint=http://$sourceAddress`:8333", 'Evidence__Storage__Region=us-east-1',
        "Evidence__Storage__QuarantineBucket=$sourceQuarantine", "Evidence__Storage__CleanBucket=$sourceClean",
        'Evidence__Storage__AllowInsecureTransport=true', 'Evidence__Storage__AllowedUploadOrigins__0=https://synthetic.sgol.invalid',
        'Evidence__Scanner__Host=synthetic-scanner.invalid', 'Evidence__Scanner__Port=3310',
        'Evidence__Scanner__ConnectTimeoutSeconds=3', 'Evidence__Scanner__ScanTimeoutSeconds=30',
        'DataProtection__ApplicationName=SGOL',
        "Backup__Storage__Endpoint=http://$destinationAddress`:8333", 'Backup__Storage__Region=us-east-1',
        "Backup__Storage__Bucket=$manifestBucket", 'Backup__Storage__Prefix=postgresql/v1',
        'Backup__Storage__AllowInsecureTransport=true', "Backup__Encryption__Recipient=$ageRecipient",
        'Backup__PgDumpPath=/usr/bin/pg_dump', 'Backup__AgePath=/usr/bin/age', 'Backup__MaximumAttempts=3',
        'Backup__StorageTimeoutSeconds=60', 'Backup__ProcessTimeoutSeconds=900',
        "Replica__Source__Endpoint=http://$sourceAddress`:8333", 'Replica__Source__Region=us-east-1',
        "Replica__Source__QuarantineBucket=$sourceQuarantine", "Replica__Source__CleanBucket=$sourceClean",
        'Replica__Source__AllowInsecureTransport=true',
        "Replica__Destination__Endpoint=http://$destinationAddress`:8333", 'Replica__Destination__Region=us-east-1',
        "Replica__Destination__QuarantineBucket=$destinationQuarantine", "Replica__Destination__CleanBucket=$destinationClean",
        "Replica__Destination__ManifestBucket=$manifestBucket", 'Replica__Destination__ManifestPrefix=objects/v1',
        'Replica__Destination__AllowInsecureTransport=true', 'Replica__MaximumAttempts=3', 'Replica__TimeoutSeconds=60',
        'Replica__BatchSize=500', 'Restore__ExpectedMigration=20260912213000_AddPortableDataProtectionKeyRing',
        "ConnectionStrings__Sgol=Host=$postgresContainer;Port=5432;Database=sgol_primary;Username=sgol_app;Password=$appPassword;SSL Mode=Disable",
        "Evidence__Storage__AccessKey=$evidenceKey", "Evidence__Storage__SecretKey=$evidenceSecret",
        "Backup__PostgreSql__ConnectionString=Host=$postgresContainer;Port=5432;Database=sgol_primary;Username=sgol_backup;Password=$backupDatabasePassword;SSL Mode=Disable",
        "Backup__Storage__AccessKey=$backupKey", "Backup__Storage__SecretKey=$backupSecret",
        "Replica__Source__AccessKey=$replicaSourceKey", "Replica__Source__SecretKey=$replicaSourceSecret",
        "Replica__Destination__AccessKey=$replicaDestinationKey", "Replica__Destination__SecretKey=$replicaDestinationSecret",
        "DataProtection__WrappingCertificate=$pfxBase64", "DataProtection__WrappingCertificatePassword=$pfxPassword",
        "Restore__PostgreSql__ConnectionString=Host=$postgresContainer;Port=5432;Database=sgol_restore;Username=sgol_restore;Password=$restorePassword;SSL Mode=Disable",
        "Restore__Encryption__Identity=$ageIdentity", 'OTEL_EXPORTER_OTLP_HEADERS=synthetic=true',
        "SGOL_BACKUP_SCHEDULED_FOR=$backupSlot", "SGOL_REPLICA_SCHEDULED_FOR=$replicaSlot",
        "SGOL_SCHEDULED_FOR=$backupSlot",
        "SGOL_BACKUP_MANIFEST_URI=s3://$manifestBucket/postgresql/v1/2026/09/12/sgol-20260912T021500Z.dump.age.manifest.json",
        "SGOL_REPLICA_MANIFEST_URI=s3://$manifestBucket/objects/v1/2026/09/12/objects-20260912T230500Z.manifest.json",
        "SGOL_IMAGE_REF=$ImageRef", "SGOL_ENV_FILE=$runtimePath",
        'SGOL_EXPECTED_MIGRATION=20260912213000_AddPortableDataProtectionKeyRing'
    )
    Write-Utf8File $runtimePath (($lines -join [Environment]::NewLine) + [Environment]::NewLine)
}

Write-RuntimeEnvironment '10.0.0.1' '10.0.0.2'
$env:SGOL_IMAGE_REF = $ImageRef
$env:SGOL_ENV_FILE = $runtimePath
$env:SGOL_EXPECTED_MIGRATION = '20260912213000_AddPortableDataProtectionKeyRing'
$compose = Join-Path $repositoryRoot 'deploy/staging/compose.yaml'
& docker compose --env-file $runtimePath -f $compose create migrate
Assert-DockerSuccess 'Could not create the isolated Compose network.'
& docker network inspect $composeNetwork *> $null
Assert-DockerSuccess 'Compose did not create the expected private network.'

& docker run --detach --name $postgresContainer --network $composeNetwork `
    --env "POSTGRES_PASSWORD=$postgresAdminPassword" --env 'POSTGRES_USER=postgres' --env 'POSTGRES_DB=postgres' `
    --health-cmd 'pg_isready -U postgres -d postgres' --health-interval 1s --health-timeout 3s --health-retries 30 `
    $postgresImage | Out-Null
Assert-DockerSuccess 'Could not create synthetic PostgreSQL.'
foreach ($attempt in 1..30) {
    $health = (& docker inspect --format '{{.State.Health.Status}}' $postgresContainer).Trim()
    if ($health -eq 'healthy') { break }
    if ($attempt -eq 30) { throw 'Synthetic PostgreSQL did not become healthy.' }
    Start-Sleep -Seconds 1
}
foreach ($sql in @(
    "CREATE ROLE sgol_app LOGIN PASSWORD '$appPassword'",
    "CREATE ROLE sgol_backup LOGIN PASSWORD '$backupDatabasePassword'",
    "CREATE ROLE sgol_restore LOGIN PASSWORD '$restorePassword'",
    'CREATE DATABASE sgol_primary OWNER sgol_app',
    'CREATE DATABASE sgol_restore OWNER sgol_restore',
    'GRANT CONNECT ON DATABASE sgol_primary TO sgol_backup'
)) {
    & docker exec --env "PGPASSWORD=$postgresAdminPassword" $postgresContainer `
        psql -v ON_ERROR_STOP=1 -U postgres -d postgres -c $sql | Out-Null
    Assert-DockerSuccess 'Could not create one of the isolated PostgreSQL roles or databases.'
}
$grants = @"
GRANT USAGE ON SCHEMA public TO sgol_backup;
ALTER DEFAULT PRIVILEGES FOR ROLE sgol_app IN SCHEMA public GRANT SELECT ON TABLES TO sgol_backup;
ALTER DEFAULT PRIVILEGES FOR ROLE sgol_app IN SCHEMA public GRANT SELECT ON SEQUENCES TO sgol_backup;
"@
& docker exec --env "PGPASSWORD=$postgresAdminPassword" $postgresContainer psql -v ON_ERROR_STOP=1 -U postgres -d sgol_primary -c $grants | Out-Null
Assert-DockerSuccess 'Could not apply read-only backup grants.'

$sourceHostPort = New-FreeLoopbackPort
$destinationHostPort = New-FreeLoopbackPort
if ($sourceHostPort -eq $destinationHostPort) { $destinationHostPort = New-FreeLoopbackPort }
& docker run --detach --name $sourceContainer --network $composeNetwork --publish "127.0.0.1:$sourceHostPort`:8333" `
    --mount "type=bind,source=$sourceConfigPath,target=/run/sgol/s3.json,readonly" `
    $seaweedImage mini '-dir=/data' '-s3.config=/run/sgol/s3.json' | Out-Null
Assert-DockerSuccess 'Could not create source S3-compatible storage.'
& docker run --detach --name $destinationContainer --network $composeNetwork --publish "127.0.0.1:$destinationHostPort`:8333" `
    --mount "type=bind,source=$destinationConfigPath,target=/run/sgol/s3.json,readonly" `
    $seaweedImage mini '-dir=/data' '-s3.config=/run/sgol/s3.json' | Out-Null
Assert-DockerSuccess 'Could not create destination S3-compatible storage.'
& docker network connect bridge $sourceContainer
Assert-DockerSuccess 'Could not attach source S3 temporarily to the local provisioning bridge.'
& docker network connect bridge $destinationContainer
Assert-DockerSuccess 'Could not attach destination S3 temporarily to the local provisioning bridge.'
$sourcePort = Get-PublishedPort $sourceContainer
$destinationPort = Get-PublishedPort $destinationContainer
Wait-TcpPort $sourcePort 'Source S3-compatible storage'
Wait-TcpPort $destinationPort 'Destination S3-compatible storage'

Set-ProvisionEnvironment 'create' $sourcePort $destinationPort
& dotnet test (Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
    --configuration Release --no-restore --filter 'Category=TechOpsProvisioning'
Assert-DockerSuccess 'Could not create the five synthetic buckets and verified seed.'

New-S3Configuration $sourceOperationalIdentities $sourceConfigPath
New-S3Configuration $destinationOperationalIdentities $destinationConfigPath
& docker restart $sourceContainer $destinationContainer | Out-Null
Assert-DockerSuccess 'Could not remove the temporary S3 provisioning identities.'
Wait-TcpPort $sourcePort 'Restricted source S3-compatible storage'
Wait-TcpPort $destinationPort 'Restricted destination S3-compatible storage'
Set-ProvisionEnvironment 'verify' $sourcePort $destinationPort
& dotnet test (Join-Path $repositoryRoot 'tests/Sgol.OperationsIntegrationTests/Sgol.OperationsIntegrationTests.csproj') `
    --configuration Release --no-restore --filter 'Category=TechOpsProvisioning'
Assert-DockerSuccess 'Synthetic bucket permissions or seed verification failed.'
& docker network disconnect bridge $sourceContainer
Assert-DockerSuccess 'Could not detach source S3 from the temporary provisioning bridge.'
& docker network disconnect bridge $destinationContainer
Assert-DockerSuccess 'Could not detach destination S3 from the temporary provisioning bridge.'

$sourceAddress = Get-ContainerAddress $sourceContainer $composeNetwork
$destinationAddress = Get-ContainerAddress $destinationContainer $composeNetwork
Write-RuntimeEnvironment $sourceAddress $destinationAddress
$summaryPath = Join-Path $outputPath 'environment-summary.json'
$summary = [ordered]@{
    kind = 'SGOL_TECH_OPS_SYNTHETIC_ENVIRONMENT'
    syntheticOnly = $true
    imageRef = $ImageRef
    runtimeEnvironmentFile = $runtimePath
    network = $composeNetwork
    containers = @($postgresContainer, $sourceContainer, $destinationContainer)
    databases = @('sgol_primary', 'sgol_restore')
    sourceBuckets = @($sourceQuarantine, $sourceClean)
    destinationBuckets = @($destinationQuarantine, $destinationClean, $manifestBucket)
    backupManifestUri = "s3://$manifestBucket/postgresql/v1/2026/09/12/sgol-20260912T021500Z.dump.age.manifest.json"
    replicaManifestUri = "s3://$manifestBucket/objects/v1/2026/09/12/objects-20260912T230500Z.manifest.json"
}
Write-Utf8File $summaryPath ($summary | ConvertTo-Json -Depth 5)

@(
    'SGOL_IMAGE_REF','SGOL_ENV_FILE','SGOL_EXPECTED_MIGRATION','SGOL_TECH_OPS_PROVISIONING_TESTS',
    'SGOL_PROVISION_PHASE','SGOL_PROVISION_SOURCE_ENDPOINT','SGOL_PROVISION_DESTINATION_ENDPOINT',
    'SGOL_PROVISION_SOURCE_QUARANTINE_BUCKET','SGOL_PROVISION_SOURCE_CLEAN_BUCKET',
    'SGOL_PROVISION_DESTINATION_QUARANTINE_BUCKET','SGOL_PROVISION_DESTINATION_CLEAN_BUCKET',
    'SGOL_PROVISION_MANIFEST_BUCKET','SGOL_PROVISION_SOURCE_ADMIN_ACCESS_KEY','SGOL_PROVISION_SOURCE_ADMIN_SECRET_KEY',
    'SGOL_PROVISION_DESTINATION_ADMIN_ACCESS_KEY','SGOL_PROVISION_DESTINATION_ADMIN_SECRET_KEY',
    'SGOL_PROVISION_EVIDENCE_ACCESS_KEY','SGOL_PROVISION_EVIDENCE_SECRET_KEY',
    'SGOL_PROVISION_REPLICA_SOURCE_ACCESS_KEY','SGOL_PROVISION_REPLICA_SOURCE_SECRET_KEY',
    'SGOL_PROVISION_BACKUP_ACCESS_KEY','SGOL_PROVISION_BACKUP_SECRET_KEY',
    'SGOL_PROVISION_REPLICA_DESTINATION_ACCESS_KEY','SGOL_PROVISION_REPLICA_DESTINATION_SECRET_KEY'
) | ForEach-Object { Remove-Item -Path "Env:$_" -ErrorAction SilentlyContinue }

Write-Output "PASS: synthetic PostgreSQL, five buckets, separated credentials and external runtime file."
Write-Output "RuntimeEnvironmentFile=$runtimePath"
Write-Output "EnvironmentSummary=$summaryPath"
Write-Output "BackupManifestUri=$($summary.backupManifestUri)"
Write-Output "ReplicaManifestUri=$($summary.replicaManifestUri)"
