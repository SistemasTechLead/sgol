using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class Front016ArchitectureTests
{
    [Fact]
    public void ConsumerUsesOnlyApprovedReadApisAndNoticeMutationWithoutDatabaseAccess()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var pages = Path.Combine(root, "src", "Sgol.Web", "Pages", "MyWork");
        var source = string.Join('\n', Directory.EnumerateFiles(pages, "*.cs").Select(File.ReadAllText));
        Assert.Contains("/api/v1/me/inbox", source, StringComparison.Ordinal);
        Assert.Contains("/api/v1/obligations", source, StringComparison.Ordinal);
        Assert.Contains("/api/v1/me/notices/", source, StringComparison.Ordinal);
        Assert.Contains("OnPostReadNoticeAsync", source, StringComparison.Ordinal);
        Assert.Contains("SendValidatedAsync<ReadInternalNoticeResult>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SgolDbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/weeks", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiMutationIntent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/conclusion", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/evidence", source, StringComparison.Ordinal);
        Assert.Contains("private, no-store", File.ReadAllText(Path.Combine(pages, "Details.cshtml.cs")), StringComparison.Ordinal);
    }
}
