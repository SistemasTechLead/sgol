using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Sgol.Organization.Contracts;
using Sgol.Web.Pages.Branches;
using Xunit;

namespace Sgol.UnitTests;

public sealed class BranchPageTests
{
    [Fact]
    public async Task Loretta_ShowsTheCanonicalActiveBranch()
    {
        var reader = new RecordingReader(CreateLoretta());
        var page = CreatePage(reader);

        await page.OnGetAsync(BranchScope.LorettaCode, CancellationToken.None);

        Assert.Equal(BranchScope.LorettaCode, page.Branch?.Code);
        Assert.Equal("Activa", page.ActiveBadge.Text);
        Assert.Null(page.Error);
        Assert.Null(page.EmptyState);
        Assert.False(page.IsLoading);
        Assert.Equal(StatusCodes.Status200OK, page.Response.StatusCode);
        Assert.Equal(1, reader.CallCount);
    }

    [Theory]
    [InlineData("TODAS")]
    [InlineData("LOR-002")]
    public async Task UnsupportedBranch_ShowsUsefulErrorWithoutReadingCatalog(string branchCode)
    {
        var reader = new RecordingReader(CreateLoretta());
        var page = CreatePage(reader);

        await page.OnGetAsync(branchCode, CancellationToken.None);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, page.Response.StatusCode);
        Assert.Contains("LOR-001", page.Error?.Message, StringComparison.Ordinal);
        Assert.Null(page.Branch);
        Assert.False(page.IsLoading);
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task MissingCanonicalSeed_ShowsEmptyStateWithRecoveryAction()
    {
        var reader = new RecordingReader(item: null);
        var page = CreatePage(reader);

        await page.OnGetAsync(BranchScope.LorettaCode, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, page.Response.StatusCode);
        Assert.NotNull(page.EmptyState);
        Assert.Equal("Volver a consultar", page.EmptyState.ActionLabel);
        Assert.Null(page.Error);
        Assert.False(page.IsLoading);
    }

    private static DetailsModel CreatePage(IBranchCatalogReader reader)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "synthetic-direction"),
                new Claim(ClaimTypes.Role, "DIRECCION"),
            ],
            "synthetic")),
        };
        context.TraceIdentifier = Guid.CreateVersion7().ToString("D");

        return new DetailsModel(reader)
        {
            PageContext = new PageContext
            {
                HttpContext = context,
                ViewData = new ViewDataDictionary(
                    new EmptyModelMetadataProvider(),
                    new ModelStateDictionary()),
            },
        };
    }

    private static BranchCatalogItem CreateLoretta() => new(
        BranchScope.LorettaId,
        BranchScope.LorettaCode,
        BranchScope.LorettaName,
        BranchScope.ActiveStatus,
        BranchScope.TimeZone);

    private sealed class RecordingReader(BranchCatalogItem? item) : IBranchCatalogReader
    {
        public int CallCount { get; private set; }

        public Task<BranchCatalogItem?> FindAsync(
            string branchCode,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Assert.Equal(BranchScope.LorettaCode, branchCode);
            return Task.FromResult(item);
        }

        public Task<IReadOnlyList<BranchCatalogItem>> ListForScopeAsync(
            string scope,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
