using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class Front017ArchitectureTests
{
    [Fact]
    public void ContributionConsumerKeepsApiBoundaryAndExcludesLaterCapabilities()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var source = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "Pages", "MyWork", "Details.Evidence.cs"));
        Assert.Contains("/api/v1/files/upload-intents", source);
        Assert.Contains("/complete", source);
        Assert.Contains("/status", source);
        Assert.Contains("ValidateRequestAsync", source);
        Assert.Contains("ApiMutationIntent.FromKey", source);
        Assert.DoesNotContain("SgolDbContext", source);
        Assert.DoesNotContain("SaveChanges", source);
        Assert.DoesNotContain("/preview", source);
        Assert.DoesNotContain("/download", source);
        Assert.DoesNotContain("/conclusion", source);
        Assert.DoesNotContain("HttpMethod.Put", source);
        var browser = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "wwwroot", "js", "evidence-contribution.js"));
        Assert.Contains("xhr.withCredentials = false", browser);
        Assert.Contains("scanState !== \"LIMPIO\"", browser);
        Assert.DoesNotContain("localStorage", browser);
        Assert.DoesNotContain("sessionStorage", browser);
        Assert.DoesNotContain("console.", browser);
    }
}
