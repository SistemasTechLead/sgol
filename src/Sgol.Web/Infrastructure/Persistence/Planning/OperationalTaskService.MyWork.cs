using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.Search;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalTaskService
{
    private async Task<(OperationalAssignmentDetails[] Items, int Total, int Page)> MineIncludingPendingProcessesAsync(
        Guid actor, string? search, int page, DateTimeOffset now, CancellationToken token)
    {
        await using var snapshot = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token) : null;
        var person = await PersonAsync(actor, token);
        // Reservations are visible work, but never executable assignments. The current attempt
        // must still have no assignment, including after returns and responsibility transfers.
        var pending = from stage in db.Set<OperationalSequenceStage>().AsNoTracking()
            join run in db.Set<OperationalSequenceRun>().AsNoTracking() on stage.RunId equals run.Id
            join attempt in db.Set<OperationalSequenceAttempt>().AsNoTracking() on stage.CurrentAttemptId equals (Guid?)attempt.Id
            join plan in db.DirectionPreparedPlans.AsNoTracking() on run.PlanId equals plan.Id
            where stage.ReservedPersonId == person && run.Status == "EN_CURSO" && attempt.AssignmentId == null &&
                (stage.Status == "EN_ESPERA" || stage.Status == "PROGRAMADA" || stage.Status == "PENDIENTE_ASIGNACION")
            select new { Stage = stage, Run = run, Plan = plan };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = SearchText.Normalize(search.Trim());
            pending = pending.Where(x => SearchText.SqlNormalize(x.Stage.Name).Contains(text) ||
                SearchText.SqlNormalize(x.Stage.TaskCode).Contains(text) || SearchText.SqlNormalize(x.Plan.Name).Contains(text));
        }
        var effective = from a in db.OperationalAssignments.AsNoTracking()
            join p in db.DirectionPreparedPlans.AsNoTracking() on a.PlanId equals p.Id
            where a.PersonId == person
            select new { Assignment = a, Plan = p };
        if (!string.IsNullOrWhiteSpace(search)) {
            var text = SearchText.Normalize(search.Trim());
            effective = effective.Where(x => SearchText.SqlNormalize(x.Assignment.Name).Contains(text) ||
                SearchText.SqlNormalize(x.Assignment.TaskCode).Contains(text) || SearchText.SqlNormalize(x.Plan.Name).Contains(text));
        }
        var keys = effective.Select(x => x.Assignment).Select(a => new { a.Id, a.DueAt, Priority = a.ReviewStatus == "NO_SATISFECHA" ? 0 : a.ReviewStatus == "REASIGNADA" ? (a.CompletedAt != null && a.CompletionKind == "ENTREGA" ? 4 : 3) : a.CompletedAt != null ? 4 : a.StartsAt > now ? 3 :
            a.DueAt <= now || (a.DueAt - now).TotalSeconds <= (a.DueAt - a.StartsAt).TotalSeconds * .1 ? 0 :
            (a.DueAt - now).TotalSeconds <= (a.DueAt - a.StartsAt).TotalSeconds * .3 ? 2 : 1 })
            .Concat(pending.Select(x => new { x.Stage.Id, DueAt = x.Stage.PlannedDueAt, Priority = 3 }));
        var total = await keys.CountAsync(token);
        page = Math.Clamp(page, 1, Math.Max(1, (total + 49) / 50));
        var ids = await keys.OrderBy(x => x.Priority).ThenBy(x => x.DueAt).ThenBy(x => x.Id)
            .Skip((page - 1) * 50).Take(50).Select(x => x.Id).ToArrayAsync(token);
        var assigned = await MineQuery(person, ids: ids).ToArrayAsync(token);
        var reservations = await pending.Where(x => ids.Contains(x.Stage.Id)).ToArrayAsync(token);
        var items = assigned.Concat(reservations.Select(x => new OperationalAssignmentDetails(
            x.Stage.Id, x.Stage.TaskCode, x.Stage.Name, x.Stage.Requirement,
            (int)Math.Min(int.MaxValue, x.Stage.CalculatedLoadMinutes ?? x.Stage.LoadMinutes), person, x.Stage.ReservedPersonName ?? "",
            x.Stage.PlannedStartsAt, x.Stage.PlannedDueAt, null, 0, null, null, x.Stage.Role,
            PlanId: x.Plan.Id, PlanName: x.Plan.Name, PlanPeriodType: x.Plan.PeriodType,
            PlanStartsOn: x.Plan.StartsOn, PlanEndsOn: x.Plan.EndsOn, Description: x.Stage.Description,
            SpecializationId: x.Stage.SpecializationId, SpecializationName: x.Stage.SpecializationName)
        { PendingProcessRunId = x.Run.Id, PendingProcessStatus = x.Stage.Status, PendingProcessReason = x.Stage.BlockReason }))
            .ToDictionary(a => a.Id);
        if (snapshot != null) await snapshot.CommitAsync(token);
        return (ids.Select(id => items[id]).ToArray(), total, page);
    }
}
