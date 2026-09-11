using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Reporting.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Execution;

public sealed class EfObligationQueryReader(
    SgolDbContext dbContext,
    IClock clock) : IObligationQueryReader, IHierarchySupervisionReader, IIndicatorReader
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

    public async Task<SupervisionPage> ReadSupervisionAsync(
        SupervisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSupervisionRequest(request);
        var filterHash = SupervisionFilterHash(request);
        var cursor = DecodeSupervisionCursor(request.Cursor, filterHash);

        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        var actor = await GetSupervisionActorAsync(request.ActorUserId, queriedAt, cancellationToken);
        var query = BuildSupervisedRows(actor, queriedAt, request.Level, request.ResponsiblePersonId);
        query = ApplySupervisionFilters(query, request.IsoYear, request.IsoWeek, request.ExecutionStatus);
        query = ApplyListCursor(query, cursor);

        var rows = await OrderRows(query).Take(request.Limit + 1).ToListAsync(cancellationToken);
        var hasNextPage = rows.Count > request.Limit;
        if (hasNextPage) rows.RemoveAt(rows.Count - 1);

        var items = await MapSupervisionItemsAsync(rows, actor, queriedAt, cancellationToken);
        var nextCursor = hasNextPage ? EncodeListCursor(rows[^1], filterHash) : null;
        await transaction.CommitAsync(cancellationToken);
        return new(items, nextCursor, queriedAt);
    }

    public async Task<PendingValidationsPage> ReadPendingValidationsAsync(
        PendingValidationsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePendingRequest(request);
        var filterHash = PendingFilterHash(request);
        var cursor = DecodePendingCursor(request.Cursor, filterHash);

        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        var actor = await GetSupervisionActorAsync(request.ActorUserId, queriedAt, cancellationToken);
        var allowedValidatorRoles = AllowedPendingValidatorRoles(actor.RoleCode);
        var query = BuildSupervisedRows(actor, queriedAt, request.Level, request.ResponsiblePersonId);
        query = ApplySupervisionFilters(query, request.IsoYear, request.IsoWeek, WorkObligationStatuses.Concluded);
        query = query.Where(row => row.ValidationPolicyVersionId != null && row.ConcludedAt != null &&
            dbContext.ValidationPolicyVersions.AsNoTracking().Any(policy =>
                policy.Id == row.ValidationPolicyVersionId &&
                policy.TaskDefinitionId == row.TaskDefinitionId &&
                policy.TaskDefinitionVersionId == row.TaskDefinitionVersionId &&
                policy.IsRequired &&
                policy.ValidatorRelation == ValidationPolicyValues.ImmediateSuperior &&
                (policy.Status == VersionStatuses.Current || policy.Status == VersionStatuses.Superseded) &&
                allowedValidatorRoles.Contains(policy.ValidatorRole) &&
                dbContext.AssignmentVersions.AsNoTracking().Any(assignment =>
                    assignment.ObligationId == row.ObligationId &&
                    assignment.Status == AssignmentVersionStatuses.Current &&
                    dbContext.AppUsers.AsNoTracking().Any(user =>
                        user.PersonId == assignment.PersonId &&
                        user.Status == BootstrapContract.ActiveAccountStatus &&
                        dbContext.EmploymentVersions.AsNoTracking().Any(employment =>
                            employment.PersonId == user.PersonId &&
                            employment.BranchId == BranchScope.LorettaId &&
                            employment.Status == EmploymentStatus.Active &&
                            employment.ValidFrom <= queriedAt &&
                            (employment.ValidTo == null || queriedAt < employment.ValidTo)) &&
                        dbContext.EmploymentVersions.AsNoTracking().Count(employment =>
                            employment.PersonId == user.PersonId &&
                            employment.BranchId == BranchScope.LorettaId &&
                            employment.Status == EmploymentStatus.Active &&
                            employment.ValidFrom <= queriedAt &&
                            (employment.ValidTo == null || queriedAt < employment.ValidTo)) == 1 &&
                        dbContext.RoleAssignmentVersions.AsNoTracking().Any(role =>
                            role.UserId == user.Id &&
                            role.BranchId == BranchScope.LorettaId &&
                            role.Status == RoleAssignmentStatus.Active &&
                            role.ValidFrom <= queriedAt &&
                            (role.ValidTo == null || queriedAt < role.ValidTo) &&
                            role.RoleCode == policy.ExecutorRole) &&
                        dbContext.RoleAssignmentVersions.AsNoTracking().Count(role =>
                            role.UserId == user.Id &&
                            role.BranchId == BranchScope.LorettaId &&
                            role.Status == RoleAssignmentStatus.Active &&
                            role.ValidFrom <= queriedAt &&
                            (role.ValidTo == null || queriedAt < role.ValidTo)) == 1))) &&
            !dbContext.ValidationRequirements.AsNoTracking().Any(requirement =>
                requirement.ObligationId == row.ObligationId &&
                dbContext.ValidationDecisionVersions.AsNoTracking().Any(decision =>
                    decision.RequirementId == requirement.Id &&
                    decision.Status == ValidationStatuses.Current)));
        query = ApplyPendingCursor(query, cursor);

        var rows = await query.OrderBy(row => row.ConcludedAt).ThenBy(row => row.ObligationId)
            .Take(request.Limit + 1).ToListAsync(cancellationToken);
        var hasNextPage = rows.Count > request.Limit;
        if (hasNextPage) rows.RemoveAt(rows.Count - 1);

        var items = await MapPendingItemsAsync(rows, actor, queriedAt, cancellationToken);
        var nextCursor = hasNextPage ? EncodePendingCursor(rows[^1], filterHash) : null;
        await transaction.CommitAsync(cancellationToken);
        return new(items, nextCursor, queriedAt);
    }

    public async Task<IndicatorResult> ReadAsync(
        IndicatorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIndicatorRequest(request);
        var range = WeekContract.Calculate(request.IsoYear, request.IsoWeek);
        var filterHash = IndicatorFilterHash(request);
        var cursor = DecodeIndicatorCursor(request.Cursor, filterHash);

        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        var actor = await GetIndicatorActorAsync(request.ActorUserId, queriedAt, cancellationToken);
        var visiblePeople = await BuildIndicatorVisiblePeopleAsync(
            actor, queriedAt, request.Level, request.ResponsiblePersonId, false, cancellationToken);
        var requestedPersonVisible = request.ResponsiblePersonId is { } requestedPersonId &&
            visiblePeople.Any(person => person.Id == requestedPersonId);
        var visiblePersonIds = visiblePeople.Select(person => person.Id).ToArray();

        var obligations =
            from obligation in dbContext.WorkObligations.AsNoTracking()
            where obligation.BranchId == BranchScope.LorettaId &&
                dbContext.WeekPeriods.AsNoTracking().Any(period =>
                    period.Id == obligation.PeriodId &&
                    period.BranchId == BranchScope.LorettaId &&
                    period.IsoYear == request.IsoYear &&
                    period.IsoWeek == request.IsoWeek) &&
                dbContext.AssignmentVersions.AsNoTracking().Any(assignment =>
                    assignment.ObligationId == obligation.Id &&
                    assignment.Status == AssignmentVersionStatuses.Current &&
                    assignment.AssignedAt <= queriedAt &&
                    visiblePersonIds.Contains(assignment.PersonId))
            select new IndicatorObligationRow
            {
                ObligationId = obligation.Id,
                ExecutionStatus = obligation.ExecutionStatus,
            };

        await EnsureIndicatorConsistencyAsync(obligations, queriedAt, cancellationToken);

        var baseCount = await obligations.Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);
        var pendingCount = await obligations.Where(row => row.ExecutionStatus == WorkObligationStatuses.Pending)
            .Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);
        var concludedCount = await obligations.Where(row => row.ExecutionStatus == WorkObligationStatuses.Concluded)
            .Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);
        var validatedCount = await obligations.Where(row =>
                dbContext.ValidationRequirements.AsNoTracking().Any(requirement =>
                    requirement.ObligationId == row.ObligationId &&
                    dbContext.ValidationDecisionVersions.AsNoTracking().Any(decision =>
                        decision.RequirementId == requirement.Id &&
                        decision.Status == ValidationStatuses.Current)))
            .Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);
        var nonCompliantCount = await obligations.Where(row =>
                dbContext.ValidationRequirements.AsNoTracking().Any(requirement =>
                    requirement.ObligationId == row.ObligationId &&
                    dbContext.ValidationDecisionVersions.AsNoTracking().Any(decision =>
                        decision.RequirementId == requirement.Id &&
                        decision.Status == ValidationStatuses.Current &&
                        decision.Result == ValidationResults.NotFulfilled)))
            .Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);

        if (pendingCount + concludedCount != baseCount || nonCompliantCount > validatedCount)
            throw new IndicatorQueryInconsistentException();

        var pageQuery = ApplyIndicatorCursor(visiblePeople, cursor)
            .OrderBy(person => person.StableCode, StringComparer.Ordinal)
            .ThenBy(person => person.Id);
        var people = pageQuery.Take(request.Limit + 1).ToList();
        var hasNextPage = people.Count > request.Limit;
        if (hasNextPage) people.RemoveAt(people.Count - 1);

        var pagePersonIds = people.Select(person => person.Id).ToArray();
        var loadCounts = await (
                from assignment in dbContext.AssignmentVersions.AsNoTracking()
                join obligation in dbContext.WorkObligations.AsNoTracking()
                    on assignment.ObligationId equals obligation.Id
                join period in dbContext.WeekPeriods.AsNoTracking()
                    on obligation.PeriodId equals period.Id
                where pagePersonIds.Contains(assignment.PersonId) &&
                    assignment.Status == AssignmentVersionStatuses.Current &&
                    assignment.AssignedAt <= queriedAt &&
                    obligation.BranchId == BranchScope.LorettaId &&
                    obligation.ExecutionStatus == WorkObligationStatuses.Pending &&
                    period.BranchId == BranchScope.LorettaId &&
                    period.IsoYear == request.IsoYear &&
                    period.IsoWeek == request.IsoWeek
                select new { assignment.PersonId, obligation.Id })
            .Distinct()
            .GroupBy(row => row.PersonId)
            .Select(group => new { PersonId = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(row => row.PersonId, row => row.Count, cancellationToken);

        var loadItems = people.Select(person => new ActiveLoadByPersonItem(
            new IndicatorPerson(person.Id, person.StableCode, person.DisplayName),
            person.RoleCode,
            loadCounts.GetValueOrDefault(person.Id))).ToArray();
        var nextCursor = hasNextPage ? EncodeIndicatorCursor(people[^1], filterHash) : null;
        var includedLevels = IndicatorIncludedLevels(actor.RoleCode, request.Level);
        var scope = new IndicatorScope(
            BranchScope.LorettaCode,
            actor.RoleCode,
            includedLevels,
            request.Level,
            requestedPersonVisible ? request.ResponsiblePersonId : null);
        var snapshot = new IndicatorSnapshot(
            new IndicatorPeriod(
                request.IsoYear,
                request.IsoWeek,
                range.StartsOn,
                range.EndsOn,
                ActivationPolicyCatalog.LorettaTimeZone),
            scope,
            baseCount,
            new IndicatorCount(pendingCount, baseCount),
            new IndicatorCount(concludedCount, baseCount),
            new IndicatorCount(validatedCount, baseCount),
            new IndicatorCount(nonCompliantCount, baseCount),
            new ActiveLoadByPerson(pendingCount, loadItems));

        await transaction.CommitAsync(cancellationToken);
        return new(snapshot, nextCursor, queriedAt);
    }

    public async Task<IndicatorResult> ReadDirectionOverviewAsync(
        IndicatorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateDirectionOverviewRequest(request);
        var range = WeekContract.Calculate(request.IsoYear, request.IsoWeek);
        var filterHash = DirectionOverviewFilterHash(request);
        var cursor = DecodeDirectionOverviewCursor(request.Cursor, filterHash);

        await using var transaction = await BeginReadOnlyAsync(cancellationToken);
        var queriedAt = clock.UtcNow;
        var actor = await GetDirectionOverviewActorAsync(request.ActorUserId, queriedAt, cancellationToken);
        var allPeople = await BuildIndicatorVisiblePeopleAsync(
            actor, queriedAt, null, null, true, cancellationToken);
        var allPersonIds = allPeople.Select(person => person.Id).ToArray();
        var visiblePeople = allPeople
            .Where(person => request.Level is null || person.RoleCode == request.Level)
            .Where(person => request.ResponsiblePersonId is null || person.Id == request.ResponsiblePersonId)
            .ToArray();
        var visiblePersonIds = visiblePeople.Select(person => person.Id).ToArray();
        var requestedPersonVisible = request.ResponsiblePersonId is { } requestedPersonId &&
            visiblePersonIds.Contains(requestedPersonId);

        var obligations =
            from obligation in dbContext.WorkObligations.AsNoTracking()
            where obligation.BranchId == BranchScope.LorettaId &&
                dbContext.WeekPeriods.AsNoTracking().Any(period =>
                    period.Id == obligation.PeriodId &&
                    period.BranchId == BranchScope.LorettaId &&
                    period.IsoYear == request.IsoYear &&
                    period.IsoWeek == request.IsoWeek)
            select new IndicatorObligationRow
            {
                ObligationId = obligation.Id,
                ExecutionStatus = obligation.ExecutionStatus,
            };

        try
        {
            await EnsureIndicatorConsistencyAsync(
                obligations, queriedAt, cancellationToken, true, allPersonIds);
        }
        catch (IndicatorQueryInconsistentException)
        {
            throw new DirectionOverviewQueryInconsistentException();
        }

        if (request.Level is not null || request.ResponsiblePersonId is not null)
        {
            obligations = obligations.Where(row =>
                dbContext.AssignmentVersions.AsNoTracking().Any(assignment =>
                    assignment.ObligationId == row.ObligationId &&
                    assignment.Status == AssignmentVersionStatuses.Current &&
                    assignment.AssignedAt <= queriedAt &&
                    visiblePersonIds.Contains(assignment.PersonId)));
        }

        var baseCount = await obligations.Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);
        var pendingCount = await obligations.Where(row => row.ExecutionStatus == WorkObligationStatuses.Pending)
            .Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);
        var concludedCount = await obligations.Where(row => row.ExecutionStatus == WorkObligationStatuses.Concluded)
            .Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);
        var validatedCount = await obligations.Where(row =>
                dbContext.ValidationRequirements.AsNoTracking().Any(requirement =>
                    requirement.ObligationId == row.ObligationId &&
                    dbContext.ValidationDecisionVersions.AsNoTracking().Any(decision =>
                        decision.RequirementId == requirement.Id &&
                        decision.Status == ValidationStatuses.Current)))
            .Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);
        var nonCompliantCount = await obligations.Where(row =>
                dbContext.ValidationRequirements.AsNoTracking().Any(requirement =>
                    requirement.ObligationId == row.ObligationId &&
                    dbContext.ValidationDecisionVersions.AsNoTracking().Any(decision =>
                        decision.RequirementId == requirement.Id &&
                        decision.Status == ValidationStatuses.Current &&
                        decision.Result == ValidationResults.NotFulfilled)))
            .Select(row => row.ObligationId).Distinct().LongCountAsync(cancellationToken);

        if (pendingCount + concludedCount != baseCount || nonCompliantCount > validatedCount)
            throw new DirectionOverviewQueryInconsistentException();

        var pageQuery = ApplyIndicatorCursor(visiblePeople, cursor)
            .OrderBy(person => person.StableCode, StringComparer.Ordinal)
            .ThenBy(person => person.Id);
        var people = pageQuery.Take(request.Limit + 1).ToList();
        var hasNextPage = people.Count > request.Limit;
        if (hasNextPage) people.RemoveAt(people.Count - 1);

        var pagePersonIds = people.Select(person => person.Id).ToArray();
        var filteredObligationIds = obligations.Select(row => row.ObligationId);
        var loadCounts = await (
                from assignment in dbContext.AssignmentVersions.AsNoTracking()
                join obligation in dbContext.WorkObligations.AsNoTracking()
                    on assignment.ObligationId equals obligation.Id
                where pagePersonIds.Contains(assignment.PersonId) &&
                    filteredObligationIds.Contains(obligation.Id) &&
                    assignment.Status == AssignmentVersionStatuses.Current &&
                    assignment.AssignedAt <= queriedAt &&
                    obligation.ExecutionStatus == WorkObligationStatuses.Pending
                select new { assignment.PersonId, obligation.Id })
            .Distinct()
            .GroupBy(row => row.PersonId)
            .Select(group => new { PersonId = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(row => row.PersonId, row => row.Count, cancellationToken);

        var loadItems = people.Select(person => new ActiveLoadByPersonItem(
            new IndicatorPerson(person.Id, person.StableCode, person.DisplayName),
            person.RoleCode,
            loadCounts.GetValueOrDefault(person.Id))).ToArray();
        var nextCursor = hasNextPage ? EncodeIndicatorCursor(people[^1], filterHash) : null;
        var scope = new IndicatorScope(
            BranchScope.LorettaCode,
            actor.RoleCode,
            IndicatorIncludedLevels(actor.RoleCode, request.Level),
            request.Level,
            requestedPersonVisible ? request.ResponsiblePersonId : null);
        var snapshot = new IndicatorSnapshot(
            new IndicatorPeriod(
                request.IsoYear,
                request.IsoWeek,
                range.StartsOn,
                range.EndsOn,
                ActivationPolicyCatalog.LorettaTimeZone),
            scope,
            baseCount,
            new IndicatorCount(pendingCount, baseCount),
            new IndicatorCount(concludedCount, baseCount),
            new IndicatorCount(validatedCount, baseCount),
            new IndicatorCount(nonCompliantCount, baseCount),
            new ActiveLoadByPerson(pendingCount, loadItems));

        await transaction.CommitAsync(cancellationToken);
        return new(snapshot, nextCursor, queriedAt);
    }

    private async Task<ActorAccess> GetIndicatorActorAsync(
        Guid actorUserId,
        DateTimeOffset queriedAt,
        CancellationToken cancellationToken)
    {
        ActorAccess actor;
        try
        {
            actor = await GetActorAsync(actorUserId, queriedAt, cancellationToken);
        }
        catch (ObligationQueryAccessDeniedException)
        {
            throw new IndicatorAccessDeniedException();
        }

        if (!RoleHierarchy.GrantsIndicatorView(actor.RoleCode))
            throw new IndicatorAccessDeniedException();
        return actor;
    }

    private async Task<ActorAccess> GetDirectionOverviewActorAsync(
        Guid actorUserId,
        DateTimeOffset queriedAt,
        CancellationToken cancellationToken)
    {
        ActorAccess actor;
        try
        {
            actor = await GetActorAsync(actorUserId, queriedAt, cancellationToken);
        }
        catch (ObligationQueryAccessDeniedException)
        {
            throw new DirectionOverviewAccessDeniedException();
        }

        if (!RoleHierarchy.GrantsDirectionOverviewView(actor.RoleCode))
            throw new DirectionOverviewAccessDeniedException();
        return actor;
    }

    private async Task<IReadOnlyList<IndicatorPersonRow>> BuildIndicatorVisiblePeopleAsync(
        ActorAccess actor,
        DateTimeOffset queriedAt,
        string? requestedLevel,
        Guid? responsiblePersonId,
        bool includeAllDirectionPeople,
        CancellationToken cancellationToken)
    {
        var lowerRoles = LowerRoleCodes(actor.RoleCode);
        var candidates = await (
            from person in dbContext.People.AsNoTracking()
            join user in dbContext.AppUsers.AsNoTracking() on person.Id equals user.PersonId
            join employment in dbContext.EmploymentVersions.AsNoTracking() on person.Id equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= queriedAt &&
                (employment.ValidTo == null || queriedAt < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= queriedAt &&
                (role.ValidTo == null || queriedAt < role.ValidTo) &&
                (includeAllDirectionPeople && actor.RoleCode == CanonicalRole.Direction ||
                 person.Id == actor.PersonId || lowerRoles.Contains(role.RoleCode)) &&
                (requestedLevel == null || role.RoleCode == requestedLevel) &&
                (responsiblePersonId == null || person.Id == responsiblePersonId)
            select new IndicatorPersonCandidateRow
            {
                Id = person.Id,
                StableCode = person.StableCode,
                DisplayName = person.DisplayName,
                UserId = user.Id,
                RoleCode = role.RoleCode,
            }).ToListAsync(cancellationToken);

        var ambiguousAccountPeople = await dbContext.AppUsers.AsNoTracking()
            .Where(user => user.Status == BootstrapContract.ActiveAccountStatus)
            .GroupBy(user => user.PersonId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);
        var ambiguousEmploymentPeople = await dbContext.EmploymentVersions.AsNoTracking()
            .Where(employment => employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= queriedAt &&
                (employment.ValidTo == null || queriedAt < employment.ValidTo))
            .GroupBy(employment => employment.PersonId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);
        var ambiguousRoleUsers = await dbContext.RoleAssignmentVersions.AsNoTracking()
            .Where(role => role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= queriedAt &&
                (role.ValidTo == null || queriedAt < role.ValidTo))
            .GroupBy(role => role.UserId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);
        var ambiguousAccounts = ambiguousAccountPeople.ToHashSet();
        var ambiguousEmployments = ambiguousEmploymentPeople.ToHashSet();
        var ambiguousRoles = ambiguousRoleUsers.ToHashSet();

        return candidates
            .Where(candidate => !ambiguousAccounts.Contains(candidate.Id) &&
                !ambiguousEmployments.Contains(candidate.Id) &&
                !ambiguousRoles.Contains(candidate.UserId))
            .Select(candidate => new IndicatorPersonRow
            {
                Id = candidate.Id,
                StableCode = candidate.StableCode,
                DisplayName = candidate.DisplayName,
                RoleCode = candidate.RoleCode,
            })
            .ToArray();
    }

    private async Task EnsureIndicatorConsistencyAsync(
        IQueryable<IndicatorObligationRow> obligations,
        DateTimeOffset queriedAt,
        CancellationToken cancellationToken,
        bool allowUnassigned = false,
        Guid[]? validPersonIds = null)
    {
        var obligationIds = obligations.Select(row => row.ObligationId);
        var invalidAssignment = allowUnassigned
            ? await obligations.AnyAsync(row =>
                dbContext.AssignmentVersions.AsNoTracking().Count(assignment =>
                    assignment.ObligationId == row.ObligationId &&
                    assignment.Status == AssignmentVersionStatuses.Current &&
                    assignment.AssignedAt <= queriedAt) > 1, cancellationToken)
            : await obligations.AnyAsync(row =>
                dbContext.AssignmentVersions.AsNoTracking().Count(assignment =>
                    assignment.ObligationId == row.ObligationId &&
                    assignment.Status == AssignmentVersionStatuses.Current &&
                    assignment.AssignedAt <= queriedAt) != 1, cancellationToken);
        var invalidResponsible = validPersonIds is not null && await obligations.AnyAsync(row =>
            dbContext.AssignmentVersions.AsNoTracking().Any(assignment =>
                assignment.ObligationId == row.ObligationId &&
                assignment.Status == AssignmentVersionStatuses.Current &&
                assignment.AssignedAt <= queriedAt &&
                !validPersonIds.Contains(assignment.PersonId)), cancellationToken);
        var duplicateRequirement = await dbContext.ValidationRequirements.AsNoTracking()
            .Where(requirement => obligationIds.Contains(requirement.ObligationId))
            .GroupBy(requirement => requirement.ObligationId)
            .AnyAsync(group => group.Count() > 1, cancellationToken);
        var invalidValidation = await (
                from requirement in dbContext.ValidationRequirements.AsNoTracking()
                join obligation in dbContext.WorkObligations.AsNoTracking()
                    on requirement.ObligationId equals obligation.Id
                where obligationIds.Contains(obligation.Id)
                select new { requirement, obligation })
            .AnyAsync(row =>
                row.obligation.ValidationPolicyVersionId == null ||
                row.requirement.PolicyVersionId != row.obligation.ValidationPolicyVersionId ||
                row.requirement.Status != ValidationStatuses.Pending &&
                row.requirement.Status != ValidationStatuses.Resolved ||
                row.requirement.Status == ValidationStatuses.Pending &&
                dbContext.ValidationDecisionVersions.AsNoTracking().Any(decision =>
                    decision.RequirementId == row.requirement.Id) ||
                row.requirement.Status == ValidationStatuses.Resolved &&
                dbContext.ValidationDecisionVersions.AsNoTracking().Count(decision =>
                    decision.RequirementId == row.requirement.Id &&
                    decision.Status == ValidationStatuses.Current) != 1 ||
                dbContext.ValidationDecisionVersions.AsNoTracking().Any(decision =>
                    decision.RequirementId == row.requirement.Id &&
                    decision.Status != ValidationStatuses.Current &&
                    decision.Status != ValidationStatuses.Superseded ||
                    decision.Status == ValidationStatuses.Current &&
                    (decision.Result != ValidationResults.Fulfilled &&
                     decision.Result != ValidationResults.Incomplete &&
                     decision.Result != ValidationResults.NotFulfilled)) ||
                row.obligation.ExecutionStatus != WorkObligationStatuses.Concluded &&
                dbContext.ValidationDecisionVersions.AsNoTracking().Any(decision =>
                    decision.RequirementId == row.requirement.Id &&
                    decision.Status == ValidationStatuses.Current),
                cancellationToken);

        if (invalidAssignment || invalidResponsible || duplicateRequirement || invalidValidation)
            throw new IndicatorQueryInconsistentException();
    }

    private async Task<ActorAccess> GetSupervisionActorAsync(Guid actorUserId, DateTimeOffset queriedAt,
        CancellationToken cancellationToken)
    {
        ActorAccess actor;
        try
        {
            actor = await GetActorAsync(actorUserId, queriedAt, cancellationToken);
        }
        catch (ObligationQueryAccessDeniedException)
        {
            throw new SupervisionAccessDeniedException();
        }
        if (!RoleHierarchy.GrantsSupervisionView(actor.RoleCode))
        {
            throw new SupervisionAccessDeniedException();
        }

        return actor;
    }

    private IQueryable<ObligationRow> BuildSupervisedRows(ActorAccess actor, DateTimeOffset queriedAt,
        string? requestedLevel, Guid? responsiblePersonId)
    {
        var obligationIds = BuildCurrentResponsibleObligationIds(
            queriedAt, actor.RoleCode, requestedLevel, responsiblePersonId);
        return BuildVisibleRows(actor, queriedAt).Where(row => obligationIds.Contains(row.ObligationId));
    }

    private IQueryable<Guid> BuildCurrentResponsibleObligationIds(DateTimeOffset queriedAt,
        string actorRoleCode, string? requestedLevel, Guid? responsiblePersonId)
    {
        var includeAdministration = actorRoleCode == CanonicalRole.Direction &&
            (requestedLevel is null || requestedLevel == CanonicalRole.Administration);
        var includeSubcoordination = actorRoleCode is CanonicalRole.Direction or CanonicalRole.Administration &&
            (requestedLevel is null || requestedLevel == CanonicalRole.Subcoordination);
        var includeSalesFloor = actorRoleCode is CanonicalRole.Direction or CanonicalRole.Administration or CanonicalRole.Subcoordination &&
            (requestedLevel is null || requestedLevel == CanonicalRole.SalesFloor);

        return
            from assignment in dbContext.AssignmentVersions.AsNoTracking()
            join user in dbContext.AppUsers.AsNoTracking() on assignment.PersonId equals user.PersonId
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where assignment.Status == AssignmentVersionStatuses.Current &&
                user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= queriedAt &&
                (employment.ValidTo == null || queriedAt < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= queriedAt &&
                (role.ValidTo == null || queriedAt < role.ValidTo) &&
                (responsiblePersonId == null || assignment.PersonId == responsiblePersonId) &&
                ((includeAdministration && role.RoleCode == CanonicalRole.Administration) ||
                 (includeSubcoordination && role.RoleCode == CanonicalRole.Subcoordination) ||
                 (includeSalesFloor && role.RoleCode == CanonicalRole.SalesFloor)) &&
                dbContext.EmploymentVersions.AsNoTracking().Count(candidate =>
                    candidate.PersonId == user.PersonId &&
                    candidate.BranchId == BranchScope.LorettaId &&
                    candidate.Status == EmploymentStatus.Active &&
                    candidate.ValidFrom <= queriedAt &&
                    (candidate.ValidTo == null || queriedAt < candidate.ValidTo)) == 1 &&
                dbContext.RoleAssignmentVersions.AsNoTracking().Count(candidate =>
                    candidate.UserId == user.Id &&
                    candidate.BranchId == BranchScope.LorettaId &&
                    candidate.Status == RoleAssignmentStatus.Active &&
                    candidate.ValidFrom <= queriedAt &&
                    (candidate.ValidTo == null || queriedAt < candidate.ValidTo)) == 1
            select assignment.ObligationId;
    }

    private static IQueryable<ObligationRow> ApplySupervisionFilters(IQueryable<ObligationRow> query,
        int? isoYear, int? isoWeek, string? executionStatus)
    {
        if (isoYear is { } year) query = query.Where(row => row.IsoYear == year);
        if (isoWeek is { } week) query = query.Where(row => row.IsoWeek == week);
        if (executionStatus is not null) query = query.Where(row => row.ExecutionStatus == executionStatus);
        return query;
    }

    private async Task<IReadOnlyList<SupervisionObligationItem>> MapSupervisionItemsAsync(
        IReadOnlyList<ObligationRow> rows, ActorAccess actor, DateTimeOffset queriedAt,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];
        var baseItems = await MapItemsAsync(rows, actor, queriedAt, cancellationToken);
        var ids = rows.Select(row => row.ObligationId).ToArray();
        var roles = await ReadResponsibleRolesAsync(ids, queriedAt, cancellationToken);
        var evidence = await ReadCurrentEvidenceAsync(ids, cancellationToken);
        var validations = await ReadCurrentValidationsAsync(ids, cancellationToken);

        return baseItems.Select(item => new SupervisionObligationItem(
            item,
            roles.GetValueOrDefault(item.ObligationId) ?? throw new SupervisionQueryInconsistentException(),
            evidence.GetValueOrDefault(item.ObligationId) ?? [],
            validations.GetValueOrDefault(item.ObligationId) ?? new(null, null),
            SupervisionLinks(item.ObligationId, includeIssue: false))).ToArray();
    }

    private async Task<IReadOnlyList<PendingValidationItem>> MapPendingItemsAsync(
        IReadOnlyList<ObligationRow> rows, ActorAccess actor, DateTimeOffset queriedAt,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];
        var baseItems = await MapItemsAsync(rows, actor, queriedAt, cancellationToken);
        var ids = rows.Select(row => row.ObligationId).ToArray();
        var roles = await ReadResponsibleRolesAsync(ids, queriedAt, cancellationToken);
        var requirements = await dbContext.ValidationRequirements.AsNoTracking()
            .Where(item => ids.Contains(item.ObligationId)).ToListAsync(cancellationToken);
        var requirementIds = requirements.Select(item => item.Id).ToArray();
        var decisionCounts = await dbContext.ValidationDecisionVersions.AsNoTracking()
            .Where(item => requirementIds.Contains(item.RequirementId))
            .GroupBy(item => item.RequirementId)
            .Select(group => new { RequirementId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.RequirementId, item => item.Count, cancellationToken);
        var policyIds = rows.Select(row => row.ValidationPolicyVersionId!.Value).Distinct().ToArray();
        var policies = await dbContext.ValidationPolicyVersions.AsNoTracking()
            .Where(item => policyIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var requirementsByObligation = requirements.ToDictionary(item => item.ObligationId);
        var rowsById = rows.ToDictionary(row => row.ObligationId);

        return baseItems.Select(item =>
        {
            var row = rowsById[item.ObligationId];
            if (row.ConcludedAt is null || row.ValidationPolicyVersionId is not { } policyId ||
                !policies.TryGetValue(policyId, out var policy))
                throw new PendingValidationQueryInconsistentException();
            requirementsByObligation.TryGetValue(item.ObligationId, out var requirement);
            if (requirement is not null && (requirement.Status != ValidationStatuses.Pending ||
                decisionCounts.GetValueOrDefault(requirement.Id) != 0))
                throw new PendingValidationQueryInconsistentException();

            var ordinary = actor.RoleCode == policy.ValidatorRole;
            var authority = new AvailableValidationAuthority(
                ordinary ? ValidationAuthorityTypes.Ordinary : ValidationAuthorityTypes.Escalation,
                policy.ValidatorRole,
                !ordinary);
            var details = requirement is null ? null : RequirementDetails(requirement);
            var rowVersion = requirement?.RowVersion ?? row.RowVersion;
            return new PendingValidationItem(
                item,
                roles.GetValueOrDefault(item.ObligationId) ?? throw new PendingValidationQueryInconsistentException(),
                row.ConcludedAt.Value,
                requirement is null ? PendingMaterializationStatuses.Derived : PendingMaterializationStatuses.Materialized,
                details,
                authority,
                $"\"{rowVersion.ToString(CultureInfo.InvariantCulture)}\"",
                SupervisionLinks(item.ObligationId, includeIssue: true));
        }).ToArray();
    }

    private async Task<Dictionary<Guid, string>> ReadResponsibleRolesAsync(Guid[] obligationIds,
        DateTimeOffset queriedAt, CancellationToken cancellationToken)
    {
        var rows = await (
            from assignment in dbContext.AssignmentVersions.AsNoTracking()
            join user in dbContext.AppUsers.AsNoTracking() on assignment.PersonId equals user.PersonId
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where obligationIds.Contains(assignment.ObligationId) &&
                assignment.Status == AssignmentVersionStatuses.Current &&
                user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= queriedAt &&
                (employment.ValidTo == null || queriedAt < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= queriedAt &&
                (role.ValidTo == null || queriedAt < role.ValidTo) &&
                dbContext.EmploymentVersions.AsNoTracking().Count(candidate =>
                    candidate.PersonId == user.PersonId &&
                    candidate.BranchId == BranchScope.LorettaId &&
                    candidate.Status == EmploymentStatus.Active &&
                    candidate.ValidFrom <= queriedAt &&
                    (candidate.ValidTo == null || queriedAt < candidate.ValidTo)) == 1 &&
                dbContext.RoleAssignmentVersions.AsNoTracking().Count(candidate =>
                    candidate.UserId == user.Id &&
                    candidate.BranchId == BranchScope.LorettaId &&
                    candidate.Status == RoleAssignmentStatus.Active &&
                    candidate.ValidFrom <= queriedAt &&
                    (candidate.ValidTo == null || queriedAt < candidate.ValidTo)) == 1
            select new ResponsibleAccessRow(assignment.ObligationId, assignment.PersonId, role.RoleCode))
            .ToListAsync(cancellationToken);
        if (rows.GroupBy(item => item.ObligationId).Any(group => group.Count() != 1))
            throw new SupervisionQueryInconsistentException();
        return rows.ToDictionary(item => item.ObligationId, item => item.RoleCode);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<CurrentEvidenceSummary>>> ReadCurrentEvidenceAsync(
        Guid[] obligationIds, CancellationToken cancellationToken)
    {
        var rows = await (
            from item in dbContext.EvidenceItems.AsNoTracking()
            join version in dbContext.EvidenceVersions.AsNoTracking() on item.Id equals version.EvidenceItemId
            where obligationIds.Contains(item.ObligationId) && version.Status == EvidenceVersionStatuses.Current
            orderby item.RequirementCode, version.Id
            select new EvidenceSummaryRow(item.ObligationId, item.Id, item.RowVersion,
                item.RequirementVersionId, item.RequirementCode, item.RequirementKind,
                version.Id, version.VersionNo, version.Status, version.SubmittedBy, version.SubmittedAt,
                version.Reason, version.SupersedesId,
                version.FileObjectId != null ? EvidenceSourceKinds.File : EvidenceSourceKinds.Structured))
            .ToListAsync(cancellationToken);

        return rows.GroupBy(item => item.ObligationId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<CurrentEvidenceSummary>)group.Select(item => new CurrentEvidenceSummary(
                item.EvidenceItemId,
                item.ItemRowVersion,
                new(item.RequirementVersionId, item.RequirementCode, item.RequirementKind),
                new(item.EvidenceVersionId, item.VersionNo, item.Status, item.SubmittedByUserId,
                    item.SubmittedAt, item.Reason, item.SupersedesEvidenceVersionId),
                item.SourceKind)).ToArray());
    }

    private async Task<Dictionary<Guid, SupervisionValidation>> ReadCurrentValidationsAsync(Guid[] obligationIds,
        CancellationToken cancellationToken)
    {
        var requirements = await dbContext.ValidationRequirements.AsNoTracking()
            .Where(item => obligationIds.Contains(item.ObligationId)).ToListAsync(cancellationToken);
        var requirementIds = requirements.Select(item => item.Id).ToArray();
        var decisions = await dbContext.ValidationDecisionVersions.AsNoTracking()
            .Where(item => requirementIds.Contains(item.RequirementId)).ToListAsync(cancellationToken);
        var current = decisions.Where(item => item.Status == ValidationStatuses.Current).ToArray();
        var snapshotIds = current.Select(item => item.EvidenceReviewSnapshotId).Distinct().ToArray();
        var snapshots = await dbContext.EvidenceReviewSnapshots.AsNoTracking()
            .Where(item => snapshotIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var decisionsByRequirement = decisions.GroupBy(item => item.RequirementId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var result = new Dictionary<Guid, SupervisionValidation>();

        foreach (var requirement in requirements)
        {
            var chain = decisionsByRequirement.GetValueOrDefault(requirement.Id) ?? [];
            var currentDecisions = chain.Where(item => item.Status == ValidationStatuses.Current).ToArray();
            if ((requirement.Status == ValidationStatuses.Pending && chain.Length != 0) ||
                (requirement.Status == ValidationStatuses.Resolved && currentDecisions.Length != 1) ||
                currentDecisions.Length > 1)
                throw new SupervisionQueryInconsistentException();

            ValidationDecisionDetails? decision = null;
            if (currentDecisions.SingleOrDefault() is { } value)
            {
                if (!snapshots.TryGetValue(value.EvidenceReviewSnapshotId, out var snapshot))
                    throw new SupervisionQueryInconsistentException();
                decision = new(value.Id, value.VersionNo, value.Result, value.Foundation, value.Status,
                    value.AuthorityType, value.ValidatorUserId, value.ValidatorRole, value.DecidedAt,
                    value.Reason, value.SupersedesId, value.EvidenceReviewSnapshotId, snapshot.EvidenceVersionIds);
            }

            result.Add(requirement.ObligationId, new(RequirementDetails(requirement), decision));
        }

        return result;
    }

    private static ValidationRequirementDetails RequirementDetails(ValidationRequirement requirement) =>
        new(requirement.Id, requirement.PolicyVersionId, requirement.Status, requirement.CreatedAt,
            requirement.ResolvedAt, requirement.RowVersion);

    private static Dictionary<string, string> SupervisionLinks(Guid obligationId, bool includeIssue)
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = $"/api/v1/obligations/{obligationId:D}",
            ["evidence"] = $"/api/v1/obligations/{obligationId:D}/evidence",
            ["evidenceReview"] = $"/api/v1/obligations/{obligationId:D}/evidence-review",
            ["validations"] = $"/api/v1/obligations/{obligationId:D}/validations",
        };
        if (includeIssue) links["issueDecision"] = $"/api/v1/obligations/{obligationId:D}/validation-decisions";
        return links;
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
                ValidationPolicyVersionId = obligation.ValidationPolicyVersionId,
                RowVersion = obligation.RowVersion,
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

    private static void ValidateSupervisionRequest(SupervisionRequest request)
    {
        if (request.ActorUserId == Guid.Empty || request.Limit is < 1 or > 100 ||
            request.ResponsiblePersonId == Guid.Empty ||
            (request.Level is not null && !CanonicalRole.IsDefined(request.Level)) ||
            (request.ExecutionStatus is not null &&
             request.ExecutionStatus is not (WorkObligationStatuses.Pending or WorkObligationStatuses.Concluded)) ||
            !ValidIsoWeek(request.IsoYear, request.IsoWeek))
            throw new SupervisionFilterInvalidException();
    }

    private static void ValidatePendingRequest(PendingValidationsRequest request)
    {
        if (request.ActorUserId == Guid.Empty || request.Limit is < 1 or > 100 ||
            request.ResponsiblePersonId == Guid.Empty ||
            (request.Level is not null && !CanonicalRole.IsDefined(request.Level)) ||
            !ValidIsoWeek(request.IsoYear, request.IsoWeek))
            throw new PendingValidationsFilterInvalidException();
    }

    private static void ValidateIndicatorRequest(IndicatorRequest request)
    {
        if (request.ActorUserId == Guid.Empty || request.Limit is < 1 or > 100 ||
            request.ResponsiblePersonId == Guid.Empty ||
            request.IsoYear is < 1 or > 9999 || request.IsoWeek is < 1 ||
            request.IsoWeek > ISOWeek.GetWeeksInYear(request.IsoYear) ||
            request.Level is not null && !CanonicalRole.IsDefined(request.Level))
        {
            throw new IndicatorFilterInvalidException();
        }
    }

    private static void ValidateDirectionOverviewRequest(IndicatorRequest request)
    {
        if (request.ActorUserId == Guid.Empty || request.Limit is < 1 or > 100 ||
            request.ResponsiblePersonId == Guid.Empty ||
            request.IsoYear is < 1 or > 9999 || request.IsoWeek is < 1 ||
            request.IsoWeek > ISOWeek.GetWeeksInYear(request.IsoYear) ||
            request.Level is not null && !CanonicalRole.IsDefined(request.Level))
        {
            throw new DirectionOverviewFilterInvalidException();
        }
    }

    private static string[] LowerRoleCodes(string actorRoleCode) => actorRoleCode switch
    {
        CanonicalRole.Direction =>
            [CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
        CanonicalRole.Administration => [CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
        CanonicalRole.Subcoordination => [CanonicalRole.SalesFloor],
        CanonicalRole.SalesFloor => [],
        _ => throw new IndicatorAccessDeniedException(),
    };

    private static string[] IndicatorIncludedLevels(string actorRoleCode, string? requestedLevel)
    {
        var levels = actorRoleCode switch
        {
            CanonicalRole.Direction =>
                new[] { CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor },
            CanonicalRole.Administration =>
                [CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
            CanonicalRole.Subcoordination => [CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
            CanonicalRole.SalesFloor => [CanonicalRole.SalesFloor],
            _ => throw new IndicatorAccessDeniedException(),
        };
        return requestedLevel is null ? levels : levels.Where(level => level == requestedLevel).ToArray();
    }

    private static string IndicatorFilterHash(IndicatorRequest request)
    {
        var canonical = string.Join('\n',
            request.ActorUserId.ToString("D", CultureInfo.InvariantCulture),
            request.IsoYear.ToString(CultureInfo.InvariantCulture),
            request.IsoWeek.ToString(CultureInfo.InvariantCulture),
            request.Level ?? string.Empty,
            request.ResponsiblePersonId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string DirectionOverviewFilterHash(IndicatorRequest request)
    {
        var canonical = string.Join('\n',
            "/api/v1/direction/overview",
            request.ActorUserId.ToString("D", CultureInfo.InvariantCulture),
            request.IsoYear.ToString(CultureInfo.InvariantCulture),
            request.IsoWeek.ToString(CultureInfo.InvariantCulture),
            request.Level ?? string.Empty,
            request.ResponsiblePersonId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static IEnumerable<IndicatorPersonRow> ApplyIndicatorCursor(
        IEnumerable<IndicatorPersonRow> query,
        IndicatorCursor? cursor) => cursor is null ? query : query.Where(person =>
            string.Compare(person.StableCode, cursor.StableCode, StringComparison.Ordinal) > 0 ||
            person.StableCode == cursor.StableCode && person.Id.CompareTo(cursor.PersonId) > 0);

    private static string EncodeIndicatorCursor(IndicatorPersonRow person, string filterHash) =>
        EncodeCursor(new IndicatorCursor(CursorVersion, person.StableCode, person.Id, filterHash));

    private static IndicatorCursor? DecodeIndicatorCursor(string? value, string filterHash)
    {
        if (value is null) return null;
        try
        {
            var cursor = DecodeCursor<IndicatorCursor>(value);
            if (cursor.Version != CursorVersion || string.IsNullOrWhiteSpace(cursor.StableCode) ||
                cursor.PersonId == Guid.Empty || cursor.FilterHash.Length != filterHash.Length ||
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
            throw new IndicatorFilterInvalidException();
        }
    }

    private static IndicatorCursor? DecodeDirectionOverviewCursor(string? value, string filterHash)
    {
        try
        {
            return DecodeIndicatorCursor(value, filterHash);
        }
        catch (IndicatorFilterInvalidException)
        {
            throw new DirectionOverviewFilterInvalidException();
        }
    }

    private static bool ValidIsoWeek(int? year, int? week)
    {
        if (year.HasValue != week.HasValue) return false;
        return year is null || year is >= 1 and <= 9999 && week is >= 1 &&
            week <= ISOWeek.GetWeeksInYear(year.Value);
    }

    private static string[] AllowedPendingValidatorRoles(string actorRoleCode) => actorRoleCode switch
    {
        CanonicalRole.Direction => [CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination],
        CanonicalRole.Administration => [CanonicalRole.Administration, CanonicalRole.Subcoordination],
        CanonicalRole.Subcoordination => [CanonicalRole.Subcoordination],
        _ => throw new SupervisionAccessDeniedException(),
    };

    private static string SupervisionFilterHash(SupervisionRequest request) => FilterHash(
        request.Level, request.ResponsiblePersonId, request.IsoYear, request.IsoWeek, request.ExecutionStatus);

    private static string PendingFilterHash(PendingValidationsRequest request) => FilterHash(
        request.Level, request.ResponsiblePersonId, request.IsoYear, request.IsoWeek, null);

    private static string FilterHash(string? level, Guid? personId, int? isoYear, int? isoWeek, string? status)
    {
        var canonical = string.Join('\n', level ?? string.Empty,
            personId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty,
            isoYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            isoWeek?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            status ?? string.Empty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static ListCursor? DecodeSupervisionCursor(string? value, string filterHash)
    {
        try { return DecodeListCursor(value, filterHash); }
        catch (ObligationQueryFilterInvalidException) { throw new SupervisionFilterInvalidException(); }
    }

    private static PendingCursor? DecodePendingCursor(string? value, string filterHash)
    {
        if (value is null) return null;
        try
        {
            var cursor = DecodeCursor<PendingCursor>(value);
            if (cursor.Version != CursorVersion || cursor.ObligationId == Guid.Empty ||
                cursor.PendingSince.Offset != TimeSpan.Zero || cursor.FilterHash.Length != filterHash.Length ||
                !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(cursor.FilterHash),
                    Encoding.ASCII.GetBytes(filterHash)))
                throw new FormatException();
            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new PendingValidationsFilterInvalidException();
        }
    }

    private static IQueryable<ObligationRow> ApplyPendingCursor(IQueryable<ObligationRow> query,
        PendingCursor? cursor) => cursor is null ? query : query.Where(row =>
            row.ConcludedAt > cursor.PendingSince ||
            row.ConcludedAt == cursor.PendingSince && row.ObligationId.CompareTo(cursor.ObligationId) > 0);

    private static string EncodePendingCursor(ObligationRow row, string filterHash)
    {
        if (row.ConcludedAt is null) throw new PendingValidationQueryInconsistentException();
        return EncodeCursor(new PendingCursor(CursorVersion, row.ConcludedAt.Value, row.ObligationId, filterHash));
    }

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
        public Guid? ValidationPolicyVersionId { get; init; }
        public required long RowVersion { get; init; }
    }

    private sealed record CurrentAssignmentRow(
        Guid ObligationId,
        Guid AssignmentId,
        Guid PersonId,
        string StableCode,
        string DisplayName,
        string AssignmentType,
        DateTimeOffset AssignedAt);

    private sealed record ResponsibleAccessRow(Guid ObligationId, Guid PersonId, string RoleCode);

    private sealed record EvidenceSummaryRow(Guid ObligationId, Guid EvidenceItemId, long ItemRowVersion,
        Guid RequirementVersionId, string RequirementCode, string RequirementKind,
        Guid EvidenceVersionId, int VersionNo, string Status, Guid SubmittedByUserId,
        DateTimeOffset SubmittedAt, string? Reason, Guid? SupersedesEvidenceVersionId, string SourceKind);

    private sealed record PendingCursor(int Version, DateTimeOffset PendingSince, Guid ObligationId,
        string FilterHash);

    private sealed record IndicatorCursor(int Version, string StableCode, Guid PersonId, string FilterHash);

    private sealed class IndicatorPersonCandidateRow
    {
        public required Guid Id { get; init; }
        public required string StableCode { get; init; }
        public required string DisplayName { get; init; }
        public required Guid UserId { get; init; }
        public required string RoleCode { get; init; }
    }

    private sealed class IndicatorPersonRow
    {
        public required Guid Id { get; init; }
        public required string StableCode { get; init; }
        public required string DisplayName { get; init; }
        public required string RoleCode { get; init; }
    }

    private sealed class IndicatorObligationRow
    {
        public required Guid ObligationId { get; init; }
        public required string ExecutionStatus { get; init; }
    }

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
