using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal static class TaskCatalogAuthorization
{
    public static async Task RequireReadAsync(SgolDbContext db, Guid actor, DateTimeOffset at, CancellationToken token)
    {
        var allowed = await (from user in db.AppUsers
                             join employment in db.EmploymentVersions on user.PersonId equals employment.PersonId
                             join role in db.RoleAssignmentVersions on user.Id equals role.UserId
                             where user.Id == actor && user.Status == AccountStatus.Active &&
                                 employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                                 employment.ValidFrom <= at && (employment.ValidTo == null || employment.ValidTo > at) &&
                                 role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active &&
                                 role.ValidFrom <= at && (role.ValidTo == null || role.ValidTo > at) &&
                                 (role.RoleCode == CanonicalRole.Direction || role.RoleCode == CanonicalRole.Administration)
                             select user.Id).AnyAsync(token);
        if (!allowed) throw new DirectionPlanException("ACCESO_DENEGADO", 403);
    }
}
