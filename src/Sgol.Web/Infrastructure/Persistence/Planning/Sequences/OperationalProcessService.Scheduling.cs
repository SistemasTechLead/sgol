using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalProcessService
{
    private async Task EnableScheduledStage(OperationalSequenceRun run, OperationalSequenceStage stage, OperationalSequenceAttempt attempt, CapacityLedger ledger, CancellationToken token)
    {
        void Block(string status, string reason) { stage.Status = status; stage.BlockReason = reason; stage.Revision++; }
        if (stage.DependencySatisfiedAt is not {} dependency) { Block("EN_ESPERA", "Espera la etapa anterior"); return; }
        var nominal = stage.NominalStartsAt!.Value;
        var release = new[] { nominal, stage.MinimumStartsAt ?? nominal, dependency, stage.PlannedStartsAt }.Max();
        stage.EligibleAt = release;
        var critical = stage.OperationalCriticality == "CRITICA";
        if (critical && (dependency > nominal || (stage.MinimumStartsAt ?? nominal) > nominal)) { Block("PENDIENTE_ASIGNACION", "La dependencia llegó después del ancla crítica. Se conserva la hora; revisa el conflicto."); return; }
        if (critical && stage.PlannedDueAt <= clock.UtcNow) { Block("FUERA_DE_PLAZO", "El bloque crítico venció antes de habilitar la etapa; no se renovó el cierre."); return; }
        if (release > clock.UtcNow) { Block("PROGRAMADA", "Espera su activación reservada"); return; }
        var downstream = await db.Set<OperationalSequenceStage>().Where(s => s.RunId == run.Id && s.Position > stage.Position && s.Status == "EN_ESPERA").OrderBy(s => s.Position).ToArrayAsync(token);
        var released = downstream.Where(s => s.OperationalCriticality != "CRITICA").Select(s => s.Id).Append(stage.Id).ToHashSet();
        ledger = new(ledger.Workers, ledger.Tasks.Where(t => !released.Contains(t.Id)).ToArray(), ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today) { Temporal = ledger.Temporal };
        var assignment = Reservation(stage, run.PlanId);
        var options = new List<(CapacityWorker Worker, SchedulingPlacement Placement)>();
        var reason = "SCHEDULING_NO_CONTIGUOUS_SLOT";
        foreach (var worker in ledger.Workers.Where(w => w.EligibleFor(stage.Role, stage.SpecializationId) && (stage.ReservedPersonId == null || w.Id == stage.ReservedPersonId)))
        {
            var placed = OperationalAgenda.Find(ledger, worker.Id, assignment, critical ? nominal : new[] { release, clock.UtcNow }.Max());
            if (!placed.Eligible) { reason = placed.Reason; continue; }
            if (downstream.Any(s => s.OperationalCriticality == "CRITICA" && placed.DueAt > s.NominalStartsAt)) { reason = "SCHEDULING_CRITICAL_OVERLAP"; continue; }
            OperationalAgenda.Apply(assignment, placed);
            if (ledger.CanAssign(worker.Id, assignment, clock.UtcNow)) options.Add((worker, placed));
            else reason = !ledger.CanFit(worker.Id, assignment) ? "PLAN_CAPACITY_EXCEEDED" : ledger.CheckTime(worker.Id, assignment, clock.UtcNow).Reason;
        }
        var selected = options.OrderBy(o => o.Placement.StartsAt).ThenBy(o => o.Worker.Code, StringComparer.Ordinal).FirstOrDefault();
        if (selected.Worker == null) { Block("PENDIENTE_ASIGNACION", TemporalEligibilityContext.Message(reason)); return; }
        OperationalAgenda.Apply(assignment, selected.Placement);
        stage.PlannedStartsAt = assignment.StartsAt; stage.PlannedDueAt = assignment.DueAt;
        if (assignment.StartsAt > clock.UtcNow) { stage.EligibleAt = assignment.StartsAt; Block("PROGRAMADA", "Espera el hueco disponible del responsable"); return; }
        assignment.Id = Guid.CreateVersion7(); assignment.PersonId = selected.Worker.Id; assignment.PersonName = selected.Worker.Name; assignment.AssignedRole = selected.Worker.Role;
        ledger.CaptureTime(assignment, clock.UtcNow);
        db.Add(assignment); attempt.AssignmentId = assignment.Id; attempt.Status = "ACTIVO"; attempt.Revision++;
        stage.ReservedPersonId = assignment.PersonId; stage.ReservedPersonName = assignment.PersonName;
        stage.Status = "DISPONIBLE"; stage.EnabledAt = assignment.StartsAt; stage.EffectiveDueAt = assignment.DueAt; stage.BlockReason = null; stage.Revision++;
        db.Add(Event(run, stage, attempt, "HABILITACION", $"enable:{attempt.Id:N}", null, assignment.StartsAt, attempt.InputDocumentId, null));
        ledger = new(ledger.Workers, ledger.Tasks.Append(assignment).ToArray(), ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today) { Temporal = ledger.Temporal };
        var previousDue = assignment.DueAt;
        foreach (var next in downstream)
        {
            var reservation = Reservation(next, run.PlanId);
            var minimum = next.OperationalCriticality == "CRITICA" ? next.NominalStartsAt!.Value : new[] { previousDue, next.PlannedStartsAt, next.NominalStartsAt!.Value }.Max();
            var placed = OperationalAgenda.Find(ledger, reservation.PersonId, reservation, minimum);
            if (!placed.Eligible || next.OperationalCriticality == "CRITICA" && previousDue > next.NominalStartsAt) { next.BlockReason = "La dependencia o agenda ya no es compatible con esta reserva. Revisa el proceso."; next.Revision++; break; }
            OperationalAgenda.Apply(reservation, placed); next.PlannedStartsAt = reservation.StartsAt; next.PlannedDueAt = reservation.DueAt; next.Revision++;
            ledger = new(ledger.Workers, ledger.Tasks.Append(reservation).ToArray(), ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today) { Temporal = ledger.Temporal };
            previousDue = reservation.DueAt;
        }
    }
}
