using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class BuildingBlocksDependencyTests
{
    [Fact]
    public void BuildingBlocks_DoesNotDependOnWeb()
    {
        var snapshot = ArchitectureRules.LoadRepository(
            ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory));
        var violations = ArchitectureRules
            .Analyze(snapshot)
            .Where(violation => string.Equals(violation.RuleId, "ARCH-004", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Sgol.BuildingBlocks dependency direction violations:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }
}
