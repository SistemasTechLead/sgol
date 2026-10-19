using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Notifications;

public sealed class EfInboxReader(SgolDbContext dbContext, Sgol.BuildingBlocks.Time.IClock clock) : IInboxReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InboxPage> ReadAsync(InboxQuery query, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            var queriedAt = clock.UtcNow;
            var actor = await GetActorAsync(query.ActorUserId, queriedAt, cancellationToken);
            Validate(query);
            var period = await GetPeriodAsync(query.PeriodId, queriedAt, cancellationToken);
            var taskRows = period is null
                ? []
                : await ReadTaskRowsAsync(actor.PersonId, period, queriedAt, query, cancellationToken);
            var tasks = period is null
                ? new InboxSection<InboxTask>([], null)
                : await BuildTasksAsync(taskRows, period, queriedAt, query, cancellationToken);
            var notices = await ReadNoticesAsync(actor, query, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            Record("success", started);
            return new(period, tasks, notices, queriedAt);
        }
        catch (InboxAccessDeniedException)
        {
            Record("denied", started);
            throw;
        }
        catch (Exception exception) when (exception is EvidenceReviewUnavailableException or InvalidOperationException)
        {
            Record("inconsistent", started);
            throw new InboxInconsistentException(exception);
        }
        catch
        {
            Record("failure", started);
            throw;
        }
    }

    private async Task<Actor> GetActorAsync(Guid actorUserId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var actors = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where user.Id == actorUserId && user.Status == BootstrapContract.ActiveAccountStatus && user.MfaEnrolledAt != null &&
                employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= at && (employment.ValidTo == null || at < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= at && (role.ValidTo == null || at < role.ValidTo)
            select new Actor(user.Id, user.PersonId, role.RoleCode)).Take(2).ToListAsync(cancellationToken);
        if (actors.Count != 1 || !RoleHierarchy.GrantsOwnInbox(actors[0].RoleCode)) throw new InboxAccessDeniedException();
        return actors[0];
    }

    private async Task<InboxPeriod?> GetPeriodAsync(Guid? requestedId, DateTimeOffset queriedAt, CancellationToken cancellationToken)
    {
        WeekPeriod? period;
        if (requestedId.HasValue)
        {
            period = await dbContext.WeekPeriods.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == requestedId && item.BranchId == BranchScope.LorettaId, cancellationToken);
            if (period is null) return null;
        }
        else
        {
            var local = WeekContract.LocalToday(queriedAt);
            var year = ISOWeek.GetYear(local.ToDateTime(TimeOnly.MinValue));
            var week = ISOWeek.GetWeekOfYear(local.ToDateTime(TimeOnly.MinValue));
            var rows = await dbContext.WeekPeriods.AsNoTracking()
                .Where(item => item.BranchId == BranchScope.LorettaId && item.IsoYear == year && item.IsoWeek == week)
                .Take(2).ToListAsync(cancellationToken);
            if (rows.Count != 1) throw new InvalidOperationException("Current period is inconsistent.");
            period = rows[0];
        }

        return new(period.Id, period.IsoYear, period.IsoWeek, period.StartsOn, period.EndsOn, CalendarContract.TimeZone);
    }

    private async Task<List<RankedTaskRow>> ReadTaskRowsAsync(
        Guid personId, InboxPeriod period, DateTimeOffset queriedAt, InboxQuery query, CancellationToken cancellationToken)
    {
        var localToday = WeekContract.LocalToday(queriedAt);
        var periodIsFuture = period.StartsOn > localToday;
        var filterHash = Hash($"{period.PeriodId:D}\n{query.TaskState ?? string.Empty}");
        TaskCursor? cursor = null;
        if (query.TaskCursor is not null)
        {
            cursor = Decode<TaskCursor>(query.TaskCursor);
            if (cursor.Version != 1 || cursor.FilterHash != filterHash || cursor.StateRank is < 0 or > 3 ||
                cursor.StartsOn != period.StartsOn || string.IsNullOrWhiteSpace(cursor.TaskCode) || cursor.ObligationId == Guid.Empty ||
                query.TaskState is not null && cursor.StateRank != InboxTaskStates.Rank(query.TaskState))
                throw new InboxFilterInvalidException();
        }

        var ranks = query.TaskState is null
            ? new[] { 0, 1, 2, 3 }
            : new[] { InboxTaskStates.Rank(query.TaskState) };
        var result = new List<RankedTaskRow>(query.TaskLimit + 1);
        foreach (var rank in ranks)
        {
            if (result.Count > query.TaskLimit || cursor is not null && rank < cursor.StateRank ||
                rank == 1 && periodIsFuture || rank == 2 && !periodIsFuture)
                continue;

            var remaining = query.TaskLimit + 1 - result.Count;
            result.AddRange(await ReadTaskBucketAsync(
                personId,
                period.PeriodId,
                queriedAt,
                rank,
                cursor is not null && rank == cursor.StateRank ? cursor : null,
                remaining,
                cancellationToken));
        }

        return result;
    }

    private async Task<IReadOnlyList<RankedTaskRow>> ReadTaskBucketAsync(
        Guid personId,
        Guid periodId,
        DateTimeOffset queriedAt,
        int rank,
        TaskCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText =
            """
            SELECT obligation.id,
                   definition.id,
                   definition.task_code,
                   definition.name,
                   request.origin_type,
                   obligation.due_at,
                   obligation.concluded_at,
                   obligation.execution_status,
                   obligation.evidence_policy_version_id
            FROM assignment_version AS assignment
            JOIN work_obligation AS obligation ON obligation.id = assignment.obligation_id
            JOIN task_definition_version AS version ON version.id = obligation.task_definition_version_id
            JOIN task_definition AS definition ON definition.id = version.task_definition_id
            JOIN generation_request AS request ON request.id = obligation.generation_request_id
            WHERE assignment.person_id = @person_id
              AND assignment.status = 'VIGENTE'
              AND obligation.branch_id = @branch_id
              AND obligation.period_id = @period_id
              AND (
                    (@rank = 0 AND obligation.execution_status = 'PENDIENTE' AND obligation.due_at IS NOT NULL AND obligation.due_at < @queried_at)
                 OR (@rank IN (1, 2) AND obligation.execution_status = 'PENDIENTE' AND (obligation.due_at IS NULL OR obligation.due_at >= @queried_at))
                 OR (@rank = 3 AND obligation.execution_status = 'CONCLUIDA')
              )
              AND (
                    NOT @use_cursor
                 OR (@cursor_due IS NOT NULL AND (
                        obligation.due_at IS NULL
                     OR obligation.due_at > @cursor_due
                     OR (obligation.due_at = @cursor_due AND (
                            definition.task_code COLLATE "C" > @cursor_task_code
                         OR (definition.task_code = @cursor_task_code AND obligation.id > @cursor_obligation_id)))))
                 OR (@cursor_due IS NULL AND obligation.due_at IS NULL AND (
                        definition.task_code COLLATE "C" > @cursor_task_code
                     OR (definition.task_code = @cursor_task_code AND obligation.id > @cursor_obligation_id)))
              )
            ORDER BY obligation.due_at ASC NULLS LAST,
                     definition.task_code COLLATE "C" ASC,
                     obligation.id ASC
            LIMIT @limit
            """;
        AddParameter(command, "person_id", personId, DbType.Guid);
        AddParameter(command, "branch_id", BranchScope.LorettaId, DbType.Guid);
        AddParameter(command, "period_id", periodId, DbType.Guid);
        AddParameter(command, "queried_at", queriedAt, DbType.DateTimeOffset);
        AddParameter(command, "rank", rank, DbType.Int32);
        AddParameter(command, "use_cursor", cursor is not null, DbType.Boolean);
        AddParameter(command, "cursor_due", cursor?.DueAt, DbType.DateTimeOffset);
        AddParameter(command, "cursor_task_code", cursor?.TaskCode ?? string.Empty, DbType.String);
        AddParameter(command, "cursor_obligation_id", cursor?.ObligationId ?? Guid.Empty, DbType.Guid);
        AddParameter(command, "limit", limit, DbType.Int32);

        var result = new List<RankedTaskRow>(limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new TaskRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8));
            result.Add(new(row, rank));
        }

        return result;
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private async Task<InboxSection<InboxTask>> BuildTasksAsync(
        List<RankedTaskRow> rows, InboxPeriod period, DateTimeOffset queriedAt, InboxQuery query, CancellationToken cancellationToken)
    {
        var filterHash = Hash($"{period.PeriodId:D}\n{query.TaskState ?? string.Empty}");
        var visible = rows.Take(query.TaskLimit).ToArray();
        var evaluations = await EvaluateAsync(visible.Select(item => item.Row).ToList(), cancellationToken);
        var localToday = WeekContract.LocalToday(queriedAt);
        var items = visible.Select(item =>
        {
            var row = item.Row;
            var evidence = evaluations[row.ObligationId];
            var taskState = InboxTaskStates.Classify(
                row.ExecutionStatus == WorkObligationStatuses.Concluded,
                row.DueAt,
                period.StartsOn,
                localToday,
                queriedAt);
            if (InboxTaskStates.Rank(taskState) != item.StateRank) throw new InvalidOperationException("Task-state projection is inconsistent.");
            var actions = new List<string> { InboxActionCodes.ViewTask };
            if (row.ExecutionStatus == WorkObligationStatuses.Pending && evidence.Result == EvidenceReviewResults.Incomplete)
                actions.Add(InboxActionCodes.ContributeEvidence);
            if (row.ExecutionStatus == WorkObligationStatuses.Pending && evidence.Result == EvidenceReviewResults.Complete)
                actions.Add(InboxActionCodes.ConcludeTask);
            var origin = row.OriginType switch
            {
                RecurringGenerationContract.OriginType => "RECURRENTE",
                ActivationOriginSchemas.ManualReference => "MANUAL",
                _ => throw new InvalidOperationException("Task origin is inconsistent."),
            };
            return new InboxTask(row.ObligationId,
                new(row.TaskDefinitionId, row.TaskCode, row.Name), period, origin, origin == "RECURRENTE",
                new(row.DueAt, row.DueAt is null ? null : WeekContract.LocalToday(row.DueAt.Value), row.ConcludedAt),
                row.ExecutionStatus, taskState, evidence, actions);
        }).ToArray();
        var next = rows.Count > query.TaskLimit
            ? Encode(Cursor(rows[query.TaskLimit - 1], period.StartsOn, filterHash))
            : null;
        return new(items, next);
    }

    private async Task<Dictionary<Guid, InboxEvidence>> EvaluateAsync(List<TaskRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];
        if (rows.Any(row => row.PolicyId is null)) throw new EvidenceReviewUnavailableException();
        var policyIds = rows.Select(row => row.PolicyId!.Value).Distinct().ToArray();
        var policies = await dbContext.EvidencePolicyVersions.AsNoTracking().Where(item => policyIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var requirements = await dbContext.EvidenceRequirementVersions.AsNoTracking().Where(item => policyIds.Contains(item.PolicyVersionId))
            .OrderBy(item => item.Ordinal).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        var obligationIds = rows.Select(row => row.ObligationId).ToArray();
        var items = await dbContext.EvidenceItems.AsNoTracking().Where(item => obligationIds.Contains(item.ObligationId)).ToListAsync(cancellationToken);
        var itemIds = items.Select(item => item.Id).ToArray();
        var versions = await dbContext.EvidenceVersions.AsNoTracking().Where(item => itemIds.Contains(item.EvidenceItemId)).ToListAsync(cancellationToken);
        var fileIds = versions.Where(item => item.FileObjectId.HasValue).Select(item => item.FileObjectId!.Value).Distinct().ToArray();
        var files = await dbContext.FileObjects.AsNoTracking().Where(item => fileIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var result = new Dictionary<Guid, InboxEvidence>();

        foreach (var row in rows)
        {
            var policyId = row.PolicyId!.Value;
            if (!policies.TryGetValue(policyId, out var policy) || policy.Status is not (VersionStatuses.Current or VersionStatuses.Superseded) ||
                !EvidencePolicyCatalog.All.TryGetValue(row.TaskCode, out var expectedDefinitions)) throw new EvidenceReviewUnavailableException();
            var policyRequirements = requirements.Where(item => item.PolicyVersionId == policyId).ToArray();
            var obligationItems = items.Where(item => item.ObligationId == row.ObligationId).ToArray();
            var requirementIds = policyRequirements.Select(item => item.Id).ToHashSet();
            if (obligationItems.Any(item => item.EvidencePolicyVersionId != policyId || !requirementIds.Contains(item.RequirementVersionId)) ||
                obligationItems.GroupBy(item => item.RequirementVersionId).Any(group => group.Count() != 1)) throw new EvidenceReviewUnavailableException();
            var itemByRequirement = obligationItems.ToDictionary(item => item.RequirementVersionId);
            var inputs = policyRequirements.Select(requirement =>
            {
                itemByRequirement.TryGetValue(requirement.Id, out var item);
                if (item is not null && (item.RequirementCode != requirement.RequirementCode || item.RequirementKind != requirement.Kind)) throw new EvidenceReviewUnavailableException();
                var inputVersions = item is null ? [] : versions.Where(version => version.EvidenceItemId == item.Id).Select(version => new EvidenceReviewVersionInput(
                    version.Id, version.Status, version.FileObjectId, version.StructuredPayload,
                    version.FileObjectId is { } fileId && files.TryGetValue(fileId, out var file)
                        ? new(file.Id, file.RequirementVersionId, file.LinkedEvidenceItemId, file.ScanStatus, file.BucketClass) : null)).ToArray();
                return new EvidenceReviewRequirementInput(requirement.Id, requirement.RequirementCode, requirement.Kind, requirement.ConditionCode,
                    requirement.Ordinal, requirement.IsRequired, item?.Id, inputVersions);
            }).ToArray();
            var evaluation = EvidenceReviewEvaluator.Evaluate(new(row.ObligationId, policyId, row.TaskCode,
                expectedDefinitions.Select(item => new EvidenceReviewExpectedRequirement(item.Code, item.Kind, item.ConditionCode, item.Ordinal)).ToArray(), inputs));
            result[row.ObligationId] = new(evaluation.Result, evaluation.MissingRequirements.Select(item => new InboxMissingRequirement(
                item.RequirementVersionId, item.RequirementCode, item.Kind, item.ConditionCode, item.Ordinal, item.MissingReason)).ToArray());
        }
        return result;
    }

    private async Task<InboxSection<InboxNotice>> ReadNoticesAsync(Actor actor, InboxQuery query, CancellationToken cancellationToken)
    {
        var filterHash = Hash(query.NoticeStatus);
        var noticeQuery = dbContext.InternalNotices.AsNoTracking().Where(item => item.RecipientUserId == actor.UserId);
        noticeQuery = query.NoticeStatus switch
        {
            InternalNoticeStatuses.Unread => noticeQuery.Where(item => item.ReadAt == null),
            InternalNoticeStatuses.Read => noticeQuery.Where(item => item.ReadAt != null),
            _ => noticeQuery,
        };
        if (query.NoticeCursor is not null)
        {
            var cursor = Decode<NoticeCursor>(query.NoticeCursor);
            if (cursor.Version != 1 || cursor.FilterHash != filterHash ||
                cursor.Status is not (InternalNoticeStatuses.Unread or InternalNoticeStatuses.Read) ||
                cursor.CreatedAt == default || cursor.NoticeId == Guid.Empty)
                throw new InboxFilterInvalidException();
            var cursorUnread = cursor.Status == InternalNoticeStatuses.Unread;
            noticeQuery = noticeQuery.Where(item =>
                (cursorUnread && item.ReadAt != null) ||
                ((item.ReadAt == null) == cursorUnread &&
                 (item.CreatedAt < cursor.CreatedAt || item.CreatedAt == cursor.CreatedAt && item.Id.CompareTo(cursor.NoticeId) < 0)));
        }
        var page = await noticeQuery.OrderBy(item => item.ReadAt != null)
            .ThenByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Take(query.NoticeLimit + 1).ToArrayAsync(cancellationToken);
        var rows = page.Take(query.NoticeLimit).ToArray();
        var assignmentIds = rows.Select(item => item.ResourceId).Distinct().ToArray();
        var resources = await (
            from assignment in dbContext.AssignmentVersions.AsNoTracking()
            join obligation in dbContext.WorkObligations.AsNoTracking() on assignment.ObligationId equals obligation.Id
            join version in dbContext.TaskDefinitionVersions.AsNoTracking() on obligation.TaskDefinitionVersionId equals version.Id
            join definition in dbContext.TaskDefinitions.AsNoTracking() on version.TaskDefinitionId equals definition.Id
            where assignmentIds.Contains(assignment.Id)
            select new NoticeResource(assignment.Id, assignment.PersonId, assignment.Status, obligation.Id, obligation.BranchId, definition.TaskCode, definition.Name))
            .ToDictionaryAsync(item => item.AssignmentId, cancellationToken);
        var items = rows.Select(row =>
        {
            var status = row.ReadAt is null ? InternalNoticeStatuses.Unread : InternalNoticeStatuses.Read;
            var available = resources.TryGetValue(row.ResourceId, out var resource) && resource.PersonId == actor.PersonId &&
                resource.Status == AssignmentVersionStatuses.Current && resource.BranchId == BranchScope.LorettaId;
            return new ProjectedNotice(row, status, new InboxNotice(row.Id, row.NoticeType, status, row.CreatedAt, row.ReadAt,
                new(row.ResourceType, row.ResourceId, available, available ? resource!.ObligationId : null,
                    available ? resource!.TaskCode : null, available ? resource!.Name : null),
                status == InternalNoticeStatuses.Unread ? [InboxActionCodes.MarkNoticeRead] : []));
        }).Select(item => item.Item).ToArray();
        var last = rows.LastOrDefault();
        var next = page.Length > query.NoticeLimit && last is not null
            ? Encode(new NoticeCursor(1, last.ReadAt is null ? InternalNoticeStatuses.Unread : InternalNoticeStatuses.Read,
                last.CreatedAt, last.Id, filterHash))
            : null;
        return new(items, next);
    }

    private static void Validate(InboxQuery query)
    {
        if (query.ActorUserId == Guid.Empty || query.TaskLimit is < 1 or > 100 || query.NoticeLimit is < 1 or > 100 ||
            query.TaskState is not (null or InboxTaskStates.Future or InboxTaskStates.Available or InboxTaskStates.Overdue or InboxTaskStates.Concluded) ||
            query.NoticeStatus is not ("ALL" or InternalNoticeStatuses.Unread or InternalNoticeStatuses.Read)) throw new InboxFilterInvalidException();
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Encode<T>(T value) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static T Decode<T>(string value)
    {
        try
        {
            if (value.Length is 0 or > 2048 || value.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')) throw new FormatException();
            var raw = value.Replace('-', '+').Replace('_', '/');
            raw = raw.PadRight(raw.Length + (4 - raw.Length % 4) % 4, '=');
            return JsonSerializer.Deserialize<T>(Convert.FromBase64String(raw), JsonOptions) ?? throw new FormatException();
        }
        catch (Exception exception) when (exception is FormatException or JsonException) { throw new InboxFilterInvalidException(); }
    }
    private static TaskCursor Cursor(RankedTaskRow item, DateOnly startsOn, string hash) => new(1, item.StateRank, item.Row.DueAt,
        startsOn, item.Row.TaskCode, item.Row.ObligationId, hash);
    private static void Record(string result, long started)
    {
        InboxTelemetry.Queries.Add(1, tag: new("result", result));
        InboxTelemetry.QueryDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tag: new("result", result));
    }

    private sealed record Actor(Guid UserId, Guid PersonId, string RoleCode);
    private sealed record TaskRow(Guid ObligationId, Guid TaskDefinitionId, string TaskCode, string Name, string OriginType, DateTimeOffset? DueAt, DateTimeOffset? ConcludedAt, string ExecutionStatus, Guid? PolicyId);
    private sealed record RankedTaskRow(TaskRow Row, int StateRank);
    private sealed record NoticeResource(Guid AssignmentId, Guid PersonId, string Status, Guid ObligationId, Guid BranchId, string TaskCode, string Name);
    private sealed record ProjectedNotice(InternalNotice Row, string Status, InboxNotice Item);
    private sealed record TaskCursor(int Version, int StateRank, DateTimeOffset? DueAt, DateOnly StartsOn, string TaskCode, Guid ObligationId, string FilterHash);
    private sealed record NoticeCursor(int Version, string Status, DateTimeOffset CreatedAt, Guid NoticeId, string FilterHash);
}
