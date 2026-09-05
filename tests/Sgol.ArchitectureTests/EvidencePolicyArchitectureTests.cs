using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class EvidencePolicyArchitectureTests
{
    [Fact]
    public void Hu024AddsOnlyTheApprovedPutAndNoEvidenceFileOrUserInterfaceSurface()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var endpoint = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Sgol.Web",
            "Interface",
            "Endpoints",
            "EvidencePolicyApiEndpoints.cs"));
        Assert.Equal(1, Count(endpoint, "MapPut("));
        Assert.Contains("/api/v1/task-definitions/{taskCode}/evidence-policy", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("MapDelete", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("file_object", endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidence_item", endpoint, StringComparison.OrdinalIgnoreCase);

        var hu024Ui = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Sgol.Web"), "*EvidencePolicy*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cshtml" or ".razor" or ".css" or ".js")
            .ToArray();
        Assert.Empty(hu024Ui);
    }

    [Fact]
    public void ConfigurationContractsRemainIndependentOfFrameworkAndFileInfrastructure()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var contracts = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Modules",
            "Configuration",
            "Contracts",
            "EvidencePolicies.cs"));
        Assert.DoesNotContain("Microsoft.AspNetCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Web", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("S3", contracts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FileObject", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("EvidenceItem", contracts, StringComparison.Ordinal);
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
