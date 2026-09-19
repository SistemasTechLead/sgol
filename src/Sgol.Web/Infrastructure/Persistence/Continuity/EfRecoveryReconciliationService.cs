using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Continuity.Contracts;
using Sgol.Identity.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Persistence.Continuity;

public sealed class EfRecoveryReconciliationService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IOutboxWriter outboxWriter,
    IClock clock,
    IUuidGenerator uuidGenerator) : IRecoveryReconciliationService, IRecoveryTechnicalWriter
{
    public const string ReferenceRequestedEventType = "RECOVERY_REFERENCE_REQUESTED";
    private const string ResourceType = "RECOVERY_RECONCILIATION";

    public async Task<RecoveryReconciliationDetails> CreateAsync(
        CreateRecoveryReconciliationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, cancellationToken);
        var reason = NormalizeReason(command.Reason);
        const string operation = "RECOVERY_RECONCILIATION_CREATE";
        const string resource = "new:LOR-001";
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource, new { reason });
        var replay = await ReplayAsync(scope, command.IdempotencyKey, requestHash, command.ActorUserId,
            command.CorrelationId, operation, null, cancellationToken);
        if (replay is not null) return replay with { Replayed = true };

        var nowValue = clock.UtcNow;
        var now = nowValue.AddTicks(-(nowValue.Ticks % TimeSpan.TicksPerMicrosecond));
        var id = uuidGenerator.NewUuid();
        var entity = new RecoveryReconciliation
        {
            Id = id,
            BranchId = BranchScope.LorettaId,
            RequestedBy = command.ActorUserId,
            Reason = reason,
            RequestedAt = now,
        };
        var requested = Event(id, 1, RecoveryReconciliationEvents.Requested,
            RecoveryReconciliationStatuses.Requested, now, command.CorrelationId, command.ActorUserId);
        var response = Details(entity, requested, [], replayed: false);
        using var payload = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            reconciliationId = id,
            status = RecoveryReconciliationStatuses.Requested
        });
        var audit = Audit(RecoveryReconciliationEvents.Requested, id, command.CorrelationId, now,
            command.ActorUserId, payload);
        using var outboxPayload = JsonSerializer.SerializeToDocument(new { reconciliationId = id, scheduledFor = now });

        try
        {
            await auditTransaction.ExecuteAsync(audit, _ =>
            {
                dbContext.RecoveryReconciliations.Add(entity);
                dbContext.RecoveryReconciliationEvents.Add(requested);
                outboxWriter.Enqueue(ReferenceRequestedEventType, id, outboxPayload.RootElement,
                    command.CorrelationId);
                dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(scope, command.IdempotencyKey,
                    requestHash, ResourceType, id, StatusCodes.Status201Created, response, now,
                    DateTimeOffset.MaxValue, "\"1\"", $"/api/v1/continuity/reconciliations/{id:D}"));
                return Task.CompletedTask;
            }, cancellationToken);
            dbContext.ChangeTracker.Clear();
            return response;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrent = await ReplayAsync(scope, command.IdempotencyKey, requestHash, command.ActorUserId,
                command.CorrelationId, operation, null, cancellationToken);
            if (concurrent is null) throw;
            return concurrent with { Replayed = true };
        }
    }

    public async Task<RecoveryReconciliationDetails> GetAsync(
        RecoveryReconciliationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await EnsureAuthorizedAsync(query.ActorUserId, cancellationToken);
        var entity = await FindAsync(query.ReconciliationId, cancellationToken);
        var latest = await LatestAsync(entity.Id, cancellationToken);
        using var payload = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            reconciliationId = entity.Id,
            status = latest.Status,
            sequence = latest.Sequence
        });
        try
        {
            await auditTransaction.ExecuteAsync(
                Audit(RecoveryReconciliationEvents.Viewed, entity.Id, query.CorrelationId, clock.UtcNow,
                    query.ActorUserId, payload), _ => Task.CompletedTask, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            throw new RecoveryAuditFailedException();
        }
        dbContext.ChangeTracker.Clear();
        return await LoadDetailsAsync(entity.Id, false, cancellationToken);
    }

    public async Task<RecoveryReconciliationDetails> ApproveAsync(
        ApproveRecoveryReconciliationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureAuthorizedAsync(command.ActorUserId, cancellationToken);
        var entity = await FindAsync(command.ReconciliationId, cancellationToken);
        var reason = NormalizeReason(command.Reason);
        const string operation = "RECOVERY_RECONCILIATION_APPROVE";
        var resource = entity.Id.ToString("D");
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource,
            new { expectedSequence = command.ExpectedSequence, reason }, command.ExpectedSequence);
        var replay = await ReplayAsync(scope, command.IdempotencyKey, requestHash, command.ActorUserId,
            command.CorrelationId, operation, entity.Id, cancellationToken);
        if (replay is not null) return replay with { Replayed = true };

        var latest = await LatestAsync(entity.Id, cancellationToken);
        if (latest.Sequence != command.ExpectedSequence) throw new RecoveryReconciliationVersionConflictException();
        if (latest.Status != RecoveryReconciliationStatuses.Matched || latest.ObservedRpoSeconds is null or > 3600 ||
            latest.ObservedRtoSeconds is null or > 14400 || latest.DifferenceCount != 0 || latest.DifferencesTruncated == true)
            throw new RecoveryReconciliationNotApprovableException();
        var now = clock.UtcNow;
        var approved = Event(entity.Id, latest.Sequence + 1, RecoveryReconciliationEvents.Approved,
            RecoveryReconciliationStatuses.Approved, now, command.CorrelationId, command.ActorUserId, reason: reason);
        var response = Details(entity, approved, [], replayed: false) with
        {
            TargetRecoveryAt = await TargetRecoveryAtAsync(entity.Id, cancellationToken),
            ReferenceRootSha256 = latest.ReferenceRootSha256,
            ActualRootSha256 = latest.ActualRootSha256,
            DifferenceCount = 0,
            ObservedRpoSeconds = latest.ObservedRpoSeconds,
            ObservedRtoSeconds = latest.ObservedRtoSeconds,
            ApprovedAt = now,
            ApprovedBy = command.ActorUserId,
        };
        using var payload = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            reconciliationId = entity.Id,
            status = RecoveryReconciliationStatuses.Approved
        });
        var audit = Audit(RecoveryReconciliationEvents.Approved, entity.Id, command.CorrelationId, now,
            command.ActorUserId, payload);
        await auditTransaction.ExecuteAsync(audit, _ =>
        {
            dbContext.RecoveryReconciliationEvents.Add(approved);
            dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(scope, command.IdempotencyKey,
                requestHash, ResourceType, entity.Id, StatusCodes.Status200OK, response, now,
                DateTimeOffset.MaxValue, $"\"{approved.Sequence.ToString(CultureInfo.InvariantCulture)}\""));
            return Task.CompletedTask;
        }, cancellationToken);
        dbContext.ChangeTracker.Clear();
        return response;
    }

    public async Task MarkReferenceReadyAsync(RecoveryReferenceReadyCommand command, CancellationToken cancellationToken = default)
    {
        ValidateHash(command.ReferenceManifestSha256);
        ValidateHash(command.ReferenceRootSha256);
        var prior = await dbContext.RecoveryReconciliationEvents.AsNoTracking().SingleOrDefaultAsync(item =>
            item.ReconciliationId == command.ReconciliationId &&
            item.EventType == RecoveryReconciliationEvents.ReferenceReady, cancellationToken);
        if (prior is not null)
        {
            if (prior.ReferenceManifestSha256 == command.ReferenceManifestSha256 &&
                prior.ReferenceRootSha256 == command.ReferenceRootSha256 &&
                await TargetRecoveryAtAsync(command.ReconciliationId, cancellationToken) == command.TargetRecoveryAt)
                return;
            throw new RecoveryContractException("RECONCILIATION_IMMUTABLE_CONFLICT");
        }
        var latest = await LatestAsync(command.ReconciliationId, cancellationToken);
        var targetRecoveryAt = await TargetRecoveryAtAsync(command.ReconciliationId, cancellationToken);
        if (targetRecoveryAt != command.TargetRecoveryAt)
            throw new RecoveryContractException("REFERENCE_BACKUP_SNAPSHOT_MISMATCH");
        await AppendTechnicalAsync(command.ReconciliationId, RecoveryReconciliationStatuses.ReferenceCapturing,
            Event(command.ReconciliationId, 0, RecoveryReconciliationEvents.ReferenceReady,
                RecoveryReconciliationStatuses.ReferenceReady, clock.UtcNow, command.CorrelationId,
                referenceManifestSha256: command.ReferenceManifestSha256,
                referenceRootSha256: command.ReferenceRootSha256), cancellationToken);
    }

    public async Task MarkRestoreStartedAsync(RecoveryRestoreStartedCommand command, CancellationToken cancellationToken = default)
    {
        ValidateHash(command.RestoreEvidenceSha256);
        var startedAt = ToPostgreSqlPrecision(command.StartedAt);
        var prior = await dbContext.RecoveryReconciliationEvents.AsNoTracking().SingleOrDefaultAsync(item =>
            item.ReconciliationId == command.ReconciliationId &&
            item.EventType == RecoveryReconciliationEvents.RestoreStarted, cancellationToken);
        if (prior is not null)
        {
            if (prior.OccurredAt == startedAt && prior.RestoreEvidenceSha256 == command.RestoreEvidenceSha256)
                return;
            throw new RecoveryContractException("RECONCILIATION_IMMUTABLE_CONFLICT");
        }
        var latest = await LatestAsync(command.ReconciliationId, cancellationToken);
        await AppendTechnicalAsync(command.ReconciliationId, RecoveryReconciliationStatuses.ReferenceReady,
            Event(command.ReconciliationId, 0, RecoveryReconciliationEvents.RestoreStarted,
                RecoveryReconciliationStatuses.RestoreStarted, startedAt, command.CorrelationId,
                restoreEvidenceSha256: command.RestoreEvidenceSha256), cancellationToken);
    }

    private static DateTimeOffset ToPostgreSqlPrecision(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
    }

    public async Task CompleteAsync(RecoveryCompletedCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command.Result);
        ValidateHash(command.ReportManifestSha256);
        var entity = await FindAsync(command.ReconciliationId, cancellationToken);
        var latest = await LatestAsync(entity.Id, cancellationToken);
        if (latest.Status is RecoveryReconciliationStatuses.Matched or RecoveryReconciliationStatuses.Different or
            RecoveryReconciliationStatuses.Failed)
        {
            if (latest.ReportManifestSha256 == command.ReportManifestSha256 &&
                latest.ReferenceRootSha256 == command.Result.ExpectedRootSha256 &&
                latest.ActualRootSha256 == command.Result.ActualRootSha256) return;
            throw new RecoveryContractException("RECONCILIATION_IMMUTABLE_CONFLICT");
        }
        if (latest.Status != RecoveryReconciliationStatuses.RestoreStarted)
            throw new RecoveryContractException("RECOVERY_STATE_INVALID");
        var status = command.Result.Status == RecoveryReconciliationStatuses.Matched &&
            command.Objectives.MeetsRpo && command.Objectives.MeetsRto
            ? RecoveryReconciliationStatuses.Matched
            : command.Result.Status == RecoveryReconciliationStatuses.Different
                ? RecoveryReconciliationStatuses.Different
                : RecoveryReconciliationStatuses.Failed;
        var start = Event(entity.Id, latest.Sequence + 1, RecoveryReconciliationEvents.Reconciling,
            RecoveryReconciliationStatuses.Reconciling, command.CompletedAt, command.CorrelationId);
        var terminal = Event(entity.Id, latest.Sequence + 2,
            status == RecoveryReconciliationStatuses.Failed ? RecoveryReconciliationEvents.Failed : RecoveryReconciliationEvents.Completed,
            status, command.CompletedAt, command.CorrelationId,
            errorCode: status == RecoveryReconciliationStatuses.Failed
                ? (command.Result.Truncated ? "CARDINALITY_LIMIT_EXCEEDED" :
                    !command.Objectives.MeetsRpo ? "RPO_EXCEEDED" :
                    !command.Objectives.MeetsRto ? "RTO_EXCEEDED" : "UNEXPECTED_RECONCILIATION_FAILURE")
                : null,
            reportManifestSha256: command.ReportManifestSha256,
            referenceRootSha256: command.Result.ExpectedRootSha256,
            actualRootSha256: command.Result.ActualRootSha256,
            differenceCount: command.Result.TotalDifferences,
            differencesTruncated: command.Result.Truncated,
            observedRpoSeconds: command.Objectives.ObservedRpoSeconds,
            observedRtoSeconds: command.Objectives.ObservedRtoSeconds);
        foreach (var difference in command.Result.Differences)
            dbContext.RecoveryReconciliationDifferences.Add(new RecoveryReconciliationDifference
            {
                Id = uuidGenerator.NewUuid(),
                ReconciliationId = entity.Id,
                Ordinal = difference.Ordinal,
                Group = difference.Group,
                ResourceType = difference.ResourceType,
                StableKey = difference.StableKey,
                Field = difference.Field,
                Kind = difference.Kind,
                ExpectedSha256 = difference.ExpectedSha256,
                ActualSha256 = difference.ActualSha256,
            });
        using var payload = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            reconciliationId = entity.Id,
            status,
            differenceCount = command.Result.TotalDifferences,
            command.Objectives.ObservedRpoSeconds,
            command.Objectives.ObservedRtoSeconds
        });
        await auditTransaction.ExecuteAsync(Audit(terminal.EventType, entity.Id, command.CorrelationId,
            command.CompletedAt, null, payload), _ =>
        {
            dbContext.RecoveryReconciliationEvents.AddRange(start, terminal);
            return Task.CompletedTask;
        }, cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    public async Task FailAsync(RecoveryFailedCommand command, CancellationToken cancellationToken = default)
    {
        ValidateErrorCode(command.ErrorCode);
        var entity = await FindAsync(command.ReconciliationId, cancellationToken);
        var latest = await LatestAsync(entity.Id, cancellationToken);
        if (latest.Status == RecoveryReconciliationStatuses.Failed && latest.ErrorCode == command.ErrorCode) return;
        if (RecoveryReconciliationStatuses.IsTerminal(latest.Status) || latest.Status == RecoveryReconciliationStatuses.Matched)
            throw new RecoveryContractException("RECOVERY_STATE_INVALID");
        var failed = Event(entity.Id, latest.Sequence + 1, RecoveryReconciliationEvents.Failed,
            RecoveryReconciliationStatuses.Failed, command.FailedAt, command.CorrelationId, errorCode: command.ErrorCode);
        using var payload = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            reconciliationId = entity.Id,
            status = RecoveryReconciliationStatuses.Failed,
            errorCode = command.ErrorCode
        });
        await auditTransaction.ExecuteAsync(Audit(RecoveryReconciliationEvents.Failed, entity.Id,
            command.CorrelationId, command.FailedAt, null, payload), _ =>
        {
            dbContext.RecoveryReconciliationEvents.Add(failed);
            return Task.CompletedTask;
        }, cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task AppendTechnicalAsync(Guid id, string expectedStatus, RecoveryReconciliationEvent item,
        CancellationToken cancellationToken)
    {
        var entity = await FindAsync(id, cancellationToken);
        var latest = await LatestAsync(id, cancellationToken);
        if (latest.Status != expectedStatus) throw new RecoveryContractException("RECOVERY_STATE_INVALID");
        item = CopyWithSequence(item, latest.Sequence + 1);
        using var payload = JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            reconciliationId = id,
            status = item.Status
        });
        await auditTransaction.ExecuteAsync(Audit(item.EventType, entity.Id, item.CorrelationId,
            item.OccurredAt, null, payload), _ =>
        {
            dbContext.RecoveryReconciliationEvents.Add(item);
            return Task.CompletedTask;
        }, cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task EnsureAuthorizedAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var roles = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join person in dbContext.People.AsNoTracking() on user.PersonId equals person.Id
            join employment in dbContext.EmploymentVersions.AsNoTracking() on person.Id equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where user.Id == actorUserId && user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= now && (employment.ValidTo == null || employment.ValidTo > now) &&
                role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= now && (role.ValidTo == null || role.ValidTo > now)
            select role.RoleCode).ToListAsync(cancellationToken);
        if (roles.Count != 1 || !RoleHierarchy.GrantsContinuityView(roles[0]))
            throw new RecoveryAccessDeniedException();
    }

    private async Task<RecoveryReconciliation> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.RecoveryReconciliations.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == id && item.BranchId == BranchScope.LorettaId, cancellationToken)
        ?? throw new RecoveryReconciliationNotFoundException();

    private Task<RecoveryReconciliationEvent> LatestAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.RecoveryReconciliationEvents.AsNoTracking().Where(item => item.ReconciliationId == id)
            .OrderByDescending(item => item.Sequence).FirstAsync(cancellationToken);

    private async Task<DateTimeOffset?> TargetRecoveryAtAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.RecoveryReconciliationEvents.AsNoTracking()
            .Where(item => item.ReconciliationId == id && item.EventType == RecoveryReconciliationEvents.ReferenceCaptureStarted)
            .Select(item => (DateTimeOffset?)item.OccurredAt).SingleOrDefaultAsync(cancellationToken);

    private async Task<RecoveryReconciliationDetails> LoadDetailsAsync(Guid id, bool replayed, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(id, cancellationToken);
        var events = await dbContext.RecoveryReconciliationEvents.AsNoTracking().Where(item => item.ReconciliationId == id)
            .OrderBy(item => item.Sequence).ToListAsync(cancellationToken);
        var latest = events[^1];
        var result = events.LastOrDefault(item => item.EventType is RecoveryReconciliationEvents.Completed or RecoveryReconciliationEvents.Failed);
        var approval = events.LastOrDefault(item => item.EventType == RecoveryReconciliationEvents.Approved);
        var differences = await dbContext.RecoveryReconciliationDifferences.AsNoTracking()
            .Where(item => item.ReconciliationId == id).OrderBy(item => item.Ordinal)
            .Select(item => new RecoveryDifferenceDetails(item.Ordinal, item.Group, item.ResourceType, item.StableKey,
                item.Field, item.Kind, item.ExpectedSha256, item.ActualSha256)).ToListAsync(cancellationToken);
        return new(entity.Id, entity.BranchId, latest.Status, entity.RequestedAt,
            events.FirstOrDefault(item => item.EventType == RecoveryReconciliationEvents.ReferenceCaptureStarted)?.OccurredAt,
            latest.Sequence, result?.ReferenceRootSha256, result?.ActualRootSha256, result?.DifferenceCount ?? 0,
            result?.DifferencesTruncated ?? false, result?.ObservedRpoSeconds, result?.ObservedRtoSeconds,
            approval?.OccurredAt, approval?.ActorUserId, differences, replayed);
    }

    private async Task<RecoveryReconciliationDetails?> ReplayAsync(string scope, Guid key, string requestHash,
        Guid actorUserId, Guid correlationId, string operation, Guid? resourceId, CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Scope == scope && item.Key == key, cancellationToken);
        if (record is null) return null;
        if (record.RequestHash != requestHash)
        {
            await IdempotencyProtocol.PersistConflictAsync(auditTransaction,
                IdempotencyProtocol.ConflictAudit(uuidGenerator.NewUuid(), clock.UtcNow, actorUserId,
                    ResourceType, resourceId, BranchScope.LorettaId, correlationId, key, operation), cancellationToken);
            throw new RecoveryContractException("IDEMPOTENCY_CONFLICT");
        }
        return IdempotencyProtocol.ReadPayload<RecoveryReconciliationDetails>(record);
    }

    private RecoveryReconciliationEvent Event(Guid id, long sequence, string eventType, string status,
        DateTimeOffset occurredAt, Guid correlationId, Guid? actorUserId = null, string? technicalActor = "SGOL_OPERATIONS",
        string? errorCode = null, string? referenceManifestSha256 = null, string? reportManifestSha256 = null,
        string? restoreEvidenceSha256 = null, string? referenceRootSha256 = null, string? actualRootSha256 = null,
        int? differenceCount = null, bool? differencesTruncated = null, long? observedRpoSeconds = null,
        long? observedRtoSeconds = null, string? reason = null) => new()
        {
            Id = uuidGenerator.NewUuid(),
            ReconciliationId = id,
            Sequence = sequence,
            EventType = eventType,
            Status = status,
            ActorUserId = actorUserId,
            TechnicalActor = actorUserId.HasValue ? null : technicalActor,
            OccurredAt = occurredAt,
            CorrelationId = correlationId,
            ErrorCode = errorCode,
            ReferenceManifestSha256 = referenceManifestSha256,
            ReportManifestSha256 = reportManifestSha256,
            RestoreEvidenceSha256 = restoreEvidenceSha256,
            ReferenceRootSha256 = referenceRootSha256,
            ActualRootSha256 = actualRootSha256,
            DifferenceCount = differenceCount,
            DifferencesTruncated = differencesTruncated,
            ObservedRpoSeconds = observedRpoSeconds,
            ObservedRtoSeconds = observedRtoSeconds,
            Reason = reason,
        };

    private static RecoveryReconciliationEvent CopyWithSequence(RecoveryReconciliationEvent item, long sequence) => new()
    {
        Id = item.Id,
        ReconciliationId = item.ReconciliationId,
        Sequence = sequence,
        EventType = item.EventType,
        Status = item.Status,
        ActorUserId = item.ActorUserId,
        TechnicalActor = item.TechnicalActor,
        OccurredAt = item.OccurredAt,
        CorrelationId = item.CorrelationId,
        ErrorCode = item.ErrorCode,
        ReferenceManifestSha256 = item.ReferenceManifestSha256,
        ReportManifestSha256 = item.ReportManifestSha256,
        RestoreEvidenceSha256 = item.RestoreEvidenceSha256,
        ReferenceRootSha256 = item.ReferenceRootSha256,
        ActualRootSha256 = item.ActualRootSha256,
        DifferenceCount = item.DifferenceCount,
        DifferencesTruncated = item.DifferencesTruncated,
        ObservedRpoSeconds = item.ObservedRpoSeconds,
        ObservedRtoSeconds = item.ObservedRtoSeconds,
        Reason = item.Reason,
    };

    private AuditEvent Audit(string action, Guid id, Guid correlationId, DateTimeOffset at, Guid? actor,
        JsonDocument after) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = at,
            ActorUserId = actor,
            ActorType = actor.HasValue ? "APP_USER" : "SYSTEM",
            Action = action,
            ResourceType = ResourceType,
            ResourceId = id,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlationId,
            AfterData = after,
            Outcome = action == RecoveryReconciliationEvents.Failed ? "FAILED" : "SUCCESS",
        };

    private static RecoveryReconciliationDetails Details(RecoveryReconciliation entity,
        RecoveryReconciliationEvent latest, IReadOnlyList<RecoveryDifferenceDetails> differences, bool replayed) =>
        new(entity.Id, entity.BranchId, latest.Status, entity.RequestedAt, null, latest.Sequence, null, null,
            differences.Count, false, null, null, null, null, differences, replayed);

    private static string NormalizeReason(string value)
    {
        var normalized = value?.Normalize(NormalizationForm.FormC).Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 500 || normalized.Any(character =>
            character is '<' or '>' or '\r' or '\n' || char.IsControl(character) && character != '\t'))
            throw new RecoveryContractException("MOTIVO_INVALIDO");
        return normalized;
    }

    private static void ValidateHash(string value)
    {
        if (value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new RecoveryContractException("MANIFEST_HASH_INVALID");
    }

    private static void ValidateErrorCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(character =>
            character is not (>= 'A' and <= 'Z' or >= '0' and <= '9' or '_')))
            throw new RecoveryContractException("ERROR_CODE_INVALID");
    }
}

