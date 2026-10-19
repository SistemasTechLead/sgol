using Microsoft.EntityFrameworkCore;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

internal static class ConfigurationAuthorizationQuery
{
    public static Task<bool> IsDirectionAsync(
        SgolDbContext dbContext,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        (
            from user in dbContext.AppUsers.AsNoTracking()
            join role in dbContext.RoleAssignmentVersions.AsNoTracking()
                on user.Id equals role.UserId
            join employment in dbContext.EmploymentVersions.AsNoTracking()
                on user.PersonId equals employment.PersonId
            where user.Id == actorUserId &&
                user.Status == BootstrapContract.ActiveAccountStatus &&
                role.BranchId == BranchScope.LorettaId &&
                role.RoleCode == BootstrapContract.DirectionRoleCode &&
                role.Status == BootstrapContract.ActiveRoleStatus &&
                role.ValidTo == null &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidTo == null
            select user.Id)
        .AnyAsync(cancellationToken);

    public static Task<bool> IsActiveLorettaUserAsync(
        SgolDbContext dbContext,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking()
                on user.PersonId equals employment.PersonId
            where user.Id == actorUserId &&
                user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidTo == null
            select user.Id)
        .AnyAsync(cancellationToken);
}
