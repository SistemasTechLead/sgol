using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class Cv04DemoArchitectureTests
{
    [Fact]
    public void TechE2eCv04_is_an_isolated_hosted_non_product_harness()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var demo = Path.Combine(root, "tests", "Sgol.Cv04Demo");
        var project = XDocument.Load(Path.Combine(demo, "Sgol.Cv04Demo.csproj"));
        var packages = project.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var references = project.Descendants("ProjectReference")
            .Select(element => (element.Attribute("Include")?.Value ?? string.Empty).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var source = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(demo, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.EndsWith("Tests.cs", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        Assert.Equal(
            ["Microsoft.NET.Test.Sdk", "Testcontainers", "Testcontainers.PostgreSql", "xunit", "xunit.runner.visualstudio"],
            packages);
        Assert.Equal(["../../src/Sgol.Web/Sgol.Web.csproj"], references);
        Assert.DoesNotContain("Microsoft.Playwright", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ClaimsPrincipal", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ClaimsIdentity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Cv03Demo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Cv02Demo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapGet(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost(", source, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(demo, "*.cshtml", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(demo, "*.razor", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(demo, "*Migration*.cs", SearchOption.AllDirectories));

        foreach (var sourceProject in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("Sgol.Cv04Demo", File.ReadAllText(sourceProject), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TechE2eCv04_launcher_is_finite_and_captures_native_exit()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var script = File.ReadAllText(Path.Combine(root, "scripts", "demo", "run-cv04.ps1"));

        Assert.Contains("tests/Sgol.Cv04Demo/Sgol.Cv04Demo.csproj", script, StringComparison.Ordinal);
        Assert.Contains("$demoExit = $LASTEXITCODE", script, StringComparison.Ordinal);
        Assert.Contains("--mode $Mode", script, StringComparison.Ordinal);
        Assert.DoesNotContain("docker ", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("playwright install", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet tool install", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sgol.Cv03Demo", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Cv02Demo", script, StringComparison.Ordinal);
    }
}
