[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Automated')]
    [string] $Mode
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar)
$resolvedGitRoot = (& git -C $repositoryRoot rev-parse --show-toplevel).Trim()
$gitExit = $LASTEXITCODE
if ($gitExit -ne 0) {
    [Console]::Error.WriteLine('CV04_PREFLIGHT_GIT_FAILED')
    exit $gitExit
}
$pathComparer = if ($IsLinux -or $IsMacOS) { [StringComparer]::Ordinal } else { [StringComparer]::OrdinalIgnoreCase }
if (-not $pathComparer.Equals([IO.Path]::GetFullPath($resolvedGitRoot), $repositoryRoot)) {
    [Console]::Error.WriteLine('CV04_PREFLIGHT_ROOT_MISMATCH')
    exit 2
}

$previousCommit = $env:CV04_COMMIT
$demoExit = 1
try {
    $env:CV04_COMMIT = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $shaExit = $LASTEXITCODE
    if ($shaExit -ne 0) {
        [Console]::Error.WriteLine('CV04_PREFLIGHT_SHA_FAILED')
        exit $shaExit
    }

    & dotnet run --project (Join-Path $repositoryRoot 'tests/Sgol.Cv04Demo/Sgol.Cv04Demo.csproj') `
        --configuration Release --no-build --no-restore -- --mode $Mode
    $demoExit = $LASTEXITCODE
}
finally {
    $env:CV04_COMMIT = $previousCommit
}

if ($demoExit -ne 0) {
    [Console]::Error.WriteLine("CV04_EXECUTION_FAILED:$demoExit")
    exit $demoExit
}

exit 0