public sealed class RecoveryReferenceRequestedOutboxHandler(
    IServiceScopeFactory scopeFactory,
    IUuidGenerator uuidGenerator,
    IClock clock) : IOutboxHandler
{
    public string EventType => EfRecoveryReconciliationService.ReferenceRequestedEventType;

    public bool IsPayloadValid(JsonElement data) => data.TryGetProperty("reconciliationId", out var value) &&
        value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var id) && id != Guid.Empty &&
        data.TryGetProperty("scheduledFor", out var scheduled) && scheduled.ValueKind == JsonValueKind.String &&
        scheduled.TryGetDateTimeOffset(out var scheduledFor) && scheduledFor.Offset == TimeSpan.Zero &&
        data.EnumerateObject().Count() == 2;

    public async Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken)
    {
        var id = context.Data.GetProperty("reconciliationId").GetGuid();
        var scheduledFor = context.Data.GetProperty("scheduledFor").GetDateTimeOffset();
        var checkpoint = JsonSerializer.Serialize(new { reconciliationId = id });
        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ScheduledJobRunner>().RunAsync(
            "CAPTURE_RECOVERY_REFERENCE", scheduledFor, context.CorrelationId, checkpoint,
            cancellationToken);
        if (result is ScheduledJobResult.Failed or ScheduledJobResult.LockBusy)
        {
            if (context.Attempt < 2)
                throw new JobExecutionException(result == ScheduledJobResult.LockBusy
                    ? "RECOVERY_REFERENCE_LOCK_BUSY" : "RECOVERY_REFERENCE_JOB_FAILED");
            var latest = await context.DbContext.RecoveryReconciliationEvents.Where(item =>
                    item.ReconciliationId == id).OrderByDescending(item => item.Sequence)
                .FirstAsync(cancellationToken);
            if (latest.Status != RecoveryReconciliationStatuses.Requested) return;
            var failedAt = clock.UtcNow;
            const string errorCode = "POSTGRES_CONCURRENCY_EXHAUSTED";
            context.DbContext.RecoveryReconciliationEvents.Add(new RecoveryReconciliationEvent
            {
                Id = uuidGenerator.NewUuid(),
                ReconciliationId = id,
                Sequence = latest.Sequence + 1,
                EventType = RecoveryReconciliationEvents.Failed,
                Status = RecoveryReconciliationStatuses.Failed,
                TechnicalActor = "SGOL_WORKER",
                OccurredAt = failedAt,
                CorrelationId = context.CorrelationId,
                ErrorCode = errorCode
            });
            var after = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                reconciliationId = id,
                status = RecoveryReconciliationStatuses.Failed,
                errorCode
            });
            context.DbContext.AuditEvents.Add(new AuditEvent
            {
                Id = uuidGenerator.NewUuid(),
                OccurredAt = failedAt,
                ActorType = "SYSTEM",
                Action = RecoveryReconciliationEvents.Failed,
                ResourceType = "RECOVERY_RECONCILIATION",
                ResourceId = id,
                BranchId = BranchScope.LorettaId,
                CorrelationId = context.CorrelationId,
                AfterData = after,
                Outcome = "FAILED"
            });
        }
    }
}
