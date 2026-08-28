[CmdletBinding()]
param(
    [Parameter()]
    [string] $Solution = "SGOL.slnx",

    [Parameter()]
    [string] $ReportPath
)

$ErrorActionPreference = "Stop"

if ($ReportPath) {
    $json = Get-Content -LiteralPath $ReportPath -Raw
}
else {
    $output = & dotnet package list --project $Solution --vulnerable --include-transitive --format json --output-version 1 --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "The NuGet vulnerability query failed with exit code $LASTEXITCODE."
    }

    $json = [string]::Join([Environment]::NewLine, [string[]] $output)
}

$report = $json | ConvertFrom-Json -Depth 32
$findings = [System.Collections.Generic.List[object]]::new()

foreach ($project in @($report.projects)) {
    if ($null -eq $project.frameworks) {
        continue
    }

    foreach ($framework in @($project.frameworks)) {
        foreach ($packageKind in @("topLevelPackages", "transitivePackages")) {
            $packages = $framework.$packageKind
            if ($null -eq $packages) {
                continue
            }

            foreach ($package in @($packages)) {
                if ($null -eq $package.vulnerabilities) {
                    continue
                }

                foreach ($vulnerability in @($package.vulnerabilities)) {
                    $findings.Add([pscustomobject]@{
                        Project = $project.path
                        Kind = $packageKind
                        Package = $package.id
                        Version = $package.resolvedVersion
                        Severity = $vulnerability.severity
                        Advisory = $vulnerability.advisoryUrl
                    })
                }
            }
        }
    }
}

if ($findings.Count -gt 0) {
    $findings |
        Sort-Object Project, Package, Advisory |
        Format-Table Project, Kind, Package, Version, Severity, Advisory -AutoSize |
        Out-String |
        Write-Host

    Write-Error "Dependency gate failed: $($findings.Count) known vulnerability finding(s) require explicit treatment."
    exit 1
}

Write-Host "PASS: no known vulnerabilities were reported for direct or transitive NuGet packages."
