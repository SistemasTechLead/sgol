using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class EvidenceInfrastructureArchitectureTests
{
    [Fact]
    public void EvidenceModuleOwnsContractsWithoutProviderOrHostDependencies()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var module = Path.Combine(root, "src", "Modules", "Evidence");
        var project = XDocument.Load(Path.Combine(module, "Sgol.Evidence.csproj"));
        var dependencies = project.Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();
        var source = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(module, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Empty(dependencies);
        Assert.DoesNotContain("Amazon.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Hu025UsesExactlyTheApprovedSurfaceAndThreePersistentAggregates()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var web = Path.Combine(root, "src", "Sgol.Web");
        var endpoint = File.ReadAllText(Path.Combine(web, "Interface", "Endpoints", "EvidenceApiEndpoints.cs"));
        var migrationSource = File.ReadAllText(
            Directory.EnumerateFiles(Path.Combine(web, "Infrastructure", "Persistence", "Migrations"),
                "*_AddVersionedEvidenceContribution.cs").Single());

        Assert.Equal(4, Count(endpoint, "MapPost("));
        Assert.Equal(3, Count(endpoint, "MapGet("));
        Assert.Contains("/api/v1/files/upload-intents", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/v1/files/{id:guid}/complete", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/v1/files/{id:guid}/status", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/v1/obligations/{id:guid}/evidence", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/v1/obligations/{id:guid}/evidence/{itemId:guid}/replacements", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("download", endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IAntiforgery", endpoint, StringComparison.Ordinal);
        Assert.Contains("X-CSRF-TOKEN", File.ReadAllText(Path.Combine(web, "Program.cs")), StringComparison.Ordinal);
        var storageAdapter = File.ReadAllText(Path.Combine(web, "Infrastructure", "Evidence", "S3PrivateObjectStorage.cs"));
        var productionSource = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
        Assert.Contains("GetCORSConfigurationAsync", storageAdapter, StringComparison.Ordinal);
        Assert.DoesNotContain("PutCORSConfigurationAsync", productionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteCORSConfigurationAsync", productionSource, StringComparison.Ordinal);
        Assert.Contains("file_object", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("evidence_item", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("evidence_version", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidence_review_snapshot", migrationSource, StringComparison.OrdinalIgnoreCase);

        var ui = Directory.EnumerateFiles(web, "*Evidence*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cshtml" or ".razor" or ".css" or ".js")
            .ToArray();
        Assert.Empty(ui);
    }

    [Fact]
    public void TechEvid002AddsStructuredEvidenceWithoutNewRouteTableOrStorageAdapter()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var web = Path.Combine(root, "src", "Sgol.Web");
        var endpoint = File.ReadAllText(Path.Combine(web, "Interface", "Endpoints", "EvidenceApiEndpoints.cs"));
        var migration = File.ReadAllText(
            Directory.EnumerateFiles(Path.Combine(web, "Infrastructure", "Persistence", "Migrations"), "*_EnableStructuredEvidence.cs").Single());

        Assert.Equal(4, Count(endpoint, "MapPost("));
        Assert.Equal(3, Count(endpoint, "MapGet("));
        Assert.Contains("structuredPayload", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateTable", migration, StringComparison.Ordinal);
        Assert.Contains("evidence_version", migration, StringComparison.Ordinal);
        Assert.Contains("sgol_evidence_structured_payload_valid", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("evidence_review_snapshot", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IPrivateObjectStorage", File.ReadAllText(Path.Combine(root, "src", "Modules", "Evidence", "Contracts", "StructuredEvidencePayloadValidator.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Hu026AddsOnlyEvidenceReviewQueryAndImmutableSnapshot()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var web = Path.Combine(root, "src", "Sgol.Web");
        var endpoint = File.ReadAllText(Path.Combine(web, "Interface", "Endpoints", "EvidenceApiEndpoints.cs"));
        var service = File.ReadAllText(Path.Combine(web, "Infrastructure", "Persistence", "Evidence", "EfEvidenceReviewService.cs"));
        var evaluator = File.ReadAllText(Path.Combine(root, "src", "Modules", "Evidence", "Contracts", "EvidenceReviews.cs"));
        var migration = File.ReadAllText(
            Directory.EnumerateFiles(Path.Combine(web, "Infrastructure", "Persistence", "Migrations"),
                "*_AddEvidenceReviewSnapshots.cs").Single());

        Assert.Equal(4, Count(endpoint, "MapPost("));
        Assert.Equal(3, Count(endpoint, "MapGet("));
        Assert.Contains("/api/v1/obligations/{id}/evidence-review", endpoint, StringComparison.Ordinal);
        Assert.Contains("evidence_review_snapshot", migration, StringComparison.Ordinal);
        Assert.Contains("SERIALIZABLE", service, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EVIDENCE_REVIEW_SNAPSHOT_CREATED", service, StringComparison.Ordinal);
        Assert.Contains("evidence_review_snapshot_guard", migration, StringComparison.Ordinal);
        Assert.Contains("Rollback is blocked", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("IPrivateObjectStorage", service, StringComparison.Ordinal);
        Assert.DoesNotContain("IPrivateObjectStorage", evaluator, StringComparison.Ordinal);
        Assert.DoesNotContain("Clam", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExecutionResult", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidationDecision", service, StringComparison.Ordinal);

        var ui = Directory.EnumerateFiles(web, "*EvidenceReview*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cshtml" or ".razor" or ".css" or ".js")
            .ToArray();
        Assert.Empty(ui);
    }

    private static int Count(string value, string search)
    {
        var count = 0;
        for (var offset = 0; (offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0; offset += search.Length) count++;
        return count;
    }
}
