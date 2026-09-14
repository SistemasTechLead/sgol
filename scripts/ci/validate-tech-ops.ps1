[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$requiredFiles = @(
    'Dockerfile',
    '.dockerignore',
    '.gitleaksignore',
    'deploy/staging/compose.yaml',
    'deploy/staging/staging.env',
    'deploy/staging/secrets.example',
    'scripts/operations/build-tech-ops-image.ps1',
    'scripts/operations/new-tech-ops-synthetic-environment.ps1',
    'scripts/operations/invoke-tech-ops-amd64-gate.ps1',
    'F07_ADENDA_32_CONTRATO_DE_OPERACION_PORTABLE_TECH_OPS_001.md'
)

foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $relativePath) -PathType Leaf)) {
        throw "TECH-OPS required file is missing: $relativePath"
    }
}

$expectedGitleaksFingerprint = '401b422efa2a02a87f325311c92543c795dff75c:F07_ADENDA_32_CONTRATO_DE_OPERACION_PORTABLE_TECH_OPS_001.md:generic-api-key:331'
$gitleaksIgnoreEntries = @(
    Get-Content -LiteralPath (Join-Path $repositoryRoot '.gitleaksignore') |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith('#', [StringComparison]::Ordinal) }
)
if ($gitleaksIgnoreEntries.Count -ne 1 -or $gitleaksIgnoreEntries[0] -ne $expectedGitleaksFingerprint) {
    throw '.gitleaksignore must contain only the reviewed historical false-positive fingerprint.'
}

$dockerfile = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Dockerfile')
$digestPattern = '@sha256:[0-9a-f]{64}'
if ([regex]::Matches($dockerfile, $digestPattern).Count -lt 2) {
    throw 'Dockerfile must pin SDK and runtime images by digest.'
}
foreach ($required in @(
    'FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:',
    'FROM restore AS publish',
    'COPY .editorconfig Directory.Build.props Directory.Packages.props global.json NuGet.config ./',
    'dotnet restore src/Sgol.Worker/Sgol.Worker.csproj --locked-mode',
    '-p:UseAppHost=false',
    'USER 1654:1654',
    'com.sgol.source.dirty="${SOURCE_DIRTY}"',
    'EXPOSE 8080',
    'Sgol.Web.dll',
    'Sgol.Worker',
    'Sgol.Operations',
    'HEALTHCHECK'
)) {
    if ($dockerfile.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Dockerfile contract is missing: $required"
    }
}
if ($dockerfile -match '(?im)^\s*(ENV|ARG)\s+.*(PASSWORD|SECRET|ACCESS_KEY)\s*=\s*[^\s$]') {
    throw 'Dockerfile contains a secret-like value.'
}

$manifest = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'deploy/staging/compose.yaml')
foreach ($required in @(
    '${SGOL_IMAGE_REF:?',
    'user: "1654:1654"',
    'read_only: true',
    'no-new-privileges:true',
    'cap_drop:',
    '/health/ready',
    'POSTGRESQL_PORTABLE_BACKUP',
    'REPLICATE_EVIDENCE_OBJECTS',
    'GENERATE_DUE_RECURRENCES',
    'CLEAN_EXPIRED_EVIDENCE_UPLOADS'
)) {
    if ($manifest.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Staging manifest contract is missing: $required"
    }
}
if ($manifest -match '(?im)^\s*build\s*:') {
    throw 'Staging must consume one immutable image and cannot build implicitly.'
}
if ($manifest -match '(?i)(AKIA[0-9A-Z]{16}|-----BEGIN .*PRIVATE KEY-----|postgres(?:ql)?://[^\s$]+:[^\s$]+@)') {
    throw 'Staging manifest contains a secret-like value.'
}

