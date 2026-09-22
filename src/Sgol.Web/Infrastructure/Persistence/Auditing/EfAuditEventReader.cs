using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.Auditing.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.Execution.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Auditing;

public sealed class EfAuditEventReader(
    SgolDbContext dbContext,
    IClock clock,
    IDataProtectionProvider dataProtectionProvider) : IAuditEventReader
{
    private const int CursorVersion = 1;
    private const int MaximumCursorLength = 2048;
    private static readonly TimeSpan CursorLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaximumRange = TimeSpan.FromDays(31);
    private static readonly HashSet<string> AllowedChangeProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion", "obligationId", "generationRequestId", "taskDefinitionVersionId",
        "configurationReleaseId", "assignmentId", "previousAssignmentId", "evidenceItemId",
        "evidenceVersionId", "previousEvidenceVersionId", "requirementId", "decisionVersionId",
        "previousDecisionVersionId", "policyVersionId", "evidenceReviewSnapshotId", "taskCode",
        "branchCode", "versionNo", "rowVersion", "status", "result", "outcome",
        "executionStatus", "assignmentType", "authorityType", "validatorRole", "requiredRole",
        "effectiveFrom", "effectiveTo", "assignedAt", "concludedAt", "decidedAt",
        "evidenceVersionCount", "selfValidation",
    };
    private static readonly HashSet<string> GovernanceResourceTypes = new(StringComparer.Ordinal)
    {
        "BRANCH", "CALENDAR_DAY", "CALENDAR_DAY_VERSION", "WEEK_PERIOD", "CONFIGURATION_RELEASE", "TASK_DEFINITION",
        "TASK_DEFINITION_VERSION", "ELIGIBILITY_POLICY_VERSION", "ACTIVATION_RULE_VERSION",
        "EVIDENCE_POLICY_VERSION", "EVIDENCE_REQUIREMENT_VERSION", "VALIDATION_POLICY_VERSION",
    };
    private readonly IDataProtector cursorProtector = dataProtectionProvider.CreateProtector(
        "SGOL.HU-033.AuditCursor.v1");

    public async Task<AuditPage> ReadAsync(
        AuditQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        var actor = await GetActorAsync(request.ActorUserId, queriedAt, cancellationToken);
        var filterHash = FilterHash(request, actor);
        var cursor = DecodeCursor(request.Cursor, filterHash, queriedAt, request.TraceObligationId.HasValue);
        var trace = request.TraceObligationId.HasValue
            ? await BuildTraceAsync(request.TraceObligationId.Value, cancellationToken)
            : null;
        if (trace is not null && actor.RoleCode != CanonicalRole.Direction &&
            !await CanViewTraceObligationAsync(trace.ObligationId, actor, queriedAt, cancellationToken))
            throw new AuditEventNotFoundException();

        var query = dbContext.AuditEvents.AsNoTracking()
            .Where(item => item.OccurredAt >= request.From && item.OccurredAt < request.To)
            .Where(item => item.BranchId == null || item.BranchId == BranchScope.LorettaId);
        if (request.ActorFilter.HasValue)
            query = query.Where(item => item.ActorUserId == request.ActorFilter);
        if (request.ResourceType is not null)
            query = query.Where(item => item.ResourceType == request.ResourceType);
        if (request.ResourceId.HasValue)
            query = query.Where(item => item.ResourceId == request.ResourceId);
        if (request.Action is not null)
            query = query.Where(item => item.Action == request.Action);
        if (request.Outcome is not null)
            query = query.Where(item => item.Outcome == request.Outcome);
        if (request.CorrelationId.HasValue)
            query = query.Where(item => item.CorrelationId == request.CorrelationId);
        if (trace is not null)
            query = query.Where(item => item.ResourceId.HasValue && trace.ResourceIds.Contains(item.ResourceId.Value));

        if (trace is not null && actor.RoleCode != CanonicalRole.Direction)
        {
            var traceUniverse = dbContext.AuditEvents.AsNoTracking()
                .Where(item => item.OccurredAt >= request.From && item.OccurredAt < request.To)
                .Where(item => item.BranchId == null || item.BranchId == BranchScope.LorettaId)
                .Where(item => item.ResourceId.HasValue && trace.ResourceIds.Contains(item.ResourceId.Value));
            if (await traceUniverse.CountAsync(cancellationToken) !=
                await ApplyHierarchyFilter(traceUniverse, actor, trace, null).CountAsync(cancellationToken))
                throw new AuditEventNotFoundException();
        }

        query = ApplyHierarchyFilter(query, actor, trace, request.Level);
        var logicalQuery = query;
        var upper = cursor is null
            ? await query.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id)
                .Select(item => new EventKey(item.OccurredAt, item.Id)).FirstOrDefaultAsync(cancellationToken)
            : new EventKey(cursor.UpperOccurredAt, cursor.UpperEventId);
        if (upper is not null)
            query = query.Where(item => item.OccurredAt < upper.OccurredAt ||
                item.OccurredAt == upper.OccurredAt && item.Id.CompareTo(upper.EventId) <= 0);
        if (cursor is not null)
            query = cursor.Ascending
                ? query.Where(item => item.OccurredAt > cursor.LastOccurredAt ||
                    item.OccurredAt == cursor.LastOccurredAt && item.Id.CompareTo(cursor.LastEventId) > 0)
                : query.Where(item => item.OccurredAt < cursor.LastOccurredAt ||
                    item.OccurredAt == cursor.LastOccurredAt && item.Id.CompareTo(cursor.LastEventId) < 0);

        var ascending = trace is not null;
        query = ascending
            ? query.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id)
            : query.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id);
        var rows = await query.Take(request.Limit + 1).ToListAsync(cancellationToken);
        var maps = await BuildScopeMapsAsync(rows, cancellationToken);
        var projected = new List<ProjectedEvent>(rows.Count);
        foreach (var row in rows)
        {
            var scope = ResolveScope(row, actor, maps, trace);
            if (trace is not null && actor.RoleCode == CanonicalRole.Direction && scope.Relation is null)
                throw new AuditScopeInconsistentException();
            if (!scope.Visible || request.Level is not null && scope.Level != request.Level) continue;
            projected.Add(new(row, scope, Project(row, scope)));
        }

        var hasNext = projected.Count > request.Limit;
        var items = projected.Take(request.Limit).ToArray();
        var nextCursor = hasNext && items.Length > 0
            ? EncodeCursor(new AuditCursor(
                CursorVersion,
                ascending,
                items[^1].Row.OccurredAt,
                items[^1].Row.Id,
                upper!.OccurredAt,
                upper.EventId,
                filterHash,
                queriedAt.Add(CursorLifetime)))
            : null;

        var snapshot = upper is null
            ? new AuditSnapshot(null, null)
            : new AuditSnapshot(upper.OccurredAt, upper.EventId);
        var completeness = trace is null ? null : await CompletenessAsync(
            trace, logicalQuery, cancellationToken);
        dbContext.ChangeTracker.Clear();
        return new(items.Select(item => item.Details).ToArray(), nextCursor, queriedAt, snapshot, completeness);
    }

    public async Task<AuditDetail> FindAsync(
        Guid actorUserId,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty || eventId == Guid.Empty) throw new AuditFilterInvalidException();
        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        var actor = await GetActorAsync(actorUserId, queriedAt, cancellationToken);
        var detailQuery = dbContext.AuditEvents.AsNoTracking()
            .Where(item => item.Id == eventId &&
                (item.BranchId == null || item.BranchId == BranchScope.LorettaId));
        var row = await ApplyHierarchyFilter(detailQuery, actor, null, null)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new AuditEventNotFoundException();
        var maps = await BuildScopeMapsAsync([row], cancellationToken);
        var scope = ResolveScope(row, actor, maps, null);
        if (!scope.Visible) throw new AuditEventNotFoundException();
        var result = new AuditDetail(Project(row, scope), queriedAt);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private async Task<ActorAccess> GetActorAsync(
        Guid actorUserId,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var actors = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where user.Id == actorUserId && user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= at && (employment.ValidTo == null || at < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= at && (role.ValidTo == null || at < role.ValidTo)
            select new ActorAccess(user.Id, user.PersonId, role.RoleCode))
            .Take(2).ToListAsync(cancellationToken);
        if (actors.Count != 1 || !RoleHierarchy.GrantsAuditView(actors[0].RoleCode))
            throw new AuditAccessDeniedException();
        return actors[0];
    }

    private async Task<TraceScope> BuildTraceAsync(Guid obligationId, CancellationToken cancellationToken)
    {
        var obligation = await dbContext.WorkObligations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == obligationId && item.BranchId == BranchScope.LorettaId,
                cancellationToken)
            ?? throw new AuditEventNotFoundException();
        var resourceIds = new HashSet<Guid>
        {
            obligation.Id,
            obligation.GenerationRequestId,
            obligation.TaskDefinitionVersionId,
        };
        var configurationIds = new HashSet<Guid> { obligation.TaskDefinitionVersionId };
        var taskVersion = await dbContext.TaskDefinitionVersions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == obligation.TaskDefinitionVersionId, cancellationToken);
        if (taskVersion is not null)
        {
            resourceIds.Add(taskVersion.ReleaseId);
            configurationIds.Add(taskVersion.ReleaseId);
        }

        if (obligation.EvidencePolicyVersionId.HasValue)
        {
            var id = obligation.EvidencePolicyVersionId.Value;
            resourceIds.Add(id); configurationIds.Add(id);
            var policy = await dbContext.EvidencePolicyVersions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (policy is not null) { resourceIds.Add(policy.ReleaseId); configurationIds.Add(policy.ReleaseId); }
        }
        if (obligation.ValidationPolicyVersionId.HasValue)
        {
            var id = obligation.ValidationPolicyVersionId.Value;
            resourceIds.Add(id); configurationIds.Add(id);
            var policy = await dbContext.ValidationPolicyVersions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (policy is not null) { resourceIds.Add(policy.ReleaseId); configurationIds.Add(policy.ReleaseId); }
        }

        var assignmentIds = await dbContext.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == obligationId).Select(item => item.Id).ToArrayAsync(cancellationToken);
        var evidenceItems = await dbContext.EvidenceItems.AsNoTracking()
            .Where(item => item.ObligationId == obligationId).Select(item => item.Id).ToArrayAsync(cancellationToken);
        var evidenceVersions = await dbContext.EvidenceVersions.AsNoTracking()
            .Where(item => evidenceItems.Contains(item.EvidenceItemId)).Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var reviewIds = await dbContext.EvidenceReviewSnapshots.AsNoTracking()
            .Where(item => item.ObligationId == obligationId).Select(item => item.Id).ToArrayAsync(cancellationToken);
        var requirementIds = await dbContext.ValidationRequirements.AsNoTracking()
            .Where(item => item.ObligationId == obligationId).Select(item => item.Id).ToArrayAsync(cancellationToken);
        var decisionIds = await dbContext.ValidationDecisionVersions.AsNoTracking()
            .Where(item => requirementIds.Contains(item.RequirementId)).Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        resourceIds.UnionWith(assignmentIds);
        resourceIds.UnionWith(evidenceItems);
        resourceIds.UnionWith(evidenceVersions);
        resourceIds.UnionWith(reviewIds);
        resourceIds.UnionWith(requirementIds);
        resourceIds.UnionWith(decisionIds);
        return new(obligationId, resourceIds, configurationIds,
            assignmentIds.ToHashSet(), evidenceItems.Concat(evidenceVersions).Concat(reviewIds).ToHashSet(),
            requirementIds.Concat(decisionIds).ToHashSet());
    }

    private IQueryable<AuditEvent> ApplyHierarchyFilter(
        IQueryable<AuditEvent> query,
        ActorAccess actor,
        TraceScope? trace,
        string? requiredLevel)
    {
        if (actor.RoleCode == CanonicalRole.Direction && requiredLevel is null) return query;
        var lowerLevels = actor.RoleCode switch
        {
            CanonicalRole.Administration => new[] { CanonicalRole.Subcoordination, CanonicalRole.SalesFloor },
            CanonicalRole.Subcoordination => new[] { CanonicalRole.SalesFloor },
            _ => Array.Empty<string>(),
        };
        var canonicalLevels = new[]
        {
            CanonicalRole.Direction, CanonicalRole.Administration,
            CanonicalRole.Subcoordination, CanonicalRole.SalesFloor,
        };
        var historicalSubjects =
            from user in dbContext.AppUsers.AsNoTracking()
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where role.BranchId == BranchScope.LorettaId && canonicalLevels.Contains(role.RoleCode)
            select new { UserId = user.Id, user.PersonId, role.RoleCode, role.ValidFrom, role.ValidTo };
        var people = dbContext.People.AsNoTracking()
            .Select(item => new { ResourceId = item.Id, PersonId = item.Id });
        var personLinks = people
            .Concat(dbContext.AppUsers.AsNoTracking()
                .Select(item => new { ResourceId = item.Id, item.PersonId }))
            .Concat(dbContext.EmploymentVersions.AsNoTracking()
                .Select(item => new { ResourceId = item.Id, item.PersonId }))
            .Concat(dbContext.RoleAssignmentVersions.AsNoTracking()
                .Join(dbContext.AppUsers.AsNoTracking(), role => role.UserId, user => user.Id,
                    (role, user) => new { ResourceId = role.Id, user.PersonId }))
            .Concat(dbContext.AvailabilityDayVersions.AsNoTracking()
                .Select(item => new { ResourceId = item.Id, item.PersonId }))
            .Concat(dbContext.AssignmentVersions.AsNoTracking()
                .Select(item => new { ResourceId = item.Id, item.PersonId }));
        var obligationLinks = dbContext.WorkObligations.AsNoTracking()
            .Select(item => new { ResourceId = item.Id, ObligationId = item.Id })
            .Concat(dbContext.GenerationRequests.AsNoTracking().Where(item => item.ObligationId != null)
                .Select(item => new { ResourceId = item.Id, ObligationId = item.ObligationId!.Value }))
            .Concat(dbContext.ExecutionResults.AsNoTracking()
                .Select(item => new { ResourceId = item.Id, item.ObligationId }))
            .Concat(dbContext.EvidenceItems.AsNoTracking()
                .Select(item => new { ResourceId = item.Id, item.ObligationId }))
            .Concat(dbContext.EvidenceVersions.AsNoTracking()
                .Join(dbContext.EvidenceItems.AsNoTracking(), version => version.EvidenceItemId, item => item.Id,
                    (version, item) => new { ResourceId = version.Id, item.ObligationId }))
            .Concat(dbContext.EvidenceReviewSnapshots.AsNoTracking()
                .Select(item => new { ResourceId = item.Id, item.ObligationId }))
            .Concat(dbContext.ValidationRequirements.AsNoTracking()
                .Select(item => new { ResourceId = item.Id, item.ObligationId }))
            .Concat(dbContext.ValidationDecisionVersions.AsNoTracking()
                .Join(dbContext.ValidationRequirements.AsNoTracking(), decision => decision.RequirementId,
                    requirement => requirement.Id,
                    (decision, requirement) => new { ResourceId = decision.Id, requirement.ObligationId }));

        return query.Where(row =>
            trace != null && (requiredLevel == null || requiredLevel == CanonicalRole.Direction) &&
                row.ResourceId.HasValue &&
                trace.ConfigurationIds.Contains(row.ResourceId.Value) ||
            row.ResourceId.HasValue && personLinks.Any(link => link.ResourceId == row.ResourceId.Value &&
                historicalSubjects.Count(subject => subject.PersonId == link.PersonId &&
                    subject.ValidFrom <= row.OccurredAt &&
                    (subject.ValidTo == null || row.OccurredAt < subject.ValidTo)) == 1 &&
                historicalSubjects.Any(subject => subject.PersonId == link.PersonId &&
                    subject.ValidFrom <= row.OccurredAt &&
                    (subject.ValidTo == null || row.OccurredAt < subject.ValidTo) &&
                    (requiredLevel == null || subject.RoleCode == requiredLevel)) &&
                (actor.RoleCode == CanonicalRole.Direction || link.PersonId == actor.PersonId ||
                    historicalSubjects.Any(subject =>
                        subject.PersonId == link.PersonId && subject.ValidFrom <= row.OccurredAt &&
                        (subject.ValidTo == null || row.OccurredAt < subject.ValidTo) &&
                        lowerLevels.Contains(subject.RoleCode)))) ||
            row.ResourceId.HasValue && obligationLinks.Any(link => link.ResourceId == row.ResourceId.Value &&
                dbContext.AssignmentVersions.AsNoTracking().Any(assignment =>
                    assignment.ObligationId == link.ObligationId && assignment.AssignedAt <= row.OccurredAt &&
                    !dbContext.AssignmentVersions.AsNoTracking().Any(other =>
                        other.ObligationId == link.ObligationId && other.AssignedAt <= row.OccurredAt &&
                        other.AssignedAt > assignment.AssignedAt) &&
                    dbContext.AssignmentVersions.AsNoTracking().Count(other =>
                        other.ObligationId == link.ObligationId &&
                        other.AssignedAt == assignment.AssignedAt) == 1 &&
                    historicalSubjects.Count(subject => subject.PersonId == assignment.PersonId &&
                        subject.ValidFrom <= row.OccurredAt &&
                        (subject.ValidTo == null || row.OccurredAt < subject.ValidTo)) == 1 &&
                    historicalSubjects.Any(subject => subject.PersonId == assignment.PersonId &&
                        subject.ValidFrom <= row.OccurredAt &&
                        (subject.ValidTo == null || row.OccurredAt < subject.ValidTo) &&
                        (requiredLevel == null || subject.RoleCode == requiredLevel)) &&
                    (actor.RoleCode == CanonicalRole.Direction || assignment.PersonId == actor.PersonId ||
                        historicalSubjects.Any(subject =>
                            subject.PersonId == assignment.PersonId && subject.ValidFrom <= row.OccurredAt &&
                            (subject.ValidTo == null || row.OccurredAt < subject.ValidTo) &&
                            lowerLevels.Contains(subject.RoleCode))))) ||
            row.Action == "AUDIT_EVENT_DELETE_ATTEMPTED" && row.ActorUserId.HasValue &&
            historicalSubjects.Count(subject => subject.UserId == row.ActorUserId.Value &&
                subject.ValidFrom <= row.OccurredAt &&
                (subject.ValidTo == null || row.OccurredAt < subject.ValidTo)) == 1 &&
            historicalSubjects.Any(subject => subject.UserId == row.ActorUserId.Value &&
                subject.ValidFrom <= row.OccurredAt &&
                (subject.ValidTo == null || row.OccurredAt < subject.ValidTo) &&
                (requiredLevel == null || subject.RoleCode == requiredLevel) &&
                (actor.RoleCode == CanonicalRole.Direction || subject.PersonId == actor.PersonId ||
                    lowerLevels.Contains(subject.RoleCode))) ||
            actor.RoleCode == CanonicalRole.Direction && requiredLevel == CanonicalRole.Direction &&
                GovernanceResourceTypes.Contains(row.ResourceType));
    }

    private async Task<ScopeMaps> BuildScopeMapsAsync(
        IReadOnlyCollection<AuditEvent> events,
        CancellationToken cancellationToken)
    {
        var ids = events.Where(item => item.ResourceId.HasValue).Select(item => item.ResourceId!.Value)
            .Distinct().ToArray();
        var eventActors = events.Where(item => item.ActorUserId.HasValue).Select(item => item.ActorUserId!.Value)
            .Distinct().ToArray();
        var obligationMap = new Dictionary<Guid, Guid>();
        foreach (var id in await dbContext.WorkObligations.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => item.Id).ToArrayAsync(cancellationToken)) obligationMap[id] = id;
        foreach (var row in await dbContext.GenerationRequests.AsNoTracking().Where(item => ids.Contains(item.Id) && item.ObligationId != null)
                     .Select(item => new { item.Id, ObligationId = item.ObligationId!.Value }).ToArrayAsync(cancellationToken)) obligationMap[row.Id] = row.ObligationId;
        foreach (var row in await dbContext.AssignmentVersions.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => new { item.Id, item.ObligationId }).ToArrayAsync(cancellationToken)) obligationMap[row.Id] = row.ObligationId;
        foreach (var row in await dbContext.ExecutionResults.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => new { item.Id, item.ObligationId }).ToArrayAsync(cancellationToken)) obligationMap[row.Id] = row.ObligationId;
        foreach (var row in await dbContext.EvidenceItems.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => new { item.Id, item.ObligationId }).ToArrayAsync(cancellationToken)) obligationMap[row.Id] = row.ObligationId;
        foreach (var row in await (from version in dbContext.EvidenceVersions.AsNoTracking()
                                   join item in dbContext.EvidenceItems.AsNoTracking() on version.EvidenceItemId equals item.Id
                                   where ids.Contains(version.Id)
                                   select new { version.Id, item.ObligationId }).ToArrayAsync(cancellationToken)) obligationMap[row.Id] = row.ObligationId;
        foreach (var row in await dbContext.EvidenceReviewSnapshots.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => new { item.Id, item.ObligationId }).ToArrayAsync(cancellationToken)) obligationMap[row.Id] = row.ObligationId;
        foreach (var row in await dbContext.ValidationRequirements.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => new { item.Id, item.ObligationId }).ToArrayAsync(cancellationToken)) obligationMap[row.Id] = row.ObligationId;
        foreach (var row in await (from decision in dbContext.ValidationDecisionVersions.AsNoTracking()
                                   join requirement in dbContext.ValidationRequirements.AsNoTracking() on decision.RequirementId equals requirement.Id
                                   where ids.Contains(decision.Id)
                                   select new { decision.Id, requirement.ObligationId }).ToArrayAsync(cancellationToken)) obligationMap[row.Id] = row.ObligationId;

        var personMap = new Dictionary<Guid, Guid>();
        foreach (var id in await dbContext.People.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => item.Id).ToArrayAsync(cancellationToken)) personMap[id] = id;
        foreach (var row in await dbContext.AppUsers.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => new { item.Id, item.PersonId }).ToArrayAsync(cancellationToken)) personMap[row.Id] = row.PersonId;
        foreach (var row in await dbContext.EmploymentVersions.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => new { item.Id, item.PersonId }).ToArrayAsync(cancellationToken)) personMap[row.Id] = row.PersonId;
        foreach (var row in await dbContext.AvailabilityDayVersions.AsNoTracking().Where(item => ids.Contains(item.Id))
                     .Select(item => new { item.Id, item.PersonId }).ToArrayAsync(cancellationToken)) personMap[row.Id] = row.PersonId;
        foreach (var row in await (from role in dbContext.RoleAssignmentVersions.AsNoTracking()
                                   join user in dbContext.AppUsers.AsNoTracking() on role.UserId equals user.Id
                                   where ids.Contains(role.Id)
                                   select new { role.Id, user.PersonId }).ToArrayAsync(cancellationToken)) personMap[row.Id] = row.PersonId;

        var obligationIds = obligationMap.Values.Distinct().ToArray();
        var assignments = await dbContext.AssignmentVersions.AsNoTracking()
            .Where(item => obligationIds.Contains(item.ObligationId))
            .Select(item => new AssignmentSubject(item.ObligationId, item.PersonId, item.AssignedAt, item.Id))
            .ToArrayAsync(cancellationToken);
        var assignmentPeople = assignments.ToDictionary(item => item.AssignmentId, item => item.PersonId);
        var personIds = assignments.Select(item => item.PersonId).Concat(personMap.Values).Distinct().ToArray();
        var users = await dbContext.AppUsers.AsNoTracking()
            .Where(item => personIds.Contains(item.PersonId) || eventActors.Contains(item.Id))
            .Select(item => new UserPerson(item.Id, item.PersonId)).ToArrayAsync(cancellationToken);
        foreach (var user in users.Where(item => eventActors.Contains(item.UserId))) personMap.TryAdd(user.UserId, user.PersonId);
        personIds = personIds.Concat(users.Select(item => item.PersonId)).Distinct().ToArray();
        var userIds = users.Select(item => item.UserId).ToArray();
        var roles = await dbContext.RoleAssignmentVersions.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId) && item.BranchId == BranchScope.LorettaId)
            .Select(item => new HistoricalRole(item.UserId, item.RoleCode, item.ValidFrom, item.ValidTo))
            .ToArrayAsync(cancellationToken);
        return new(obligationMap, personMap, assignmentPeople, assignments, users, roles);
    }

    private static ResolvedScope ResolveScope(
        AuditEvent row,
        ActorAccess actor,
        ScopeMaps maps,
        TraceScope? trace)
    {
        if (row.BranchId.HasValue && row.BranchId != BranchScope.LorettaId) return ResolvedScope.Hidden;
        if (trace is not null && row.ResourceId.HasValue && trace.ConfigurationIds.Contains(row.ResourceId.Value))
            return new(true, null, CanonicalRole.Direction, "DEPENDENCY", trace.ObligationId);

        Guid? obligationId = null;
        Guid? personId = null;
        if (row.ResourceId.HasValue && maps.ObligationByResource.TryGetValue(row.ResourceId.Value, out var obligation))
            obligationId = obligation;
        if (row.ResourceId.HasValue && maps.PersonByResource.TryGetValue(row.ResourceId.Value, out var person))
            personId = person;
        if (row.Action == "AUDIT_EVENT_DELETE_ATTEMPTED" && row.ActorUserId.HasValue &&
            maps.PersonByResource.TryGetValue(row.ActorUserId.Value, out var securityActor))
            personId = securityActor;
        if (row.ResourceId.HasValue && maps.AssignmentPersonByResource.TryGetValue(
                row.ResourceId.Value, out var assignedPerson))
            personId = assignedPerson;
        else if (obligationId.HasValue)
        {
            var assignment = maps.Assignments.Where(item => item.ObligationId == obligationId && item.AssignedAt <= row.OccurredAt)
                .OrderByDescending(item => item.AssignedAt).ThenByDescending(item => item.AssignmentId).FirstOrDefault();
            personId = assignment?.PersonId;
        }

        if (personId.HasValue)
        {
            var userIds = maps.Users.Where(item => item.PersonId == personId).Select(item => item.UserId).ToHashSet();
            var levels = maps.Roles.Where(item => userIds.Contains(item.UserId) && item.ValidFrom <= row.OccurredAt &&
                    (item.ValidTo is null || row.OccurredAt < item.ValidTo) && CanonicalRole.IsDefined(item.RoleCode))
                .Select(item => item.RoleCode).Distinct(StringComparer.Ordinal).ToArray();
            if (levels.Length == 1)
            {
                var visible = actor.RoleCode == CanonicalRole.Direction || actor.PersonId == personId ||
                    RoleHierarchy.IsStrictlySuperior(actor.RoleCode, levels[0]);
                var relation = actor.RoleCode == CanonicalRole.Direction ? "ALL" :
                    actor.PersonId == personId ? "OWN" : visible ? "LOWER" : null;
                return new(visible, personId, levels[0], relation, obligationId);
            }
        }

        if (GovernanceResourceTypes.Contains(row.ResourceType))
            return new(actor.RoleCode == CanonicalRole.Direction, null, CanonicalRole.Direction,
                actor.RoleCode == CanonicalRole.Direction ? "ALL" : null, obligationId);
        if (actor.RoleCode == CanonicalRole.Direction)
            return new(true, null, null, null, obligationId);
        return ResolvedScope.Hidden;
    }

    private static AuditEventDetails Project(AuditEvent row, ResolvedScope scope)
    {
        var before = Minimize(row.BeforeData, out var beforeOmitted);
        var after = Minimize(row.AfterData, out var afterOmitted);
        return new(
            row.Id,
            row.OccurredAt,
            new(row.ActorType, row.ActorUserId),
            row.Action,
            new(row.ResourceType, row.ResourceId,
                row.BranchId == BranchScope.LorettaId ? BranchScope.LorettaCode : null),
            new(scope.PersonId, scope.Level, scope.Relation, scope.ObligationId),
            new(before, after, beforeOmitted + afterOmitted),
            new(!string.IsNullOrWhiteSpace(row.Reason)),
            row.Outcome,
            row.CorrelationId);
    }

    private static Dictionary<string, JsonElement>? Minimize(JsonDocument? document, out int omitted)
    {
        omitted = 0;
        if (document is null) return null;
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            omitted = 1;
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!AllowedChangeProperties.Contains(property.Name) || !IsAllowedScalar(property.Value))
            {
                omitted++;
                continue;
            }
            result[property.Name] = property.Value.Clone();
        }
        return result;
    }

    private static bool IsAllowedScalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number => true,
        JsonValueKind.String => (value.GetString()?.Length ?? 0) <= 128,
        _ => false,
    };

    private static async Task<AuditCompleteness> CompletenessAsync(
        TraceScope trace,
        IQueryable<AuditEvent> query,
        CancellationToken cancellationToken) => new(
        await query.AnyAsync(item => item.ResourceId.HasValue &&
            trace.ConfigurationIds.Contains(item.ResourceId.Value), cancellationToken),
        await query.AnyAsync(item => item.ResourceId.HasValue &&
            trace.AssignmentIds.Contains(item.ResourceId.Value), cancellationToken),
        await query.AnyAsync(item => item.ResourceId.HasValue &&
            trace.EvidenceIds.Contains(item.ResourceId.Value), cancellationToken),
        await query.AnyAsync(item => item.ResourceId.HasValue &&
            trace.ValidationIds.Contains(item.ResourceId.Value), cancellationToken));

    private string EncodeCursor(AuditCursor cursor) => cursorProtector.Protect(
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(cursor)).TrimEnd('=').Replace('+', '-').Replace('/', '_'));

    private AuditCursor? DecodeCursor(string? value, string filterHash, DateTimeOffset queriedAt, bool ascending)
    {
        if (value is null) return null;
        try
        {
            if (value.Length is 0 or > MaximumCursorLength) throw new FormatException();
            var unprotected = cursorProtector.Unprotect(value);
            var raw = unprotected.Replace('-', '+').Replace('_', '/');
            raw = raw.PadRight(raw.Length + (4 - raw.Length % 4) % 4, '=');
            var cursor = JsonSerializer.Deserialize<AuditCursor>(Convert.FromBase64String(raw))
                ?? throw new FormatException();
            if (cursor.Version != CursorVersion || cursor.Ascending != ascending || cursor.LastEventId == Guid.Empty ||
                cursor.UpperEventId == Guid.Empty || cursor.ExpiresAt <= queriedAt ||
                cursor.FilterHash.Length != filterHash.Length ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(cursor.FilterHash), Encoding.ASCII.GetBytes(filterHash)))
                throw new FormatException();
            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or CryptographicException)
        {
            throw new AuditCursorInvalidException();
        }
    }

    private static string FilterHash(AuditQueryRequest request, ActorAccess actor)
    {
        var canonical = string.Join('\n', "/api/v1/audit-events", actor.UserId.ToString("D"),
            actor.PersonId.ToString("D"), actor.RoleCode, request.From.ToString("O", CultureInfo.InvariantCulture),
            request.To.ToString("O", CultureInfo.InvariantCulture), request.ActorFilter?.ToString("D") ?? string.Empty,
            request.ResourceType ?? string.Empty, request.ResourceId?.ToString("D") ?? string.Empty,
            request.Action ?? string.Empty, request.Outcome ?? string.Empty,
            request.CorrelationId?.ToString("D") ?? string.Empty, request.BranchCode, request.Level ?? string.Empty,
            request.TraceObligationId?.ToString("D") ?? string.Empty, request.Limit.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void Validate(AuditQueryRequest request)
    {
        if (request.ActorUserId == Guid.Empty || request.From.Offset != TimeSpan.Zero || request.To.Offset != TimeSpan.Zero ||
            request.To <= request.From || request.To - request.From > MaximumRange || request.Limit is < 1 or > 100 ||
            request.BranchCode != BranchScope.LorettaCode || request.Level is not null && !CanonicalRole.IsDefined(request.Level) ||
            request.ResourceId.HasValue && request.ResourceType is null ||
            request.TraceObligationId.HasValue && (request.ActorFilter.HasValue || request.ResourceType is not null ||
                request.ResourceId.HasValue || request.CorrelationId.HasValue || request.Level is not null))
            throw new AuditFilterInvalidException();
    }

    private async Task<bool> CanViewTraceObligationAsync(
        Guid obligationId,
        ActorAccess actor,
        DateTimeOffset queriedAt,
        CancellationToken cancellationToken)
    {
        var subjects = await (
            from assignment in dbContext.AssignmentVersions.AsNoTracking()
            join user in dbContext.AppUsers.AsNoTracking() on assignment.PersonId equals user.PersonId
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where assignment.ObligationId == obligationId && assignment.Status == AssignmentVersionStatuses.Current &&
                user.Status == BootstrapContract.ActiveAccountStatus && employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active && employment.ValidFrom <= queriedAt &&
                (employment.ValidTo == null || queriedAt < employment.ValidTo) && role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active && role.ValidFrom <= queriedAt &&
                (role.ValidTo == null || queriedAt < role.ValidTo)
            select new { user.PersonId, role.RoleCode }).Take(2).ToArrayAsync(cancellationToken);
        return subjects.Length == 1 && CanonicalRole.IsDefined(subjects[0].RoleCode) &&
            (subjects[0].PersonId == actor.PersonId || RoleHierarchy.IsStrictlySuperior(actor.RoleCode, subjects[0].RoleCode));
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginReadOnlyAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        return transaction;
    }

    private sealed record ActorAccess(Guid UserId, Guid PersonId, string RoleCode);
    private sealed record EventKey(DateTimeOffset OccurredAt, Guid EventId);
    private sealed record ProjectedEvent(AuditEvent Row, ResolvedScope Scope, AuditEventDetails Details);
    private sealed record AssignmentSubject(Guid ObligationId, Guid PersonId, DateTimeOffset AssignedAt, Guid AssignmentId);
    private sealed record UserPerson(Guid UserId, Guid PersonId);
    private sealed record HistoricalRole(Guid UserId, string RoleCode, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);
    private sealed record ScopeMaps(
        IReadOnlyDictionary<Guid, Guid> ObligationByResource,
        IReadOnlyDictionary<Guid, Guid> PersonByResource,
        IReadOnlyDictionary<Guid, Guid> AssignmentPersonByResource,
        IReadOnlyCollection<AssignmentSubject> Assignments,
        IReadOnlyCollection<UserPerson> Users,
        IReadOnlyCollection<HistoricalRole> Roles);
    private sealed record TraceScope(
        Guid ObligationId,
        HashSet<Guid> ResourceIds,
        HashSet<Guid> ConfigurationIds,
        HashSet<Guid> AssignmentIds,
        HashSet<Guid> EvidenceIds,
        HashSet<Guid> ValidationIds);
    private sealed record ResolvedScope(bool Visible, Guid? PersonId, string? Level, string? Relation, Guid? ObligationId)
    {
        public static ResolvedScope Hidden { get; } = new(false, null, null, null, null);
    }
    private sealed record AuditCursor(
        int Version,
        bool Ascending,
        DateTimeOffset LastOccurredAt,
        Guid LastEventId,
        DateTimeOffset UpperOccurredAt,
        Guid UpperEventId,
        string FilterHash,
        DateTimeOffset ExpiresAt);
}
