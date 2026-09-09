using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Assignment.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Assignment;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Sgol.Web.Infrastructure.Persistence.Notifications;

namespace Sgol.JobInfrastructure;

public static class JobServiceCollectionExtensions
{
    public const string ConnectionStringName = "Sgol";

    public static IServiceCollection AddSgolJobInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(_ => new JobDatabaseOptions(GetRequiredConnectionString(configuration)));
        services.AddDbContext<SgolDbContext>(options =>
        {
            var connectionString = GetRequiredConnectionString(configuration);
            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(SgolDbContext).Assembly.FullName);
                    npgsql.CommandTimeout(15);
                });
        });
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IUuidGenerator, Uuid7Generator>();
        services.AddScoped<OutboxHandlerRegistry>();
        services.AddScoped<ScheduledJobRegistry>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<OutboxProcessor>();
        services.AddScoped<ScheduledJobRunner>();
        services.AddSingleton<IOutboxLoop, OutboxLoop>();

        return services;
    }

    public static bool HasValidConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        try
        {
            _ = GetRequiredConnectionString(configuration);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    public static IServiceCollection AddSgolRecurringGeneration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(_ =>
            TimeZoneInfo.FindSystemTimeZoneById(RecurringGenerationContract.TimeZone));
        services.AddScoped<AuditTransaction>();
        services.AddScoped<IWorkObligationMaterializer, EfWorkObligationMaterializer>();
        services.AddScoped<IEligibilityEvaluationService, EfEligibilityEvaluationService>();
        services.AddScoped<IInternalNoticeWriter, EfInternalNoticeWriter>();
        services.AddScoped<IAutomaticAssignmentService, EfAutomaticAssignmentService>();
        services.AddScoped<IRecurringOccurrenceProcessor, EfRecurringOccurrenceProcessor>();
        services.AddScoped<IScheduledJob, RecurringGenerationJob>();
        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is required when persistence is used.");
        }

        _ = new NpgsqlConnectionStringBuilder(connectionString);
        return connectionString;
    }
}
