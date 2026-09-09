using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Evidence;

public sealed class EfEvidenceConclusionReviewService(
    SgolDbContext dbContext,
    IUuidGenerator uuidGenerator) : IEvidenceConclusionReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EvidenceConclusionReview> ReviewAsync(
        EvidenceConclusionReviewQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ActorUserId == Guid.Empty || query.CorrelationId == Guid.Empty || query.ObligationId == Guid.Empty ||
            dbContext.Database.CurrentTransaction is null)
        {
            throw new EvidenceReviewUnavailableException();
        }

        var obligation = dbContext.WorkObligations.Local.SingleOrDefault(item => item.Id == query.ObligationId)
            ?? throw new EvidenceReviewUnavailableException();
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

        var items = await dbContext.EvidenceItems
            .FromSqlInterpolated($"""
                SELECT item.*
                FROM evidence_item item
                JOIN evidence_requirement_version requirement ON requirement.id = item.requirement_version_id
                WHERE item.obligation_id = {obligation.Id}
                ORDER BY requirement.ordinal, item.id
                FOR UPDATE OF item
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);
        var requirementIds = requirements.Select(item => item.Id).ToHashSet();
        if (items.Any(item => item.EvidencePolicyVersionId != policy.Id || !requirementIds.Contains(item.RequirementVersionId)) ||
            items.GroupBy(item => item.RequirementVersionId).Any(group => group.Count() != 1))
        {
            throw new EvidenceReviewUnavailableException();
        }

        var versions = new List<EvidenceVersion>();
        foreach (var item in items)
        {
            versions.AddRange(await dbContext.EvidenceVersions
                .FromSqlInterpolated($"SELECT * FROM evidence_version WHERE evidence_item_id = {item.Id} ORDER BY id FOR UPDATE")
                .AsTracking()
                .ToListAsync(cancellationToken));
        }

        var files = new Dictionary<Guid, FileObject>();
        foreach (var fileId in versions.Where(version => version.FileObjectId.HasValue)
                     .Select(version => version.FileObjectId!.Value).Distinct().Order())
        {
            var file = await dbContext.FileObjects
                .FromSqlInterpolated($"SELECT * FROM file_object WHERE id = {fileId} FOR UPDATE")
                .AsTracking()
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new EvidenceReviewUnavailableException();
            files.Add(file.Id, file);
        }

        var itemByRequirement = items.ToDictionary(item => item.RequirementVersionId);
        var versionsByItem = versions.ToLookup(version => version.EvidenceItemId);
        var inputs = new List<EvidenceReviewRequirementInput>(requirements.Count);
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
                    version.FileObjectId is { } id && files.TryGetValue(id, out var file)
                        ? new EvidenceReviewFileInput(file.Id, file.RequirementVersionId, file.LinkedEvidenceItemId, file.ScanStatus, file.BucketClass)
                        : null)).ToArray();
            inputs.Add(new(
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
            expectedDefinitions.Select(item => new EvidenceReviewExpectedRequirement(
                item.Code, item.Kind, item.ConditionCode, item.Ordinal)).ToArray(),
            inputs));
        if (evaluation.Result != EvidenceReviewResults.Complete)
        {
            return new(evaluation.Result, null, evaluation.MissingRequirements);
        }

        var existing = await dbContext.EvidenceReviewSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(snapshot => snapshot.ObligationId == obligation.Id &&
                snapshot.InputFingerprint == evaluation.InputFingerprint, cancellationToken);
        if (existing is not null)
        {
            EnsureExistingMatches(existing, evaluation);
            return new(evaluation.Result, existing.Id, evaluation.MissingRequirements);
        }

        var snapshot = new EvidenceReviewSnapshot(
            uuidGenerator.NewUuid(),
            obligation.Id,
            policy.Id,
            evaluation,
            query.ActorUserId,
            query.EvaluatedAt,
            query.CorrelationId);
        dbContext.EvidenceReviewSnapshots.Add(snapshot);
        dbContext.AuditEvents.Add(CreateAudit(snapshot, evaluation, query));
        return new(evaluation.Result, snapshot.Id, evaluation.MissingRequirements);
    }

    private static void EnsureExistingMatches(EvidenceReviewSnapshot existing, EvidenceReviewEvaluation evaluation)
    {
        using var canonical = JsonDocument.Parse(evaluation.CanonicalInput);
        using var requirements = JsonSerializer.SerializeToDocument(evaluation.Requirements, JsonOptions);
        using var missing = JsonSerializer.SerializeToDocument(evaluation.MissingRequirements, JsonOptions);
        if (existing.Result != EvidenceReviewResults.Complete ||
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
        EvidenceConclusionReviewQuery query) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = query.EvaluatedAt,
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
                evaluatedAt = query.EvaluatedAt.UtcDateTime,
                correlationId = query.CorrelationId,
            }),
            Outcome = "SUCCESS",
        };
}
