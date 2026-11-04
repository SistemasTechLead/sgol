using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class OperationalIncidenceService(SgolDbContext db, IClock clock, OperationalSupervisionService supervision)
{
    static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    static OperationalAssignment Copy(OperationalAssignment a) => AssignmentDefinition.Copy(a);
    static DirectionPlanException Error(string message, int status = 409) => new(message, status);

    public async Task<CapacityWorker[]> PeopleAsync(Guid actor, DateOnly day, CancellationToken ct)
    {
        var role = await supervision.AuthorizeAsync(actor, ct);
        var own = await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync(ct);
        var at = day == WeekContract.LocalToday(clock.UtcNow) ? clock.UtcNow : OperationalSchedule.Instant(day);
        return (await OperationalCapacityService.WorkersAsync(db, at, ct))
            .GroupBy(w => w.Id).Where(g => g.Count() == 1).Select(g => g.Single())
            .Where(w => OperationalSupervisionService.RolesFor(role).Contains(w.Role) && (role == CanonicalRole.Direction || w.Id != own))
            .OrderBy(w => w.Name).ThenBy(w => w.Id).ToArray();
    }

    public async Task<OperationalIncidence[]> HistoryAsync(Guid actor, DateOnly day, CancellationToken ct)
    {
        var ids = (await PeopleAsync(actor, day, ct)).Select(w => w.Id).ToArray();
        var receipts = await db.Set<OperationalIncidence>().AsNoTracking().Where(i => i.Day == day && ids.Contains(i.PersonId)).OrderBy(i => i.At).ToArrayAsync(ct);
        if (await supervision.AuthorizeAsync(actor, ct) != CanonicalRole.Direction)
            foreach (var receipt in receipts)
            {
                var saved = JsonSerializer.Deserialize<IncidencePreview>(receipt.ReceiptJson)!;
                receipt.ReceiptJson = JsonSerializer.Serialize(saved with {
                    Request = saved.Request with { Decisions = [] },
                    Changes = saved.Changes.Where(c => ids.Contains(c.Before.PersonId) && (c.After == null || ids.Contains(c.After.PersonId))).ToArray(),
                    Balances = saved.Balances.Where(b => ids.Contains(b.PersonId)).ToArray()
                });
            }
        return receipts;
    }

    public async Task<IncidencePersonSummary[]> SummariesAsync(Guid actor, DateOnly day, CancellationToken ct)
    {
        var people = await PeopleAsync(actor, day, ct);
        var ledger = await OperationalCapacityService.LedgerAsync(db, day == WeekContract.LocalToday(clock.UtcNow) ? clock.UtcNow : OperationalSchedule.Instant(day), ct);
        return people.Select(p => new IncidencePersonSummary(p.Id, !ledger.Unavailable.Contains((p.Id, day)),
            ledger.Tasks.Count(a => a.PersonId == p.Id && OperationalSupervisionService.Transferable(a) && ledger.Footprint(a).Any(f => f.Day == day)),
            ledger.Tasks.Count(a => a.PersonId == p.Id && OperationalSupervisionService.Transferable(a) && a.DueAt <= clock.UtcNow && ledger.Footprint(a).Any(f => f.Day == day)),
            Pending(ledger, p.Id, day))).ToArray();
    }

    public async Task<IncidencePreview> PreviewAsync(Guid actor, IncidenceRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await OperationalTaskService.LockAsync(db, ct);
        var result = await BuildAsync(actor, request, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    async Task<IncidencePreview> BuildAsync(Guid actor, IncidenceRequest request, CancellationToken ct)
    {
        var now = clock.UtcNow; var today = WeekContract.LocalToday(now);
        if (request.Day < today || request.Day > today.AddDays(366) || request.Key == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 5 or > 1000 ||
            request.Decisions.Length > 1000 || request.Decisions.Select(d => d.AssignmentId).Distinct().Count() != request.Decisions.Length ||
            request.NotBefore.Offset != TimeSpan.Zero || request.NotBefore < OperationalSchedule.Instant(request.Day) ||
            request.NotBefore >= OperationalSchedule.Instant(request.Day.AddDays(1)))
            throw Error("Selecciona hoy o una fecha futura, un motivo de 5 a 1,000 caracteres y vuelve a revisar la propuesta.", 422);
        var people = await PeopleAsync(actor, request.Day, ct);
        var person = people.SingleOrDefault(w => w.Id == request.PersonId) ?? throw Error("ACCESO_DENEGADO", 403);
        if (await db.Set<OperationalIncidence>().AnyAsync(i => i.PersonId == person.Id && i.Day == request.Day, ct))
            throw Error($"{person.Name}: ya existe una ausencia confirmada para esta fecha. Consulta su recibo.");
        var source = await OperationalCapacityService.LedgerAsync(db, request.Day == today ? now : OperationalSchedule.Instant(request.Day), ct);
        var all = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync(ct);
        // Ledger omits elapsed completed slots; they still consume the daily operating budget.
        var byId = all.ToDictionary(a => a.Id);
        source = new(source.Workers, source.Tasks.Select(a => byId.GetValueOrDefault(a.Id) ?? a).Concat(all.Where(a => a.CompletedAt != null &&
            WeekContract.LocalToday(a.StartsAt) == request.Day)).DistinctBy(a => a.Id).ToArray(),
            source.Capacities, source.Unavailable, source.Legacy, source.Today) { Temporal = source.Temporal };
        var stages = await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s => db.Set<OperationalSequenceRun>().Any(r => r.Id == s.RunId && r.Status == "EN_CURSO")).OrderBy(s => s.RunId).ThenBy(s => s.Position).ToArrayAsync(ct);
        var attempts = await db.Set<OperationalSequenceAttempt>().AsNoTracking().OrderBy(a => a.Id).ToArrayAsync(ct);
        var prepared = await db.Set<PreparedProcessStart>().AsNoTracking().Where(s => s.Status == "PREPARADO" && s.IncidenceHoldId == null).OrderBy(s => s.Id).ToArrayAsync(ct);
        var preparedSteps = await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => prepared.Select(p => p.VersionId).Contains(s.VersionId)).OrderBy(s => s.Position).ToArrayAsync(ct);
        var preparedRows = prepared.ToDictionary(p => p.Id, p => p.AgendaJson != null ? JsonSerializer.Deserialize<OperationalAssignment[]>(p.AgendaJson)! :
            ProcessPlanningService.Reservations(p.Id, p.VersionId, p.StartsAt, JsonSerializer.Deserialize<Dictionary<int, Guid>>(p.Participants)!, preparedSteps.Where(s => s.VersionId == p.VersionId).ToArray()).ToArray());
        var carryovers = await db.Set<OperationalCarryoverRequest>().AsNoTracking().OrderBy(r => r.Id).ToArrayAsync(ct);
        var facts = await db.Set<TaskBonusFact>().AsNoTracking().OrderBy(f => f.RecordedAt).ThenBy(f => f.Revision).ToArrayAsync(ct);
        var transfers = await db.Set<OperationalTransfer>().AsNoTracking().OrderBy(t => t.At).ToArrayAsync(ct);
        var priorDecisions = await (from d in db.Set<OperationalIncidenceDisposition>().AsNoTracking()
            join i in db.Set<OperationalIncidence>().AsNoTracking() on d.IncidenceId equals i.Id
            where i.At <= now
            select new IncidenceBonusDecision(d.AssignmentId, d.ObligationId, d.StageId, d.PersonId, d.RecipientId, d.EvaluationDay, d.Action, i.At)).ToArrayAsync(ct);
        var obligations = TaskBonusService.Materialize(facts, transfers, carryovers, now, DateTimeOffset.MaxValue, priorDecisions);
        var availability = await db.AvailabilityDayVersions.AsNoTracking().Where(a => a.Status == AvailabilityVersionStatus.Current).OrderBy(a => a.Id).ToArrayAsync(ct);
        var closures = await db.Set<TaskBonusClosure>().AsNoTracking().OrderBy(c => c.Id).ToArrayAsync(ct);
        var evidence = await db.OperationalPhotographs.AsNoTracking().OrderBy(p => p.Id).Select(p => new { p.Id, p.AssignmentId, p.AttachedAt }).ToArrayAsync(ct);
        var attachments = await db.OperationalAttachments.AsNoTracking().OrderBy(p => p.AssignmentId).Select(p => new { p.AssignmentId, p.CreatedAt }).ToArrayAsync(ct);
        var cases = new List<IncidenceCase>(); var changes = new List<IncidenceChange>();
        var absent = source.Unavailable.Append((person.Id, request.Day)).ToHashSet();
        CapacityLedger State(IEnumerable<OperationalAssignment> rows) => new(source.Workers, rows.ToArray(), source.Capacities, absent, source.Legacy, source.Today) { Temporal = source.Temporal };
        var eligibilityLedger = State([]);
        var recipientReasons = new Dictionary<(Guid Assignment, Guid Person), string?>();
        string? RecipientReason(OperationalAssignment a, CapacityWorker worker)
        {
            var key = (a.Id, worker.Id);
            if (recipientReasons.TryGetValue(key, out var cached)) return cached;
            string? reason = null;
            if (worker.Id == person.Id || absent.Contains((worker.Id, request.Day))) reason = "Tiene ausencia registrada para esta fecha.";
            else if (a.SpecializationId == null && worker.Role != a.AssignedRole)
                reason = $"Su rol es {SessionPresentation.RoleName(worker.Role) ?? worker.Role}; la tarea requiere {SessionPresentation.RoleName(a.AssignedRole ?? "") ?? a.AssignedRole}.";
            else if (!worker.Qualified(a.SpecializationId))
                reason = worker.IsOnTrial ? "Está en periodo de prueba y la tarea requiere especialidad." : $"No tiene la especialidad requerida: {a.SpecializationName ?? "especialidad de la tarea"}.";
            else
            {
                var projected = Copy(a); projected.PersonId = worker.Id;
                if (projected.GoalUnits > 0) projected.StartsAt = request.NotBefore;
                var slot = OperationalAgenda.Find(eligibilityLedger, worker.Id, projected, request.NotBefore);
                if (!slot.Eligible) reason = TemporalEligibilityContext.Message(slot.Reason);
                else
                {
                    OperationalAgenda.Apply(projected, slot);
                    var time = eligibilityLedger.CheckTime(worker.Id, projected);
                    if (!time.Eligible) reason = TemporalEligibilityContext.Message(time.Reason);
                }
            }
            recipientReasons[key] = reason;
            return reason;
        }
        void Block(OperationalAssignment a, string cause, string effect, string[] options, params string[] details) =>
            cases.Add(new(a.Id, a.Name, cause, effect, options, details) {
                EligibleRecipientIds = people.Where(w => RecipientReason(a, w) == null).Select(w => w.Id).ToArray()
            });
        var temporal = JsonSerializer.SerializeToNode(source.Temporal)!; temporal.AsObject().Remove("Now");
        var fingerprint = Hash(new { request, all, stages, attempts, prepared, preparedSteps, carryovers, facts, transfers, priorDecisions, availability, closures, evidence, attachments,
            source.Workers, source.Capacities, source.Legacy, Temporal = temporal, Algorithm = 2 });
        var selected = all.Where(a => a.PersonId == person.Id &&
            (WeekContract.LocalToday(a.StartsAt) == request.Day || source.Footprint(a).Any(f => f.Day == request.Day) ||
             WeekContract.LocalToday(a.DueAt.AddTicks(-1)) == request.Day)).ToArray();
        // Prepared and not-yet-enabled stages are real reservations, not executable assignment IDs.
        foreach (var reserved in source.Tasks.Where(a => a.PersonId == person.Id && !all.Any(x => x.Id == a.Id) && source.Footprint(a).Any(f => f.Day == request.Day)))
            selected = selected.Append(reserved).ToArray();
        var selectedIds = selected.Select(a => a.Id).ToHashSet();
        if (request.Decisions.Any(d => !selectedIds.Contains(d.AssignmentId) || d.Action is not ("TRANSFER" or "RETIRE") || d.AuthorizeOverload && (d.Action != "TRANSFER" || d.RecipientId == null)))
            throw Error("Las decisiones contienen una tarea ajena a esta propuesta. Vuelve a revisar.", 422);
        var state = State(source.Tasks.Where(a => !selectedIds.Contains(a.Id)));
        var jobs = new List<(OperationalAssignment Row, Guid? Root, DateOnly Day, Guid? Stage)>();
        foreach (var a in selected.OrderBy(a => OperationalAgenda.Priority(a.OperationalCriticality)).ThenBy(a => a.DueAt).ThenByDescending(a => a.RemainingMinutes).ThenBy(a => a.Id))
        {
            var linked = attempts.SingleOrDefault(t => t.AssignmentId == a.Id);
            var stage = stages.SingleOrDefault(s => s.CurrentAttemptId == linked?.Id) ?? stages.SingleOrDefault(s => s.Id == a.Id);
            var root = TaskBonusService.IncidenceRoot(facts, a.Id, priorDecisions);
            var obligation = obligations.SingleOrDefault(o => o.Id == root);
            var evaluated = obligation?.Day ?? WeekContract.LocalToday(a.DueAt.AddTicks(-1));
            if (!OperationalSupervisionService.Transferable(a))
            {
                if (a.CompletedAt is {} completed && WeekContract.LocalToday(completed) == request.Day)
                    Block(a, "Ya hay trabajo entregado este día.", "No se borrará ni excluirá su cumplimiento. La ausencia de día completo requiere resolver esta contradicción.", ["Revisar en Supervisión"]);
                state = state.WithAdded(a); continue;
            }
            if (evaluated < request.Day) { state = state.WithAdded(a); continue; }
            if (carryovers.Any(r => r.PendingItems().Any(i => i.AssignmentId == a.Id)))
            { Block(a, "Tiene una solicitud de arrastre o posposición pendiente.", "La incidencia no decide ni cierra esa solicitud.", ["Resolver en Supervisión"]); continue; }
            if (evaluated > request.Day)
            {
                var multiDecision = request.Decisions.SingleOrDefault(d => d.AssignmentId == a.Id);
                if (multiDecision?.Action == "RETIRE")
                { Block(a, "La obligación vence otro día.", "No se permite retirar toda su obligación ni su bono por la ausencia de un día.", ["Conservar obligación", "Reasignar obligación completa"]); continue; }
                if (multiDecision?.Action == "TRANSFER" && a.GoalUnits > 0 && stage == null && obligation != null && obligation.PersonId == person.Id)
                { jobs.Add((a, root, evaluated, null)); continue; }
                state = state.WithAdded(a);
                var footprint = state.Footprint(a).ToArray();
                if (a.GoalUnits == 0 || footprint.Any(f => f.Day == request.Day) || footprint.Any(f => state.DayLoad(a.PersonId, f.Day) > state.DayCapacity(a.PersonId, f.Day)))
                    Block(a, "La obligación vence otro día y su esfuerzo no cabe en los días restantes.", "Conserva obligación, plazo y bono. No se puede retirar completa por esta ausencia.", ["Resolver capacidad o reasignación en Supervisión"], $"Vencimiento: {a.DueAt:u}; esfuerzo pendiente: {a.RemainingMinutes} min.");
                else changes.Add(new(a, Copy(a), "REDISTRIBUTE", root, evaluated, stage?.Id));
                continue;
            }
            if (all.Any(x => x.Id == a.Id) && (obligation == null || obligation.Incidence != null))
            { Block(a, "Falta una raíz de obligación o una instantánea económica acreditada.", "No se inventará una exclusión del bono.", ["Revisar historial del bono"]); continue; }
            if (obligation != null && (obligation.PersonId != person.Id || closures.Any(c => c.PersonId == person.Id && c.Week == TaskBonusCalculator.Monday(evaluated))))
            { Block(a, "La obligación pertenece a otra persona o tiene un cierre semanal persistido.", "No se trasladan descuentos ajenos ni se modifica el cierre.", ["Revisar historial del bono"]); continue; }
            var decision = request.Decisions.SingleOrDefault(d => d.AssignmentId == a.Id);
            if (a.DueAt <= now || decision?.Action == "RETIRE")
            {
                if (a.DueAt <= now && decision?.Action == "TRANSFER")
                { Block(a, "La tarea ya venció.", "No puede transferirse. Revisa el retiro y la exclusión.", ["Revisar retiro"]); continue; }
                changes.Add(new(a, null, "RETIRE", root, evaluated, stage?.Id)); continue;
            }
            if (!all.Any(x => x.Id == a.Id) && stage == null && !preparedRows.Any(p => p.Value.Any(r => r.Id == a.Id)))
            { Block(a, "Es una reserva de un plan todavía preparado.", "Debe revisarse el plan antes de registrar la ausencia; no se tratará como una asignación ejecutable.", ["Revisar plan preparado"]); continue; }
            if (a.SchedulingPolicyId == null || a.ClosureMarginPercent == null)
            { Block(a, "La asignación no tiene un bloque de agenda acreditado.", "No se inventa una duración de calendario.", ["Adoptar calendario en Actualizar asignaciones", "Retirar por ausencia"]); continue; }
            if (prepared.Any(p => p.AgendaJson == null && preparedRows[p.Id].Any(r => r.Id == a.Id)))
            { Block(a, "La reserva preparada no tiene una agenda acreditada.", "La transferencia necesita revisar primero el plan preparado.", ["Revisar plan preparado", "Retirar por ausencia"]); continue; }
            if ((a.ProgressUnits > 0 || a.Comment != null || OperationalChecklist.Checked(a.ChecklistDraftJson).Length > 0 || evidence.Any(p => p.AssignmentId == a.Id) || attachments.Any(p => p.AssignmentId == a.Id)) && decision == null)
            { Block(a, "Tiene avances o evidencia protegida.", "Se conservarán íntegros, con su autoría.", ["Transferir conservando lo realizado", "Retirar conservando historial"]); continue; }
            jobs.Add((a, root, evaluated, stage?.Id));
        }
        foreach (var retired in changes.Where(c => c.Action == "RETIRE" && c.StageId != null).ToArray())
        {
            var affectedStage = stages.Single(s => s.Id == retired.StageId);
            foreach (var successor in stages.Where(s => s.RunId == affectedStage.RunId && s.Position > affectedStage.Position && s.Status is not ("CONCLUIDA" or "OMITIDA" or "CANCELADA")))
            {
                var successorAttempt = attempts.SingleOrDefault(a => a.Id == successor.CurrentAttemptId);
                var successorRow = all.SingleOrDefault(a => a.Id == successorAttempt?.AssignmentId) ?? source.Tasks.SingleOrDefault(a => a.Id == successor.Id);
                if (successorRow == null) continue;
                if (successorAttempt?.AssignmentId != null)
                { Block(successorRow, "Una sucesora ya tiene asignación ejecutable.", "Revisa el proceso; no se retirará trabajo ajeno implícitamente.", ["Revisar proceso"]); continue; }
                jobs.RemoveAll(j => j.Row.Id == successorRow.Id);
                changes.RemoveAll(c => c.Before.Id == successorRow.Id);
                changes.Add(new(successorRow, null, "HOLD", null, WeekContract.LocalToday(successorRow.DueAt.AddTicks(-1)), successor.Id));
                state = State(state.Tasks.Where(a => a.Id != successorRow.Id));
            }
        }
        foreach (var preparedStart in prepared)
        {
            var rows = preparedRows[preparedStart.Id];
            if (!changes.Any(c => c.Action == "RETIRE" && rows.Any(r => r.Id == c.Before.Id))) continue;
            changes.RemoveAll(c => rows.Any(r => r.Id == c.Before.Id));
            jobs.RemoveAll(j => rows.Any(r => r.Id == j.Row.Id));
            state = State(state.Tasks.Where(a => !rows.Any(r => r.Id == a.Id)));
            foreach (var reservation in rows) changes.Add(new(reservation, null, "HOLD_PREPARED", null,
                WeekContract.LocalToday(reservation.DueAt.AddTicks(-1))) { PreparedStartId = preparedStart.Id });
        }
        var placed = new List<IncidenceChange>(); var nodes = 0; var exhausted = false;
        var exclusive = stages.Select(s => s.Id).Concat(attempts.Where(a => a.AssignmentId != null).Select(a => a.AssignmentId!.Value))
            .Concat(preparedRows.Values.SelectMany(rows => rows).Select(a => a.Id)).ToHashSet();
        var neighbours = request.Reflow ? all.Where(a => a.PersonId != person.Id && a.StartsAt > now &&
            WeekContract.LocalToday(a.StartsAt) == request.Day && a.DueAt > now && a.SchedulingPolicyId != null &&
            a.OperationalCriticality != "CRITICA" && a.GoalUnits == 0 && a.ProgressUnits == 0 && a.Comment == null &&
            a.OccasionId == null && a.ReassignedFromId == null && a.TransferRemainingMinutes == null &&
            OperationalSupervisionService.Transferable(a) && !exclusive.Contains(a.Id) &&
            !evidence.Any(e => e.AssignmentId == a.Id) && !attachments.Any(e => e.AssignmentId == a.Id) &&
            !carryovers.Any(r => r.Items().Any(i => i.AssignmentId == a.Id)) && OperationalChecklist.Checked(a.ChecklistDraftJson).Length == 0).ToArray() : [];
        var neighbourIds = neighbours.Select(a => a.Id).ToHashSet();
        state = State(state.Tasks.Where(a => !neighbourIds.Contains(a.Id)));
        jobs.AddRange(neighbours.Select(a => (a, (Guid?)null, request.Day, (Guid?)null)));
        var earliest = request.NotBefore;
        bool DependenciesFit()
        {
            foreach (var chain in stages.GroupBy(s => s.RunId))
            {
                OperationalSequenceStage? previous = null;
                foreach (var stage in chain.OrderBy(s => s.Position).Where(s => s.Status is not ("OMITIDA" or "CANCELADA")))
                {
                    var moved = placed.SingleOrDefault(c => c.StageId == stage.Id);
                    var priorMove = previous == null ? null : placed.SingleOrDefault(c => c.StageId == previous.Id);
                    if (previous != null && previous.Status != "CONCLUIDA" && (moved != null || priorMove != null) &&
                        (moved?.After?.StartsAt ?? stage.PlannedStartsAt) < (priorMove?.After?.DueAt ?? previous.EffectiveDueAt ?? previous.PlannedDueAt)) return false;
                    previous = stage;
                }
            }
            foreach (var start in prepared)
            {
                OperationalAssignment? previous = null;
                foreach (var step in preparedSteps.Where(s => s.VersionId == start.VersionId).OrderBy(s => s.Position))
                {
                    var original = preparedRows[start.Id].Single(a => a.TaskId == step.TaskId);
                    var row = placed.SingleOrDefault(c => c.Before.Id == original.Id)?.After ?? original;
                    if (previous != null && row.StartsAt < previous.DueAt) return false;
                    previous = row;
                }
            }
            return true;
        }
        bool Search(CapacityLedger ledger, int index)
        {
            ct.ThrowIfCancellationRequested();
            if (index == jobs.Count) return DependenciesFit();
            if (++nodes > 20000) { exhausted = true; return false; }
            var job = jobs[index]; var a = job.Row; var decision = request.Decisions.SingleOrDefault(d => d.AssignmentId == a.Id);
            var candidates = people.Where(w => (!neighbourIds.Contains(a.Id) || w.Id == a.PersonId) && RecipientReason(a, w) == null &&
                (decision?.RecipientId == null || w.Id == decision.RecipientId))
                .OrderBy(w => RemainingCapacity(ledger, w.Id, request.Day, earliest) is var capacity && capacity > 0 ?
                    (Pending(ledger, w.Id, request.Day) + a.RemainingMinutes) / capacity : decimal.MaxValue).ThenBy(w => w.Code, StringComparer.Ordinal);
            foreach (var worker in candidates)
            {
                var projected = Copy(a); projected.PersonId = worker.Id; projected.PersonName = worker.Name;
                if (projected.GoalUnits > 0) projected.StartsAt = earliest;
                var overload = decision?.AuthorizeOverload == true;
                // Overlap permission is local to this transfer. Process reservations and dependencies
                // remain occupied; no persistent capacity setting is changed.
                var placementLedger = overload && job.Stage == null ? State(ledger.Tasks.Where(t => t.PersonId != worker.Id || exclusive.Contains(t.Id))) : ledger;
                // Keep required role and the captured effort; a specialist may have a different employment role.
                foreach (var slot in OperationalAgenda.Alternatives(placementLedger, worker.Id, projected, earliest))
                {
                    if (++nodes > 20000) { exhausted = true; return false; }
                    OperationalAgenda.Apply(projected, slot);
                    var footprint = ledger.Footprint(projected).ToArray();
                    if (overload && projected.GoalUnits > 0 && footprint.Any(f => {
                        var available = (ledger.Temporal.Day(worker.Id,f.Day,false).Intervals ?? []).Sum(interval => {
                            var from = new[] { TemporalEligibilityContext.At(f.Day,interval.Entry), projected.StartsAt }.Max();
                            var through = new[] { TemporalEligibilityContext.At(f.Day,interval.Exit), projected.DueAt }.Min();
                            if (through <= from) return 0m;
                            var occupied = ledger.Tasks.Where(t => t.PersonId == worker.Id && exclusive.Contains(t.Id) && t.StartsAt < through && t.DueAt > from)
                                .Sum(t => (decimal)(new[] { t.DueAt,through }.Min() - new[] { t.StartsAt,from }.Max()).TotalMinutes);
                            return Math.Max(0,(decimal)(through-from).TotalMinutes-occupied);
                        });
                        return ledger.Unavailable.Contains((worker.Id,f.Day)) || f.Minutes > available;
                    })) continue;
                    if (projected.GoalUnits == 0 && WeekContract.LocalToday(projected.DueAt.AddTicks(-1)) != request.Day ||
                        !ledger.CheckTime(worker.Id, projected).Eligible || !OperationalAgenda.CircuitFits(ledger.Tasks, projected, projected.StartsAt, projected.DueAt) ||
                        !overload && (!ledger.CanAssign(worker.Id, projected) ||
                        Pending(ledger, worker.Id, request.Day) + ledger.Footprint(projected).Where(f => f.Day == request.Day).Sum(f => f.Minutes) > RemainingCapacity(ledger, worker.Id, request.Day, earliest))) continue;
                    var peers = placed.Select(c => c.After!).Concat(all).DistinctBy(x => x.Id);
                    if (peers.Any(x => x.Id != a.Id && x.PersonId == worker.Id &&
                        (a.OccasionId != null && x.OccasionId == a.OccasionId || projected.DifferentPeople && x.TaskId == a.TaskId && WeekContract.LocalToday(x.StartsAt) == request.Day))) continue;
                    if (job.Stage is {} sid && stages.Single(s => s.Id == sid) is {} stage &&
                        (projected.StartsAt < stage.MinimumStartsAt || projected.StartsAt < stage.DependencySatisfiedAt ||
                         stage.FixedDueAt is {} fixedDue && projected.DueAt > fixedDue ||
                         stages.Any(s => s.RunId == stage.RunId && s.Position > stage.Position && s.OperationalCriticality == "CRITICA" && s.NominalStartsAt < projected.DueAt))) continue;
                    var saved = Copy(projected);
                    placed.Add(new(a, saved, neighbourIds.Contains(a.Id) ? "MOVE" : "TRANSFER", job.Root, job.Day, job.Stage, overload) {
                        ExcessMinutes = Math.Max(0, Pending(ledger, worker.Id, request.Day) + footprint.Where(f=>f.Day==request.Day).Sum(f=>f.Minutes) - RemainingCapacity(ledger, worker.Id, request.Day, earliest)),
                        OverloadDays = overload && a.GoalUnits > 0 ? footprint.Select(f => $"{f.Day:dd/MM/yyyy}: {f.Minutes:0.#} min de esta obligación; exceso {Math.Max(0, Pending(ledger,worker.Id,f.Day)+f.Minutes-RemainingCapacity(ledger,worker.Id,f.Day,earliest)):0.#} min.").ToArray() : [],
                        Overlaps = ledger.Tasks.Where(t => t.PersonId == worker.Id && t.StartsAt < saved.DueAt && t.DueAt > saved.StartsAt).Select(t => t.Name).ToArray()
                    });
                    if (Search(ledger.WithAdded(saved), index + 1)) return true;
                    placed.RemoveAt(placed.Count - 1);
                    if (exhausted) return false;
                }
            }
            return false;
        }
        if (!Search(state, 0))
        {
            placed.Clear();
            foreach (var job in jobs.Where(j => !neighbourIds.Contains(j.Row.Id)))
            {
                var decision = request.Decisions.SingleOrDefault(d => d.AssignmentId == job.Row.Id);
                var recipient = people.SingleOrDefault(w => w.Id == decision?.RecipientId);
                var cause = exhausted ? "La búsqueda agotó 20,000 pasos; no demuestra falta de espacio." : "Esta tarea forma parte de un reparto conjunto pendiente de resolver. Revisa las restricciones de las destinatarias y las demás tareas.";
                if (decision?.RecipientId != null && recipient == null) cause = "La destinataria seleccionada ya no está dentro del alcance disponible. Elige otra persona.";
                else if (recipient != null && RecipientReason(job.Row, recipient) is {} reason) cause = $"{recipient.Name}: {reason}";
                else if (recipient != null && job.Row.GoalUnits == 0 && job.Day == request.Day && decision?.AuthorizeOverload != true &&
                    Pending(state, recipient.Id, request.Day) + job.Row.RemainingMinutes > RemainingCapacity(state, recipient.Id, request.Day, earliest))
                    cause = $"{recipient.Name}: la carga pendiente ({Pending(state, recipient.Id, request.Day):0.#} min) más esta tarea ({job.Row.RemainingMinutes} min) supera la capacidad restante ({RemainingCapacity(state, recipient.Id, request.Day, earliest):0.#} min). Revisa la carga o autoriza explícitamente la sobrecarga.";
                else if (!people.Any(w => RecipientReason(job.Row, w) == null)) cause = "No hay destinatarias que cumplan rol, especialidad, disponibilidad y horario para esta tarea. Revisa los detalles.";
                Block(job.Row, cause,
                "No se guardará ningún reparto parcial ni se reducirán los minutos necesarios.", job.Day > request.Day ? ["Elegir destinataria y volver a calcular", "Revisar capacidad en Supervisión"] : ["Elegir destinataria y volver a calcular", "Retirar por ausencia"],
                people.Where(w => w.Id != person.Id).Select(w => $"{w.Name}: " + (RecipientReason(job.Row, w) ?? $"capacidad restante {RemainingCapacity(state, w.Id, request.Day, earliest):0.#} min; carga pendiente {Pending(state, w.Id, request.Day):0.#} min; la revisión conjunta comprueba huecos, dependencias y capacidad")).ToArray());
            }
        }
        changes.AddRange(placed);
        foreach (var preparedStart in prepared)
        {
            var rows = preparedRows[preparedStart.Id];
            for (var n = 0; n < changes.Count; n++)
                if (rows.Any(r => r.Id == changes[n].Before.Id)) changes[n] = changes[n] with { PreparedStartId = preparedStart.Id };
        }
        // Classification is clock-sensitive, while the input-state digest is clock-independent.
        // A transfer must never silently turn into a retirement between review and apply.
        fingerprint = Hash(new { State = fingerprint, Dispositions = changes.Select(c => new { c.Before.Id, c.Action, c.StageId, c.After, c.ObligationId }), Cases = cases });
        var finalState = placed.Aggregate(state, (current, change) => current.WithAdded(change.After!));
        var balances = people.Where(w => w.Id != person.Id).Select(w => new IncidenceBalance(w.Id, w.Name,
            RemainingCapacity(source, w.Id, request.Day, earliest), Pending(source, w.Id, request.Day), Pending(finalState, w.Id, request.Day))).ToArray();
        return new(request, person.Name, now, fingerprint, changes.ToArray(), cases.ToArray(), balances);
    }

    public static decimal Pending(CapacityLedger ledger, Guid person, DateOnly day) =>
        ledger.Tasks.Where(a => a.PersonId == person && OperationalSupervisionService.Transferable(a)).Sum(a => ledger.Footprint(a).Where(f => f.Day == day).Sum(f => f.Minutes)) +
        (day == ledger.Today ? ledger.Legacy.GetValueOrDefault(person) : 0);

    public static decimal RemainingCapacity(CapacityLedger ledger, Guid person, DateOnly day, DateTimeOffset now)
    {
        if (ledger.Unavailable.Contains((person, day))) return 0;
        var remaining = (ledger.Temporal.Day(person, day, false).Intervals ?? []).Sum(i =>
            (decimal)Math.Max(0, (TemporalEligibilityContext.At(day, i.Exit) - new[] { now, TemporalEligibilityContext.At(day, i.Entry) }.Max()).TotalMinutes));
        var consumed = ledger.Tasks.Where(a => a.PersonId == person && a.CompletedAt != null && WeekContract.LocalToday(a.StartsAt) == day &&
            !(a.CompletionKind == "SUPERVISION" && a.ReviewStatus == "REASIGNADA")).Sum(a => (decimal)a.LoadMinutes);
        return Math.Max(0, Math.Min(remaining, ledger.DayCapacity(person, day) - consumed));
    }

    public async Task<OperationalIncidence> ApplyAsync(Guid actor, IncidenceRequest request, string fingerprint, CancellationToken ct)
    {
        if (await supervision.AuthorizeAsync(actor, ct) != CanonicalRole.Direction) throw Error("ACCESO_DENEGADO", 403);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await OperationalTaskService.LockAsync(db, ct);
            var hash = Hash(request);
            var prior = await db.Set<OperationalIncidence>().AsNoTracking().SingleOrDefaultAsync(i => i.ActorId == actor && i.IntentKey == request.Key, ct);
            if (prior != null)
            {
                if (prior.IntentHash != hash) throw Error("Esta clave ya confirmó otra intención. Consulta el recibo original.");
                await tx.CommitAsync(ct); return prior;
            }
            var preview = await BuildAsync(actor, request, ct);
            if (!preview.CanApply || preview.Fingerprint != fingerprint || preview.Changes.Any(c => c.Action is "TRANSFER" or "MOVE" &&
                (c.Before.DueAt <= clock.UtcNow || c.After!.StartsAt < clock.UtcNow)))
                throw Error("La propuesta cambió o cruzó un límite temporal. Revisa otra vez; no se guardó ningún cambio.");
            var receipt = new OperationalIncidence { Id = Guid.CreateVersion7(), PersonId = request.PersonId, Day = request.Day,
                ActorId = actor, At = clock.UtcNow, IntentKey = request.Key, IntentHash = hash, Fingerprint = fingerprint,
                Reason = request.Reason.Trim(), ReceiptJson = JsonSerializer.Serialize(preview) };
            db.Add(receipt);
            db.PendingIncidenceLimits.AddRange(preview.Changes.Where(c => c.Action is "TRANSFER" or "MOVE").Select(c => (c.Before.DueAt, c.After!.StartsAt)));
            var current = await db.AvailabilityDayVersions.SingleOrDefaultAsync(a => a.PersonId == request.PersonId && a.BranchId == BranchScope.LorettaId && a.LocalDate == request.Day && a.Status == AvailabilityVersionStatus.Current, ct);
            var next = current == null ? new AvailabilityDayVersion(Guid.CreateVersion7(), request.PersonId, BranchScope.LorettaId, request.Day, false, actor) : current.CreateSuccessor(Guid.CreateVersion7(), false, actor);
            // Retire the predecessor before inserting its successor under the partial unique index.
            if (current != null) await db.SaveChangesAsync(ct);
            db.Add(next);
            foreach (var change in preview.Changes)
            {
                var a = await db.OperationalAssignments.SingleOrDefaultAsync(a => a.Id == change.Before.Id, ct);
                if (change.Action == "RETIRE" && a != null) { a.RetiredAt = clock.UtcNow; a.Revision++; }
                if (change.Action is "TRANSFER" or "MOVE" && a != null)
                {
                    if (change.Action == "TRANSFER") db.Add(new OperationalTransfer { Id = Guid.CreateVersion7(), AssignmentId = a.Id, TaskName = a.Name,
                        FromPersonId = a.PersonId, FromName = a.PersonName, ToPersonId = change.After!.PersonId, ToName = change.After.PersonName,
                        ActorId = actor, At = clock.UtcNow, PreviousStartsAt = a.StartsAt, PreviousDueAt = a.DueAt, NewDueAt = change.After.DueAt,
                        ProgressUnits = a.ProgressUnits, RemainingMinutes = a.RemainingMinutes, Reason = request.Reason.Trim(), SnapshotJson = JsonSerializer.Serialize(a) });
                    a.PersonId = change.After!.PersonId; a.PersonName = change.After.PersonName;
                    a.StartsAt = change.After.StartsAt; a.DueAt = change.After.DueAt; a.Revision++;
                }
                if (change.StageId is {} sid) await ApplyStageAsync(actor, receipt, change, sid, ct);
                if (change.PreparedStartId is {} preparedId)
                {
                    var start = await db.Set<PreparedProcessStart>().SingleAsync(s => s.Id == preparedId, ct);
                    if (change.Action == "HOLD_PREPARED") start.IncidenceHoldId = receipt.Id;
                    else if (change.Action == "TRANSFER")
                    {
                        var agenda = start.AgendaJson == null ? null : JsonSerializer.Deserialize<OperationalAssignment[]>(start.AgendaJson)!;
                        if (agenda == null) throw Error("La reserva preparada requiere adoptar una agenda verificable antes de transferirla.");
                        var position = await db.Set<OperationalSequenceStep>().Where(s => s.VersionId == start.VersionId && s.TaskId == change.Before.TaskId).Select(s => s.Position).SingleAsync(ct);
                        var participants = JsonSerializer.Deserialize<Dictionary<int, Guid>>(start.Participants)!;
                        participants[position] = change.After!.PersonId; start.Participants = JsonSerializer.Serialize(participants);
                        start.AgendaJson = JsonSerializer.Serialize(agenda.Select(a => a.Id == change.Before.Id ? change.After : a));
                    }
                    start.Revision++;
                }
                db.Add(new OperationalIncidenceDisposition { Id = Guid.CreateVersion7(), IncidenceId = receipt.Id, AssignmentId = change.Before.Id,
                    StageId = change.StageId, ObligationId = change.ObligationId, PersonId = request.PersonId, EvaluationDay = change.EvaluationDay,
                    RecipientId = change.Action == "TRANSFER" ? change.After!.PersonId : null, Action = change.Action,
                    OverloadAuthorized = change.OverloadAuthorized, BeforeJson = JsonSerializer.Serialize(change.Before), AfterJson = JsonSerializer.Serialize(change.After) });
                if (change.Action == "RETIRE" && change.ObligationId is {} root)
                {
                    var week = TaskBonusCalculator.Monday(change.EvaluationDay);
                    if (!await db.Set<TaskBonusExclusion>().AnyAsync(e => e.ObligationId == root && e.PersonId == request.PersonId && e.Week == week, ct))
                        db.Add(new TaskBonusExclusion { ObligationId = root, PersonId = request.PersonId, Week = week, ActorId = actor, At = clock.UtcNow, Reason = request.Reason.Trim() });
                }
            }
            db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow,
                ResourceId = receipt.Id, ResourceType = "OPERATIONAL_INCIDENCE", BranchId = BranchScope.LorettaId, Action = "ABSENCE_APPLIED",
                Outcome = "SUCCESS", CorrelationId = request.Key, AfterData = JsonSerializer.SerializeToDocument(preview) });
            await db.SaveChangesAsync(ct);
            if (preview.Changes.Any(c => c.Action is "TRANSFER" or "MOVE" && (c.Before.DueAt <= clock.UtcNow || c.After!.StartsAt < clock.UtcNow)))
                throw Error("La propuesta cruzó un límite temporal al guardarse. Revisa otra vez; no se guardó ningún cambio.");
            await tx.CommitAsync(ct); return receipt;
        }
        catch
        {
            // PostgreSQL can already have rolled back when a deferred constraint rejects COMMIT.
            // Preserve that original failure rather than masking it with a second rollback error.
            try { await tx.RollbackAsync(CancellationToken.None); }
            catch (InvalidOperationException) { }
            db.ChangeTracker.Clear(); db.PendingBonusFacts.Clear(); db.PendingBonusAvailability.Clear(); db.PendingBonusExclusions.Clear();
            db.PendingIncidenceLimits.Clear();
            throw;
        }
    }

    async Task ApplyStageAsync(Guid actor, OperationalIncidence receipt, IncidenceChange change, Guid sid, CancellationToken ct)
    {
        var stage = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == sid, ct);
        var run = await db.Set<OperationalSequenceRun>().SingleAsync(r => r.Id == stage.RunId, ct);
        if (change.Action == "TRANSFER")
        {
            stage.ReservedPersonId = change.After!.PersonId; stage.ReservedPersonName = change.After.PersonName;
            stage.PlannedStartsAt = change.After.StartsAt; stage.PlannedDueAt = change.After.DueAt;
            if (stage.EnabledAt != null) { stage.EnabledAt = change.After.StartsAt; stage.EffectiveDueAt = change.After.DueAt; }
            stage.Revision++;
        }
        if (change.Action is "RETIRE" or "HOLD")
        {
            stage.Status = "PENDIENTE_REPROGRAMACION"; stage.BlockReason = $"Incidencia {receipt.Id}: reprogramación explícita requerida; no se satisface esta dependencia."; stage.Revision++;
        }
        run.Revision++;
        db.Add(new OperationalSequenceEvent { Id = Guid.CreateVersion7(), RunId = run.Id, StageId = stage.Id, AttemptId = stage.CurrentAttemptId!.Value,
            ActorId = actor, OccurredAt = clock.UtcNow, Kind = change.Action == "TRANSFER" ? "REASIGNACION" : "BLOQUEO", EventKey = $"incidence:{receipt.Id:N}:{sid:N}", Comment = receipt.Reason });
    }
}
