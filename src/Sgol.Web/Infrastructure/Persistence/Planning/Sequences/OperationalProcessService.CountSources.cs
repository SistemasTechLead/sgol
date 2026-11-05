using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalProcessService
{
    private async Task<string> SelectedCountFingerprint(OperationalSequenceRun run, OperationalSequenceAttempt attempt, int? quantity, CancellationToken ct)
    {
        var stages = await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s => s.RunId == run.Id).OrderBy(s => s.Position).ToArrayAsync(ct);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            RunId = run.Id, RunRevision = run.Revision, stages, AttemptId = attempt.Id, AttemptRevision = attempt.Revision, quantity,
            Tasks = ledger.Tasks.OrderBy(t => t.Id), Workers = ledger.Workers.OrderBy(w => w.Id), ledger.Capacities, ledger.Temporal,
            Unavailable = ledger.Unavailable.OrderBy(p => p.Item1).ThenBy(p => p.Item2).Select(p => new { p.Item1, p.Item2 }),
            Minute = clock.UtcNow.ToUnixTimeSeconds() / 60
        })));
    }
    private async Task ReturnSelectedCount(OperationalSequenceRun run, ProcessRunView view, OperationalSequenceAttempt producer,
        Guid actor, Guid key, string reason, CancellationToken ct)
    {
        var source = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == producer.StageId, ct);
        var old = await db.Set<OperationalSequenceAttempt>().SingleAsync(a => a.Id == producer.Id, ct);
        var paused = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.RunId == run.Id && s.Position == run.ActivePosition, ct);
        paused.Status = "EN_ESPERA"; paused.BlockReason = $"Espera la corrección del conteo de {source.TaskCode}; su trabajo se conserva."; paused.Revision++;
        // Keep completed receipts and consumers intact until the producer's corrected count is reviewed.
        var next = NewAttempt(source, run, (await db.Set<OperationalSequenceAttempt>().Where(a => a.StageId == source.Id).MaxAsync(a => a.Number, ct)) + 1, old.InputDocumentId, clock.UtcNow);
        next.ContinuationAttemptId = old.Id; next.UnresolvedDifferences = old.UnresolvedDifferences;
        db.Add(next); source.CurrentAttemptId = next.Id; source.Status = "EN_ESPERA";
        source.EnabledAt = null; source.EffectiveDueAt = null; source.EligibleAt = null; source.BlockReason = null;
        source.Revision++; RebaseReturnedAgenda(source, clock.UtcNow);
        var evt = Event(run, source, next, "INICIO", $"return-selected-count:{key:N}", actor, clock.UtcNow, old.InputDocumentId, reason);
        db.Add(evt); source.DependencySatisfiedAt = clock.UtcNow; source.DependencyEventId = evt.Id;
        run.ActivePosition = source.Position; run.Round++; run.Revision++;
        await db.SaveChangesAsync(ct); await EnableActive(run, ct);
    }

    private async Task<bool> ReviewSelectedCorrection(OperationalSequenceRun run, OperationalSequenceStage source,
        OperationalSequenceAttempt attempt, Guid actor, int? quantity, string? resolution, string? fingerprint, CancellationToken ct)
    {
        if (run.CountSemanticsVersion < ProcessCountSources.CurrentVersion || source.OutputItemRateId == null || attempt.ContinuationAttemptId == null) return false;
        if (!await db.Set<OperationalSequenceAttempt>().AnyAsync(a => a.StageId == source.Id && a.ItemQuantity != null, ct)) return false;
        var stages = await db.Set<OperationalSequenceStage>().Where(s => s.RunId == run.Id).OrderBy(s => s.Position).ToArrayAsync(ct);
        var affected = new HashSet<Guid> { source.TaskId };
        foreach (var stage in stages.Where(s => s.Position > source.Position))
            if (stage.InputCountSourceTaskId is {} origin && affected.Contains(origin)) affected.Add(stage.TaskId);
        var recipients = stages.Where(s => s.Id != source.Id && affected.Contains(s.TaskId)).ToArray();
        var attempts = await db.Set<OperationalSequenceAttempt>().Where(a => a.RunId == run.Id).OrderBy(a => a.Id).ToArrayAsync(ct);
        var assignments = await db.OperationalAssignments.AsNoTracking().Where(a => attempts.Select(t => t.AssignmentId).Contains(a.Id)).OrderBy(a => a.Id).ToArrayAsync(ct);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
        var expected = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            run, stages, attempts, assignments, quantity, ledger.Tasks, ledger.Workers, ledger.Capacities, ledger.Temporal,
            Minute = clock.UtcNow.ToUnixTimeSeconds() / 60
        })));
        var lines = recipients.Select(s => {
            var current = attempts.Single(a => a.Id == s.CurrentAttemptId);
            var load = s.InputCountSourceTaskId == source.TaskId ? quantity == 0 ? "omisión por cero" : $"{ProcessItemRules.Calculate(quantity, source.OutputItemMinutes!.Value)} min" : "pendiente del nuevo conteo de su origen";
            return $"Etapa {s.Position} · {s.TaskCode} · {s.Name} · {s.ReservedPersonName}: {s.Status}, {s.CalculatedLoadMinutes ?? s.LoadMinutes} min → {load}. " +
                (current.AssignmentId != null ? "Se conserva el intento y su evidencia; se creará una continuación." : "Se recalculará su reserva sin registrar trabajo ficticio.");
        }).ToArray();
        if (resolution != "PENDING" || fingerprint != expected)
        {
            var error = Error("PROCESS_COUNT_IMPACT_REQUIRED", 409);
            error.Data["ItemConflict"] = "Revisa el impacto del conteo corregido antes de confirmar. " + string.Join(" ", lines) +
                " Las etapas independientes conservan sus intentos, cargas y entregas. Los conflictos de capacidad permanecerán pendientes, sin cambiar responsables.";
            error.Data["CapacityFingerprint"] = expected; error.Data["CanSearch"] = false; error.Data["CanForce"] = false;
            throw error;
        }
        foreach (var stage in recipients)
        {
            var previous = attempts.Single(a => a.Id == stage.CurrentAttemptId);
            if (previous.AssignmentId is {} assignmentId)
            {
                var work = await db.OperationalAssignments.SingleAsync(a => a.Id == assignmentId, ct);
                if (work.CompletedAt == null) { work.ReviewStatus = "NO_SATISFECHA"; work.ReviewDecision = "NO_SATISFECHA"; work.ReviewedBy = actor; work.ReviewedAt = clock.UtcNow; work.ReviewComment = "Continuación por corrección de conteo revisada."; work.Revision++; }
            }
            var next = NewAttempt(stage, run, attempts.Where(a => a.StageId == stage.Id).Max(a => a.Number) + 1, previous.InputDocumentId, clock.UtcNow);
            next.ContinuationAttemptId = previous.Id; next.UnresolvedDifferences = previous.UnresolvedDifferences;
            db.Add(next); stage.CurrentAttemptId = next.Id; stage.Status = "EN_ESPERA";
            stage.LoadMinutes = stage.EstimatedLoadMinutes ?? stage.LoadMinutes;
            stage.CalculatedLoadMinutes = null; stage.LoadSourceAttemptId = null; stage.OmittedByAttemptId = null;
            stage.DependencySatisfiedAt = null; stage.DependencyEventId = null; stage.EnabledAt = null; stage.EffectiveDueAt = null; stage.EligibleAt = null; stage.BlockReason = null;
            stage.Revision++; RebaseReturnedAgenda(stage, clock.UtcNow);
        }
        db.Add(Event(run, source, attempt, "DEVOLUCION", $"count-impact:{attempt.Id:N}", actor, clock.UtcNow, attempt.InputDocumentId,
            "Impacto revisado: " + string.Join(" ", lines)));
        return true;
    }

    private async Task<OperationalSequenceStage?> ApplySelectedCounts(OperationalSequenceRun run,
        OperationalSequenceStage source, OperationalSequenceAttempt attempt, int? quantity, string? resolution, CancellationToken ct)
    {
        var stages = await db.Set<OperationalSequenceStage>().Where(s => s.RunId == run.Id && s.Position > source.Position)
            .OrderBy(s => s.Position).ToArrayAsync(ct);
        if (source.OutputItemRateId != null)
        {
            var minutes = ProcessItemRules.Calculate(quantity, source.OutputItemMinutes!.Value);
            attempt.ItemQuantity = quantity; attempt.CapacityResolution = resolution;
            var recipients = stages.Where(s => s.InputCountSourceTaskId == source.TaskId).ToArray();
            foreach (var recipient in recipients)
            {
                if (recipient.Status is "CONCLUIDA" or "DISPONIBLE")
                    throw ProcessCountSources.Invalid($"{recipient.TaskCode}: tiene trabajo vigente. Revisa el impacto de la corrección antes de sustituir su conteo.");
                recipient.EstimatedLoadMinutes ??= recipient.LoadMinutes;
                recipient.Revision++;
                if (quantity == 0)
                {
                    recipient.Status = "OMITIDA"; recipient.OmittedByAttemptId = attempt.Id;
                    recipient.LoadMinutes = recipient.EstimatedLoadMinutes.Value;
                    recipient.CalculatedLoadMinutes = null; recipient.LoadSourceAttemptId = null;
                    recipient.DependencySatisfiedAt = null; recipient.DependencyEventId = null;
                    recipient.EnabledAt = null; recipient.EffectiveDueAt = null; recipient.EligibleAt = null;
                    recipient.BlockReason = $"Omitida por conteo cero de etapa {source.Position} · {source.TaskCode}, intento {attempt.Number}. No ejecutada.";
                }
                else
                {
                    recipient.CalculatedLoadMinutes = minutes; recipient.LoadSourceAttemptId = attempt.Id;
                    recipient.OmittedByAttemptId = null;
                    if (minutes <= 4800) recipient.LoadMinutes = checked((int)minutes);
                }
            }
            if (quantity == 0) attempt.OmittedStagesJson = JsonSerializer.Serialize(recipients.Select(s => new { s.Id, s.Position, s.TaskCode, s.Name }));
        }
        return stages.FirstOrDefault(s => s.Status is not ("OMITIDA" or "CONCLUIDA"));
    }

    private async Task<bool> HasSelectedCount(OperationalSequenceRun run, OperationalSequenceStage recipient, CancellationToken ct)
    {
        if (run.CountSemanticsVersion < ProcessCountSources.CurrentVersion || recipient.InputCountSourceTaskId == null) return true;
        var source = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.RunId == run.Id && s.TaskId == recipient.InputCountSourceTaskId, ct);
        var attempt = await db.Set<OperationalSequenceAttempt>().SingleAsync(a => a.Id == source.CurrentAttemptId, ct);
        if (source.Status == "CONCLUIDA" && attempt.ItemQuantity > 0 && recipient.LoadSourceAttemptId == attempt.Id) return true;
        recipient.Status = "PENDIENTE_ASIGNACION";
        recipient.BlockReason = $"{recipient.TaskCode}: falta conteo vigente de etapa {source.Position} · {source.TaskCode}. " +
            (source.Status == "OMITIDA" ? "El origen fue omitido; revisa el conteo que causó su omisión para resolver esta dependencia." : "Revisa y entrega el conteo del origen antes de asignar esta tarea.");
        return false;
    }

    private async Task EnableSelectedRecipients(OperationalSequenceRun run, OperationalSequenceStage source,
        OperationalSequenceAttempt attempt, string? resolution, string? expectedFingerprint, CancellationToken ct, bool correctionReviewed, string fingerprint)
    {
        var stages = await db.Set<OperationalSequenceStage>().Where(s => s.RunId == run.Id).OrderBy(s => s.Position).ToArrayAsync(ct);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
        DirectionPlanException Conflict(string detail)
        {
            var error = Error("PROCESS_ITEM_CAPACITY", 409);
            error.Data["ItemConflict"] = detail;
            error.Data["CapacityFingerprint"] = fingerprint;
            error.Data["CanSearch"] = false; error.Data["CanForce"] = false;
            return error;
        }
        if (!correctionReviewed && !string.IsNullOrEmpty(resolution) && expectedFingerprint != fingerprint)
            throw Conflict("Las reservas o los conteos cambiaron desde la revisión. Revisa nuevamente las etapas afectadas.");
        if (resolution is not (null or "" or "PENDING"))
            throw Conflict("Este plan conserva los responsables elegidos. Revisa la planificación o guarda la entrega con los conflictos pendientes.");
        var future = stages.Where(s => s.Position >= run.ActivePosition && s.Status is not ("OMITIDA" or "CONCLUIDA")).ToArray();
        var own = future.Select(s => s.Id).ToHashSet();
        ledger = new(ledger.Workers, ledger.Tasks.Where(t => !own.Contains(t.Id)).ToArray(), ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today) { Temporal = ledger.Temporal };
        var conflicts = new List<string>();
        var previousDue = clock.UtcNow;
        foreach (var stage in future)
        {
            var reservation = Reservation(stage, run.PlanId);
            var minimum = new[] { previousDue, stage.MinimumStartsAt ?? previousDue, stage.NominalStartsAt ?? previousDue }.Max();
            string? reason = null;
            if (stage.CalculatedLoadMinutes > 4800) reason = $"demanda {stage.CalculatedLoadMinutes} min superior al límite programable de 4800 min";
            else if (stage.OperationalCriticality == "CRITICA" && minimum > stage.NominalStartsAt) reason = "la dependencia invade su ancla crítica";
            else
            {
                var placement = OperationalAgenda.Find(ledger, reservation.PersonId, reservation, minimum);
                if (!placement.Eligible) reason = TemporalEligibilityContext.Message(placement.Reason);
                else
                {
                    OperationalAgenda.Apply(reservation, placement);
                    if (stage.FixedDueAt is {} due && reservation.DueAt > due) reason = "el bloque excede su plazo fijo";
                    else if (!ledger.CanAssign(reservation.PersonId, reservation, clock.UtcNow)) reason = "el responsable no tiene capacidad o disponibilidad para el bloque completo";
                }
            }
            if (reason != null)
            {
                stage.BlockReason = $"Etapa {stage.Position} · {stage.TaskCode} · {stage.ReservedPersonName}: {stage.CalculatedLoadMinutes ?? stage.LoadMinutes} min; {reason}. Revisa la planificación.";
                stage.Revision++; conflicts.Add(stage.BlockReason);
                // Preserve the remaining reservations; a conflict must not silently release another person's work.
            }
            else
            {
                stage.PlannedStartsAt = reservation.StartsAt; stage.PlannedDueAt = reservation.DueAt;
                stage.BlockReason = null; stage.Revision++;
            }
            ledger = ledger.WithAdded(reservation);
            previousDue = reservation.DueAt;
        }
        if (conflicts.Count > 0 && resolution != "PENDING") throw Conflict(string.Join(" ", conflicts));
        await EnableActive(run, ct);
    }
}
