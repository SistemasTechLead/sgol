using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class MigrationConsumerInventoryTests
{
    [Theory]
    [InlineData("src/Sgol.Operations/FunctionalSnapshotReader.cs", "private const string ExpectedLatestMigration = ", 1)]
    [InlineData("tests/Sgol.Cv04Demo/DemoContract.cs", "public const string LatestMigration = ", 1)]
    [InlineData("tests/Sgol.Cv05Demo/DemoContract.cs", "public const string LatestMigration = ", 1)]
    [InlineData("scripts/operations/invoke-hu-035-amd64-gate.ps1", "$expectedMigration = ", 1)]
    [InlineData("tests/Sgol.OperationsIntegrationTests/FunctionalRecoveryAmd64GateTests.cs", "expectedMigration = ", 2)]
    public void CurrentSchemaConsumersPinTheLatestMigration(string relativePath, string declaration, int expectedCount)
    {
        var root = FindRepositoryRoot();
        var migrations = Path.Combine(root, "src", "Sgol.Web", "Infrastructure", "Persistence", "Migrations");
        var latest = Directory.EnumerateFiles(migrations, "*.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is { Length: > 15 } && name.Take(14).All(char.IsAsciiDigit)
                && !name.EndsWith(".Designer", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).Last();
        var declarations = File.ReadAllLines(Path.Combine(root, relativePath))
            .Select(line => line.TrimStart())
            .Where(line => line.StartsWith(declaration, StringComparison.Ordinal)).ToArray();
        Assert.Equal(expectedCount, declarations.Length);
        foreach (var line in declarations)
            Assert.Equal(latest, line.Split('"', '\'')[1]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGOL.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
