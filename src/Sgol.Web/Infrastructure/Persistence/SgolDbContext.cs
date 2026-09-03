using Microsoft.EntityFrameworkCore;
using Sgol.Configuration.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Infrastructure.Persistence;

public sealed class SgolDbContext(DbContextOptions<SgolDbContext> options) : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<EmploymentVersion> EmploymentVersions => Set<EmploymentVersion>();

    public DbSet<AvailabilityDayVersion> AvailabilityDayVersions => Set<AvailabilityDayVersion>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<IdentityCredential> IdentityCredentials => Set<IdentityCredential>();

    public DbSet<RoleAssignmentVersion> RoleAssignmentVersions => Set<RoleAssignmentVersion>();

    public DbSet<DirectionBootstrapMarker> DirectionBootstrapMarkers => Set<DirectionBootstrapMarker>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<ConfigurationRelease> ConfigurationReleases => Set<ConfigurationRelease>();

    public DbSet<CalendarDayVersion> CalendarDayVersions => Set<CalendarDayVersion>();

    public DbSet<TaskDefinition> TaskDefinitions => Set<TaskDefinition>();

    public DbSet<TaskDefinitionVersion> TaskDefinitionVersions => Set<TaskDefinitionVersion>();

    public DbSet<EligibilityPolicyVersion> EligibilityPolicyVersions => Set<EligibilityPolicyVersion>();

    public DbSet<ActivationRuleVersion> ActivationRuleVersions => Set<ActivationRuleVersion>();

    public DbSet<WeekPeriod> WeekPeriods => Set<WeekPeriod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new BranchConfiguration());
        modelBuilder.ApplyConfiguration(new PersonConfiguration());
        modelBuilder.ApplyConfiguration(new EmploymentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new AvailabilityDayVersionConfiguration());
        modelBuilder.ApplyConfiguration(new AppUserConfiguration());
        modelBuilder.ApplyConfiguration(new IdentityCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new RoleAssignmentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new DirectionBootstrapMarkerConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyRecordConfiguration());
        modelBuilder.ApplyConfiguration(new ConfigurationReleaseConfiguration());
        modelBuilder.ApplyConfiguration(new CalendarDayVersionConfiguration());
        modelBuilder.ApplyConfiguration(new TaskDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new TaskDefinitionVersionConfiguration());
        modelBuilder.ApplyConfiguration(new EligibilityPolicyVersionConfiguration());
        modelBuilder.ApplyConfiguration(new ActivationRuleVersionConfiguration());
        modelBuilder.ApplyConfiguration(new WeekPeriodConfiguration());
    }
}
