using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class Front019ArchitectureTests
{
    [Fact]
    public void ConsumerUsesExistingApiAndReadOnlyHistoryWithoutIndicatorsOrBinaryDownloads()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory); var web = Path.Combine(root, "src", "Sgol.Web");
        var index = File.ReadAllText(Path.Combine(web, "Pages", "Validations", "Index.cshtml.cs"));
        var detail = File.ReadAllText(Path.Combine(web, "Pages", "MyWork", "Details.Validation.cs"));
        var service = File.ReadAllText(Path.Combine(web, "Infrastructure", "Persistence", "Validation", "EfValidationDecisionService.cs"));
        Assert.Contains("/api/v1/validations/pending", index); Assert.Contains("/api/v1/supervision/obligations", index);
        Assert.DoesNotContain("SaveChanges", index); Assert.DoesNotContain("SgolDbContext", index); Assert.DoesNotContain("/api/v1/indicators", index);
        Assert.Contains("EvidenceFormAsync", detail); Assert.Contains("ApiMutationIntent.FromKey", detail); Assert.Contains("intention.ETag", detail);
        Assert.Contains("IsolationLevel.RepeatableRead", service); Assert.Contains("SET TRANSACTION READ ONLY", service);
        Assert.Contains("ValidationDecisionAuthorization.Issue", service); Assert.Contains("ValidationDecisionAuthorization.Replace", service);
        Assert.DoesNotContain("download", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CreateTable", index); Assert.DoesNotContain("upload-intents", detail);
    }
    [Fact]
    public void ViewCompositionKeepsSharedComponentsAndThreeExplicitHumanResults()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var source = File.ReadAllText(Path.Combine(root, "src", "Sgol.Web", "Pages", "MyWork", "_ValidationDecision.cshtml"));
        Assert.Contains("_RadioGroup", source); Assert.Contains("_TextArea", source); Assert.Contains("data-front-confirm", source);
        Assert.Contains("CUMPLIDA", source); Assert.Contains("INCOMPLETA", source); Assert.Contains("NO_CUMPLIDA", source);
        Assert.DoesNotContain("style=", source); Assert.DoesNotContain("localStorage", source); Assert.DoesNotContain("sessionStorage", source);
    }
}
