using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Evidence;

public sealed class EfEvidenceReviewService(
    SgolDbContext dbContext,
    IClock clock,
    IUuidGenerator uuidGenerator) : IEvidenceReviewService
{
    private const int MaximumAttempts = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EvidenceReviewDetails> ReviewAsync(
        EvidenceReviewQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var started = Stopwatch.GetTimestamp();
        try
        {
            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                try
                {
                    var result = await ReviewOnceAsync(query, cancellationToken);
                    EvidenceTelemetry.Reviews.Add(1, tag: new("result", result.Result == EvidenceReviewResults.Complete ? "complete" : "incomplete"));
                    return result;
                }
                catch (Exception exception) when (IsRetryable(exception) && attempt < MaximumAttempts)
                {
                    dbContext.ChangeTracker.Clear();
                }
                catch (Exception exception) when (IsRetryable(exception))
                {
                    dbContext.ChangeTracker.Clear();
                    throw new EvidenceReviewFailedException();
                }
            }

            throw new EvidenceReviewFailedException();
        }
        catch (EvidenceReviewUnavailableException)
        {
            EvidenceTelemetry.Reviews.Add(1, tag: new("result", "inconsistent"));
            throw;
        }
        catch (Exception exception) when (exception is EvidenceReviewAccessDeniedException or EvidenceReviewObligationNotFoundException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (EvidenceReviewFailedException)
        {
            EvidenceTelemetry.Reviews.Add(1, tag: new("result", "failed"));
            throw;
        }
        catch (Exception)
        {
            EvidenceTelemetry.Reviews.Add(1, tag: new("result", "failed"));
            throw new EvidenceReviewFailedException();
        }
        finally
        {
            EvidenceTelemetry.ReviewDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private async Task<EvidenceReviewDetails> ReviewOnceAsync(
        EvidenceReviewQuery query,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var evaluatedAt = clock.UtcNow;
        var actor = await GetActorAsync(query.ActorUserId, evaluatedAt, cancellationToken);
        if (!await CanViewAsync(actor, query.ObligationId, evaluatedAt, cancellationToken))
        {
            throw new EvidenceReviewObligationNotFoundException();
        }

        var obligation = await dbContext.WorkObligations
            .FromSqlInterpolated($"SELECT * FROM work_obligation WHERE id = {query.ObligationId} AND branch_id = {BranchScope.LorettaId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new EvidenceReviewObligationNotFoundException();
        if (obligation.EvidencePolicyVersionId is null)
        {
            throw new EvidenceReviewUnavailableException();
        }

        var task = await (
            from version in dbContext.TaskDefinitionVersions.AsNoTracking()
            join definition in dbContext.TaskDefinitions.AsNoTracking() on version.TaskDefinitionId equals definition.Id
            where version.Id == obligation.TaskDefinitionVersionId
            select new { definition.Id, definition.TaskCode })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new EvidenceReviewUnavailableException();
        var policy = await dbContext.EvidencePolicyVersions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == obligation.EvidencePolicyVersionId, cancellationToken)
            ?? throw new EvidenceReviewUnavailableException();
        if (policy.TaskDefinitionId != task.Id || policy.Status is not (VersionStatuses.Current or VersionStatuses.Superseded))
        {
            throw new EvidenceReviewUnavailableException();
        }

        var requirements = await dbContext.EvidenceRequirementVersions.AsNoTracking()
            .Where(item => item.PolicyVersionId == policy.Id)
            .OrderBy(item => item.Ordinal)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (!EvidencePolicyCatalog.All.TryGetValue(task.TaskCode, out var expectedDefinitions))
        {
            throw new EvidenceReviewUnavailableException();
        }

        var expected = expectedDefinitions
            .Select(item => new EvidenceReviewExpectedRequirement(item.Code, item.Kind, item.ConditionCode, item.Ordinal))
            .ToArray();

        var items = await dbContext.EvidenceItems.AsNoTracking()
            .Where(item => item.ObligationId == obligation.Id)
            .ToListAsync(cancellationToken);
        var requirementIds = requirements.Select(item => item.Id).ToHashSet();
        if (items.Any(item => item.EvidencePolicyVersionId != policy.Id || !requirementIds.Contains(item.RequirementVersionId)) ||
            items.GroupBy(item => item.RequirementVersionId).Any(group => group.Count() != 1))
        {
            throw new EvidenceReviewUnavailableException();
        }

        var itemIds = items.Select(item => item.Id).ToArray();
        var versions = await dbContext.EvidenceVersions.AsNoTracking()
            .Where(version => itemIds.Contains(version.EvidenceItemId))
            .ToListAsync(cancellationToken);
        var fileIds = versions.Where(version => version.FileObjectId.HasValue)
            .Select(version => version.FileObjectId!.Value)
            .Distinct()
            .ToArray();
        var files = await dbContext.FileObjects.AsNoTracking()
            .Where(file => fileIds.Contains(file.Id))
            .ToDictionaryAsync(file => file.Id, cancellationToken);
        var itemByRequirement = items.ToDictionary(item => item.RequirementVersionId);
        var versionsByItem = versions.ToLookup(version => version.EvidenceItemId);

        var inputRequirements = new List<EvidenceReviewRequirementInput>(requirements.Count);
        foreach (var requirement in requirements)
        {
            itemByRequirement.TryGetValue(requirement.Id, out var item);
            if (item is not null && (item.RequirementCode != requirement.RequirementCode || item.RequirementKind != requirement.Kind))
            {
                throw new EvidenceReviewUnavailableException();
            }

            var inputVersions = item is null
                ? []
                : versionsByItem[item.Id].Select(version => new EvidenceReviewVersionInput(
                    version.Id,
                    version.Status,
                    version.FileObjectId,
                    version.StructuredPayload,
                    version.FileObjectId is { } fileId && files.TryGetValue(fileId, out var file)
                        ? new EvidenceReviewFileInput(file.Id, file.RequirementVersionId, file.LinkedEvidenceItemId, file.ScanStatus, file.BucketClass)
                        : null)).ToArray();
            inputRequirements.Add(new(
                requirement.Id,
                requirement.RequirementCode,
                requirement.Kind,
                requirement.ConditionCode,
                requirement.Ordinal,
                requirement.IsRequired,
                item?.Id,
                inputVersions));
        }

        var evaluation = EvidenceReviewEvaluator.Evaluate(new(
            obligation.Id,
            policy.Id,
            task.TaskCode,
            expected,
            inputRequirements));
        var existing = await dbContext.EvidenceReviewSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(snapshot => snapshot.ObligationId == obligation.Id &&
                snapshot.InputFingerprint == evaluation.InputFingerprint, cancellationToken);
        if (existing is not null)
        {
            EnsureExistingMatches(existing, evaluation);
            await transaction.CommitAsync(cancellationToken);
            EvidenceTelemetry.ReviewSnapshots.Add(1, tag: new("result", "reused"));
            return ToDetails(existing.Id, existing.ObligationId, existing.EvidencePolicyVersionId,
                existing.Result, existing.EvaluatedAt, evaluation);
        }

        var snapshot = new EvidenceReviewSnapshot(
            uuidGenerator.NewUuid(),
            obligation.Id,
            policy.Id,
            evaluation,
            query.ActorUserId,
            evaluatedAt,
            query.CorrelationId);
        dbContext.EvidenceReviewSnapshots.Add(snapshot);
        dbContext.AuditEvents.Add(CreateAudit(snapshot, evaluation, query));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        EvidenceTelemetry.ReviewSnapshots.Add(1, tag: new("result", "created"));
        return ToDetails(snapshot.Id, snapshot.ObligationId, snapshot.EvidencePolicyVersionId,
            snapshot.Result, snapshot.EvaluatedAt, evaluation);
    }

    private async Task<ActorAccess> GetActorAsync(
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        var actors = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where user.Id == actorUserId && user.Status == BootstrapContract.ActiveAccountStatus && user.MfaEnrolledAt != null &&
                employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= evaluatedAt && (employment.ValidTo == null || evaluatedAt < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= evaluatedAt && (role.ValidTo == null || evaluatedAt < role.ValidTo)
            select new ActorAccess(user.PersonId, role.RoleCode))
            .Take(2)
            .ToListAsync(cancellationToken);
        if (actors.Count != 1 || !CanonicalRole.IsDefined(actors[0].RoleCode))
        {
            throw new EvidenceReviewAccessDeniedException();
        }

        return actors[0];
    }

    private async Task<bool> CanViewAsync(
        ActorAccess actor,
        Guid obligationId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        if (actor.RoleCode == CanonicalRole.Direction)
        {
            return await dbContext.WorkObligations.AsNoTracking()
                .AnyAsync(item => item.Id == obligationId && item.BranchId == BranchScope.LorettaId, cancellationToken);
        }

        var targets = await (
            from obligation in dbContext.WorkObligations.AsNoTracking()
            join assignment in dbContext.AssignmentVersions.AsNoTracking() on obligation.Id equals assignment.ObligationId
            join user in dbContext.AppUsers.AsNoTracking() on assignment.PersonId equals user.PersonId
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where obligation.Id == obligationId && obligation.BranchId == BranchScope.LorettaId &&
                assignment.Status == AssignmentVersionStatuses.Current && user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= evaluatedAt && (employment.ValidTo == null || evaluatedAt < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= evaluatedAt && (role.ValidTo == null || evaluatedAt < role.ValidTo)
            select new { assignment.PersonId, role.RoleCode })
            .Take(2)
            .ToListAsync(cancellationToken);
        return targets.Count == 1 && RoleHierarchy.CanAccess(
            actor.RoleCode,
            targets[0].RoleCode,
            actor.PersonId == targets[0].PersonId);
    }

    private static void EnsureExistingMatches(EvidenceReviewSnapshot existing, EvidenceReviewEvaluation evaluation)
    {
        using var canonical = JsonDocument.Parse(evaluation.CanonicalInput);
        using var requirements = JsonSerializer.SerializeToDocument(evaluation.Requirements, JsonOptions);
        using var missing = JsonSerializer.SerializeToDocument(evaluation.MissingRequirements, JsonOptions);
        if (existing.Result != evaluation.Result ||
            !JsonElement.DeepEquals(existing.CanonicalInput.RootElement, canonical.RootElement) ||
            !JsonElement.DeepEquals(existing.RequirementsSnapshot.RootElement, requirements.RootElement) ||
            !JsonElement.DeepEquals(existing.MissingRequirements.RootElement, missing.RootElement) ||
            !existing.EvidenceVersionIds.SequenceEqual(evaluation.EvidenceVersionIds))
        {
            throw new EvidenceReviewUnavailableException();
        }
    }

    private AuditEvent CreateAudit(
        EvidenceReviewSnapshot snapshot,
        EvidenceReviewEvaluation evaluation,
        EvidenceReviewQuery query) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = snapshot.EvaluatedAt,
            ActorUserId = query.ActorUserId,
            ActorType = "HUMAN",
            Action = "EVIDENCE_REVIEW_SNAPSHOT_CREATED",
            ResourceType = "EVIDENCE_REVIEW_SNAPSHOT",
            ResourceId = snapshot.Id,
            BranchId = BranchScope.LorettaId,
            CorrelationId = query.CorrelationId,
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                schemaVersion = 1,
                snapshotId = snapshot.Id,
                obligationId = snapshot.ObligationId,
                evidencePolicyVersionId = snapshot.EvidencePolicyVersionId,
                result = snapshot.Result,
                applicableCount = evaluation.Requirements.Count(item => item.Applicability == EvidenceReviewApplicability.Applicable),
                missingCount = evaluation.MissingRequirements.Count,
                requestedByUserId = query.ActorUserId,
                evaluatedBy = "SYSTEM",
                evaluatedAt = snapshot.EvaluatedAt.UtcDateTime,
                correlationId = query.CorrelationId,
            }),
            Outcome = "SUCCESS",
        };

    private static EvidenceReviewDetails ToDetails(
        Guid snapshotId,
        Guid obligationId,
        Guid policyId,
        string result,
        DateTimeOffset evaluatedAt,
        EvidenceReviewEvaluation evaluation) => new(
            snapshotId,
            obligationId,
            policyId,
            result,
            evaluatedAt,
            evaluation.Requirements,
            evaluation.MissingRequirements);

    private static bool IsRetryable(Exception exception)
    {
        var postgres = exception as PostgresException ?? exception.InnerException as PostgresException;
        return postgres?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation;
    }

    private sealed record ActorAccess(Guid PersonId, string RoleCode);
}
