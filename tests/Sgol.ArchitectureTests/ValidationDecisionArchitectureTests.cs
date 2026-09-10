using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class ValidationDecisionArchitectureTests
{
    [Fact]
    public void Hu028AddsOnlyApprovedValidationDecisionSurface()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var web = Path.Combine(root, "src", "Sgol.Web");
        var endpoint = File.ReadAllText(Path.Combine(web, "Interface", "Endpoints", "ValidationDecisionApiEndpoints.cs"));
        var service = File.ReadAllText(Path.Combine(web, "Infrastructure", "Persistence", "Validation", "EfValidationDecisionService.cs"));
        var migration = File.ReadAllText(Directory.EnumerateFiles(Path.Combine(web, "Infrastructure", "Persistence", "Migrations"),
            "*_AddValidationDecisions.cs").Single());

        Assert.Contains("/api/v1/obligations/{id}/validation-decisions", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/v1/validation-decisions/{id}/replacements", endpoint, StringComparison.Ordinal);
        Assert.Contains("/api/v1/obligations/{id}/validations", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/validations/pending", endpoint, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", service, StringComparison.Ordinal);
        Assert.Contains("IEvidenceConclusionReviewService", service, StringComparison.Ordinal);
        Assert.Contains("POLITICA_VALIDACION_NO_DISPONIBLE", service, StringComparison.Ordinal);
        Assert.Contains("AUTOVALIDACION_NO_PERMITIDA", service, StringComparison.Ordinal);
        Assert.Contains("validation_requirement", migration, StringComparison.Ordinal);
        Assert.Contains("validation_decision_version", migration, StringComparison.Ordinal);
        Assert.Contains("Rollback is blocked for AddValidationDecisions", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("InternalNotice", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Outbox", service, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationDecisionContractsRemainFrameworkIndependent()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var contract = File.ReadAllText(Path.Combine(root, "src", "Modules", "Execution", "Contracts", "ValidationDecisions.cs"));
        Assert.DoesNotContain("Microsoft.AspNetCore", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", contract, StringComparison.Ordinal);
        Assert.Contains("PER-VALIDACION-EMITIR", contract, StringComparison.Ordinal);
        Assert.Contains("PER-VALIDACION-ESCALAR", contract, StringComparison.Ordinal);
        Assert.Contains("PER-VALIDACION-SUSTITUIR", contract, StringComparison.Ordinal);
    }
}
