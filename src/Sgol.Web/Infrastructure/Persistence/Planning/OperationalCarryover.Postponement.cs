using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Organization.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record OverdueCarryoverTask(OperationalAssignment Assignment, string? Blocked);
public sealed record PostponementDay(DateOnly Day, decimal Actual, decimal Forecast, decimal Added,
    decimal Capacity, DateOnly? Reference)
{
    public bool ReferenceKnown { get; init; } = true;
}
public sealed record PostponementPreview(DateOnly From, DateOnly Through, CarryoverItem[] Items,
    PostponementDay[] Days, string[] Conflicts, string Fingerprint, string ContextFingerprint)
{
    public DateOnly? BeforeDate { get; init; }
}

public static class PostponementWindow
{
    public static (DateOnly From, DateOnly Through, DateOnly? BeforeDate) For(DateOnly today, int? beforeWeekday)
    {
        if (beforeWeekday is < 0 or > 6) throw new DirectionPlanException("ARRASTRE_DATOS_INVALIDOS", 422);
        var from = today.AddDays(1);
        if (beforeWeekday == null) return (from, today.AddDays(7), null);
        var distance = (beforeWeekday.Value - (int)today.DayOfWeek + 7) % 7;
        var before = today.AddDays(distance == 0 ? 7 : distance);
        if (before <= from) throw new DirectionPlanException("ARRASTRE_LIMITE_SIN_DIAS", 422);
        return (from, before.AddDays(-1), before);
    }
}

