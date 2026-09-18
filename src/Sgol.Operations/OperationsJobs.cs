using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Sgol.Continuity.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Continuity;
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

public sealed class CaptureRecoveryReferenceJob(
    IFunctionalRecoveryOperations operations,
    IUuidGenerator uuidGenerator,
    TimeProvider timeProvider) : IScheduledJob
{
    public const string JobName = "CAPTURE_RECOVERY_REFERENCE";
    public string Name => JobName;
    public bool PreventOverlappingSlots => true;
    public string ConcurrencyExhaustedErrorCode => "POSTGRES_CONCURRENCY_EXHAUSTED";

    public async Task ExecuteAsync(ScheduledJobContext context, CancellationToken cancellationToken)
    {
        if (!TryReadReconciliationId(context.Checkpoint, out var reconciliationId))
            throw new JobExecutionException("RECOVERY_RECONCILIATION_ID_INVALID");
        var dbContext = context.DbContext;
        var latest = await dbContext.RecoveryReconciliationEvents.Where(item =>
                item.ReconciliationId == reconciliationId)
            .OrderByDescending(item => item.Sequence).FirstOrDefaultAsync(cancellationToken)
            ?? throw new JobExecutionException("RECOVERY_RECONCILIATION_NOT_FOUND");
        if (latest.Status == RecoveryReconciliationStatuses.ReferenceCapturing) return;
        if (latest.Status != RecoveryReconciliationStatuses.Requested)
            throw new JobExecutionException("RECOVERY_STATE_INVALID");

        try
        {
            var receipt = await operations.CaptureReferenceAsync(reconciliationId, cancellationToken);
            dbContext.RecoveryReconciliationEvents.Add(new RecoveryReconciliationEvent
            {
                Id = uuidGenerator.NewUuid(),
                ReconciliationId = reconciliationId,
                Sequence = latest.Sequence + 1,
                EventType = RecoveryReconciliationEvents.ReferenceCaptureStarted,
                Status = RecoveryReconciliationStatuses.ReferenceCapturing,
                TechnicalActor = "SGOL_WORKER",
                OccurredAt = receipt.TargetRecoveryAt,
                CorrelationId = context.CorrelationId,
                ReferenceManifestSha256 = receipt.ManifestSha256,
                ReferenceRootSha256 = receipt.RootSha256,
            });
            dbContext.AuditEvents.Add(Audit(uuidGenerator.NewUuid(), reconciliationId, context.CorrelationId,
                receipt.TargetRecoveryAt, RecoveryReconciliationEvents.ReferenceCaptureStarted,
                RecoveryReconciliationStatuses.ReferenceCapturing, null));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var errorCode = exception switch
            {
                OperationsConfigurationException configured => configured.ErrorCode,
                OperationsIntegrityException integrity => integrity.ErrorCode,
                OperationsReferenceCaptureException reference => reference.ErrorCode,
                RecoveryContractException contract => contract.ErrorCode,
                _ => "UNEXPECTED_RECONCILIATION_FAILURE"
            };
            dbContext.RecoveryReconciliationEvents.Add(new RecoveryReconciliationEvent
            {
                Id = uuidGenerator.NewUuid(),
                ReconciliationId = reconciliationId,
                Sequence = latest.Sequence + 1,
                EventType = RecoveryReconciliationEvents.Failed,
                Status = RecoveryReconciliationStatuses.Failed,
                TechnicalActor = "SGOL_WORKER",
                OccurredAt = timeProvider.GetUtcNow(),
                CorrelationId = context.CorrelationId,
                ErrorCode = errorCode,
            });
            dbContext.AuditEvents.Add(Audit(uuidGenerator.NewUuid(), reconciliationId, context.CorrelationId,
                timeProvider.GetUtcNow(), RecoveryReconciliationEvents.Failed,
                RecoveryReconciliationStatuses.Failed, errorCode));
        }
    }

    private static bool TryReadReconciliationId(string? checkpoint, out Guid reconciliationId)
    {
        reconciliationId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(checkpoint)) return false;

        try
        {
            using var document = JsonDocument.Parse(checkpoint);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.EnumerateObject().Count() == 1 &&
                root.TryGetProperty("reconciliationId", out var value) &&
                value.ValueKind == JsonValueKind.String &&
                value.TryGetGuid(out reconciliationId) &&
                reconciliationId != Guid.Empty;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static AuditEvent Audit(Guid id, Guid reconciliationId, Guid correlationId, DateTimeOffset occurredAt,
        string action, string status, string? errorCode)
    {
        var after = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            reconciliationId,
            status,
            errorCode
        }));
        return new AuditEvent
        {
            Id = id,
            OccurredAt = occurredAt,
            ActorType = "SYSTEM",
            Action = action,
            ResourceType = "RECOVERY_RECONCILIATION",
            ResourceId = reconciliationId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            AfterData = after,
            Outcome = errorCode is null ? "SUCCESS" : "FAILED"
        };
    }
}

public static class OperationsServiceCollectionExtensions
{
    public static IServiceCollection AddSgolPortableOperations(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IBackupProcessPipeline, BackupProcessPipeline>();
        services.TryAddSingleton<IPostgreSqlPortableBackup, PostgreSqlPortableBackup>();
        services.TryAddSingleton<IObjectReplica, ObjectReplica>();
        services.TryAddSingleton<IFunctionalRecoveryOperations, FunctionalRecoveryOperations>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IScheduledJob, PostgreSqlPortableBackupJob>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IScheduledJob, ReplicateEvidenceObjectsJob>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IScheduledJob, CaptureRecoveryReferenceJob>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxHandler, RecoveryReferenceRequestedOutboxHandler>());
        return services;
    }
}
