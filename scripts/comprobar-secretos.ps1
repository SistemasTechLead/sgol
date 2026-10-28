$ErrorActionPreference = 'Stop'
$sgolRoot = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot/secret-store.ps1"
$directory = Join-Path $sgolRoot ('artifacts/security-audit/implementation/secret-fixture-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
Set-SgolPrivateAcl $directory
$value = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
@{ DatabasePassword = $value; OperationalProcessesEnabled = $false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'config.json')
$first = Move-SgolLegacySecrets $directory
$second = Move-SgolLegacySecrets $directory
if ($first.DatabasePassword -cne $value -or $second.DatabasePassword -cne $value) { throw 'Migración no idempotente.' }
if ((Get-Content -LiteralPath (Join-Path $directory 'config.json') -Raw).Contains($value)) { throw 'Secreto conservado en metadatos.' }
if ((Get-Content -LiteralPath (Join-Path $directory 'secrets.dpapi.json') -Raw).Contains($value)) { throw 'Secreto no protegido.' }
if (!(Get-Content -LiteralPath (Join-Path $directory 'config.json') -Raw).Contains('OperationalProcessesEnabled')) { throw 'Se perdió configuración no secreta.' }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
public static class SgolRestrictedRead {
  [StructLayout(LayoutKind.Sequential)] struct SidAndAttributes { public IntPtr Sid; public uint Attributes; }
  [DllImport("advapi32.dll", SetLastError=true)] static extern bool ConvertStringSidToSid(string text, out IntPtr sid);
  [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr ptr);
  [DllImport("advapi32.dll", SetLastError=true)] static extern bool CreateRestrictedToken(IntPtr token, uint flags,
    uint disableCount, IntPtr disable, uint deleteCount, IntPtr delete, uint restrictedCount, ref SidAndAttributes restricted, out SafeAccessTokenHandle result);
  public static bool Denied(string path) {
    if (!ConvertStringSidToSid("S-1-5-32-545", out var sid)) throw new Exception("SID conversion failed");
    try {
      var restriction = new SidAndAttributes { Sid=sid, Attributes=0 };
      using var current=WindowsIdentity.GetCurrent();
      if (!CreateRestrictedToken(current.Token, 1, 0, IntPtr.Zero, 0, IntPtr.Zero, 1, ref restriction, out var token))
        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
      using(token) return WindowsIdentity.RunImpersonated(token, () => {
        try { System.IO.File.ReadAllText(path); return false; }
        catch(UnauthorizedAccessException) { return true; }
      });
    } finally { LocalFree(sid); }
  }
}
'@
$denied = [SgolRestrictedRead]::Denied((Join-Path $directory 'secrets.dpapi.json'))
if (!$denied) { throw 'El token restringido puede leer el secreto.' }
@{ currentIdentityRead = $true; restrictedUsersTokenDenied = $denied; dpapiRoundtrip = $true; migrationIdempotent = $true;
    separateWindowsAccountTested = $false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'result.json')
Write-Host "PASS DPAPI, metadatos, migración repetida y denegación con token restringido Users. Evidencia: $directory"
