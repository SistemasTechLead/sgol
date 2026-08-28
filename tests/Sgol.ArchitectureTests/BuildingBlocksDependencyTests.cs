using System.Xml.Linq;
using Sgol.BuildingBlocks;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class BuildingBlocksDependencyTests
{
    [Fact]
    public void BuildingBlocks_DoesNotDependOnWeb()
    {
        var assemblyReferences = typeof(BuildingBlocksAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies();

        Assert.DoesNotContain(
            assemblyReferences,
            reference => string.Equals(reference.Name, "Sgol.Web", StringComparison.Ordinal));

        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "Sgol.BuildingBlocks",
            "Sgol.BuildingBlocks.csproj");
        var project = XDocument.Load(projectPath);
        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null);

        Assert.DoesNotContain(
            projectReferences,
            include => include!.Replace('\\', '/').EndsWith("/Sgol.Web.csproj", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SGOL.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the SGOL repository root.");
    }
}
