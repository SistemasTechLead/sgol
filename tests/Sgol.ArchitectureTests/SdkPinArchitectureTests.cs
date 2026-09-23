using System.Text.Json;
using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class SdkPinArchitectureTests
{
    [Fact]
    public void GlobalWorkflowAndOperationsDocumentationUseTheSameExactSdk()
    {
        const string expectedVersion = "10.0.400";
        const string expectedSdkDigest = "sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510";
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

        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));
        var fromLines = dockerfile.Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("FROM ", StringComparison.Ordinal))
            .ToArray();
        var sdkStage = Assert.Single(fromLines, line =>
            line.Contains("mcr.microsoft.com/dotnet/sdk:10.0-noble@", StringComparison.Ordinal));
        var runtimeStage = Assert.Single(fromLines, line =>
            line.Contains("mcr.microsoft.com/dotnet/aspnet:10.0-noble@", StringComparison.Ordinal));

        Assert.Contains($"mcr.microsoft.com/dotnet/sdk:10.0-noble@{expectedSdkDigest}", sdkStage,
            StringComparison.Ordinal);
        Assert.DoesNotContain("/aspnet:", sdkStage, StringComparison.Ordinal);
        Assert.DoesNotContain("/sdk:", runtimeStage, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSdkDigest, runtimeStage, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
