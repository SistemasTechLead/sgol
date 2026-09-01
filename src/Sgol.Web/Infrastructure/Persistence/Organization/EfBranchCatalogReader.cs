using Microsoft.EntityFrameworkCore;
using Sgol.Organization.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Organization;

public sealed class EfBranchCatalogReader(SgolDbContext context) : IBranchCatalogReader
{
    public Task<BranchCatalogItem?> FindAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        var canonicalCode = BranchScope.RequireLoretta(branchCode);

        return context.Branches
            .AsNoTracking()
            .Where(branch => branch.Code == canonicalCode)
            .Select(branch => new BranchCatalogItem(
                branch.Id,
                branch.Code,
                branch.Name,
                branch.Status,
                branch.TimeZone))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BranchCatalogItem>> ListForScopeAsync(
        string scope,
        CancellationToken cancellationToken = default)
    {
        var canonicalCode = BranchScope.ResolveQueryScope(scope);

        return await context.Branches
            .AsNoTracking()
            .Where(branch => branch.Code == canonicalCode)
            .Select(branch => new BranchCatalogItem(
                branch.Id,
                branch.Code,
                branch.Name,
                branch.Status,
                branch.TimeZone))
            .ToArrayAsync(cancellationToken);
    }
}
