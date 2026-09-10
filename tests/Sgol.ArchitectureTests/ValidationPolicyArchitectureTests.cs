using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class ValidationPolicyArchitectureTests
{
    [Fact]
    public void Hu027AddsOnlyTheApprovedPutAndNoDecisionOrUiSurface()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var endpoint = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Sgol.Web",
            "Interface",
            "Endpoints",
            "ValidationPolicyApiEndpoints.cs"));
        Assert.Equal(1, Count(endpoint, "MapPut("));
        Assert.Contains("/api/v1/task-definitions/{taskCode}/validation-policy", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("MapDelete", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("validation_requirement", endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation_decision", endpoint, StringComparison.OrdinalIgnoreCase);

        var ui = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Sgol.Web"), "*ValidationPolicy*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cshtml" or ".razor" or ".css" or ".js")
            .ToArray();
        Assert.Empty(ui);
    }

    [Fact]
    public void ValidationContractsRemainIndependentOfFrameworkAndDecisionPersistence()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var contracts = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Modules",
            "Configuration",
            "Contracts",
            "ValidationPolicies.cs"));
        Assert.DoesNotContain("Microsoft.AspNetCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Web", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidationRequirement", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidationDecision", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationIsForwardOnlyAndContainsOnlyPolicyAndSnapshotPersistence()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var migration = File.ReadAllText(Directory.EnumerateFiles(
            Path.Combine(root, "src", "Sgol.Web", "Infrastructure", "Persistence", "Migrations"),
            "*_AddValidationPolicies.cs").Single());
        Assert.Contains("validation_policy_version", migration, StringComparison.Ordinal);
        Assert.Contains("validation_policy_version_id", migration, StringComparison.Ordinal);
        Assert.Contains("Rollback is intentionally blocked", migration, StringComparison.Ordinal);
        Assert.Contains("EX_validation_policy_version_validity", migration, StringComparison.Ordinal);
        Assert.Contains("work_obligation_validation_policy_protected", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("validation_requirement", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation_decision", migration, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string value, string search)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += search.Length;
        }

        return count;
    }
}
