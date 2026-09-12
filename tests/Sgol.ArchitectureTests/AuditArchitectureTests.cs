using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class AuditArchitectureTests
{
    [Fact]
    public void AuditContractsRemainHostAndProviderIndependent()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var module = Path.Combine(root, "src", "Modules", "Execution");
        var project = XDocument.Load(Path.Combine(module, "Sgol.Execution.csproj"));
        var packages = project.Descendants().Where(item => item.Name.LocalName == "PackageReference").ToArray();
        var contracts = File.ReadAllText(Path.Combine(module, "Contracts", "AuditQueries.cs"));
        Assert.Empty(packages);
        Assert.Contains("namespace Sgol.Auditing.Contracts", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Web", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void Hu033AddsOnlyApprovedReadAndSecuritySurfaces()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var web = Path.Combine(root, "src", "Sgol.Web");
        var endpoint = File.ReadAllText(Path.Combine(web, "Interface", "Endpoints", "AuditApiEndpoints.cs"));
        var reader = File.ReadAllText(Path.Combine(web, "Infrastructure", "Persistence", "Auditing", "EfAuditEventReader.cs"));
        var middleware = File.ReadAllText(Path.Combine(web, "Infrastructure", "Http", "AuditDeleteAttemptMiddleware.cs"));
        Assert.Equal(2, Count(endpoint, "MapGet("));
        Assert.DoesNotContain("MapPost(", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("MapDelete(", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/v1/audit-events", endpoint, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.RepeatableRead", reader, StringComparison.Ordinal);
        Assert.Contains("SET TRANSACTION READ ONLY", reader, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking()", reader, StringComparison.Ordinal);
        Assert.True(reader.IndexOf("ApplyHierarchyFilter(query", StringComparison.Ordinal) <
            reader.IndexOf("Take(request.Limit + 1).ToListAsync", StringComparison.Ordinal));
        Assert.DoesNotContain("projected.Sort", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditEvents.Add", reader, StringComparison.Ordinal);
        Assert.Contains("AUDIT_EVENT_DELETE_ATTEMPTED", middleware + File.ReadAllText(Path.Combine(
            web, "Infrastructure", "Persistence", "Auditing", "EfAuditSecurityEventWriter.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("recovery-runs", endpoint + reader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotency", endpoint + reader, StringComparison.OrdinalIgnoreCase);

        var migrations = Directory.EnumerateFiles(Path.Combine(web, "Infrastructure", "Persistence", "Migrations"),
            "*.cs", SearchOption.TopDirectoryOnly).ToArray();
        Assert.DoesNotContain(migrations,
            path => Path.GetFileName(path).Contains("AuditQuery", StringComparison.OrdinalIgnoreCase));
    }

    private static int Count(string value, string search)
    {
        var count = 0;
        for (var offset = 0; (offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0;
             offset += search.Length) count++;
        return count;
    }
}
