using Microsoft.EntityFrameworkCore;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.Web.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    private const string ConnectionStringName = "Sgol";

    public static IServiceCollection AddSgolPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<SgolDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is required when persistence is used.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(SgolDbContext).Assembly.FullName));
        });
        services.AddScoped<AuditTransaction>();
        services.AddScoped<VersioningTransaction>();
        services.AddScoped<IConfigurationReleaseService, EfConfigurationReleaseService>();
        services.AddScoped<ICalendarService, EfCalendarService>();
        services.AddScoped<IWeekPeriodService, EfWeekPeriodService>();
        services.AddDirectionBootstrap();
        services.AddScoped<IBranchCatalogReader, EfBranchCatalogReader>();
        services.AddScoped<IPersonAdministrationService, EfPersonAdministrationService>();
        services.AddScoped<IAvailabilityAdministrationService, EfAvailabilityAdministrationService>();
        services.AddScoped<IAccountAdministrationService, EfAccountAdministrationService>();
        services.AddScoped<EfRoleAssignmentService>();
        services.AddScoped<IRoleAssignmentService>(provider => provider.GetRequiredService<EfRoleAssignmentService>());
        services.AddScoped<IRoleHierarchyResolver>(provider => provider.GetRequiredService<EfRoleAssignmentService>());

        return services;
    }
}
