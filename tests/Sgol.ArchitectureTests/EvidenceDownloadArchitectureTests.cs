using Xunit;

namespace Sgol.ArchitectureTests;

public sealed class EvidenceDownloadArchitectureTests
{
    [Fact]
    public void DownloadContractHasNoProviderAndReadServiceCannotWriteOrRepair()
    {
        var root = ArchitectureBoundaryTests.FindRepositoryRoot(AppContext.BaseDirectory);
        var web = Path.Combine(root, "src", "Sgol.Web");
        var contracts = File.ReadAllText(Path.Combine(root, "src", "Modules", "Evidence", "Contracts", "EvidenceDownloads.cs"));
        foreach (var provider in new[] { "Microsoft.EntityFrameworkCore", "Amazon.S3", "Microsoft.AspNetCore" })
            Assert.DoesNotContain(provider, contracts, StringComparison.Ordinal);
        var source = File.ReadAllText(Path.Combine(web, "Infrastructure", "Persistence", "Evidence", "EfEvidenceDownloadService.cs"));
        Assert.Contains("IsolationLevel.RepeatableRead", source, StringComparison.Ordinal);
        Assert.Contains("SET TRANSACTION READ ONLY", source, StringComparison.Ordinal);
        foreach (var operation in new[] { "SaveChanges", "ExecuteUpdate", "ExecuteDelete", "PromoteToClean", "DeleteAsync", "PutQuarantine", "IOutboxWriter", "AuditTransaction" })
            Assert.DoesNotContain(operation, source, StringComparison.Ordinal);
        var endpoint = File.ReadAllText(Path.Combine(web, "Interface", "Endpoints", "EvidenceApiEndpoints.cs"));
        var download = endpoint[endpoint.IndexOf("public static async Task<IResult> DownloadAsync", StringComparison.Ordinal)..endpoint.IndexOf("public static async Task<IResult> ListAsync", StringComparison.Ordinal)];
        Assert.DoesNotContain("DbContext", download, StringComparison.Ordinal);
        Assert.DoesNotContain("AmazonS3", download, StringComparison.Ordinal);
        Assert.DoesNotContain("Results.Redirect", download, StringComparison.Ordinal);
        Assert.DoesNotContain("Results.File", download, StringComparison.Ordinal);
        Assert.DoesNotContain("/preview", endpoint, StringComparison.Ordinal);
    }
}
