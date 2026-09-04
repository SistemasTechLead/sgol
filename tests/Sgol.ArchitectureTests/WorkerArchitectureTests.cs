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
}