$secretLines = Get-Content -LiteralPath (Join-Path $repositoryRoot 'deploy/staging/secrets.example')
$secretNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in $secretLines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line -notmatch '^([A-Za-z][A-Za-z0-9_]*)(__[A-Za-z0-9_]+)*=REQUIRED_EXTERNAL_SECRET$') {
        throw "Secret inventory contains a value or invalid name: $line"
    }
    $name = $line.Split('=', 2)[0]
    if (-not $secretNames.Add($name)) {
        throw "Duplicate secret name: $name"
    }
}
if ($secretNames.Count -ne 15) {
    throw "Expected 15 external secret names; found $($secretNames.Count)."
}
$expectedSecretNames = @(
    'ConnectionStrings__Sgol',
    'Evidence__Storage__AccessKey', 'Evidence__Storage__SecretKey',
    'Backup__PostgreSql__ConnectionString', 'Backup__Storage__AccessKey', 'Backup__Storage__SecretKey',
    'Replica__Source__AccessKey', 'Replica__Source__SecretKey',
    'Replica__Destination__AccessKey', 'Replica__Destination__SecretKey',
    'DataProtection__WrappingCertificate', 'DataProtection__WrappingCertificatePassword',
    'Restore__PostgreSql__ConnectionString', 'Restore__Encryption__Identity',
    'OTEL_EXPORTER_OTLP_HEADERS'
)
foreach ($expectedSecretName in $expectedSecretNames) {
    if (-not $secretNames.Contains($expectedSecretName)) {
        throw "Secret inventory is missing: $expectedSecretName"
    }
}

$stagingConfiguration = Get-Content -LiteralPath (Join-Path $repositoryRoot 'deploy/staging/staging.env')
if ($stagingConfiguration | Where-Object { $_ -match '(?i)(Password|SecretKey|AccessKey)=' }) {
    throw 'Versioned staging configuration contains secret fields.'
}
foreach ($requiredSetting in @(
    'Backup__MaximumAttempts=3', 'Backup__StorageTimeoutSeconds=60',
    'Backup__ProcessTimeoutSeconds=900', 'Replica__MaximumAttempts=3',
    'Replica__TimeoutSeconds=60', 'Replica__BatchSize=500'
)) {
    if ($stagingConfiguration -notcontains $requiredSetting) {
        throw "Versioned staging configuration is missing: $requiredSetting"
    }
}

$migrationPath = Join-Path $repositoryRoot 'src/Sgol.Web/Infrastructure/Persistence/Migrations/20260912213000_AddPortableDataProtectionKeyRing.cs'
$migrationBytes = [IO.File]::ReadAllBytes($migrationPath)
if ($migrationBytes.Length -ge 3 -and $migrationBytes[0] -eq 0xEF -and $migrationBytes[1] -eq 0xBB -and $migrationBytes[2] -eq 0xBF) {
    throw 'TECH-OPS migration contains a UTF-8 BOM.'
}
$migration = [Text.Encoding]::UTF8.GetString($migrationBytes)
if ($migration.IndexOf('throw new NotSupportedException', [StringComparison]::Ordinal) -lt 0) {
    throw 'TECH-OPS migration Down() must be blocked.'
}

$forbiddenSources = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src') -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]|[\\/]bin[\\/]' }
$forbiddenText = $forbiddenSources | Select-String -Pattern 'DigitalOcean|Kubernetes|StackExchange\.Redis' -CaseSensitive:$false
if ($forbiddenText) {
    throw 'Provider, Kubernetes or Redis dependency detected in source.'
}

$protected = & git -C $repositoryRoot status --short -- Fuentes docs/design/logo.svg docs/design/mapa-pantallas.md
if ($LASTEXITCODE -ne 0) { throw 'Could not verify protected paths.' }
if ($protected) { throw "Protected paths changed: $protected" }

foreach ($scriptPath in @(
    (Join-Path $repositoryRoot 'scripts/ci/validate-tech-ops.ps1'),
    (Join-Path $repositoryRoot 'scripts/operations/build-tech-ops-image.ps1'),
    (Join-Path $repositoryRoot 'scripts/operations/new-tech-ops-synthetic-environment.ps1'),
    (Join-Path $repositoryRoot 'scripts/operations/invoke-tech-ops-amd64-gate.ps1'),
    (Join-Path $repositoryRoot 'scripts/operations/verify-tech-ops-external.ps1')
)) {
    $tokens = $null
    $parseErrors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -ne 0) {
        throw "PowerShell syntax validation failed: $scriptPath"
    }
}

