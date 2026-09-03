using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Generation;

internal static class GenerationAuthorizationQuery
{
    public static Task<string?> GetCreatorRoleAsync(
        SgolDbContext dbContext,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        (
            from user in dbContext.AppUsers.AsNoTracking()
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            where user.Id == actorUserId &&
                user.Status == BootstrapContract.ActiveAccountStatus &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == BootstrapContract.ActiveRoleStatus &&
                role.ValidTo == null &&
                role.RoleCode != CanonicalRole.SalesFloor &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidTo == null
            select role.RoleCode)
        .SingleOrDefaultAsync(cancellationToken);
}
