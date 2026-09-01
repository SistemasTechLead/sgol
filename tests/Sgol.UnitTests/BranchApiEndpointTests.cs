using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sgol.Organization.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class BranchApiEndpointTests
{
    [Fact]
    public async Task AuthenticatedLorettaRequest_ReturnsCatalogItem()
    {
        var reader = new RecordingReader(CreateLoretta());
        var context = CreateContext(isAuthenticated: true);

        var result = await BranchApiEndpoints.HandleGetAsync(
            BranchScope.LorettaCode,
            context,
            reader,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(1, reader.CallCount);
    }

    [Theory]
    [InlineData("TODAS")]
    [InlineData("LOR-002")]
    public async Task UnsupportedCode_IsRejectedWithoutReadingCatalog(string branchCode)
    {
        var reader = new RecordingReader(CreateLoretta());
        var context = CreateContext(isAuthenticated: true);

        var result = await BranchApiEndpoints.HandleGetAsync(
            branchCode,
            context,
            reader,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status422UnprocessableEntity,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task UnauthenticatedRequest_IsDeniedWithoutReadingCatalog()
    {
        var reader = new RecordingReader(CreateLoretta());
        var context = CreateContext(isAuthenticated: false);

        var result = await BranchApiEndpoints.HandleGetAsync(
            BranchScope.LorettaCode,
            context,
            reader,
            CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, reader.CallCount);
    }

    private static DefaultHttpContext CreateContext(bool isAuthenticated)
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = Guid.CreateVersion7().ToString("D");
        if (isAuthenticated)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "synthetic-direction")],
                "synthetic"));
        }

        return context;
    }

    private static BranchCatalogItem CreateLoretta() => new(
        BranchScope.LorettaId,
        BranchScope.LorettaCode,
        BranchScope.LorettaName,
        BranchScope.ActiveStatus,
        BranchScope.TimeZone);

    private sealed class RecordingReader(BranchCatalogItem item) : IBranchCatalogReader
    {
        public int CallCount { get; private set; }

        public Task<BranchCatalogItem?> FindAsync(
            string branchCode,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Assert.Equal(BranchScope.LorettaCode, branchCode);
            return Task.FromResult<BranchCatalogItem?>(item);
        }

        public Task<IReadOnlyList<BranchCatalogItem>> ListForScopeAsync(
            string scope,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
