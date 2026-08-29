using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class TraceabilityTemplateTests
{
    private static readonly string[] RequiredFields =
    [
        "ID de prueba",
        "HU",
        "CAP",
        "CA/CP/CAT/NFR",
        "Commit o digest",
        "Entorno",
        "Fecha",
        "Datos sintéticos",
        "Resultado",
        "Artefactos",
        "Defecto relacionado"
    ];

    [Fact]
    public void EvidenceTemplate_ContainsRequiredTraceabilityAndSafetyFields()
    {
        var repositoryRoot = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var templatePath = Path.Combine(
            repositoryRoot,
            "docs",
            "traceability",
            "TEST_EVIDENCE_TEMPLATE.md");
        var template = File.ReadAllText(templatePath);

        Assert.All(
            RequiredFields,
            field => Assert.Contains(field, template, StringComparison.Ordinal));
        Assert.Contains("No registrar", template, StringComparison.Ordinal);
        Assert.Contains("TOTP", template, StringComparison.Ordinal);
        Assert.Contains("datos personales", template, StringComparison.Ordinal);
        Assert.Contains("evidencia real", template, StringComparison.Ordinal);
    }
}
