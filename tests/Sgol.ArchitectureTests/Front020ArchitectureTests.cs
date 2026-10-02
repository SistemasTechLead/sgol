using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class Front020ArchitectureTests
{
    [Fact]
    public void Front020UsesContractsWithoutPersistenceOperationsOrProviderDependencies()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        foreach (var area in new[] { "Indicators", "Audit", "Continuity" })
            foreach (var file in Directory.GetFiles(Path.Combine(root, "src", "Sgol.Web", "Pages", area), "*.cs*"))
            {
                var source = File.ReadAllText(file);
                Assert.DoesNotContain("SgolDbContext", source); Assert.DoesNotContain("SaveChanges", source);
                Assert.DoesNotContain("HttpMethod.Delete", source); Assert.DoesNotContain("IFunctionalRecoveryOperations", source);
                Assert.DoesNotContain("style=", source); Assert.DoesNotContain("localStorage", source);
            }
        var continuity = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "Pages", "Continuity", "Index.cshtml.cs"));
        Assert.Contains("ApiMutationIntent.FromKey", continuity); Assert.Contains("intent.ETag", continuity);
        var report = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "Pages", "Continuity", "_Continuity.cshtml"));
        Assert.Contains("DifferencesTruncated", report); Assert.Contains("DIFFERENT", report); Assert.Contains("data-initial-focus", report);
    }
}
