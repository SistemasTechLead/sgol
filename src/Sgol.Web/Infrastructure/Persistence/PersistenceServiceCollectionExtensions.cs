using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

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
        services.AddDirectionBootstrap();

        return services;
    }
}
