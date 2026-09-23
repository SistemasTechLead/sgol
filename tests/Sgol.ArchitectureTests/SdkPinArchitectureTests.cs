using System.Text.Json;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class SdkPinArchitectureTests
{
    [Fact]
    public void GlobalWorkflowAndOperationsDocumentationUseTheSameExactSdk()
    {
        const string expectedVersion = "10.0.400";
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);

        using var globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "global.json")));
        var sdk = globalJson.RootElement.GetProperty("sdk");
        Assert.Equal(expectedVersion, sdk.GetProperty("version").GetString());
        Assert.Equal("disable", sdk.GetProperty("rollForward").GetString());

        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "pull-request.yml"));
        Assert.Equal(2, CountOccurrences(workflow, $"dotnet-version: {expectedVersion}"));
        Assert.Contains($"actual_version=\"$(dotnet --version)\"", workflow, StringComparison.Ordinal);
        Assert.Contains($"[[ \"$actual_version\" != \"{expectedVersion}\" ]]", workflow, StringComparison.Ordinal);
        Assert.Contains($"test \"$(dotnet --version)\" = '{expectedVersion}'", workflow, StringComparison.Ordinal);

        var operations = File.ReadAllText(Path.Combine(root, "docs", "operations", "pull-request-pipeline.md"));
        Assert.Contains($"SDK .NET exactamente `{expectedVersion}`", operations, StringComparison.Ordinal);
        Assert.Contains($"| .NET SDK | {expectedVersion} |", operations, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
