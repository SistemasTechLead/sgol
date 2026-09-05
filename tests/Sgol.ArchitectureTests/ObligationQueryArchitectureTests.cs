using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed partial class ObligationQueryArchitectureTests
{
    [Fact]
    public void ExecutionContractsRemainIndependentAndReaderIsStructurallyReadOnly()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var projectPath = Path.Combine(root, "src", "Modules", "Execution", "Sgol.Execution.csproj");
        var project = XDocument.Load(projectPath);
        Assert.DoesNotContain(project.Descendants(), item => item.Name.LocalName == "ProjectReference");
        Assert.DoesNotContain(project.Descendants(), item => item.Name.LocalName == "PackageReference");

        var contracts = File.ReadAllText(Path.Combine(
            root, "src", "Modules", "Execution", "Contracts", "ObligationQueries.cs"));
        Assert.DoesNotContain("Microsoft.AspNetCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Web", contracts, StringComparison.Ordinal);

        var reader = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Sgol.Web",
            "Infrastructure",
            "Persistence",
            "Execution",
            "EfObligationQueryReader.cs"));
        Assert.Contains("AsNoTracking()", reader, StringComparison.Ordinal);
        Assert.Contains("SET TRANSACTION READ ONLY", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditEvents.Add", reader, StringComparison.Ordinal);
    }

    [Fact]
    public void Hu023AddsOnlyTheTwoApprovedGetRoutesAndNoUserInterface()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var endpoints = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Sgol.Web",
            "Interface",
            "Endpoints",
            "ObligationQueryApiEndpoints.cs"));
        var routes = ObligationRouteRegex().Matches(endpoints).Select(match => match.Value).ToArray();
        Assert.Equal(2, routes.Length);
        Assert.Contains("MapGet(\"/api/v1/obligations\"", routes, StringComparer.Ordinal);
        Assert.Contains("MapGet(\"/api/v1/obligations/{id}\"", routes, StringComparer.Ordinal);
        Assert.DoesNotContain("MapPost", endpoints, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPut", endpoints, StringComparison.Ordinal);
        Assert.DoesNotContain("MapDelete", endpoints, StringComparison.Ordinal);

        var hu023InterfaceFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Sgol.Web"), "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetFileName(path).Contains("Obligation", StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetExtension(path) is ".cshtml" or ".razor" or ".css" or ".js")
            .ToArray();
        Assert.Empty(hu023InterfaceFiles);
    }

    [GeneratedRegex("MapGet\\(\\\"/api/v1/obligations(?:/\\{id\\})?\\\"")]
    private static partial Regex ObligationRouteRegex();
}
