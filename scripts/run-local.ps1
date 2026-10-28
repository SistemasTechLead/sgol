param([ValidateSet('web', 'worker', 'proxy')][string]$Service = 'web', [switch]$Red, [string]$BinaryPath)
. "$PSScriptRoot/local-env.ps1"
if ($Red) { . "$PSScriptRoot/red-env.ps1" }
Set-Location -LiteralPath $sgolRoot
$env:SGOL_BOOTSTRAP_INITIAL_PASSWORD = $null
if ($BinaryPath) {
    $binary = [IO.Path]::GetFullPath($BinaryPath)
    if (!$binary.StartsWith($sgolRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        !(Test-Path -LiteralPath $binary -PathType Leaf)) { throw 'El binario debe existir dentro del workspace.' }
    if ($Service -eq 'proxy') { $env:ConnectionStrings__Sgol = $null; $env:Evidence__Storage__SecretKey = $null; $env:ASPNETCORE_URLS = $null }
    if ($Service -eq 'web') { Set-Location -LiteralPath (Join-Path $sgolRoot 'src/Sgol.Web') }
    dotnet $binary
    exit $LASTEXITCODE
}
if ($Service -eq 'proxy') {
    $env:ConnectionStrings__Sgol = $null
    $env:Evidence__Storage__SecretKey = $null
    # Kestrel endpoints are configured explicitly by the proxy.
    $env:ASPNETCORE_URLS = $null
    dotnet run --project tools/Sgol.LanProxy/Sgol.LanProxy.csproj --no-launch-profile
} elseif ($Service -eq 'web') {
    # SDK 10.0.401 can crash while hot-reloading static assets; restart the web instead.
    dotnet watch --project src/Sgol.Web/Sgol.Web.csproj --no-hot-reload run --no-launch-profile
} else {
    dotnet watch --project src/Sgol.Worker/Sgol.Worker.csproj --non-interactive run --no-launch-profile -- outbox
}
exit $LASTEXITCODE
