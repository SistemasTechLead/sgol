using Microsoft.EntityFrameworkCore;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence;

public sealed class SgolDbContext(DbContextOptions<SgolDbContext> options) : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<EmploymentVersion> EmploymentVersions => Set<EmploymentVersion>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<IdentityCredential> IdentityCredentials => Set<IdentityCredential>();

    public DbSet<RoleAssignmentVersion> RoleAssignmentVersions => Set<RoleAssignmentVersion>();

    public DbSet<DirectionBootstrapMarker> DirectionBootstrapMarkers => Set<DirectionBootstrapMarker>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new BranchConfiguration());
        modelBuilder.ApplyConfiguration(new PersonConfiguration());
        modelBuilder.ApplyConfiguration(new EmploymentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new AppUserConfiguration());
        modelBuilder.ApplyConfiguration(new IdentityCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new RoleAssignmentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new DirectionBootstrapMarkerConfiguration());
    }
}
