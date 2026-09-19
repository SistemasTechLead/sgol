[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Read-Required([string]$RelativePath) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "AUTH_VALIDATION_FILE_MISSING"
    }
    return [IO.File]::ReadAllText($path)
}

function Require-Contains([string]$Text, [string]$Expected, [string]$Code) {
    if ($Text.IndexOf($Expected, [StringComparison]::Ordinal) -lt 0) {
        throw $Code
    }
}

$program = Read-Required 'src/Sgol.Web/Program.cs'
$infrastructure = Read-Required 'src/Sgol.Web/Infrastructure/Authentication/HostedAuthenticationInfrastructure.cs'
$endpoints = Read-Required 'src/Sgol.Web/Interface/Endpoints/AuthenticationApiEndpoints.cs'
$service = Read-Required 'src/Sgol.Web/Infrastructure/Persistence/Identity/EfHostedAuthenticationService.cs'
$migrationPath = Join-Path $root 'src/Sgol.Web/Infrastructure/Persistence/Migrations/20260918001719_AddHostedAuthentication.cs'
$migration = Read-Required 'src/Sgol.Web/Infrastructure/Persistence/Migrations/20260918001719_AddHostedAuthentication.cs'
$gitleaks = Read-Required '.gitleaks.toml'
$smoke = Read-Required 'tests/Sgol.IntegrationTests/HostedAuthenticationKestrelSmokeTests.cs'
$evidence = Read-Required 'tests/Sgol.IntegrationTests/HostedAuthenticationSmokeEvidence.cs'

$rate = $program.IndexOf('app.UseRateLimiter();', [StringComparison]::Ordinal)
$authentication = $program.IndexOf('app.UseAuthentication();', [StringComparison]::Ordinal)
$antiforgery = $program.IndexOf('app.UseMiddleware<AntiforgeryValidationMiddleware>();', [StringComparison]::Ordinal)
$authorization = $program.IndexOf('app.UseAuthorization();', [StringComparison]::Ordinal)
if ($rate -lt 0 -or $authentication -le $rate -or $antiforgery -le $authentication -or $authorization -le $antiforgery) {
    throw 'AUTH_VALIDATION_MIDDLEWARE_ORDER'
}

foreach ($required in @(
    'CookieSecurePolicy.Always',
    'HttpOnly = true',
    'SameSite = SameSiteMode.Strict',
    'Path = "/"',
    'options.SlidingExpiration = false',
    'TimeSpan.FromMinutes(15)',
    'RecordSessionRejectedAsync')) {
    Require-Contains $infrastructure $required 'AUTH_VALIDATION_COOKIE_CONTRACT'
}

foreach ($route in @(
    '"/csrf"', '"/login"', '"/password/change"', '"/mfa/enroll"',
    '"/mfa/confirm"', '"/mfa/verify"', '"/recovery-codes/regenerate"',
    '"/session"', '"/logout"')) {
    Require-Contains $endpoints $route 'AUTH_VALIDATION_ROUTE_MISSING'
}
Require-Contains $endpoints 'MapGet("/session", GetSessionAsync).RequireAuthorization()' 'AUTH_VALIDATION_SESSION_UNPROTECTED'
Require-Contains $endpoints 'RecordLogoutAsync' 'AUTH_VALIDATION_LOGOUT_AUDIT'

foreach ($required in @(
    'FOR UPDATE',
    'AUTH_SESSION_REJECTED',
    'AUTH_LOGOUT',
    'LastAcceptedTimeStep',
    'AuthenticationRowVersion',
    'BeginTransactionAsync')) {
    Require-Contains $service $required 'AUTH_VALIDATION_PERSISTENCE_CONTRACT'
}

Require-Contains $migration 'Rollback is blocked because hosted authentication state is security-sensitive and append-only.' 'AUTH_VALIDATION_DOWN_NOT_BLOCKED'
$bytes = [IO.File]::ReadAllBytes($migrationPath)
if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) {
    throw 'AUTH_VALIDATION_MIGRATION_BOM'
}

Require-Contains $gitleaks '^GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ$' 'AUTH_VALIDATION_GITLEAKS_SCOPE'
Require-Contains $gitleaks 'regexTarget = "secret"' 'AUTH_VALIDATION_GITLEAKS_TARGET'
if ([regex]::Matches($gitleaks, '(?m)^\[\[allowlists\]\]\r?$').Count -ne 1) {
    throw 'AUTH_VALIDATION_GITLEAKS_ALLOWLIST_COUNT'
}

foreach ($stage in @('CSRF','LOGIN','PASSWORD_CHANGE','MFA_ENROLL','MFA_CONFIRM','MFA_VERIFY','RECOVERY_REGENERATE','SESSION_VALIDATE','LOGOUT','MFA_RESET','CLEANUP')) {
    Require-Contains $evidence $stage 'AUTH_VALIDATION_DIAGNOSTIC_STAGE'
}
foreach ($field in @('stage = Stage','scenario = Scenario','exit = Exit','errorCode = ErrorCode','state = State')) {
    Require-Contains $evidence $field 'AUTH_VALIDATION_DIAGNOSTIC_FIELD'
}
Require-Contains $smoke 'ProcessStartInfo("dotnet")' 'AUTH_VALIDATION_KESTREL_PROCESS'
Require-Contains $smoke 'Kestrel__Certificates__Default__Path' 'AUTH_VALIDATION_HTTPS_CERTIFICATE'
Require-Contains $smoke 'ServerCertificateCustomValidationCallback' 'AUTH_VALIDATION_CERTIFICATE_PINNING'
Require-Contains $smoke 'PostgreSqlPersistenceTests.CreateContainerForTests()' 'AUTH_VALIDATION_POSTGRESQL'

Write-Output 'AUTH_VALIDATION_SUCCEEDED'