public sealed partial class OperationalCarryoverService
{
    private async Task<bool> PendingPostponement(Guid person, Guid[] ids, CancellationToken ct) =>
        (await db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r => r.PersonId == person && r.Kind == "POSPONER" && r.State == "PENDIENTE").ToArrayAsync(ct))
        .Any(r => r.Items().Any(i => ids.Contains(i.AssignmentId)));

    private async Task<Guid[]> ImmediateSupervisors(Guid person, CancellationToken ct)
    {
        var workers = await OperationalCapacityService.WorkersAsync(db, clock.UtcNow, ct);
        var own = workers.SingleOrDefault(w => w.Id == person);
        if (own == null) return [];
        // Existing role hierarchy: sales floor -> subcoordination -> administration -> direction.
        // Missing levels are skipped. Direction retains its branch-wide supervision authority.
        string[] levels = [CanonicalRole.SalesFloor, CanonicalRole.Subcoordination, CanonicalRole.Administration, CanonicalRole.Direction];
        foreach (var role in levels.Skip(Array.IndexOf(levels, own.Role) + 1))
        {
            var people = workers.Where(w => w.Id != person && w.Role == role).Select(w => w.Id).ToArray();
            if (people.Length > 0) return await db.AppUsers.AsNoTracking().Where(u => people.Contains(u.PersonId) && u.Status == AccountStatus.Active).Select(u => u.Id).ToArrayAsync(ct);
        }
        return [];
    }
    private async Task RequireImmediateSupervisor(Guid actor, Guid person, CancellationToken ct)
    {
        if (await supervision.AuthorizeAsync(actor, ct) == CanonicalRole.Direction) return;
        if (!(await ImmediateSupervisors(person, ct)).Contains(actor)) throw Error("ACCESO_DENEGADO", 403);
    }

    public async Task<OverdueCarryoverTask[]> OverdueAsync(Guid actor, CancellationToken ct)
    {
        var person = await Person(actor, ct);
        var now = clock.UtcNow;
        var rows = await db.OperationalAssignments.AsNoTracking().Where(a => a.PersonId == person && a.DueAt < now && a.CompletedAt == null &&
            (a.ReviewStatus == null || a.ReviewStatus == "PENDIENTE") &&
            db.DirectionPreparedPlans.Any(p => p.Id == a.PlanId && p.BranchId == BranchScope.LorettaId)).OrderBy(a => a.DueAt).ThenBy(a => a.Id).ToArrayAsync(ct);
        var ids = rows.Select(a => a.Id).ToArray();
        var processes = await db.Set<OperationalSequenceAttempt>().Where(a => a.AssignmentId != null && ids.Contains(a.AssignmentId.Value)).Select(a => a.AssignmentId!.Value).ToArrayAsync(ct);
        var pending = (await db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r => r.PersonId == person && r.State == "PENDIENTE").ToArrayAsync(ct))
            .SelectMany(r => r.Items()).Select(i => i.AssignmentId).ToHashSet();
        return rows.Select(a => new OverdueCarryoverTask(a, pending.Contains(a.Id) ? "Petición pendiente de autorización" :
            processes.Contains(a.Id) ? "Requiere revisar las dependencias del proceso" :
            a.GoalUnits > 0 || a.ScheduleMode == "PROGRESO" ? "Requiere revisar su ventana de progreso" :
            a.SchedulingPolicyId == null ? "Requiere una agenda con horario verificable" :
            OperationalAgenda.CashCircuit.Any(r => r.Before == a.TaskCode || r.After == a.TaskCode) || a.TaskCode == "TAR-0201" ? "Requiere revisar el circuito diario de caja" : null)).ToArray();
    }

    public async Task<PostponementPreview> PreviewPostponementAsync(Guid actor, Guid[] selected, string cause, string? explanation, CancellationToken ct, int? beforeWeekday = null)
    {
        var person = await Person(actor, ct);
        explanation = explanation?.Trim() ?? "";
        if (selected.Length == 0 || selected.Distinct().Count() != selected.Length || !CarryoverCauses.All.ContainsKey(cause) || explanation.Length is < 1 or > 4000)
            throw Error("ARRASTRE_DATOS_INVALIDOS", 422);
        var overdue = await OverdueAsync(actor, ct);
        var rows = selected.Select(id => overdue.SingleOrDefault(t => t.Assignment.Id == id)).ToArray();
        if (rows.Any(r => r == null || r.Blocked != null)) throw Error("ARRASTRE_CONFLICTO");
        var today = WeekContract.LocalToday(clock.UtcNow);
        var window = PostponementWindow.For(today, beforeWeekday);
        var (ledger, days, context) = await PostponementContext(person, selected, window.From, window.Through, ct);
        var conflicts = new List<string>();
        if ((await ImmediateSupervisors(person, ct)).Length == 0) conflicts.Add("No hay un supervisor inmediato activo para resolver esta petición.");
        var (items, loads, failures) = PostponementAllocator.Calculate(rows.Select(r => r!.Assignment).ToArray(), ledger, days);
        if (window.BeforeDate != null) items = items.Select(i => i with { BeforeDate = window.BeforeDate }).ToArray();
        conflicts.AddRange(failures);
        var immediate = await ImmediateSupervisors(person, ct);
        var authorized = false;
        foreach (var supervisor in immediate) {
            var supervised = (await supervision.ListAsync(supervisor, ct)).Select(t => t.Assignment.Id).ToHashSet();
            if (selected.All(supervised.Contains)) { authorized = true; break; }
        }
        if (!authorized && immediate.Length > 0) conflicts.Add("El supervisor inmediato no está autorizado sobre todas las tareas seleccionadas.");
        var fingerprint = Hash(new { person, cause, explanation, context, items, loads, conflicts });
        return new(window.From, window.Through, items, loads, conflicts.ToArray(), fingerprint, context) { BeforeDate = window.BeforeDate };
    }

    private async Task<(CapacityLedger Ledger, PostponementDay[] Days, string Fingerprint)> PostponementContext(Guid person, Guid[] selected, DateOnly from, DateOnly through, CancellationToken ct)
    {
        var source = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
        var ledger = new CapacityLedger(source.Workers, source.Tasks.Where(t => !selected.Contains(t.Id) && t.ReviewStatus is not ("REASIGNADA" or "NO_SATISFECHA" or "CONCLUIDA")).ToArray(), source.Capacities, source.Unavailable, source.Legacy, source.Today) { Temporal = source.Temporal };
        var earlier = await db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r => r.PersonId == person && r.BranchId == BranchScope.LorettaId && r.Kind == "POSPONER" && r.State == "APROBADA").OrderBy(r => r.SubmittedAt).ThenBy(r => r.Id).ToArrayAsync(ct);
        var origins = earlier.SelectMany(r => r.Items().Where(i => r.Results().Any(x => x.AssignmentId == i.AssignmentId && x.Kind == "POSPOSICION")))
            .GroupBy(i => i.AssignmentId).ToDictionary(g => g.Key, g => g.First().Start);
        var postponedIds = origins.Keys.ToArray();
        DateOnly ReferenceDay(OperationalAssignment a) => WeekContract.LocalToday(origins.GetValueOrDefault(a.Id, a.StartsAt));
        // The immediately preceding equivalent weekday supplies a forecast only where no assigned weekly plan exists.
        // Read completed rows too: historical demand is scheduled work, not today's outstanding work.
        var first = OperationalSchedule.Instant(from.AddDays(-7)); var end = OperationalSchedule.Instant(through.AddDays(-6));
        var history = await db.OperationalAssignments.AsNoTracking().Where(a => a.PersonId == person && (a.StartsAt >= first && a.StartsAt < end || postponedIds.Contains(a.Id)) &&
            a.PeriodType == "SEMANAL" && a.GoalUnits == 0 && a.ReviewStatus != "REASIGNADA" &&
            db.DirectionPreparedPlans.Any(p => p.Id == a.PlanId && p.BranchId == BranchScope.LorettaId)).OrderBy(a => a.Id).ToArrayAsync(ct);
        var referenceFrom = from.AddDays(-7);
        var plans = await db.DirectionPreparedPlans.AsNoTracking().Where(p => p.BranchId == BranchScope.LorettaId && p.PeriodType == "SEMANAL" && p.Status == "ASIGNADO" && p.StartsOn <= through && p.EndsOn >= referenceFrom)
            .OrderBy(p => p.Id).Select(p => new { p.Id, p.StartsOn, p.EndsOn, p.IsWeeklyOperationsPlan }).ToArrayAsync(ct);
        var days = CapacityLedger.Days(from, through).Select(day => {
            var actual = ledger.DayLoad(person, day);
            var hasPlan = plans.Any(p => p.IsWeeklyOperationsPlan && p.StartsOn <= day && p.EndsOn >= day);
            var reference = day.AddDays(-7);
            var historical = history.Where(a => ReferenceDay(a) == reference).Sum(a => (decimal)a.LoadMinutes);
            // Only weekly demand is substituted; monthly, occasional and process reservations remain additive.
            // Approved backlog is extra demand, never a substitute for the next ordinary weekly occurrence.
            var weekly = ledger.Tasks.Where(a => a.PersonId == person && a.PeriodType == "SEMANAL" && a.GoalUnits == 0 && a.OccasionId == null && !origins.ContainsKey(a.Id)).Sum(a => ledger.Footprint(a).Where(f => f.Day == day).Sum(f => f.Minutes));
            return new PostponementDay(day, actual, hasPlan ? 0 : Math.Max(0, historical - weekly), 0, ledger.DayCapacity(person, day), hasPlan ? null : reference)
                { ReferenceKnown = hasPlan || history.Any(a => ReferenceDay(a) == reference) || plans.Any(p => p.StartsOn <= reference && p.EndsOn >= reference) };
        }).ToArray();
        var fingerprint = Hash(new { person, from, through, history = history.Select(a => new { a.Id, a.TaskId, Day = ReferenceDay(a), a.LoadMinutes }), origins = origins.OrderBy(x => x.Key), plans, days,
            source.Workers, ledger.Tasks, source.Capacities, source.Temporal,
            unavailable = source.Unavailable.OrderBy(x => x.Item1).ThenBy(x => x.Item2), legacy = source.Legacy.OrderBy(x => x.Key) });
        return (ledger, days, fingerprint);
    }

    public async Task<Guid> SubmitPostponementAsync(Guid actor, Guid[] selected, string cause, string? explanation, string fingerprint, Guid intent, CancellationToken ct, int? beforeWeekday = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await OperationalTaskService.LockAsync(db, ct);
        var person = await Person(actor, ct);
        var old = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleOrDefaultAsync(r => r.IntentKey == intent, ct);
        if (old != null) {
            if (old.RequestedBy != actor || old.Kind != "POSPONER" || old.Fingerprint != fingerprint) throw Error("ARRASTRE_CONFLICTO");
            await tx.CommitAsync(ct); return old.Id;
        }
        if (intent == Guid.Empty) throw Error("ARRASTRE_DATOS_INVALIDOS", 422);
        var preview = await PreviewPostponementAsync(actor, selected, cause, explanation, ct, beforeWeekday);
        if (preview.Conflicts.Length > 0 || preview.Fingerprint != fingerprint) throw Error("ARRASTRE_CONFLICTO");
        var r = new OperationalCarryoverRequest { Id = Guid.CreateVersion7(), Kind = "POSPONER", AlgorithmVersion = preview.BeforeDate == null ? 2 : 3,
            BranchId = BranchScope.LorettaId, PersonId = person, RequestedBy = actor, IntentKey = intent,
            Cause = cause, CauseDescription = CarryoverCauses.All[cause], Explanation = explanation!.Trim(),
            From = preview.From, Through = preview.Through, Minutes = preview.Items.Sum(i => i.WorkMinutes),
            IncidentStart = clock.UtcNow, IncidentEnd = clock.UtcNow, SubmittedAt = clock.UtcNow,
            Fingerprint = fingerprint, ScheduleFingerprint = preview.ContextFingerprint, SnapshotJson = JsonSerializer.Serialize(preview.Items) };
        db.Add(r); Audit(actor, r, "OPERATIONAL_POSTPONEMENT_SUBMITTED");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return r.Id;
    }

    private async Task<CarryoverResult[]> ApplyPostponement(OperationalCarryoverRequest request, CancellationToken ct)
    {
        await Person(request.RequestedBy, ct);
        var items = request.Items(); var ids = items.Select(i => i.AssignmentId).ToArray();
        var (ledger, days, fingerprint) = await PostponementContext(request.PersonId, ids, request.From!.Value, request.Through!.Value, ct);
        if (fingerprint != request.ScheduleFingerprint) throw Error("ARRASTRE_CONFLICTO");
        var current = await db.OperationalAssignments.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        // Validate all records before touching any tracked assignment.
        var placements = new List<(OperationalAssignment Row, OperationalAssignment Proposed, CarryoverItem Item)>();
        foreach (var item in items) {
            if (!current.TryGetValue(item.AssignmentId, out var row) || PostponementAllocator.Structure(row) != item.Structure || row.ReviewStatus == "REASIGNADA") throw Error("ARRASTRE_CONFLICTO");
            if (row.CompletedAt != null) continue;
            if (row.ReviewStatus is not (null or "PENDIENTE") || item.ProposedStart == null || item.ProposedStart <= clock.UtcNow || row.RemainingMinutes != item.WorkMinutes ||
                WeekContract.LocalToday(item.ProposedStart.Value) < request.From || WeekContract.LocalToday(item.Proposed) > request.Through ||
                item.BeforeDate is {} before && item.Proposed >= OperationalSchedule.Instant(before)) throw Error("ARRASTRE_CONFLICTO");
            var proposed = PostponementAllocator.Copy(row);
            proposed.StartsAt = item.ProposedStart.Value; proposed.DueAt = item.Proposed; proposed.NominalStartsAt = proposed.StartsAt;
            if (!ledger.CanAssign(row.PersonId, proposed)) throw Error("ARRASTRE_CONFLICTO");
            ledger.CaptureTime(proposed); ledger = ledger.WithAdded(proposed);
            placements.Add((row, proposed, item));
        }
        foreach (var (row, proposed, _) in placements) {
            row.StartsAt = proposed.StartsAt; row.DueAt = proposed.DueAt; row.NominalStartsAt = proposed.NominalStartsAt;
            row.TemporalDecisionJson = proposed.TemporalDecisionJson; row.Revision++;
        }
        return items.Select(i => { var row = current[i.AssignmentId]; return new CarryoverResult(row.Id, i.Due, row.DueAt, row.CompletedAt == null ? i.Proposed : i.Due, row.CompletedAt,
            row.CompletedAt == null ? "POSPOSICION" : "ENTREGADA_SIN_CAMBIOS"); }).ToArray();
    }
}

