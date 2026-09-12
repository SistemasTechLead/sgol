using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class ObligationConclusionArchitectureTests
{
    [Fact]
    public void Hu022KeepsContractsIndependentAndAddsOnlyApprovedConclusionSurface()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var module = Path.Combine(root, "src", "Modules", "Execution");
        var project = XDocument.Load(Path.Combine(module, "Sgol.Execution.csproj"));
        Assert.DoesNotContain(project.Descendants(), item =>
            item.Name.LocalName is "ProjectReference" or "PackageReference");

        var contracts = File.ReadAllText(Path.Combine(module, "Contracts", "ObligationConclusions.cs"));
        Assert.Contains("PER-TAREA-EJECUTAR", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Web", contracts, StringComparison.Ordinal);

        var web = Path.Combine(root, "src", "Sgol.Web");
        var endpoint = File.ReadAllText(Path.Combine(web, "Interface", "Endpoints", "ObligationConclusionApiEndpoints.cs"));
        var service = File.ReadAllText(Path.Combine(
            web, "Infrastructure", "Persistence", "Execution", "EfObligationConclusionService.cs"));
        var review = File.ReadAllText(Path.Combine(
            web, "Infrastructure", "Persistence", "Evidence", "EfEvidenceConclusionReviewService.cs"));
        var migration = File.ReadAllText(Directory.EnumerateFiles(
            Path.Combine(web, "Infrastructure", "Persistence", "Migrations"),
            "*_AddObligationConclusions.cs").Single());

        Assert.Equal(1, Count(endpoint, "MapPost("));
        Assert.Contains("/api/v1/obligations/{id}/conclusion", endpoint, StringComparison.Ordinal);
        Assert.Contains("IdempotencyKeyHeader.Parse", endpoint, StringComparison.Ordinal);
        Assert.Contains("IfMatch", endpoint, StringComparison.Ordinal);
        Assert.Contains("IAntiforgery", endpoint, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", service, StringComparison.Ordinal);
        Assert.Contains("FOR UPDATE", service, StringComparison.Ordinal);
        Assert.Contains("OBLIGATION_CONCLUDED", service, StringComparison.Ordinal);
        Assert.Contains("EvidenceReviewEvaluator.Evaluate", review, StringComparison.Ordinal);
        Assert.Contains("execution_result", migration, StringComparison.Ordinal);
        Assert.Contains("execution_result_guard", migration, StringComparison.Ordinal);
        Assert.Contains("actor.status = 'ACTIVA'", migration, StringComparison.Ordinal);
        Assert.Contains("Rollback is blocked", migration, StringComparison.Ordinal);

        var combined = string.Join(Environment.NewLine, endpoint, service, review, migration);
        Assert.DoesNotContain("validation_requirement", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation_decision", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal_notice", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outbox", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IPrivateObjectStorage", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Seaweed", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Clam", combined, StringComparison.OrdinalIgnoreCase);

        var ui = Directory.EnumerateFiles(web, "*Conclusion*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetExtension(path) is ".cshtml" or ".razor" or ".css" or ".js")
            .ToArray();
        Assert.Empty(ui);
    }

    private static int Count(string value, string search)
    {
        var count = 0;
        for (var offset = 0; (offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0; offset += search.Length)
        {
            count++;
        }

        return count;
    }
}
