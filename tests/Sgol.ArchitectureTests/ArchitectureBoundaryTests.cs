using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly string[] ExpectedSyntheticRuleIds =
        ["ARCH-001", "ARCH-002", "ARCH-003", "ARCH-004"];

    [Fact]
    public void Repository_RespectsApprovedDependencyDirection()
    {
        var snapshot = ArchitectureRules.LoadRepository(FindRepositoryRoot(AppContext.BaseDirectory));
        var violations = ArchitectureRules.Analyze(snapshot);

        Assert.True(
            violations.Count == 0,
            "Approved architecture rule violations:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void SyntheticForbiddenDependencies_AreDetectedWithRuleDiagnostics()
    {
        var snapshot = new ArchitectureSnapshot(
        [
            new ArchitectureProject(
                "Sgol.Organization",
                "src/Modules/Organization/Organization.csproj",
                [],
                [
                    new ArchitectureSource(
                        "src/Modules/Organization/Domain/Person.cs",
                        "using Microsoft.EntityFrameworkCore; namespace Sgol.Organization.Domain;"),
                    new ArchitectureSource(
                        "src/Modules/Organization/Features/Assign.cs",
                        "using Sgol.Assignment.Infrastructure; namespace Sgol.Organization.Features;")
                ]),
            new ArchitectureProject(
                "Sgol.Web",
                "src/Sgol.Web/Sgol.Web.csproj",
                [],
                [
                    new ArchitectureSource(
                        "src/Sgol.Web/Features/BusinessRule.cs",
                        "using Sgol.Organization.Domain; namespace Sgol.Web.Features;")
                ]),
            new ArchitectureProject(
                "Sgol.BuildingBlocks",
                "src/Sgol.BuildingBlocks/Sgol.BuildingBlocks.csproj",
                ["Sgol.Worker"],
                [])
        ]);

        var violations = ArchitectureRules.Analyze(snapshot);
        var ruleIds = violations.Select(violation => violation.RuleId).ToHashSet(StringComparer.Ordinal);

        Assert.True(
            ExpectedSyntheticRuleIds.All(ruleIds.Contains),
            "The synthetic forbidden dependencies were not all detected:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
        Assert.All(violations, violation => Assert.False(string.IsNullOrWhiteSpace(violation.Message)));
    }

    internal static string FindRepositoryRoot(string startDirectory)
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
