$ErrorActionPreference = 'Stop'

function Set-SgolPrivateAcl([string]$Path) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $isDirectory = Test-Path -LiteralPath $Path -PathType Container
    $allowed = @($identity.Value, 'S-1-5-18', 'S-1-5-32-544')
    $existing = Get-Acl -LiteralPath $Path
    $rules = @($existing.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]))
    $expectedInheritance = if ($isDirectory) { 3 } else { 0 }
    if ($existing.AreAccessRulesProtected -and $rules.Count -eq 3 -and
        @($rules | Where-Object { $_.IdentityReference.Value -notin $allowed -or
            $_.AccessControlType -ne 'Allow' -or $_.FileSystemRights -ne 'FullControl' -or
            [int]$_.InheritanceFlags -ne $expectedInheritance }).Count -eq 0) { return }
    $acl = if ($isDirectory) { [Security.AccessControl.DirectorySecurity]::new() } else { [Security.AccessControl.FileSecurity]::new() }
    $acl.SetAccessRuleProtection($true, $false)
    $inherit = if ($isDirectory) { 'ContainerInherit,ObjectInherit' } else { 'None' }
    foreach ($sid in $allowed) {
        $principal = [Security.Principal.SecurityIdentifier]::new($sid)
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($principal, 'FullControl', $inherit, 'None', 'Allow'))
    }
    $item = Get-Item -LiteralPath $Path
    [IO.FileSystemAclExtensions]::SetAccessControl($item, $acl)
}

function Read-SgolSecrets([string]$Directory, [string[]]$Names = @()) {
    $path = Join-Path $Directory 'secrets.dpapi.json'
    $result = @{}
    if (Test-Path -LiteralPath $path) {
        $stored = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
        foreach ($key in $stored.Keys) {
            if ($Names.Count -gt 0 -and $key -notin $Names) { continue }
            $result[$key] = [Net.NetworkCredential]::new('', (ConvertTo-SecureString $stored[$key])).Password
        }
    }
    return $result
}

function Write-SgolSecrets([string]$Directory, [hashtable]$Secrets) {
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    Set-SgolPrivateAcl $Directory
    $stored = @{}
    foreach ($key in $Secrets.Keys) {
        $stored[$key] = ConvertFrom-SecureString (ConvertTo-SecureString $Secrets[$key] -AsPlainText -Force)
    }
    $path = Join-Path $Directory 'secrets.dpapi.json'
    $temporary = Join-Path $Directory ([Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $stored | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding utf8
        Set-SgolPrivateAcl $temporary
        # Verify every DPAPI value before atomically replacing the previous store.
        $check = Get-Content -LiteralPath $temporary -Raw | ConvertFrom-Json -AsHashtable
        foreach ($key in $Secrets.Keys) {
            $value = [Net.NetworkCredential]::new('', (ConvertTo-SecureString $check[$key])).Password
            if ($value -cne $Secrets[$key]) { throw 'No se pudo verificar el almacén protegido.' }
        }
        [IO.File]::Move($temporary, $path, $true)
        Set-SgolPrivateAcl $path
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
}

function Move-SgolLegacySecrets([string]$Directory) {
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    Set-SgolPrivateAcl $Directory
    $secrets = Read-SgolSecrets $Directory
    foreach ($name in @('config.json', 'red.json')) {
        $path = Join-Path $Directory $name
        if (!(Test-Path -LiteralPath $path)) { continue }
        Set-SgolPrivateAcl $path
        $metadata = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
        $changed = $false
        foreach ($key in @('DatabasePassword','RuntimePassword','MigrationPassword','StoragePassword','InitialPassword','CertificatePassword')) {
            if ($metadata.ContainsKey($key)) { $secrets[$key] = $metadata[$key]; $changed = $true }
        }
        if ($changed) {
            Write-SgolSecrets $Directory $secrets
            foreach ($key in $secrets.Keys) { $metadata.Remove($key) | Out-Null }
            $temporary = Join-Path $Directory ($name + '.tmp')
            $metadata | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $temporary -Encoding utf8
            Set-SgolPrivateAcl $temporary
            [IO.File]::Move($temporary, $path, $true)
        }
    }
    foreach ($name in @('compose.env','red.pfx','acceso.txt','equipo-pruebas.txt')) {
        $path = Join-Path $Directory $name
        if (Test-Path -LiteralPath $path) { Set-SgolPrivateAcl $path }
    }
    return $secrets
}

function Write-SgolStorageIdentities([string]$Directory, [hashtable]$Secrets, [string]$MaintenanceKey = 'sgol-maintenance', [string]$RuntimeKey = 'sgol-runtime', [string]$MaintenanceName = 'maintenance') {
    $runtimeActions = foreach ($bucket in @('sgol-quarantine','sgol-clean')) {
        foreach ($action in @('Read','Write','List','Tagging')) { "${action}:${bucket}" }
    }
    $identities = @{ identities = @(
        @{ name = $MaintenanceName; account = @{ id = 'admin'; displayName = 'admin' }; credentials = @(@{ accessKey = $MaintenanceKey; secretKey = $Secrets.StoragePassword }); actions = @('Admin') },
        @{ name = 'runtime'; credentials = @(@{ accessKey = $RuntimeKey; secretKey = $Secrets.StorageRuntimePassword }); actions = @($runtimeActions) }
    ) }
    # SeaweedFS requires this native credential file. It is distinct from public metadata,
    # ACL restricted, mounted read-only, and readable by Docker administrators by design.
    $path = Join-Path $Directory 'storage-identities.secret.json'
    $temporary = $path + '.tmp'
    $identities | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temporary -Encoding utf8
    Set-SgolPrivateAcl $temporary
    [IO.File]::Move($temporary, $path, $true)
    return $path
}
