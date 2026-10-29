using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalProcessService
{
    private async Task<OperationalSequenceStage?> ApplyItemCount(OperationalSequenceRun run, OperationalSequenceStage source,
        OperationalSequenceAttempt attempt, Guid actor, int? quantity, string? resolution, CancellationToken ct)
    {
        if (source.OutputItemRateId == null) return null;
        var minutes = ProcessItemRules.Calculate(quantity, source.OutputItemMinutes!.Value);
        attempt.ItemQuantity = quantity; attempt.CapacityResolution = resolution;
        var stages = await db.Set<OperationalSequenceStage>().Where(s => s.RunId == run.Id && s.Position > source.Position).OrderBy(s => s.Position).ToArrayAsync(ct);
        if (quantity == 0) {
            var audit = stages.SingleOrDefault(s => s.IsFinalAudit) ?? throw Error("PROCESS_ITEM_AUDIT_REQUIRED");
            foreach (var omitted in stages.Where(s => s.Position < audit.Position)) {
                omitted.Status = "OMITIDA"; omitted.OmittedByAttemptId = attempt.Id; omitted.Revision++;
                omitted.DependencySatisfiedAt = null; omitted.DependencyEventId = null;
                omitted.EnabledAt = null; omitted.EffectiveDueAt = null; omitted.EligibleAt = null;
                omitted.BlockReason = $"Omitida por conteo cero en {source.TaskCode}, intento {attempt.Number}. No ejecutada.";
            }
            attempt.OmittedStagesJson = JsonSerializer.Serialize(stages.Where(s => s.Position < audit.Position).Select(s => new { s.Id, s.Position, s.TaskCode, s.Name }));
            // The audit keeps its own workload, even when it is the immediate next stage.
            audit.LoadMinutes = audit.EstimatedLoadMinutes ?? audit.LoadMinutes;
            audit.CalculatedLoadMinutes = null; audit.LoadSourceAttemptId = null;
            return audit;
        }
        var next = stages.First();
        next.EstimatedLoadMinutes ??= next.LoadMinutes;
        next.CalculatedLoadMinutes = minutes; next.LoadSourceAttemptId = attempt.Id;
        if (minutes <= 4800) next.LoadMinutes = checked((int)minutes);
        next.Revision++;
        return next;
    }

    private async Task EnableItemRecipient(OperationalSequenceRun run, OperationalSequenceStage source, OperationalSequenceStage next,
        OperationalSequenceAttempt sourceAttempt, Guid actor, string? resolution, string? expectedFingerprint, CancellationToken ct)
    {
        if (source.OutputItemRateId == null) { await EnableActive(run, ct); return; }
        var inputs = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
        var ownStages = await db.Set<OperationalSequenceStage>().Where(s => s.RunId == run.Id).Select(s => s.Id).ToArrayAsync(ct);
        var ownReservations = await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s=>s.RunId==run.Id && s.Position>source.Position).OrderBy(s=>s.Position)
            .Select(s=>new {s.Id,s.Position,s.ReservedPersonId,s.Role,s.SpecializationId,s.LoadMinutes,s.NominalStartsAt,s.PlannedStartsAt,s.PlannedDueAt,s.MinimumStartsAt,s.OperationalCriticality,s.Revision}).ToArrayAsync(ct);
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            ownReservations, next.Id, next.LoadSourceAttemptId, next.CalculatedLoadMinutes, sourceAttempt.ItemQuantity, source.OutputItemRateId,
            Peers = inputs.Tasks.Where(t => !ownStages.Contains(t.Id) && t.Id != sourceAttempt.AssignmentId),
            Minute=clock.UtcNow.ToUnixTimeSeconds()/60, inputs.Workers, inputs.Capacities, inputs.Temporal,
            Unavailable=inputs.Unavailable.OrderBy(x=>x.Item1).ThenBy(x=>x.Item2).Select(x=>new{x.Item1,x.Item2}), Legacy=inputs.Legacy.OrderBy(x=>x.Key)
        })));
        DirectionPlanException Conflict(string reason, bool? allowSearch = null) {
            var error=ItemConflict(next,reason); error.Data["CapacityFingerprint"]=fingerprint;
            var permitted=ProcessItemRules.CanResolve(source.Role);
            var proposed=Reservation(next,run.PlanId); proposed.StartsAt=new[]{clock.UtcNow,next.DependencySatisfiedAt!.Value,next.MinimumStartsAt??clock.UtcNow}.Max();
            var criticalDelay=next.OperationalCriticality=="CRITICA" && next.NominalStartsAt<proposed.StartsAt;
            var programmable=next.CalculatedLoadMinutes is null or <=4800;
            error.Data["CanSearch"]=permitted && programmable && !criticalDelay && allowSearch!=false;
            error.Data["CanForce"]=false;
            if (permitted && programmable && !criticalDelay) {
                if (next.OperationalCriticality=="CRITICA") proposed.StartsAt=next.NominalStartsAt!.Value;
                proposed.DueAt=proposed.StartsAt.AddMinutes(OperationalAgenda.Duration(next.LoadMinutes,next.ClosureMarginPercent??0));
                var forced=ForceWindow(inputs,proposed);
                var overlapsAnchor=ownReservations.Any(s=>s.Position>next.Position && s.OperationalCriticality=="CRITICA" && proposed.DueAt>s.NominalStartsAt) || next.FixedDueAt is {} fixedDue && proposed.DueAt>fixedDue;
                error.Data["CanForce"]=forced.Eligible && !overlapsAnchor;
                if (forced.Eligible && !overlapsAnchor) error.Data["ItemConflict"] += $" Si fuerzas ahora: {proposed.StartsAt.ToOffset(TimeSpan.FromHours(-6)):HH:mm}–{proposed.DueAt.ToOffset(TimeSpan.FromHours(-6)):HH:mm}. {forced.Description}";
                else error.Data["ItemConflict"] += overlapsAnchor ? " La excepción invadiría un ancla crítica posterior." : " La excepción tampoco es válida: "+TemporalEligibilityContext.Message(forced.Reason);
            }
            return error;
        }
        if (!string.IsNullOrEmpty(resolution) && expectedFingerprint != fingerprint) throw Conflict("La agenda cambió desde el aviso o falta su revisión. Revisa las opciones y confirma de nuevo.");
        await EnableActive(run, ct);
        if (next.Status != "PENDIENTE_ASIGNACION" && next.Status != "FUERA_DE_PLAZO") return;
        if (resolution == "PENDING") return;
        if (resolution is "LOWEST" or "FORCE") {
            if (!ProcessItemRules.CanResolve(source.Role)) throw Error("ACCESO_DENEGADO", 403);
            if (next.CalculatedLoadMinutes > 4800) throw Conflict( "La demanda excede 4800 min. Guarda pendiente para resolver la planificación.");
            var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
            var downstream = await db.Set<OperationalSequenceStage>().Where(s => s.RunId == run.Id && s.Position >= next.Position && s.Status != "OMITIDA").ToArrayAsync(ct);
            var removed = downstream.Where(s => s.OperationalCriticality != "CRITICA").Select(s => s.Id).Append(next.Id).ToHashSet();
            ledger = new(ledger.Workers, ledger.Tasks.Where(t => !removed.Contains(t.Id)).ToArray(), ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today) { Temporal = ledger.Temporal };
            if (resolution == "LOWEST") {
                var day = WeekContract.LocalToday(next.NominalStartsAt ?? next.PlannedStartsAt);
                foreach (var candidate in ledger.Workers.Where(w => w.EligibleFor(next.Role, next.SpecializationId))
                    .OrderBy(w => ledger.Occupancy(w.Id, day, day)).ThenBy(w => ledger.Load(w.Id, day, day)).ThenBy(w => w.Code, StringComparer.Ordinal)) {
                    var previous = next.ReservedPersonId; var previousName = next.ReservedPersonName;
                    next.ReservedPersonId = candidate.Id; next.ReservedPersonName = candidate.Name;
                    await EnableActive(run, ct);
                    if (next.Status is "DISPONIBLE" or "PROGRAMADA") {
                        db.Add(Event(run,next,await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==next.CurrentAttemptId,ct),"ASIGNACION",$"item-lowest:{sourceAttempt.Id:N}",actor,clock.UtcNow,null,$"Persona compatible de menor carga: {candidate.Name}; origen {source.TaskCode}."));
                        return;
                    }
                    next.ReservedPersonId = previous; next.ReservedPersonName = previousName;
                }
                throw Conflict("No hay una persona compatible con un hueco y capacidad normales. Guarda pendiente o revisa la excepción permitida.",false);
            }
            var assignment = Reservation(next, run.PlanId);
            var start = new[] { clock.UtcNow, next.DependencySatisfiedAt!.Value, next.MinimumStartsAt ?? clock.UtcNow }.Max();
            if (next.OperationalCriticality == "CRITICA") {
                if (next.NominalStartsAt < start) throw Conflict( "La dependencia llegó después del ancla crítica; requiere resolución de planificación.");
                start = next.NominalStartsAt!.Value;
            }
            assignment.StartsAt = start;
            assignment.DueAt = start.AddMinutes(OperationalAgenda.Duration(next.LoadMinutes, next.ClosureMarginPercent ?? 0));
            if(next.FixedDueAt is {} fixedDue && assignment.DueAt>fixedDue) throw Conflict("El intervalo excede el límite fijo de la etapa receptora.");
            if (downstream.Any(s => s.Position > next.Position && s.OperationalCriticality == "CRITICA" && assignment.DueAt > s.NominalStartsAt))
                throw Conflict( "El intervalo invade el ancla crítica de una etapa posterior.");
            var exception = ForceWindow(ledger, assignment);
            if (!exception.Eligible) throw Conflict( TemporalEligibilityContext.Message(exception.Reason));
            var attempt = await db.Set<OperationalSequenceAttempt>().SingleAsync(a => a.Id == next.CurrentAttemptId, ct);
            assignment.Id = Guid.CreateVersion7(); assignment.TemporalDecisionJson = exception.Json;
            attempt.CapacityExceptionJson = exception.Json; attempt.CapacityResolution = "FORCE";
            attempt.AssignmentId = assignment.Id; attempt.Status = "ACTIVO"; attempt.Revision++;
            next.Status = "DISPONIBLE"; next.EnabledAt = assignment.StartsAt; next.EffectiveDueAt = assignment.DueAt;
            next.PlannedStartsAt = assignment.StartsAt; next.PlannedDueAt = assignment.DueAt; next.EligibleAt = start; next.BlockReason = null; next.Revision++;
            await RestoreContinuation(attempt, assignment, ct);
            db.Add(assignment);
            db.Add(Event(run, next, attempt, "ASIGNACION", $"item-force:{sourceAttempt.Id:N}", actor, clock.UtcNow, attempt.InputDocumentId,
                $"Asignación excepcional autorizada desde {source.TaskCode}. {exception.Description}"));
            return;
        }
        throw Conflict( next.BlockReason ?? "No hay capacidad para la siguiente etapa.");
    }

    private static DirectionPlanException ItemConflict(OperationalSequenceStage stage, string reason)
    {
        var e = Error("PROCESS_ITEM_CAPACITY", 409);
        e.Data["ItemConflict"] = $"{stage.ReservedPersonName} · {stage.TaskCode} · {stage.Name}: {stage.CalculatedLoadMinutes ?? stage.LoadMinutes} min. {reason}";
        return e;
    }

    internal static (bool Eligible, string Reason, string? Json, string? Description) ForceWindow(CapacityLedger ledger, OperationalAssignment task)
    {
        var worker = ledger.Workers.SingleOrDefault(w => w.Id == task.PersonId && w.EligibleFor(task.AssignedRole, task.SpecializationId));
        if (worker == null) return (false, "SCHEDULE_ACCOUNT_INACTIVE", null, null);
        if (worker.EmploymentEnd < task.DueAt || worker.RoleEnd < task.DueAt) return (false, "SCHEDULE_EMPLOYMENT_END", null, null);
        var day = WeekContract.LocalToday(task.StartsAt);
        if (WeekContract.LocalToday(task.DueAt.AddTicks(-1)) != day || ledger.Unavailable.Contains((task.PersonId, day))) return (false, "SCHEDULE_ABSENT", null, null);
        var effective = ledger.Temporal.Day(task.PersonId, day, ledger.Temporal.UsesCoverageSchedule(task));
        var intervals = effective.Intervals?.OrderBy(i => i.Entry).ToArray();
        if (intervals == null) return (false, "SCHEDULE_MISSING", null, null);
        if (intervals.Length == 0) return (false, "SCHEDULE_ABSENT", null, null);
        if (task.StartsAt < TemporalEligibilityContext.At(day, intervals.First().Entry) || task.DueAt > TemporalEligibilityContext.At(day, intervals.Last().Exit))
            return (false, "SCHEDULING_NO_CONTIGUOUS_SLOT", null, null);
        var breaks = intervals.Zip(intervals.Skip(1)).Select(p => new { From = TemporalEligibilityContext.At(day, p.First.Exit), Through = TemporalEligibilityContext.At(day, p.Second.Entry) })
            .Where(b => b.From < task.DueAt && b.Through > task.StartsAt).Select(b => new { From = new[] { b.From, task.StartsAt }.Max(), Through = new[] { b.Through, task.DueAt }.Min() }).ToArray();
        var text = breaks.Length == 0 ? "Sobrecarga/solapamiento explícitos, sin descanso afectado." : "Descanso utilizado: " + string.Join(", ", breaks.Select(b => $"{b.From.ToOffset(TimeSpan.FromHours(-6)):HH:mm}–{b.Through.ToOffset(TimeSpan.FromHours(-6)):HH:mm}"));
        return (true, "ELIGIBLE", JsonSerializer.Serialize(new { Kind = "PROCESS_ITEM_FORCE", task.StartsAt, task.DueAt, effective.Schedule?.Id, ExceptionId = effective.Exception?.Id, Breaks = breaks, Description = text }), text);
    }

    private async Task RestoreContinuation(OperationalSequenceAttempt attempt, OperationalAssignment assignment, CancellationToken ct)
    {
        var previous = attempt.ContinuationAttemptId;
        while (previous is {} id) {
            var old = await db.Set<OperationalSequenceAttempt>().AsNoTracking().SingleAsync(a => a.Id == id && a.RunId == attempt.RunId && a.StageId == attempt.StageId, ct);
            if (old.AssignmentId is {} oldId) {
                var work = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == oldId, ct);
                assignment.ChecklistDraftJson = OperationalChecklist.Reconcile(work.ChecklistJson, assignment.ChecklistJson, work.ChecklistDraftJson);
                assignment.Comment = work.Comment;
                return;
            }
            previous = old.ContinuationAttemptId;
        }
    }
}
