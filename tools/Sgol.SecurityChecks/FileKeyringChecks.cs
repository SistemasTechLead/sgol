using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

internal static class FileKeyringChecks
{
    public static void Run()
    {
        var source = new DirectoryInfo(Environment.GetEnvironmentVariable("SGOL_KEYRING_SOURCE") ?? throw new Exception("Missing keyring source."));
        var restored = new DirectoryInfo(Environment.GetEnvironmentVariable("SGOL_KEYRING_COPY") ?? throw new Exception("Missing keyring copy."));
        if (source.FullName == restored.FullName || !restored.FullName.Contains("security-audit", StringComparison.OrdinalIgnoreCase)) throw new Exception("Isolated copy required.");
        // These providers may only read the preserved keys, including their DPAPI wrapping.
        var original = DataProtectionProvider.Create(source, b => b.SetApplicationName("SGOL-Recovery-Verification").DisableAutomaticKeyGeneration());
        var copy = DataProtectionProvider.Create(restored, b => b.SetApplicationName("SGOL-Recovery-Verification").DisableAutomaticKeyGeneration());
        var marker = Guid.NewGuid().ToString("N");
        var protectedValue = original.CreateProtector("recovery-test").Protect(marker);
        if (copy.CreateProtector("recovery-test").Unprotect(protectedValue) != marker) throw new Exception("Restored keyring cannot decrypt.");
        Console.WriteLine("PASS copied effective filesystem keyring decrypts existing protection; automatic generation disabled on both providers.");
    }
}
