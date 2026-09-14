[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9./:_-]+$')]
    [string]$Tag = 'sgol:tech-ops-local',

    [ValidateScript({ -not (Test-Path -LiteralPath $_ -PathType Leaf) })]
    [string]$EvidenceDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$contextName = 'sgol-tech-ops-context-' + [guid]::NewGuid().ToString('N')
$contextPath = Join-Path $temporaryRoot $contextName

function Test-AbsolutePath([string]$value) {
    if ([IO.Path]::DirectorySeparatorChar -eq '\') {
        return $value -match '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+)'
    }
    return $value.StartsWith('/', [StringComparison]::Ordinal)
}

if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $temporaryRoot ('sgol-tech-ops-oci-' + [guid]::NewGuid().ToString('N'))
}
elseif (-not (Test-AbsolutePath $EvidenceDirectory)) {
    throw 'EvidenceDirectory must be an absolute path outside the repository.'
}

$evidencePath = [IO.Path]::GetFullPath($EvidenceDirectory)
if ($evidencePath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OCI evidence must remain outside the repository.'
}
if (Test-Path -LiteralPath $evidencePath) {
    if (-not (Test-Path -LiteralPath $evidencePath -PathType Container)) {
        throw 'EvidenceDirectory must identify a directory.'
    }
    if (Get-ChildItem -LiteralPath $evidencePath -Force | Select-Object -First 1) {
        throw 'EvidenceDirectory must be empty to prevent overwriting evidence.'
    }
}
else {
    New-Item -ItemType Directory -Path $evidencePath | Out-Null
}

function Copy-MaterializedFile {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    $destinationDirectory = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    $sourceStream = [IO.File]::Open($Source, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $destinationStream = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
            $sourceStream.CopyTo($destinationStream)
        }
        finally {
            $destinationStream.Dispose()
        }
    }
    finally {
        $sourceStream.Dispose()
    }
}

$rootFiles = @(
    'Dockerfile',
    '.dockerignore',
    '.editorconfig',
    'Directory.Build.props',
    'Directory.Packages.props',
    'global.json',
    'NuGet.config'
)

