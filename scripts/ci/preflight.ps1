$headValue = git rev-parse HEAD
$branchValue = git branch --show-current
$treeCleanValue = [string]::IsNullOrEmpty((git status --porcelain))
$fuentesCleanValue = [string]::IsNullOrEmpty((git status --porcelain -- Fuentes))
$dotnetVersionValue = dotnet --version
$ghCommand = Get-Command gh -ErrorAction SilentlyContinue

if ($null -eq $ghCommand) {
    $ghSessionValue = 'unavailable'
}
else {
    & $ghCommand.Source auth status *> $null
    $ghSessionValue = if ($LASTEXITCODE -eq 0) { 'authenticated' } else { 'unauthenticated' }
}

Write-Output "head=$headValue"
Write-Output "branch=$branchValue"
Write-Output "treeClean=$($treeCleanValue.ToString().ToLowerInvariant())"
Write-Output "fuentesClean=$($fuentesCleanValue.ToString().ToLowerInvariant())"
Write-Output "dotnetVersion=$dotnetVersionValue"
Write-Output "ghSession=$ghSessionValue"
