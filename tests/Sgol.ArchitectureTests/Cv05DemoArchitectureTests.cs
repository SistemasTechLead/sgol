using System.Xml.Linq;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class Cv05DemoArchitectureTests
{
    [Fact]
    public void DemoReferencesOnlyWebAndCannotCreateAnAcceptancePrincipal()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var demo = Path.Combine(root, "tests", "Sgol.Cv05Demo");
        var project = XDocument.Load(Path.Combine(demo, "Sgol.Cv05Demo.csproj"));
        var references = project.Descendants("ProjectReference")
            .Select(item => item.Attribute("Include")!.Value.Replace('\\', '/'))
            .ToArray();
        Assert.Equal(["../../src/Sgol.Web/Sgol.Web.csproj"], references);
        var source = string.Join(Environment.NewLine, Directory.EnumerateFiles(demo, "*.cs",
            SearchOption.TopDirectoryOnly).Select(File.ReadAllText));
        Assert.DoesNotContain("new ClaimsPrincipal", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ClaimsIdentity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Cv04Demo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Cv03Demo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sgol.Cv02Demo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Playwright", source, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(demo, "*.cshtml", SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.EnumerateFiles(demo, "*.razor", SearchOption.TopDirectoryOnly));
        foreach (var module in Directory.EnumerateDirectories(Path.Combine(root, "src")))
            foreach (var sourceProject in Directory.EnumerateFiles(module, "*.csproj", SearchOption.TopDirectoryOnly))
                Assert.DoesNotContain("Sgol.Cv05Demo", File.ReadAllText(sourceProject), StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherCapturesTheNativeExitAndKeepsOneAutomatedMode()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var script = File.ReadAllText(Path.Combine(root, "scripts", "demo", "run-cv05.ps1"));
        Assert.Contains("$demoExit = $LASTEXITCODE", script, StringComparison.Ordinal);
        Assert.Contains("[ValidateSet('Automated')]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("playwright", script, StringComparison.OrdinalIgnoreCase);
    }
}
