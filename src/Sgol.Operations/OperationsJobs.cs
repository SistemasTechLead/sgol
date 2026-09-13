using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sgol.JobInfrastructure;

namespace Sgol.Operations;

public sealed class PostgreSqlPortableBackupJob(IPostgreSqlPortableBackup backup) : IScheduledJob
{
    public const string JobName = "POSTGRESQL_PORTABLE_BACKUP";
    public string Name => JobName;
    public bool PreventOverlappingSlots => true;
    public string ConcurrencyExhaustedErrorCode => "BACKUP_POSTGRES_CONCURRENCY_EXHAUSTED";

    public Task ExecuteAsync(ScheduledJobContext context, CancellationToken cancellationToken) =>
        backup.ExecuteAsync(context.ScheduledFor, cancellationToken);
}
public sealed class ReplicateEvidenceObjectsJob(IObjectReplica replica) : IScheduledJob
{
    public const string JobName = "REPLICATE_EVIDENCE_OBJECTS";
    public string Name => JobName;
    public bool PreventOverlappingSlots => true;
    public string ConcurrencyExhaustedErrorCode => "REPLICA_POSTGRES_CONCURRENCY_EXHAUSTED";

    public Task ExecuteAsync(ScheduledJobContext context, CancellationToken cancellationToken) =>
        replica.ExecuteAsync(context.ScheduledFor, cancellationToken);
}

public static class OperationsServiceCollectionExtensions
{
    public static IServiceCollection AddSgolPortableOperations(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IBackupProcessPipeline, BackupProcessPipeline>();
        services.TryAddSingleton<IPostgreSqlPortableBackup, PostgreSqlPortableBackup>();
        services.TryAddSingleton<IObjectReplica, ObjectReplica>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IScheduledJob, PostgreSqlPortableBackupJob>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IScheduledJob, ReplicateEvidenceObjectsJob>());
        return services;
    }
}
