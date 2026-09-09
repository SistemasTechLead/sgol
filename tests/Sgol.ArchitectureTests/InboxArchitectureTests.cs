using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class InboxArchitectureTests
{
    [Fact]
    public void NotificationsContractsRemainHostAndProviderIndependent()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var module = Path.Combine(root, "src", "Modules", "Notifications");
        var project = XDocument.Load(Path.Combine(module, "Sgol.Notifications.csproj"));
        var dependencies = project.Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();
        var source = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(module, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Empty(dependencies);
        Assert.DoesNotContain("Microsoft.AspNetCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Web", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Hu030UsesOnlyApprovedRoutesPersistenceAndReadOnlyProjection()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var web = Path.Combine(root, "src", "Sgol.Web");
        var endpoint = File.ReadAllText(Path.Combine(web, "Interface", "Endpoints", "InboxApiEndpoints.cs"));
        var reader = File.ReadAllText(Path.Combine(web, "Infrastructure", "Persistence", "Notifications", "EfInboxReader.cs"));
        var migration = File.ReadAllText(
            Directory.EnumerateFiles(Path.Combine(web, "Infrastructure", "Persistence", "Migrations"),
                "*_AddInternalNotices.cs").Single());

        Assert.Equal(1, Count(endpoint, "MapGet("));
        Assert.Equal(1, Count(endpoint, "MapPost("));
        Assert.Contains("/api/v1/me/inbox", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/v1/me/notices/{id}/read", endpoint, StringComparison.Ordinal);
        Assert.Contains("IAntiforgery", endpoint, StringComparison.Ordinal);
        Assert.Contains("SET TRANSACTION READ ONLY", reader, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.RepeatableRead", reader, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking()", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditEvents.Add", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("IPrivateObjectStorage", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("Clam", reader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internal_notice", migration, StringComparison.Ordinal);
        Assert.Contains("TR_assignment_version_notice", migration, StringComparison.Ordinal);
        Assert.Contains("TR_internal_notice_no_delete", migration, StringComparison.Ordinal);
        Assert.Contains("Rollback is blocked", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("validation_requirement", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outbox", migration, StringComparison.OrdinalIgnoreCase);

        var ui = Directory.EnumerateFiles(web, "*Inbox*", SearchOption.AllDirectories)
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
