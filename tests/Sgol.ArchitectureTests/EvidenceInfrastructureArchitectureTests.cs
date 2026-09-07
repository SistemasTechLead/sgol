using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class EvidenceInfrastructureArchitectureTests
{
    [Fact]
    public void EvidenceModuleOwnsContractsWithoutProviderOrHostDependencies()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var module = Path.Combine(root, "src", "Modules", "Evidence");
        var project = XDocument.Load(Path.Combine(module, "Sgol.Evidence.csproj"));
        var dependencies = project.Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();
        var source = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(module, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Empty(dependencies);
        Assert.DoesNotContain("Amazon.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TechEvidDoesNotIntroduceHu025SurfaceOrPersistence()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var web = Path.Combine(root, "src", "Sgol.Web");
        var evidenceInfrastructure = Path.Combine(web, "Infrastructure", "Evidence");
        var source = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(evidenceInfrastructure, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        var migrationSource = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(web, "Infrastructure", "Persistence", "Migrations"), "*.cs")
                .Select(File.ReadAllText));

        Assert.DoesNotContain("MapPost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("upload-intents", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EvidenceItem", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EvidenceVersion", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FileObject", source, StringComparison.Ordinal);
        Assert.DoesNotContain("file_object", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidence_item", migrationSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidence_version", migrationSource, StringComparison.OrdinalIgnoreCase);
    }
}
