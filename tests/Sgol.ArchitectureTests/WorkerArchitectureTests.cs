using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class WorkerArchitectureTests
{
    [Fact]
    public void WorkerIsAnExecutableCompositionHostWithoutHttpSurfaceOrBusinessRules()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var projectPath = Path.Combine(root, "src", "Sgol.Worker", "Sgol.Worker.csproj");
        var project = XDocument.Load(projectPath);
        Assert.Contains(
            project.Descendants().Where(element => element.Name.LocalName == "OutputType"),
            element => string.Equals(element.Value, "Exe", StringComparison.Ordinal));

        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.GetDirectoryName(projectPath)!, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("WebApplication", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapGet", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Kestrel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Sgol.Worker.Domain", source, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Sgol.Worker.Features", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RecurringRulesRemainOutsideTheWorkerAndGenerationDomainAvoidsInfrastructure()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var workerDirectory = Path.Combine(root, "src", "Sgol.Worker");
        var workerSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(workerDirectory, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("WORKING_DAY_WINDOW_V1", workerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("12:00", workerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("17:00", workerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", workerSource, StringComparison.Ordinal);

        var generationProject = XDocument.Load(Path.Combine(
            root,
            "src",
            "Modules",
            "Generation",
            "Sgol.Generation.csproj"));
        var dependencies = generationProject.Descendants()
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();
        Assert.DoesNotContain(dependencies, dependency =>
            dependency.Contains("Sgol.Web", StringComparison.Ordinal) ||
            dependency.Contains("EntityFrameworkCore", StringComparison.Ordinal) ||
            dependency.Contains("Npgsql", StringComparison.Ordinal));
    }
}