$imageBuilder = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'scripts/operations/build-tech-ops-image.ps1')
foreach ($required in @(
    '[IO.File]::Open',
    '--sbom=true',
    '--provenance=mode=max',
    '--metadata-file',
    '1654:1654',
    '/usr/bin/pg_dump',
    '/usr/bin/age'
)) {
    if ($imageBuilder.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "OCI builder contract is missing: $required"
    }
}
if ($imageBuilder -match '(?i)Fuentes|git\s+(?:add|commit|push)|docker\s+(?:login|push)') {
    throw 'OCI builder references a protected or externally mutating operation.'
}

$syntheticProvisioner = Get-Content -Raw -LiteralPath (
    Join-Path $repositoryRoot 'scripts/operations/new-tech-ops-synthetic-environment.ps1')
foreach ($required in @(
    'SGOL_SYNTHETIC_ONLY=true',
    'sgol_primary', 'sgol_restore',
    'sgol-staging-evidence-quarantine', 'sgol-staging-evidence-clean',
    'sgol-staging-evidence-quarantine-replica', 'sgol-staging-evidence-clean-replica',
    'sgol-staging-portable-backups',
    'source-provisioner', 'destination-provisioner',
    'SGOL_TECH_OPS_PROVISIONING_TESTS',
    'docker network disconnect bridge'
)) {
    if ($syntheticProvisioner.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Synthetic provisioner contract is missing: $required"
    }
}
if ($syntheticProvisioner -match '(?i)Fuentes|git\s+(?:add|commit|push)|docker\s+(?:rm|rmi|system\s+prune|login|push)') {
    throw 'Synthetic provisioner references deletion, a protected path or an external publication operation.'
}

$amd64Gate = Get-Content -Raw -LiteralPath (
    Join-Path $repositoryRoot 'scripts/operations/invoke-tech-ops-amd64-gate.ps1')
foreach ($required in @(
    'AMD64_HOST_REQUIRED',
    "Architecture]::X64",
    "Architecture -ne 'amd64'",
    'runtime-private',
    'evidence-private',
    'evidence-public',
    'new-tech-ops-synthetic-environment.ps1',
    'verify-tech-ops-external.ps1'
)) {
    if ($amd64Gate.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Native AMD64 gate contract is missing: $required"
    }
}

$workflow = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot '.github/workflows/pull-request.yml')
foreach ($required in @(
    'runs-on: ubuntu-24.04',
    'IMPLEMENTATION_SHA: ${{ github.event.pull_request.head.sha }}',
    'ref: ${{ github.event.pull_request.head.sha }}',
    '--driver docker-container',
    'test "$(git rev-parse HEAD)" = "$IMPLEMENTATION_SHA"',
    'invoke-tech-ops-amd64-gate.ps1',
    'actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a',
    '${{ runner.temp }}/sgol-tech-ops-amd64/evidence-public/**'
)) {
    if ($workflow.IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "AMD64 workflow contract is missing: $required"
    }
}
if ($workflow.IndexOf('GITHUB_SHA', [StringComparison]::Ordinal) -ge 0 -or
    $workflow.IndexOf('${{ github.sha }}', [StringComparison]::Ordinal) -ge 0) {
    throw 'Pull-request OCI evidence must identify the exact implementation head SHA, not the synthetic merge SHA.'
}
if ($workflow -match '(?im)^\s*\$\{\{ runner\.temp \}\}/sgol-tech-ops-amd64/runtime-private') {
    throw 'Private synthetic runtime must never be uploaded as a CI artifact.'
}
if ($workflow -match '(?im)^\s*\$\{\{ runner\.temp \}\}/sgol-tech-ops-amd64/evidence-private') {
    throw 'Private TECH-OPS evidence must never be uploaded as a CI artifact.'
}

Write-Output "PASS: TECH-OPS static contract validated ($($requiredFiles.Count) files, $($secretNames.Count) secret names)."
