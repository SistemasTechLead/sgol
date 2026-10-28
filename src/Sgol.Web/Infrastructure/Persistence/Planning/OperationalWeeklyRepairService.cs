using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record WeeklyRepairDifference(OperationalAssignment? Before, OperationalAssignment? After);
public sealed record WeeklyRepairCatalog(OperationalTask Before, OperationalTask After);
public sealed record WeeklyRepairPreview(Guid PlanId, DateOnly From, DateOnly Through, string Fingerprint,
    WeeklyRepairDifference[] Differences, WeeklyRepairCatalog[] Catalog, string[] Conflicts)
{
    public bool CanApply => Conflicts.Length == 0;
}

// One explicit adoption of the agreed catalogue and policy, never a background replan.
public sealed class OperationalWeeklyRepairService(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock)
{
    public static readonly string[] CatalogCodes = ["TAR-0062", "TAR-0222", "TAR-0325", "TAR-0004", "TAR-0032", "TAR-0200", "TAR-0201", "TAR-0330", "TAR-0344", "TAR-0221", "TAR-0402", "TAR-0063"];
    public static void Configure(OperationalTask task)
    {
        if (task.TaskCode is "TAR-0222" or "TAR-0325" or "TAR-0004" or "TAR-0032" or "TAR-0330" or "TAR-0344" or "TAR-0221" or "TAR-0402") task.OperationalCriticality = "IMPORTANTE";
        switch (task.TaskCode)
        {
            case "TAR-0004": task.StartHour = 18; task.StartMinute = 0; task.SundayStartMinute = 772; task.WeekDays |= 1; task.Repetitions = 7; task.Name = "Elaborar el precorte de ventas"; break;
            case "TAR-0032": task.LoadMinutes = 20; task.SundayStartMinute = 802; break;
            case "TAR-0200": task.StartHour = 19; task.StartMinute = 52; task.SundayStartMinute = 832; break;
            case "TAR-0201": task.StartHour = 17; task.StartMinute = 30; break;
            case "TAR-0063": task.Active = false; break;
        }
    }
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    public async Task<WeeklyRepairPreview> PreviewAsync(Guid actor, Guid planId, DateOnly from, DateOnly through, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token);
        var result = await BuildAsync(planId, from, through, token);
        await tx.CommitAsync(token); return result;
    }
    private async Task<WeeklyRepairPreview> BuildAsync(Guid planId, DateOnly from, DateOnly through, CancellationToken token)
    {
        if (through < from || through.DayNumber - from.DayNumber > 6 || from < WeekContract.LocalToday(clock.UtcNow)) throw new DirectionPlanException("SCHEDULE_DATE_INVALID", 422);
        var plan = await db.DirectionPreparedPlans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == planId && p.BranchId == BranchScope.LorettaId && p.Status == "ASIGNADO", token)
            ?? throw new DirectionPlanException("DIRECTION_PLAN_NOT_FOUND", 404);
        if (from < plan.StartsOn || through > plan.EndsOn || plan.PeriodType != "SEMANAL" || plan.Year != 2026 || plan.PeriodNumber != 43 ||
            from < new DateOnly(2026,10,22) || through > new DateOnly(2026,10,25)) throw new DirectionPlanException("SCHEDULE_DATE_INVALID", 422);
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, token);
        if (policy.AgendaVersion < 2) throw new DirectionPlanException("PLAN_CAPACITY_STALE", 409);
        var start = OperationalSchedule.Instant(from); var end = OperationalSchedule.Instant(through.AddDays(1));
        var original = await db.OperationalAssignments.AsNoTracking().Where(a => a.PlanId == planId && a.StartsAt >= start && a.StartsAt < end).OrderBy(a => a.Id).ToArrayAsync(token);
        var ids = original.Select(t => t.Id).ToArray();
        var codes = original.Select(t => t.TaskCode).Concat(CatalogCodes).Distinct().ToArray();
        var definitions = await db.OperationalTasks.AsNoTracking().Where(t => codes.Contains(t.TaskCode)).OrderBy(t => t.TaskCode).ToArrayAsync(token);
        var catalog = definitions.Where(t => CatalogCodes.Contains(t.TaskCode)).Select(t => {
            var after = Copy(t); Configure(after); after.Validate();
            var changed = JsonSerializer.Serialize(t) != JsonSerializer.Serialize(after);
            if (changed) after.Revision++;
            return new WeeklyRepairCatalog(t, after);
        }).Where(c => c.Before.Revision != c.After.Revision).ToArray();
        var effective = definitions.ToDictionary(t => t.Id, Copy);
        foreach (var change in catalog) effective[change.After.Id] = change.After;
        var evidence = (await db.OperationalPhotographs.Where(p => ids.Contains(p.AssignmentId)).Select(p => p.AssignmentId).ToArrayAsync(token))
            .Concat(await db.OperationalAttachments.Where(p => ids.Contains(p.AssignmentId)).Select(p => p.AssignmentId).ToArrayAsync(token))
            .Concat(await db.OperationalProgressEntries.Where(p => ids.Contains(p.AssignmentId)).Select(p => p.AssignmentId).ToArrayAsync(token)).Distinct().Order().ToArray();
        var processLinks = await db.Set<OperationalSequenceAttempt>().Where(a => a.AssignmentId != null && ids.Contains(a.AssignmentId.Value)).Select(a => a.AssignmentId!.Value).ToArrayAsync(token);
        var reassignedLinks = await db.OperationalAssignments.Where(a => a.ReassignedFromId != null && ids.Contains(a.ReassignedFromId.Value)).Select(a => a.ReassignedFromId!.Value).ToArrayAsync(token);
        bool Protected(OperationalAssignment a) => a.TransferRemainingMinutes != null || a.CompletedAt != null || a.ProgressUnits > 0 || a.ReviewedAt != null || a.ReviewStatus is not (null or "PENDIENTE") || a.ReassignedFromId != null || a.OccasionId != null ||
            a.StartsAt <= clock.UtcNow || evidence.Contains(a.Id) || processLinks.Contains(a.Id) || reassignedLinks.Contains(a.Id);
        var conflicts = new List<string>();
        var retirement = original.Where(t => t.TaskCode == "TAR-0063").ToArray();
        foreach (var row in retirement.Where(Protected)) conflicts.Add($"No se puede retirar {row.TaskCode} ({row.Id}): ya está activada o tiene actividad protegida.");
        var movable = original.Where(t => t.TaskCode != "TAR-0063" && !Protected(t)).Select(Copy).ToList();
        foreach (var row in movable)
        {
            var task = effective[row.TaskId];
            if (!task.Active || task.Deleted) { conflicts.Add($"{task.TaskCode} está inactiva y requiere revisión explícita."); continue; }
            row.LoadMinutes = task.LoadMinutes; row.TaskRevision = task.Revision; row.Repetitions = task.Repetitions;
            OperationalAgenda.Capture(row, policy, task.OperationalCriticality, task.StartOn(WeekContract.LocalToday(row.StartsAt)));
        }
        var added = new List<OperationalAssignment>();
        foreach (var sunday in CapacityLedger.Days(from, through).Where(d => d.DayOfWeek == DayOfWeek.Sunday))
        {
            if (original.Any(t => t.TaskCode == "TAR-0004" && WeekContract.LocalToday(t.StartsAt) == sunday)) continue;
            var template = movable.FirstOrDefault(t => t.TaskCode == "TAR-0004");
            var closing = original.FirstOrDefault(t => t.TaskCode == "TAR-0200" && WeekContract.LocalToday(t.StartsAt) == sunday);
            if (template == null || closing == null) { conflicts.Add("Falta una ocurrencia de referencia para el precorte dominical."); continue; }
            var row = Copy(template); row.Id = new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"repair:{planId}:{sunday}:TAR-0004"))[..16]);
            row.PersonId = closing.PersonId; row.PersonName = closing.PersonName; row.AssignedRole = closing.AssignedRole;
            row.ScheduleKey = $"{row.TaskId:N}:{sunday:yyyyMMdd}:0";
            OperationalAgenda.Capture(row, policy, "IMPORTANTE", effective[row.TaskId].StartOn(sunday)); added.Add(row);
        }
        var source = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, token);
        var retiredIds = retirement.Where(t => !Protected(t)).Select(t => t.Id).ToHashSet();
        var baseline = new CapacityLedger(source.Workers, source.Tasks.Where(t => !retiredIds.Contains(t.Id)).ToArray(), source.Capacities, source.Unavailable, source.Legacy, source.Today) { Temporal = source.Temporal };
        OperationalAssignment[] agenda = [];
        if (conflicts.Count == 0)
            try { agenda = OperationalAgendaSearch.SolveFixed(baseline, movable.Concat(added), start > clock.UtcNow ? start : clock.UtcNow, token); }
            catch (DirectionPlanException e) when (e.Code is "SCHEDULING_INFEASIBLE" or "SCHEDULING_SEARCH_INCONCLUSIVE" or "SCHEDULING_LEGACY_RESERVATION")
            {
                conflicts.Add(TemporalEligibilityContext.Message(e.Code));
                foreach (var earlier in source.Tasks.Where(t => t.StartsAt < start && t.DueAt > start && t.CompletedAt == null && t.GoalUnits == 0))
                    conflicts.Add($"Compromiso protegido: {earlier.PersonName} · {earlier.TaskCode} · {earlier.Id} · cierre {TimeZoneInfo.ConvertTime(earlier.DueAt, OperationalSchedule.Zone):dd/MM HH:mm}.");
            }
        var differences = agenda.Select(t => new WeeklyRepairDifference(original.SingleOrDefault(a => a.Id == t.Id), t))
            .Concat(retirement.Select(t => new WeeklyRepairDifference(t, null))).ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { planId, from, through, policy, definitions, original, evidence, processLinks, reassignedLinks,
            source.Workers, source.Tasks, source.Temporal, source.Capacities, Unavailable = source.Unavailable.OrderBy(x => x.Item1).ThenBy(x => x.Item2), Legacy = source.Legacy.OrderBy(x => x.Key),
            Search = OperationalAgendaSearch.Version, OperationalAgendaSearch.Budget, OperationalAgendaSearch.CandidateBudget, differences, catalog, conflicts })));
        return new(planId, from, through, fingerprint, differences, catalog, conflicts.ToArray());
    }
    public async Task ApplyAsync(Guid actor, Guid planId, DateOnly from, DateOnly through, string fingerprint, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        if (await db.AuditEvents.AnyAsync(a => a.Action == "OPERATIONAL_WEEKLY_REPAIR_APPLIED" && a.ResourceId == planId && a.AfterData != null && a.AfterData.RootElement.GetProperty("Fingerprint").GetString() == fingerprint, token)) { await tx.CommitAsync(token); return; }
        var preview = await BuildAsync(planId, from, through, token);
        if (!preview.CanApply || preview.Fingerprint != fingerprint) throw new DirectionPlanException("CAPACITY_STALE", 409);
        foreach (var change in preview.Catalog)
        {
            var task = await db.OperationalTasks.SingleAsync(t => t.Id == change.Before.Id, token);
            db.Entry(task).CurrentValues.SetValues(change.After);
            db.AuditEvents.Add(Event(actor, task.Id, "OPERATIONAL_TASK_SAVED", change.Before, change.After));
        }
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, token);
        foreach (var diff in preview.Differences)
        {
            if (diff.After == null)
            {
                var row = await db.OperationalAssignments.SingleAsync(t => t.Id == diff.Before!.Id, token);
                db.Remove(row); db.AuditEvents.Add(Event(actor, row.Id, "OPERATIONAL_ASSIGNMENT_RETIRED", diff.Before, new { Reason = "Definición TAR-0063 inactiva; retiro explícito sin actividad" })); continue;
            }
            var after = diff.After;
            after.Revision = (diff.Before?.Revision ?? 0) + 1;
            ledger.CaptureTime(after);
            after.TemporalPolicyJson = JsonSerializer.Serialize(new { PolicyId = after.SchedulingPolicyId, AgendaVersion = 2, AdoptedBy = actor, AdoptedAt = clock.UtcNow });
            if (diff.Before == null) db.Add(after);
            else
            {
                var row = await db.OperationalAssignments.SingleAsync(t => t.Id == after.Id, token);
                db.Entry(row).CurrentValues.SetValues(after);
            }
            db.AuditEvents.Add(Event(actor, after.Id, "OPERATIONAL_ASSIGNMENT_AGENDA_ADOPTED", diff.Before, after));
        }
        db.AuditEvents.Add(Event(actor, planId, "OPERATIONAL_WEEKLY_REPAIR_APPLIED", null, preview));
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }
    private AuditEvent Event(Guid actor, Guid id, string action, object? before, object after) => new() {
        Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow, ResourceId = id, ResourceType = "OPERATIONAL_AGENDA",
        BranchId = BranchScope.LorettaId, Action = action, Outcome = "SUCCESS", CorrelationId = Guid.CreateVersion7(), BeforeData = before == null ? null : JsonSerializer.SerializeToDocument(before), AfterData = JsonSerializer.SerializeToDocument(after) };
}
