using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sgol.Cv05Demo;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.FrontendBrowserTests;

internal sealed partial class BrowserFixture
{
    // The existing CV-05 owner validates/provisions this disposable database and owns its cleanup.
    internal async Task AttachTechFront005Async(Cv05Infrastructure owner)
    {
        connectionString = owner.ConnectionString;
        BaseAddress = owner.BaseAddress;
        Accounts = [new(owner.DirectionUserId, owner.DirectionPersonId, owner.DirectionUserName,
            CanonicalRole.Direction, owner.DirectionTemporaryPassword, owner.DirectionNewPassword), .. Accounts.Skip(1)];
        await using var db = owner.CreateContext();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var hasher = new PasswordHasher<AppUser>();
        foreach (var account in Accounts.Skip(1))
        {
            db.People.Add(new Person
            {
                Id = account.PersonId,
                StableCode = "TF005-" + account.PersonId.ToString("N"),
                DisplayName = "Persona sintética " + account.Role,
                CreatedAt = now
            });
            db.EmploymentVersions.Add(new EmploymentVersion(Guid.CreateVersion7(), account.PersonId,
                BranchScope.LorettaId, EmploymentStatus.Active, now));
            var user = new AppUser
            {
                Id = account.UserId,
                PersonId = account.PersonId,
                Status = AccountStatus.Active,
                MustChangePassword = true,
                SecurityStamp = Guid.CreateVersion7().ToString("N")
            };
            db.AppUsers.Add(user);
            db.IdentityCredentials.Add(new IdentityCredential
            {
                UserId = user.Id,
                UserName = account.UserName,
                NormalizedUserName = account.UserName.ToUpperInvariant(),
                PasswordHash = hasher.HashPassword(user, account.TemporaryPassword)
            });
            db.RoleAssignmentVersions.Add(new RoleAssignmentVersion
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                BranchId = BranchScope.LorettaId,
                RoleCode = account.Role,
                Status = RoleAssignmentStatus.Active,
                ValidFrom = now
            });
        }
        await db.SaveChangesAsync();
    }

    internal SgolDbContext TechFront005Context() => new(new DbContextOptionsBuilder<SgolDbContext>()
        .UseNpgsql(connectionString ?? throw new InvalidOperationException("Fixture unavailable.")).Options);
}
