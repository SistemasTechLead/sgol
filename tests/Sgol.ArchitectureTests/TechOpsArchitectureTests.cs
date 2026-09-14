using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class TechOpsArchitectureTests
{
    [Fact]
    public void DockerfileIsDigestPinnedNonRootAndContainsAllApprovedCommands()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));

        Assert.Contains("sdk:10.0-noble@sha256:", dockerfile, StringComparison.Ordinal);
        Assert.Contains("aspnet:10.0-noble@sha256:", dockerfile, StringComparison.Ordinal);
        Assert.Contains("FROM restore AS publish", dockerfile, StringComparison.Ordinal);
        Assert.Contains("global.json NuGet.config", dockerfile, StringComparison.Ordinal);
        Assert.Equal(2, dockerfile.Split('@').Count(part => part.StartsWith("sha256:", StringComparison.Ordinal)));
        Assert.Contains("USER 1654:1654", dockerfile, StringComparison.Ordinal);
        Assert.Contains("EXPOSE 8080", dockerfile, StringComparison.Ordinal);
        Assert.Contains("Sgol.Web.dll", dockerfile, StringComparison.Ordinal);
        Assert.Contains("Sgol.Worker", dockerfile, StringComparison.Ordinal);
        Assert.Contains("Sgol.Operations", dockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("COPY .env", dockerfile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PASSWORD=", dockerfile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StagingManifestUsesOneImmutableImageAndHardenedRuntimeWithoutSecretValues()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var manifest = File.ReadAllText(Path.Combine(root, "deploy", "staging", "compose.yaml"));
        var secrets = File.ReadAllLines(Path.Combine(root, "deploy", "staging", "secrets.example"));

        Assert.Contains("${SGOL_IMAGE_REF:?", manifest, StringComparison.Ordinal);
        Assert.Contains("user: \"1654:1654\"", manifest, StringComparison.Ordinal);
        Assert.Contains("read_only: true", manifest, StringComparison.Ordinal);
        Assert.Contains("no-new-privileges:true", manifest, StringComparison.Ordinal);
        Assert.Contains("cap_drop:", manifest, StringComparison.Ordinal);
        Assert.Contains("/tmp:size=268435456,mode=0700,uid=1654,gid=1654,noexec,nosuid,nodev", manifest,
            StringComparison.Ordinal);
        Assert.Contains("/health/ready", manifest, StringComparison.Ordinal);
        Assert.Contains("POSTGRESQL_PORTABLE_BACKUP", manifest, StringComparison.Ordinal);
        Assert.Contains("REPLICATE_EVIDENCE_OBJECTS", manifest, StringComparison.Ordinal);
        Assert.Contains("verify-postgresql-backup", manifest, StringComparison.Ordinal);
        Assert.Contains("verify-object-replica", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("build:", manifest, StringComparison.Ordinal);
        Assert.All(secrets.Where(line => line.Contains('=', StringComparison.Ordinal)),
            line => Assert.EndsWith("=REQUIRED_EXTERNAL_SECRET", line, StringComparison.Ordinal));
    }

    [Fact]
    public void OperationsStayOutOfDomainAndDoNotContainProviderOrDestructiveCommands()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var operations = Path.Combine(root, "src", "Sgol.Operations");
        var source = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(operations, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var domain = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(root, "src", "Modules"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("DigitalOcean", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE OBJECT", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP DATABASE", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--clean", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Operations", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("Amazon.S3", domain, StringComparison.Ordinal);
    }

    [Fact]
    public void PortableKeyRingMigrationIsForwardOnlyAndNotFilesystemBacked()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var migrationPath = Path.Combine(root, "src", "Sgol.Web", "Infrastructure", "Persistence",
            "Migrations", "20260912213000_AddPortableDataProtectionKeyRing.cs");
        var migration = File.ReadAllText(migrationPath);
        var dataProtection = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(root, "src", "Sgol.Web", "Infrastructure", "Persistence",
                    "DataProtection"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.False(File.ReadAllBytes(migrationPath).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Contains("data_protection_key", migration, StringComparison.Ordinal);
        Assert.Contains("throw new NotSupportedException", migration, StringComparison.Ordinal);
        Assert.Contains("PostgreSqlXmlRepository", dataProtection, StringComparison.Ordinal);
        Assert.DoesNotContain("PersistKeysToFileSystem", dataProtection, StringComparison.Ordinal);
    }
}
