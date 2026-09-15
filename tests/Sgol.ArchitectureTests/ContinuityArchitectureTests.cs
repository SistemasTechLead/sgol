using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class ContinuityArchitectureTests
{
    [Fact]
    public void ContinuityDomainHasNoProviderOrWebDependencies()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var directory = Path.Combine(root, "src", "Modules", "Continuity");
        var source = string.Join('\n', Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.DoesNotContain("Amazon.S3", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EntityFrameworkCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionString", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApiIsMinimalAndDoesNotExposeArtifactLocations()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var source = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "Interface", "Endpoints",
            "ContinuityApiEndpoints.cs"));

        Assert.Equal(3, source.Split("endpoints.Map", StringSplitOptions.None).Length - 1);
        Assert.Contains("PER-CONTINUIDAD-VER", File.ReadAllText(Path.Combine(root, "src", "Modules", "Continuity",
            "Contracts", "RecoveryReconciliation.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("ManifestUri", source, StringComparison.Ordinal);
        Assert.DoesNotContain("S3", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistenceIsAppendOnlyAndForwardOnly()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var path = Path.Combine(root, "src", "Sgol.Web", "Infrastructure", "Persistence", "Migrations",
            "20260914210503_AddRecoveryReconciliation.cs");
        var source = File.ReadAllText(path);

        Assert.False(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Equal(3, source.Split("BEFORE UPDATE OR DELETE", StringSplitOptions.None).Length - 1);
        Assert.Contains("ERRCODE = '55000'", source, StringComparison.Ordinal);
        Assert.Contains("throw new NotSupportedException", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DropTable", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteData", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationsContainApprovedCommandsAndRejectPrimaryRestore()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var program = File.ReadAllText(Path.Combine(root, "src", "Sgol.Operations", "Program.cs"));
        var recovery = File.ReadAllText(Path.Combine(root, "src", "Sgol.Operations",
            "FunctionalRecoveryOperations.cs"));

        Assert.Contains("complete-functional-reference", program, StringComparison.Ordinal);
        Assert.Contains("reconcile-functional-restore", program, StringComparison.Ordinal);
        Assert.Contains("RESTORE_PRIMARY_TARGET_REJECTED", recovery, StringComparison.Ordinal);
        Assert.DoesNotContain("--clean", recovery, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteObject", recovery, StringComparison.Ordinal);
    }

    [Fact]
    public void TelemetryUsesOnlyLowCardinalityLabels()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var source = File.ReadAllText(Path.Combine(root, "src", "Sgol.Operations",
            "FunctionalRecoveryTelemetry.cs"));

        foreach (var label in new[] { "operation", "stage", "result", "errorClass" })
            Assert.Contains($"\"{label}\"", source, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "reconciliationId", "actor", "resourceId", "bucket", "sha256", "uri" })
            Assert.DoesNotContain($"\"{forbidden}\"", source, StringComparison.OrdinalIgnoreCase);
    }
}