New-Item -ItemType Directory -Path $contextPath | Out-Null
try {
    foreach ($relativePath in $rootFiles) {
        Copy-MaterializedFile `
            -Source (Join-Path $repositoryRoot $relativePath) `
            -Destination (Join-Path $contextPath $relativePath)
    }

    $sourceRoot = Join-Path $repositoryRoot 'src'
    $sourcePrefix = $sourceRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($sourceFile in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force) {
        if (-not $sourceFile.FullName.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Source file resolved outside the approved OCI context: $($sourceFile.FullName)"
        }
        $relativePath = Join-Path 'src' $sourceFile.FullName.Substring($sourcePrefix.Length)
        $segments = $relativePath -split '[\\/]'
        if ($segments -contains 'bin' -or $segments -contains 'obj') { continue }
        if ($sourceFile.Name -match '^(?i:secrets\.)' -or $sourceFile.Extension -match '^(?i:\.pfx|\.pem|\.key)$') {
            throw "Secret-like source file is not allowed in the OCI context: $relativePath"
        }
        Copy-MaterializedFile -Source $sourceFile.FullName -Destination (Join-Path $contextPath $relativePath)
    }

    $revision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') {
        throw 'Could not resolve the exact Git revision for OCI labels.'
    }
    $created = (& git -C $repositoryRoot show -s --format=%cI HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($created)) {
        throw 'Could not resolve the commit creation timestamp for OCI labels.'
    }
    $workingTreeChanges = @(& git -C $repositoryRoot status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'Could not determine whether the OCI source tree is clean.' }
    $sourceDirty = if ($workingTreeChanges.Count -eq 0) { 'false' } else { 'true' }
    $version = 'tech-ops-' + $revision.Substring(0, 12)
    if ($sourceDirty -eq 'true') { $version += '-dirty' }
    $iidPath = Join-Path $evidencePath 'image.iid'
    $inspectPath = Join-Path $evidencePath 'image-inspect.json'
    $metadataPath = Join-Path $evidencePath 'build-metadata.json'
    $ociPath = Join-Path $evidencePath 'image-with-attestations.oci.tar'

    & docker buildx build `
        --platform linux/amd64 `
        --build-arg "VCS_REF=$revision" `
        --build-arg "IMAGE_VERSION=$version" `
        --build-arg "BUILD_CREATED=$created" `
        --build-arg "SOURCE_DIRTY=$sourceDirty" `
        --tag $Tag `
        --iidfile $iidPath `
        --load $contextPath
    if ($LASTEXITCODE -ne 0) { throw 'OCI image build failed.' }

    $inspectionJson = & docker image inspect $Tag
    if ($LASTEXITCODE -ne 0) { throw 'OCI image inspection failed.' }
    $utf8WithoutBom = New-Object Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($inspectPath, ($inspectionJson -join [Environment]::NewLine), $utf8WithoutBom)
    $inspection = ($inspectionJson | ConvertFrom-Json)[0]
    if ($inspection.Config.User -ne '1654:1654') { throw 'OCI image is not configured as UID/GID 1654.' }
    $exposedPorts = @($inspection.Config.ExposedPorts.PSObject.Properties.Name)
    if ($exposedPorts.Count -ne 1 -or $exposedPorts[0] -ne '8080/tcp') {
        throw 'OCI image must expose only 8080/tcp.'
    }
    if (-not $inspection.Config.Healthcheck) { throw 'OCI image healthcheck is missing.' }
    foreach ($labelName in @(
        'org.opencontainers.image.created',
        'org.opencontainers.image.revision',
        'org.opencontainers.image.version'
    )) {
        if ([string]::IsNullOrWhiteSpace($inspection.Config.Labels.$labelName)) {
            throw "OCI image label is missing: $labelName"
        }
    }
    if ($inspection.Config.Labels.'com.sgol.source.dirty' -ne $sourceDirty) {
        throw 'OCI dirty-source label does not match the working tree state.'
    }

    & docker run --rm --entrypoint dotnet $Tag Sgol.Worker.dll --help
    if ($LASTEXITCODE -ne 0) { throw 'Worker command smoke test failed.' }
    & docker run --rm --entrypoint /usr/bin/pg_dump $Tag --version
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump smoke test failed.' }
    & docker run --rm --entrypoint /usr/bin/age $Tag --version
    if ($LASTEXITCODE -ne 0) { throw 'age smoke test failed.' }

    & docker buildx build `
        --platform linux/amd64 `
        --build-arg "VCS_REF=$revision" `
        --build-arg "IMAGE_VERSION=$version" `
        --build-arg "BUILD_CREATED=$created" `
        --build-arg "SOURCE_DIRTY=$sourceDirty" `
        --sbom=true `
        --provenance=mode=max `
        --metadata-file $metadataPath `
        --output "type=oci,dest=$ociPath" `
        $contextPath
    if ($LASTEXITCODE -ne 0) { throw 'OCI layout build with SBOM and provenance failed.' }

    foreach ($evidenceFile in @($iidPath, $inspectPath, $metadataPath, $ociPath)) {
        if (-not (Test-Path -LiteralPath $evidenceFile -PathType Leaf) -or (Get-Item -LiteralPath $evidenceFile).Length -eq 0) {
            throw "OCI evidence is missing or empty: $evidenceFile"
        }
    }

    $imageId = (Get-Content -Raw -LiteralPath $iidPath).Trim()
    if ($imageId -notmatch '^sha256:[0-9a-f]{64}$') { throw 'BuildKit returned an invalid image ID.' }
    Write-Output "PASS: OCI image, smoke tests, SBOM and provenance layout. ImageRef=$imageId Evidence=$evidencePath"
}
finally {
    $resolvedContext = [IO.Path]::GetFullPath($contextPath)
    $expectedPrefix = $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar + 'sgol-tech-ops-context-'
    if ($resolvedContext.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Parent $resolvedContext) -eq $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar)) {
        if (Test-Path -LiteralPath $resolvedContext -PathType Container) {
            [IO.Directory]::Delete($resolvedContext, $true)
        }
    }
    else {
        Write-Warning "Refused to remove an unexpected OCI context path: $resolvedContext"
    }
}
