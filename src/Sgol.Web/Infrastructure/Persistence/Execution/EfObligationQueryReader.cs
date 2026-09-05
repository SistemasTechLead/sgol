using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Execution;

public sealed class EfObligationQueryReader(
    SgolDbContext dbContext,
    IClock clock) : IObligationQueryReader
{
    private const int CursorVersion = 1;
    private const int MaximumCursorLength = 2048;
    private const int MaximumManualReferenceRunes = 120;
    private static readonly JsonSerializerOptions CursorJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo LorettaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(ActivationPolicyCatalog.LorettaTimeZone);

    public async Task<ObligationPage> ListAsync(
        ObligationListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateListRequest(request);
        var filterHash = FilterHash(request);
        var cursor = DecodeListCursor(request.Cursor, filterHash);

        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        var actor = await GetActorAsync(request.ActorUserId, queriedAt, cancellationToken);
        var query = BuildVisibleRows(actor, queriedAt);

        if (request.PeriodId is { } periodId)
        {
            query = query.Where(item => item.PeriodId == periodId);
        }

        if (request.TaskCode is { } taskCode)
        {
            query = query.Where(item => item.TaskCode == taskCode);
        }

        if (request.ExecutionStatus is { } executionStatus)
        {
            query = query.Where(item => item.ExecutionStatus == executionStatus);
        }

        if (request.Condition == ObligationConditions.Overdue)
        {
            query = query.Where(item =>
                item.ExecutionStatus == WorkObligationStatuses.Pending &&
                item.DueAt != null && item.DueAt < queriedAt);
        }
        else if (request.Condition == ObligationConditions.NotOverdue)
        {
            query = query.Where(item =>
                item.ExecutionStatus != WorkObligationStatuses.Pending ||
                item.DueAt == null || item.DueAt >= queriedAt);
        }

        if (request.ResponsiblePersonId is { } responsiblePersonId)
        {
            query = query.Where(item => dbContext.AssignmentVersions.AsNoTracking().Any(assignment =>
                assignment.ObligationId == item.ObligationId &&
                assignment.Status == AssignmentVersionStatuses.Current &&
                assignment.PersonId == responsiblePersonId));
        }

        query = ApplyListCursor(query, cursor);
        var rows = await OrderRows(query)
            .Take(request.Limit + 1)
            .ToListAsync(cancellationToken);
        var hasNextPage = rows.Count > request.Limit;
        if (hasNextPage)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var items = await MapItemsAsync(rows, actor, queriedAt, cancellationToken);
        var nextCursor = hasNextPage ? EncodeListCursor(rows[^1], filterHash) : null;
        await transaction.CommitAsync(cancellationToken);
        return new ObligationPage(items, nextCursor, queriedAt);
    }

    public async Task<ObligationDetailPage> GetAsync(
        ObligationDetailRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ObligationId == Guid.Empty || request.HistoryLimit is < 1 or > 100)
        {
            throw new ObligationHistoryFilterInvalidException();
        }

        var cursor = DecodeHistoryCursor(request.HistoryCursor, request.ObligationId);
        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        var actor = await GetActorAsync(request.ActorUserId, queriedAt, cancellationToken);
        var row = await BuildVisibleRows(actor, queriedAt)
            .SingleOrDefaultAsync(item => item.ObligationId == request.ObligationId, cancellationToken)
            ?? throw new ObligationQueryNotFoundException();
        var item = AssertSingle(await MapItemsAsync([row], actor, queriedAt, cancellationToken));
        var historyRows = await ReadHistoryAsync(row, cursor, request.HistoryLimit, cancellationToken);
        var hasNextPage = historyRows.Count > request.HistoryLimit;
        if (hasNextPage)
        {
            historyRows.RemoveAt(historyRows.Count - 1);
        }

        var history = historyRows.Select(MapHistory).ToArray();
        var nextCursor = hasNextPage
            ? EncodeHistoryCursor(request.ObligationId, historyRows[^1])
            : null;
        var detail = new ObligationDetail(
            item.ObligationId,
            item.Task,
            item.Origin,
            item.Period,
            item.Dates,
            item.ExecutionStatus,
            item.Condition,
            item.CurrentAssignment,
            item.Links,
            new ObligationGenerationRequest(
                row.GenerationRequestId,
                row.GenerationResult,
                row.RequestedAt,
                row.RequestedByUserId),
            history);

        await transaction.CommitAsync(cancellationToken);
        return new ObligationDetailPage(detail, nextCursor, queriedAt);
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginReadOnlyAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        return transaction;
    }

    private async Task<ActorAccess> GetActorAsync(
        Guid actorUserId,
        DateTimeOffset queriedAt,
        CancellationToken cancellationToken)
    {
        var actors = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking()
                on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking()
                on user.Id equals role.UserId
            where user.Id == actorUserId &&
                user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= queriedAt &&
                (employment.ValidTo == null || queriedAt < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= queriedAt &&
                (role.ValidTo == null || queriedAt < role.ValidTo)
            select new ActorAccess(user.PersonId, role.RoleCode))
            .Take(2)
            .ToListAsync(cancellationToken);

        if (actors.Count != 1 || !CanonicalRole.IsDefined(actors[0].RoleCode))
        {
            throw new ObligationQueryAccessDeniedException();
        }

        return actors[0];
    }

    private IQueryable<ObligationRow> BuildVisibleRows(ActorAccess actor, DateTimeOffset queriedAt)
    {
        var visibleRoleCodes = VisibleRoleCodes(actor.RoleCode);
        var visibleResponsibleIds = (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking()
                on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking()
                on user.Id equals role.UserId
            where user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= queriedAt &&
                (employment.ValidTo == null || queriedAt < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= queriedAt &&
                (role.ValidTo == null || queriedAt < role.ValidTo) &&
                (user.PersonId == actor.PersonId || visibleRoleCodes.Contains(role.RoleCode))
            select user.PersonId)
            .Distinct();

        return
            from obligation in dbContext.WorkObligations.AsNoTracking()
            join request in dbContext.GenerationRequests.AsNoTracking()
                on obligation.GenerationRequestId equals request.Id
            join taskVersion in dbContext.TaskDefinitionVersions.AsNoTracking()
                on obligation.TaskDefinitionVersionId equals taskVersion.Id
            join task in dbContext.TaskDefinitions.AsNoTracking()
                on taskVersion.TaskDefinitionId equals task.Id
            join period in dbContext.WeekPeriods.AsNoTracking()
                on obligation.PeriodId equals period.Id
            where obligation.BranchId == BranchScope.LorettaId &&
                request.BranchId == obligation.BranchId &&
                request.PeriodId == obligation.PeriodId &&
                request.ObligationId == obligation.Id &&
                period.BranchId == obligation.BranchId &&
                (actor.RoleCode == CanonicalRole.Direction ||
                 dbContext.AssignmentVersions.AsNoTracking().Any(assignment =>
                     assignment.ObligationId == obligation.Id &&
                     assignment.Status == AssignmentVersionStatuses.Current &&
                     visibleResponsibleIds.Contains(assignment.PersonId)))
            select new ObligationRow
            {
                ObligationId = obligation.Id,
                TaskDefinitionId = task.Id,
                TaskCode = task.TaskCode,
                TaskOrder = task.TaskCode == "TAR-0005" ? 1 :
                    task.TaskCode == "TAR-0007" ? 2 :
                    task.TaskCode == "TAR-0008" ? 3 :
                    task.TaskCode == "TAR-0011" ? 4 :
                    task.TaskCode == "TAR-0018" ? 5 :
                    task.TaskCode == "TAR-0026" ? 6 :
                    task.TaskCode == "TAR-0092" ? 7 :
                    task.TaskCode == "TAR-0093" ? 8 : 9,
                TaskName = task.Name,
                TaskDefinitionVersionId = taskVersion.Id,
                TaskVersionNo = taskVersion.VersionNo,
                TaskVersionStatus = taskVersion.Status,
                TaskEffectiveFrom = taskVersion.EffectiveFrom,
                TaskEffectiveTo = taskVersion.EffectiveTo,
                TaskSchemaVersion = taskVersion.SchemaVersion,
                GenerationRequestId = request.Id,
                ActivationRuleVersionId = request.RuleVersionId,
                OriginType = request.OriginType,
                OriginReference = request.OriginReference,
                GenerationResult = request.Result,
                RequestedByUserId = request.RequestedBy,
                RequestedAt = request.RequestedAt,
                PeriodId = period.Id,
                IsoYear = period.IsoYear,
                IsoWeek = period.IsoWeek,
                StartsOn = period.StartsOn,
                EndsOn = period.EndsOn,
                DueAt = obligation.DueAt,
                ConcludedAt = obligation.ConcludedAt,
                ExecutionStatus = obligation.ExecutionStatus,
            };
    }

    private async Task<IReadOnlyList<ObligationListItem>> MapItemsAsync(
        IReadOnlyList<ObligationRow> rows,
        ActorAccess actor,
        DateTimeOffset queriedAt,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var obligationIds = rows.Select(item => item.ObligationId).ToArray();
        var assignments = await (
            from assignment in dbContext.AssignmentVersions.AsNoTracking()
            join person in dbContext.People.AsNoTracking()
                on assignment.PersonId equals person.Id
            where obligationIds.Contains(assignment.ObligationId) &&
                assignment.Status == AssignmentVersionStatuses.Current
            select new CurrentAssignmentRow(
                assignment.ObligationId,
                assignment.Id,
                assignment.PersonId,
                person.StableCode,
                person.DisplayName,
                assignment.AssignmentType,
                assignment.AssignedAt))
            .ToDictionaryAsync(item => item.ObligationId, cancellationToken);

        var explainableRoleCodes = ExplainableRoleCodes(actor.RoleCode);
        var explainableIds = await (
            from evaluation in dbContext.EligibilityEvaluations.AsNoTracking()
            join policy in dbContext.EligibilityPolicyVersions.AsNoTracking()
                on evaluation.PolicyVersionId equals policy.Id
            where obligationIds.Contains(evaluation.ObligationId) &&
                explainableRoleCodes.Contains(policy.RequiredRole)
            select evaluation.ObligationId)
            .Distinct()
            .ToHashSetAsync(cancellationToken);

        return rows.Select(row => MapItem(
            row,
            assignments.GetValueOrDefault(row.ObligationId),
            explainableIds.Contains(row.ObligationId),
            queriedAt)).ToArray();
    }

    private static ObligationListItem MapItem(
        ObligationRow row,
        CurrentAssignmentRow? assignment,
        bool includeEligibilityLink,
        DateTimeOffset queriedAt)
    {
        var origin = MapOrigin(row);
        var isOverdue = row.ExecutionStatus == WorkObligationStatuses.Pending &&
            row.DueAt is { } dueAt && dueAt < queriedAt;
        DateOnly? dueLocalDate = row.DueAt is { } due
            ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(due, LorettaTimeZone).DateTime)
            : null;
        var links = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = $"/api/v1/obligations/{row.ObligationId:D}",
        };
        if (includeEligibilityLink)
        {
            links["eligibility"] = $"/api/v1/obligations/{row.ObligationId:D}/eligibility";
        }

        return new ObligationListItem(
            row.ObligationId,
            new ObligationTask(
                row.TaskDefinitionId,
                row.TaskCode,
                row.TaskName,
                new ObligationTaskVersion(
                    row.TaskDefinitionVersionId,
                    row.TaskVersionNo,
                    row.TaskVersionStatus,
                    row.TaskEffectiveFrom,
                    row.TaskEffectiveTo,
                    row.TaskSchemaVersion)),
            origin,
            new ObligationPeriod(
                row.PeriodId,
                row.IsoYear,
                row.IsoWeek,
                row.StartsOn,
                row.EndsOn,
                ActivationPolicyCatalog.LorettaTimeZone),
            new ObligationDates(row.DueAt, dueLocalDate, row.ConcludedAt),
            row.ExecutionStatus,
            isOverdue ? ObligationConditions.Overdue : ObligationConditions.NotOverdue,
            assignment is null
                ? null
                : new ObligationAssignmentSummary(
                    assignment.AssignmentId,
                    new ObligationPerson(
                        assignment.PersonId,
                        assignment.StableCode,
                        assignment.DisplayName),
                    assignment.AssignmentType,
                    assignment.AssignedAt),
            links);
    }

    private static ObligationOrigin MapOrigin(ObligationRow row)
    {
        var kind = row.OriginType switch
        {
            ActivationOriginSchemas.ManualReference => ObligationOriginKinds.Manual,
            ActivationOriginSchemas.WorkingDayWindow => ObligationOriginKinds.Recurring,
            _ => throw new ObligationQueryInconsistentException(),
        };
        var reference = row.OriginType == ActivationOriginSchemas.ManualReference
            ? SanitizeManualReference(row.OriginReference)
            : ValidateRecurringReference(row.OriginReference);
        return new ObligationOrigin(
            kind,
            row.OriginType,
            reference,
            row.GenerationRequestId,
            row.ActivationRuleVersionId,
            row.RequestedAt);
    }

    private async Task<List<HistoryRow>> ReadHistoryAsync(
        ObligationRow obligation,
        HistoryCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = new List<HistoryRow>(checked((limit + 1) * 3));
        var generation = new HistoryRow(
            EventId: obligation.GenerationRequestId,
            EventType: ObligationHistoryEventTypes.GenerationRequested,
            TypeRank: HistoryRank.Generation,
            OccurredAt: obligation.RequestedAt,
            ActorType: obligation.RequestedByUserId is null
                ? ObligationHistoryActorTypes.System
                : ObligationHistoryActorTypes.Human,
            ActorUserId: obligation.RequestedByUserId,
            Reason: null,
            AssignmentId: null,
            AssignmentStatus: null,
            AssignmentType: null,
            ResponsiblePersonId: null,
            ResponsibleStableCode: null,
            ResponsibleDisplayName: null,
            SupersedesAssignmentId: null,
            PlanId: null,
            PublicationId: null,
            PublicationVersionNo: null,
            PublicationStatus: null,
            PublicationScopeRole: null,
            PublicationAssignmentVersionId: null,
            SupersedesPublicationId: null);
        if (IsAfterCursor(generation, cursor))
        {
            rows.Add(generation);
        }

        var assignmentsQuery =
            from assignment in dbContext.AssignmentVersions.AsNoTracking()
            join person in dbContext.People.AsNoTracking()
                on assignment.PersonId equals person.Id
            where assignment.ObligationId == obligation.ObligationId
            select new AssignmentHistoryRow
            {
                Id = assignment.Id,
                Status = assignment.Status,
                AssignmentType = assignment.AssignmentType,
                PersonId = assignment.PersonId,
                StableCode = person.StableCode,
                DisplayName = person.DisplayName,
                AssignedAt = assignment.AssignedAt,
                AssignedBy = assignment.AssignedBy,
                Reason = assignment.Reason,
                SupersedesId = assignment.SupersedesId,
            };
        if (cursor is not null)
        {
            assignmentsQuery = assignmentsQuery.Where(item =>
                item.AssignedAt > cursor.OccurredAt ||
                (item.AssignedAt == cursor.OccurredAt &&
                 ((item.AssignmentType == AssignmentTypes.Automatic
                       ? HistoryRank.AutomaticAssignment
                       : HistoryRank.CorrectedAssignment) > cursor.TypeRank ||
                  ((item.AssignmentType == AssignmentTypes.Automatic
                        ? HistoryRank.AutomaticAssignment
                        : HistoryRank.CorrectedAssignment) == cursor.TypeRank &&
                   item.Id.CompareTo(cursor.EventId) > 0))));
        }

        var assignments = await assignmentsQuery
            .OrderBy(item => item.AssignedAt)
            .ThenBy(item => item.AssignmentType == AssignmentTypes.Automatic
                ? HistoryRank.AutomaticAssignment
                : HistoryRank.CorrectedAssignment)
            .ThenBy(item => item.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        rows.AddRange(assignments.Select(item => new HistoryRow(
            EventId: item.Id,
            EventType: item.AssignmentType == AssignmentTypes.Automatic
                ? ObligationHistoryEventTypes.AutomaticAssignment
                : ObligationHistoryEventTypes.CorrectedAssignment,
            TypeRank: item.AssignmentType == AssignmentTypes.Automatic
                ? HistoryRank.AutomaticAssignment
                : HistoryRank.CorrectedAssignment,
            OccurredAt: item.AssignedAt,
            ActorType: item.AssignedBy is null
                ? ObligationHistoryActorTypes.System
                : ObligationHistoryActorTypes.Human,
            ActorUserId: item.AssignedBy,
            Reason: item.Reason,
            AssignmentId: item.Id,
            AssignmentStatus: item.Status,
            AssignmentType: item.AssignmentType,
            ResponsiblePersonId: item.PersonId,
            ResponsibleStableCode: item.StableCode,
            ResponsibleDisplayName: item.DisplayName,
            SupersedesAssignmentId: item.SupersedesId,
            PlanId: null,
            PublicationId: null,
            PublicationVersionNo: null,
            PublicationStatus: null,
            PublicationScopeRole: null,
            PublicationAssignmentVersionId: null,
            SupersedesPublicationId: null)));

        var publicationsQuery =
            from membership in dbContext.PlanVersionObligations.AsNoTracking()
            join publication in dbContext.PlanVersions.AsNoTracking()
                on membership.PlanVersionId equals publication.Id
            where membership.ObligationId == obligation.ObligationId
            select new PublicationHistoryRow
            {
                Id = publication.Id,
                PlanId = publication.PlanId,
                VersionNo = publication.VersionNo,
                Status = publication.Status,
                ScopeRole = publication.ScopeRole,
                PublishedBy = publication.PublishedBy,
                PublishedAt = publication.PublishedAt,
                AssignmentVersionId = membership.AssignmentVersionId,
                SupersedesId = publication.SupersedesId,
            };
        if (cursor is not null)
        {
            publicationsQuery = publicationsQuery.Where(item =>
                item.PublishedAt > cursor.OccurredAt ||
                (item.PublishedAt == cursor.OccurredAt &&
                 (HistoryRank.Publication > cursor.TypeRank ||
                  (HistoryRank.Publication == cursor.TypeRank && item.Id.CompareTo(cursor.EventId) > 0))));
        }

        var publications = await publicationsQuery
            .OrderBy(item => item.PublishedAt)
            .ThenBy(item => item.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        rows.AddRange(publications.Select(item => new HistoryRow(
            EventId: item.Id,
            EventType: ObligationHistoryEventTypes.IncludedInPublication,
            TypeRank: HistoryRank.Publication,
            OccurredAt: item.PublishedAt,
            ActorType: ObligationHistoryActorTypes.Human,
            ActorUserId: item.PublishedBy,
            Reason: null,
            AssignmentId: null,
            AssignmentStatus: null,
            AssignmentType: null,
            ResponsiblePersonId: null,
            ResponsibleStableCode: null,
            ResponsibleDisplayName: null,
            SupersedesAssignmentId: null,
            PlanId: item.PlanId,
            PublicationId: item.Id,
            PublicationVersionNo: item.VersionNo,
            PublicationStatus: item.Status,
            PublicationScopeRole: item.ScopeRole,
            PublicationAssignmentVersionId: item.AssignmentVersionId,
            SupersedesPublicationId: item.SupersedesId)));

        return rows
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.TypeRank)
            .ThenBy(item => item.EventId)
            .Take(limit + 1)
            .ToList();
    }

    private static ObligationHistoryEvent MapHistory(HistoryRow row)
    {
        var assignment = row.AssignmentId is { } assignmentId
            ? new ObligationHistoryAssignment(
                assignmentId,
                row.AssignmentStatus!,
                row.AssignmentType!,
                new ObligationPerson(
                    row.ResponsiblePersonId!.Value,
                    row.ResponsibleStableCode!,
                    row.ResponsibleDisplayName!),
                row.SupersedesAssignmentId)
            : null;
        var publication = row.PublicationId is { } publicationId
            ? new ObligationHistoryPublication(
                row.PlanId!.Value,
                publicationId,
                row.PublicationVersionNo!.Value,
                row.PublicationStatus!,
                row.PublicationScopeRole!,
                row.PublicationAssignmentVersionId!.Value,
                row.SupersedesPublicationId)
            : null;
        return new ObligationHistoryEvent(
            row.EventId,
            row.EventType,
            row.OccurredAt,
            row.ActorType,
            row.ActorUserId,
            row.Reason,
            assignment,
            publication);
    }

    private static IQueryable<ObligationRow> ApplyListCursor(
        IQueryable<ObligationRow> query,
        ListCursor? cursor)
    {
        if (cursor is null)
        {
            return query;
        }

        if (cursor.DueAt is { } cursorDueAt)
        {
            var cursorTaskOrder = TaskOrder(cursor.TaskCode);
            return query.Where(item =>
                item.DueAt == null || item.DueAt > cursorDueAt ||
                (item.DueAt == cursorDueAt &&
                 (item.StartsOn < cursor.StartsOn ||
                  (item.StartsOn == cursor.StartsOn &&
                   (item.TaskOrder > cursorTaskOrder ||
                    (item.TaskOrder == cursorTaskOrder &&
                     item.ObligationId.CompareTo(cursor.ObligationId) > 0))))));
        }

        var nullDueCursorTaskOrder = TaskOrder(cursor.TaskCode);
        return query.Where(item =>
            item.DueAt == null &&
            (item.StartsOn < cursor.StartsOn ||
             (item.StartsOn == cursor.StartsOn &&
              (item.TaskOrder > nullDueCursorTaskOrder ||
               (item.TaskOrder == nullDueCursorTaskOrder &&
                item.ObligationId.CompareTo(cursor.ObligationId) > 0)))));
    }

    private static IOrderedQueryable<ObligationRow> OrderRows(IQueryable<ObligationRow> query) =>
        query.OrderBy(item => item.DueAt == null)
            .ThenBy(item => item.DueAt)
            .ThenByDescending(item => item.StartsOn)
            .ThenBy(item => item.TaskOrder)
            .ThenBy(item => item.ObligationId);

    private static int TaskOrder(string taskCode) => taskCode switch
    {
        "TAR-0005" => 1,
        "TAR-0007" => 2,
        "TAR-0008" => 3,
        "TAR-0011" => 4,
        "TAR-0018" => 5,
        "TAR-0026" => 6,
        "TAR-0092" => 7,
        "TAR-0093" => 8,
        _ => 9,
    };

    private static void ValidateListRequest(ObligationListRequest request)
    {
        if (request.ActorUserId == Guid.Empty || request.Limit is < 1 or > 100 ||
            request.PeriodId == Guid.Empty || request.ResponsiblePersonId == Guid.Empty ||
            (request.TaskCode is not null && !TaskDefinitionCatalog.All.Any(
                item => item.TaskCode == request.TaskCode)) ||
            (request.ExecutionStatus is not null &&
             request.ExecutionStatus is not (WorkObligationStatuses.Pending or WorkObligationStatuses.Concluded)) ||
            (request.Condition is not null &&
             request.Condition is not (ObligationConditions.Overdue or ObligationConditions.NotOverdue)))
        {
            throw new ObligationQueryFilterInvalidException();
        }
    }

    private static string[] VisibleRoleCodes(string actorRoleCode) => actorRoleCode switch
    {
        CanonicalRole.Direction =>
            [CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
        CanonicalRole.Administration => [CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
        CanonicalRole.Subcoordination => [CanonicalRole.SalesFloor],
        CanonicalRole.SalesFloor => [],
        _ => throw new ObligationQueryAccessDeniedException(),
    };

    private static string[] ExplainableRoleCodes(string actorRoleCode) => actorRoleCode switch
    {
        CanonicalRole.Direction =>
            [CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
        CanonicalRole.Administration =>
            [CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
        CanonicalRole.Subcoordination => [CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
        CanonicalRole.SalesFloor => [CanonicalRole.SalesFloor],
        _ => [],
    };

    private static string FilterHash(ObligationListRequest request)
    {
        var canonical = string.Join(
            '\n',
            request.PeriodId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty,
            request.TaskCode ?? string.Empty,
            request.ExecutionStatus ?? string.Empty,
            request.Condition ?? string.Empty,
            request.ResponsiblePersonId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string EncodeListCursor(ObligationRow row, string filterHash) => EncodeCursor(
        new ListCursor(
            CursorVersion,
            row.DueAt,
            row.StartsOn,
            row.TaskCode,
            row.ObligationId,
            filterHash));

    private static ListCursor? DecodeListCursor(string? value, string filterHash)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var cursor = DecodeCursor<ListCursor>(value);
            if (cursor.Version != CursorVersion || cursor.ObligationId == Guid.Empty ||
                string.IsNullOrWhiteSpace(cursor.TaskCode) ||
                !TaskDefinitionCatalog.All.Any(item => item.TaskCode == cursor.TaskCode) ||
                string.IsNullOrWhiteSpace(cursor.FilterHash) ||
                cursor.FilterHash.Length != filterHash.Length ||
                (cursor.DueAt is { Offset: var offset } && offset != TimeSpan.Zero) ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(cursor.FilterHash),
                    Encoding.ASCII.GetBytes(filterHash)))
            {
                throw new FormatException();
            }

            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ObligationQueryFilterInvalidException();
        }
    }

    private static string EncodeHistoryCursor(Guid obligationId, HistoryRow row) => EncodeCursor(
        new HistoryCursor(
            CursorVersion,
            obligationId,
            row.OccurredAt,
            row.TypeRank,
            row.EventId));

    private static HistoryCursor? DecodeHistoryCursor(string? value, Guid obligationId)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var cursor = DecodeCursor<HistoryCursor>(value);
            if (cursor.Version != CursorVersion || cursor.ObligationId != obligationId ||
                cursor.EventId == Guid.Empty || cursor.TypeRank is < 1 or > 4 ||
                cursor.OccurredAt.Offset != TimeSpan.Zero)
            {
                throw new FormatException();
            }

            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ObligationHistoryFilterInvalidException();
        }
    }

    private static string EncodeCursor<T>(T cursor)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cursor, CursorJsonOptions);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static T DecodeCursor<T>(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumCursorLength || value.Length % 4 == 1)
        {
            throw new FormatException();
        }

        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (value.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };
        return JsonSerializer.Deserialize<T>(Convert.FromBase64String(padded), CursorJsonOptions)
            ?? throw new FormatException();
    }

    private static bool IsAfterCursor(HistoryRow row, HistoryCursor? cursor) =>
        cursor is null || row.OccurredAt > cursor.OccurredAt ||
        (row.OccurredAt == cursor.OccurredAt &&
         (row.TypeRank > cursor.TypeRank ||
          (row.TypeRank == cursor.TypeRank && row.EventId.CompareTo(cursor.EventId) > 0)));

    private static string SanitizeManualReference(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Any(char.IsControl))
        {
            throw new ObligationQueryInconsistentException();
        }

        var runes = trimmed.EnumerateRunes().ToArray();
        if (runes.Length <= MaximumManualReferenceRunes)
        {
            return trimmed;
        }

        return string.Concat(runes.Take(MaximumManualReferenceRunes - 1).Select(rune => rune.ToString())) + '…';
    }

    private static string ValidateRecurringReference(string value)
    {
        var parts = value.Split('|', StringSplitOptions.None);
        if (parts.Length != 3 || parts[0] != BranchScope.LorettaCode ||
            !DateOnly.TryParseExact(
                parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var localDate) ||
            !TimeOnly.TryParseExact(
                parts[2], "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var localTime) ||
            parts[1] != localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ||
            parts[2] != localTime.ToString("HH:mm", CultureInfo.InvariantCulture) ||
            localTime is not ({ Hour: 12, Minute: 0 } or { Hour: 17, Minute: 0 }))
        {
            throw new ObligationQueryInconsistentException();
        }

        return value;
    }

    private static T AssertSingle<T>(IReadOnlyList<T> items)
    {
        if (items.Count != 1)
        {
            throw new ObligationQueryInconsistentException();
        }

        return items[0];
    }

    private sealed record ActorAccess(Guid PersonId, string RoleCode);

    private sealed class ObligationRow
    {
        public required Guid ObligationId { get; init; }
        public required Guid TaskDefinitionId { get; init; }
        public required string TaskCode { get; init; }
        public required int TaskOrder { get; init; }
        public required string TaskName { get; init; }
        public required Guid TaskDefinitionVersionId { get; init; }
        public required int TaskVersionNo { get; init; }
        public required string TaskVersionStatus { get; init; }
        public DateTimeOffset? TaskEffectiveFrom { get; init; }
        public DateTimeOffset? TaskEffectiveTo { get; init; }
        public required int TaskSchemaVersion { get; init; }
        public required Guid GenerationRequestId { get; init; }
        public required Guid ActivationRuleVersionId { get; init; }
        public required string OriginType { get; init; }
        public required string OriginReference { get; init; }
        public required string GenerationResult { get; init; }
        public Guid? RequestedByUserId { get; init; }
        public required DateTimeOffset RequestedAt { get; init; }
        public required Guid PeriodId { get; init; }
        public required int IsoYear { get; init; }
        public required int IsoWeek { get; init; }
        public required DateOnly StartsOn { get; init; }
        public required DateOnly EndsOn { get; init; }
        public DateTimeOffset? DueAt { get; init; }
        public DateTimeOffset? ConcludedAt { get; init; }
        public required string ExecutionStatus { get; init; }
    }

    private sealed record CurrentAssignmentRow(
        Guid ObligationId,
        Guid AssignmentId,
        Guid PersonId,
        string StableCode,
        string DisplayName,
        string AssignmentType,
        DateTimeOffset AssignedAt);

    private sealed class AssignmentHistoryRow
    {
        public required Guid Id { get; init; }
        public required string Status { get; init; }
        public required string AssignmentType { get; init; }
        public required Guid PersonId { get; init; }
        public required string StableCode { get; init; }
        public required string DisplayName { get; init; }
        public required DateTimeOffset AssignedAt { get; init; }
        public Guid? AssignedBy { get; init; }
        public string? Reason { get; init; }
        public Guid? SupersedesId { get; init; }
    }

    private sealed class PublicationHistoryRow
    {
        public required Guid Id { get; init; }
        public required Guid PlanId { get; init; }
        public required int VersionNo { get; init; }
        public required string Status { get; init; }
        public required string ScopeRole { get; init; }
        public required Guid PublishedBy { get; init; }
        public required DateTimeOffset PublishedAt { get; init; }
        public required Guid AssignmentVersionId { get; init; }
        public Guid? SupersedesId { get; init; }
    }

    private sealed record HistoryRow(
        Guid EventId,
        string EventType,
        int TypeRank,
        DateTimeOffset OccurredAt,
        string ActorType,
        Guid? ActorUserId,
        string? Reason,
        Guid? AssignmentId,
        string? AssignmentStatus,
        string? AssignmentType,
        Guid? ResponsiblePersonId,
        string? ResponsibleStableCode,
        string? ResponsibleDisplayName,
        Guid? SupersedesAssignmentId,
        Guid? PlanId,
        Guid? PublicationId,
        int? PublicationVersionNo,
        string? PublicationStatus,
        string? PublicationScopeRole,
        Guid? PublicationAssignmentVersionId,
        Guid? SupersedesPublicationId);

    private sealed record ListCursor(
        int Version,
        DateTimeOffset? DueAt,
        DateOnly StartsOn,
        string TaskCode,
        Guid ObligationId,
        string FilterHash);

    private sealed record HistoryCursor(
        int Version,
        Guid ObligationId,
        DateTimeOffset OccurredAt,
        int TypeRank,
        Guid EventId);

    private static class HistoryRank
    {
        public const int Generation = 1;
        public const int AutomaticAssignment = 2;
        public const int CorrectedAssignment = 3;
        public const int Publication = 4;
    }
}
