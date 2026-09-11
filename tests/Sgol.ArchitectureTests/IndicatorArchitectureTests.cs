using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class IndicatorArchitectureTests
{
    [Fact]
    public void IndicatorContractsRemainHostAndProviderIndependent()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var module = Path.Combine(root, "src", "Modules", "Execution");
        var project = XDocument.Load(Path.Combine(module, "Sgol.Execution.csproj"));
        var packages = project.Descendants().Where(element => element.Name.LocalName == "PackageReference").ToArray();
        var source = File.ReadAllText(Path.Combine(module, "Contracts", "Indicators.cs"));

        Assert.Empty(packages);
        Assert.Contains("namespace Sgol.Reporting.Contracts", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Web", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Hu029AddsOnlyTheApprovedReadSurface()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var web = Path.Combine(root, "src", "Sgol.Web");
        var endpoint = File.ReadAllText(Path.Combine(web, "Interface", "Endpoints", "IndicatorApiEndpoints.cs"));
        var reader = File.ReadAllText(Path.Combine(
            web, "Infrastructure", "Persistence", "Execution", "EfObligationQueryReader.cs"));

        Assert.Equal(1, Count(endpoint, "MapGet("));
        Assert.Contains("/api/v1/indicators", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost(", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/direction/overview", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("amount", endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payroll", endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsolationLevel.RepeatableRead", reader, StringComparison.Ordinal);
        Assert.Contains("SET TRANSACTION READ ONLY", reader, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking()", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditEvents.Add", reader, StringComparison.Ordinal);

        var migrations = Directory.EnumerateFiles(Path.Combine(web, "Infrastructure", "Persistence", "Migrations"),
            "*.cs", SearchOption.TopDirectoryOnly).ToArray();
        Assert.DoesNotContain(migrations,
            path => Path.GetFileName(path).Contains("Indicator", StringComparison.OrdinalIgnoreCase));
    }

    private static int Count(string value, string search)
    {
        var count = 0;
        for (var offset = 0;
             (offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0;
             offset += search.Length)
        {
            count++;
        }

        return count;
    }
}
