param([ValidateSet('web', 'worker', 'proxy')][string]$Service = 'web', [switch]$Red)
. "$PSScriptRoot/local-env.ps1"
if ($Red) { . "$PSScriptRoot/red-env.ps1" }
Set-Location -LiteralPath $sgolRoot
$env:SGOL_BOOTSTRAP_INITIAL_PASSWORD = $null
if ($Service -eq 'proxy') {
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
