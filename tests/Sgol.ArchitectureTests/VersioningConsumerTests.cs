using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class VersioningConsumerTests
{
    [Fact]
    public void ProductionVersioningConsumers_AreExactlyTheAuthorizedSeven()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var consumers = Directory.EnumerateFiles(Path.Combine(root, "src", "Modules"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(": IVersionedEntity", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(consumers.SetEquals(
        [
            "src/Modules/Configuration/Contracts/Calendar.cs",
            "src/Modules/Configuration/Contracts/ConfigurationReleases.cs",
            "src/Modules/Configuration/Contracts/ActivationPolicies.cs",
            "src/Modules/Configuration/Contracts/EligibilityPolicies.cs",
            "src/Modules/Configuration/Contracts/EvidencePolicies.cs",
            "src/Modules/Configuration/Contracts/TaskDefinitions.cs",
            "src/Modules/Configuration/Contracts/ValidationPolicies.cs",
        ]));
    }
}