public static class PostponementAllocator
{
    public static string Structure(OperationalAssignment row) => JsonSerializer.Serialize(new { Original = OperationalCarryoverCalculator.Structure(row), row.NominalStartsAt, row.ClosureMarginPercent,
        row.TransferRemainingMinutes, row.TransferProgressUnits, row.GoalUnits, row.ProgressUnits, row.ScheduleKey });
    public static OperationalAssignment Copy(OperationalAssignment row) => JsonSerializer.Deserialize<OperationalAssignment>(JsonSerializer.Serialize(row))!;
    public static (CarryoverItem[] Items, PostponementDay[] Days, string[] Conflicts) Calculate(OperationalAssignment[] rows, CapacityLedger ledger, PostponementDay[] days)
    {
        var loads = days.ToDictionary(d => d.Day); var items = new List<CarryoverItem>(); var conflicts = new List<string>();
        foreach (var row in rows.OrderBy(r => OperationalAgenda.Priority(r.OperationalCriticality)).ThenByDescending(r => r.RemainingMinutes).ThenBy(r => r.DueAt).ThenBy(r => r.Id))
        {
            var choices = new List<(OperationalAssignment Row, PostponementDay Day)>();
            foreach (var day in loads.Values.Where(d => d.ReferenceKnown && d.Capacity > 0 && d.Actual + d.Forecast + d.Added + row.RemainingMinutes <= d.Capacity)) {
                var next = Copy(row); var local = TimeZoneInfo.ConvertTime(row.NominalStartsAt ?? row.StartsAt, OperationalSchedule.Zone);
                var nominal = TemporalEligibilityContext.At(day.Day, row.OperationalCriticality == "CRITICA" ? local.Hour * 60 + local.Minute : 0);
                next.NominalStartsAt = nominal; next.StartsAt = nominal;
                var placement = OperationalAgenda.Find(ledger, row.PersonId, next, OperationalSchedule.Instant(day.Day));
                if (!placement.Eligible) continue;
                OperationalAgenda.Apply(next, placement);
                if (ledger.CanAssign(row.PersonId, next)) choices.Add((next, day));
            }
            var chosen = choices.OrderBy(c => (c.Day.Actual + c.Day.Forecast + c.Day.Added + row.RemainingMinutes) / c.Day.Capacity).ThenBy(c => c.Day.Day).FirstOrDefault();
            if (chosen.Row == null) { conflicts.Add($"{row.TaskCode}: no se encontró un espacio verificable dentro de los días permitidos. No se enviará una petición parcial."); continue; }
            var d = chosen.Day; var nextRow = chosen.Row;
            var summary = $"Carga programada {d.Actual:0.#} min; estimada adicional {d.Forecast:0.#} min; pospuestas {d.Added + row.RemainingMinutes:0.#} min; capacidad {d.Capacity:0.#} min." +
                (d.Reference is {} reference ? $" Referencia: {reference:dd/MM/yyyy}." : " Plan semanal existente.");
            items.Add(new(row.Id, row.PersonId, row.PlanId, row.Revision, row.TaskCode + " · " + row.Name, row.AssignedRole, row.StartsAt, row.DueAt, nextRow.DueAt, true,
                row.Id, row.ReviewStatus, row.OperationalCriticality == "CRITICA" ? "Crítica: requiere autorización expresa; se conserva el vencimiento anterior en el historial." : "", Structure(row), row.PersonName)
                { ProposedStart = nextRow.StartsAt, WorkMinutes = row.RemainingMinutes, LoadSummary = summary });
            ledger = ledger.WithAdded(nextRow); loads[d.Day] = d with { Added = d.Added + row.RemainingMinutes };
        }
        return (items.ToArray(), days.Select(d => loads[d.Day]).ToArray(), conflicts.ToArray());
    }
}
