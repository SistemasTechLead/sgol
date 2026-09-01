namespace Sgol.Organization.Contracts;

public sealed record BranchCatalogItem(
    Guid Id,
    string Code,
    string Name,
    string Status,
    string TimeZone);

public interface IBranchCatalogReader
{
    Task<BranchCatalogItem?> FindAsync(string branchCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchCatalogItem>> ListForScopeAsync(
        string scope,
        CancellationToken cancellationToken = default);
}
