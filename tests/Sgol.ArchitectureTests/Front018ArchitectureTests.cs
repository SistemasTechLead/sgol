using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class Front018ArchitectureTests
{
    private static readonly string[] ConsumerFiles = ["Details.EvidenceReview.cs", "Details.EvidenceReplacement.cs", "Details.Conclusion.cs"];
    [Fact]
    public void ApprovedConsumerKeepsReadBoundaryExplicitReviewAndNoLaterCapabilities()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var pages = Path.Combine(root, "src", "Sgol.Web", "Pages", "MyWork");
        var source = string.Join('\n', ConsumerFiles.Select(f => File.ReadAllText(Path.Combine(pages, f))));
        Assert.Contains("/evidence-review", source); Assert.Contains("/replacements", source); Assert.Contains("/conclusion", source);
        Assert.Contains("Body: null", source); Assert.Contains("IfMatch: intention.ETag", source); Assert.Contains("ReplacementETag(intention)", source);
        Assert.DoesNotContain("SgolDbContext", source); Assert.DoesNotContain("SaveChanges", source); Assert.DoesNotContain("/download", source);
        Assert.DoesNotContain("/preview", source); Assert.DoesNotContain("/validation", source); Assert.DoesNotContain("/api/v1/audit", source);
        var get = File.ReadAllText(Path.Combine(pages, "Details.cshtml.cs"));
        Assert.DoesNotContain("/evidence-review", get); Assert.DoesNotContain("LoadReview", get);
        var browser = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "wwwroot", "js", "my-work.js"));
        Assert.DoesNotContain("localStorage", browser); Assert.DoesNotContain("sessionStorage", browser); Assert.DoesNotContain("console.", browser);
    }
}
