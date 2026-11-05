using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record CarryoverDecision(Guid RequestId, Guid AssignmentId, long Revision, string Action,
    Guid? Recipient, DateOnly? Day, string Comment, Guid Intent);
public sealed record CarryoverBlock(string Title, string Detail, string Action);
public sealed record CarryoverDecisionPreview(CarryoverDecision Decision, OperationalAssignment Current,
    OperationalAssignment? Proposed, string[] Conflicts, string Fingerprint, decimal LoadBefore,
    decimal LoadAfter, decimal Capacity, decimal OriginBefore, decimal OriginAfter)
{
    public CarryoverBlock[] Blocks { get; init; } = [];
}

public sealed partial class OperationalCarryoverService
{
    public static string ResolutionLabel(string kind) => kind switch {
        "POSPOSICION" => "Propuesta aprobada", "REPROGRAMACION" => "Día cambiado", "REASIGNACION" => "Reasignada",
        "RECHAZO" => "Rechazada", "ENTREGADA_SIN_CAMBIOS" => "Ya entregada, sin cambios", _ => kind };

    private async Task<(OperationalCarryoverRequest Request, CarryoverItem Item, OperationalAssignment Current)> ResolutionSource(
        Guid actor, CarryoverDecision command, CancellationToken ct)
    {
        var person = await Person(actor, ct);
        var request = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleOrDefaultAsync(r =>
            r.Id == command.RequestId && r.BranchId == BranchScope.LorettaId && r.Kind == "POSPONER", ct) ?? throw Error("TASK_NOT_FOUND", 404);
        if (request.PersonId == person && await supervision.AuthorizeAsync(actor, ct) != CanonicalRole.Direction) throw Error("ACCESO_DENEGADO", 403);
        await RequireImmediateSupervisor(actor, request.PersonId, ct);
        var item = request.Items().SingleOrDefault(i => i.AssignmentId == command.AssignmentId) ?? throw Error("TASK_NOT_FOUND", 404);
        await supervision.AuthorizeAssignmentAsync(actor, item.AssignmentId, ct);
        var current = await db.OperationalAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.Id == item.AssignmentId, ct) ?? throw Error("TASK_NOT_FOUND", 404);
        return (request, item, current);
    }

    public async Task<CarryoverDecisionPreview> PreviewDecisionAsync(Guid actor, CarryoverDecision command, CancellationToken ct)
    {
        var (request, item, current) = await ResolutionSource(actor, command, ct);
        var issues = new List<string>();
        var blocks = new List<CarryoverBlock>();
        void Block(string title, string detail, string action) {
            blocks.Add(new(title, detail, action));
            issues.Add($"{item.Task}: {title}. {detail} {action}");
        }
        string Prefix(string message) => $"{item.Task}: {message}";
        if (request.State != "PENDIENTE" || request.Results().Any(r => r.AssignmentId == item.AssignmentId)) issues.Add(Prefix("esta tarea ya tiene una decisión; actualiza la bandeja."));
        if (command.Revision != current.Revision) issues.Add(Prefix("cambió la tarea; actualiza y vuelve a revisar."));
        if (command.Action is not ("APPROVE" or "REASSIGN" or "RESCHEDULE" or "REJECT")) issues.Add(Prefix("elige una acción válida."));
        OperationalAssignment? proposed = null;
        decimal before = 0, after = 0, capacity = 0, originBefore = 0, originAfter = 0;
        string? contextHash = null;
        if (command.Action != "REJECT" && issues.Count == 0 && current.CompletedAt == null)
        {
            if (current.PersonId != item.PersonId || PostponementAllocator.Structure(current) != item.Structure)
                issues.Add(Prefix("cambió el responsable, la agenda o la carga desde la solicitud. Rechaza esta petición y solicita una nueva."));
            if (current.ReviewStatus is not (null or "PENDIENTE") || current.GoalUnits > 0 || current.ScheduleMode == "PROGRESO" ||
                current.SchedulingPolicyId == null || OperationalAgenda.CashCircuit.Any(r => r.Before == current.TaskCode || r.After == current.TaskCode) || current.TaskCode == "TAR-0201" ||
                await db.Set<OperationalSequenceAttempt>().AnyAsync(a => a.AssignmentId == current.Id, ct))
                issues.Add(Prefix("su estado o dependencia no permite posponerla desde aquí. Revisa la tarea o el proceso."));
            var recipient = current.PersonId;
            CapacityWorker? worker = null;
            if (command.Action == "REASSIGN")
            {
                worker = (await supervision.TransferRecipientsAsync(actor, current.Id, ct)).SingleOrDefault(w => w.Id == command.Recipient);
                if (worker == null) issues.Add(Prefix("elige otra persona activa dentro de tu alcance: con la especialidad de la tarea, o con el mismo rol si la tarea no tiene especialidad."));
                else recipient = worker.Id;
            }
            var day = command.Action == "APPROVE" ? item.ProposedStart is {} start ? WeekContract.LocalToday(start) : (DateOnly?)null : command.Day;
            var through = PostponementWindow.DecisionThrough(request, item, current);
            if (day == null || day < request.From || day > through || item.BeforeDate is {} limit && day >= limit)
                issues.Add(Prefix($"elige un día entre {request.From:dd/MM/yyyy} y {through:dd/MM/yyyy}, respetando el límite de la petición."));
            if (issues.Count == 0)
            {
                var context = await PostponementContext(recipient, [current.Id], day!.Value, day.Value, ct);
                contextHash = context.Fingerprint;
                var ledger = context.Ledger;
                var load = context.Days.Single();
                before = load.Actual + load.Forecast; capacity = load.Capacity;
                if (!load.ReferenceKnown) Block("Carga sin verificar", "No hay un plan o una referencia de carga para el día solicitado.", "Revisa el plan semanal.");
                if (before + current.RemainingMinutes > capacity) Block("Capacidad insuficiente", $"Quedarían {before + current.RemainingMinutes:0.#} min de trabajo para una capacidad de {capacity:0.#} min.", "Cambia de día o reasigna.");
                proposed = PostponementAllocator.Copy(current);
                proposed.PersonId = recipient;
                if (worker != null) { proposed.PersonName = worker.Name; proposed.AssignedRole = worker.Role; }
                if (command.Action == "APPROVE") {
                    proposed.StartsAt = item.ProposedStart!.Value; proposed.DueAt = item.Proposed; proposed.NominalStartsAt = proposed.StartsAt;
                } else {
                    var local = TimeZoneInfo.ConvertTime(current.NominalStartsAt ?? current.StartsAt, OperationalSchedule.Zone);
                    proposed.NominalStartsAt = TemporalEligibilityContext.At(day.Value, current.OperationalCriticality == "CRITICA" ? local.Hour * 60 + local.Minute : 0);
                    proposed.StartsAt = proposed.NominalStartsAt.Value;
                    var placement = OperationalAgenda.Find(ledger, recipient, proposed,
                        OperationalSchedule.Instant(day.Value) > clock.UtcNow ? OperationalSchedule.Instant(day.Value) : clock.UtcNow);
                    if (!placement.Eligible) issues.Add(Prefix($"no hay un tramo completo compatible de horario el {day:dd/MM/yyyy}. Elige otra persona o día."));
                    else OperationalAgenda.Apply(proposed, placement);
                }
                if (proposed.StartsAt <= clock.UtcNow || WeekContract.LocalToday(proposed.DueAt) != day || item.BeforeDate is {} boundary && proposed.DueAt >= OperationalSchedule.Instant(boundary))
                    issues.Add(Prefix("el horario propuesto ya pasó o supera el día permitido. Cambia de día y revisa nuevamente."));
                var time = ledger.CheckTime(recipient, proposed);
                if (!time.Eligible) Block("Disponibilidad incompatible", TemporalEligibilityContext.Message(time.Reason), "Revisa el horario o el responsable.");
                if (!ledger.CanFit(recipient, proposed) && !blocks.Any(b => b.Title == "Capacidad insuficiente")) Block("Capacidad insuficiente", "La carga actual no deja capacidad para esta tarea.", "Cambia de día o reasigna.");
                var exact = OperationalAgenda.FindExact(ledger, recipient, proposed);
                if (!exact.Eligible || exact.StartsAt != proposed.StartsAt || exact.DueAt != proposed.DueAt) {
                    blocks.AddRange(DescribeScheduleBlock(ledger, proposed, exact));
                    foreach (var block in blocks.Where(b => b.Title is "Cruce de horario" or "Horario incompatible"))
                        issues.Add(Prefix($"{block.Title}. {block.Detail} {block.Action}"));
                }
                if (issues.Count == 0) ledger.CaptureTime(proposed);
                after = before + current.RemainingMinutes;
                var source = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
                var originDay = WeekContract.LocalToday(current.StartsAt);
                originBefore = source.DayLoad(current.PersonId, originDay);
                originAfter = Math.Max(0, originBefore - source.Footprint(current).Where(f => f.Day == originDay).Sum(f => f.Minutes));
            }
        }
        if (current.CompletedAt != null && command.Action is "REASSIGN" or "RESCHEDULE") issues.Add(Prefix("ya fue entregada; conserva fechas, responsable y decisión. Cierra la petición sin cambios."));
        var fingerprint = Hash(new { command, request.Revision, current, proposed, contextHash, issues });
        // Keep full contextual strings for non-card consumers; cards receive structured, non-repeating text.
        foreach (var issue in issues.Where(i => !blocks.Any(b => i.Contains(b.Detail, StringComparison.Ordinal))))
            blocks.Add(new("No se puede aprobar", issue.StartsWith(item.Task + ": ", StringComparison.Ordinal) ? issue[(item.Task.Length + 2)..] : issue, ""));
        return new(command, current, proposed, issues.Distinct().ToArray(), fingerprint, before, after, capacity, originBefore, originAfter) { Blocks = blocks.Distinct().ToArray() };
    }

    internal static CarryoverBlock[] DescribeScheduleBlock(CapacityLedger ledger, OperationalAssignment proposed, SchedulingPlacement exact)
    {
        string Hour(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, OperationalSchedule.Zone).ToString("HH:mm");
        var overlaps = ledger.SchedulingPeers(proposed.PersonId, WeekContract.LocalToday(proposed.StartsAt))
            .Where(t => t.Id != proposed.Id && t.SchedulingPolicyId != null && t.ReviewStatus is not ("REASIGNADA" or "NO_SATISFECHA") &&
                t.StartsAt < proposed.DueAt && t.DueAt > proposed.StartsAt).OrderBy(t => t.StartsAt).ToArray();
        if (overlaps.Length > 0)
            return overlaps.Select(t => new CarryoverBlock("Cruce de horario", $"{Hour(t.StartsAt)}–{Hour(t.DueAt)} · {t.Name}", "Cambia de día o reasigna para evitar el cruce.")).ToArray();
        var detail = exact.Reason switch {
            "SCHEDULE_ABSENT" => "No hay jornada disponible ese día.",
            "SCHEDULE_MISSING" => "Falta configurar el horario de trabajo.",
            "SCHEDULE_ACCOUNT_INACTIVE" => "La persona ya no está activa para recibir trabajo.",
            "SCHEDULING_LEGACY_RESERVATION" => "Hay trabajo reservado sin un horario verificable.",
            "SCHEDULING_CRITICAL_ANCHOR_PASSED" => "La hora fija de esta tarea crítica ya pasó.",
            "SCHEDULING_NO_CONTIGUOUS_SLOT" => "No hay un espacio seguido de trabajo que incluya la tarea y su margen de cierre.",
            _ when exact.Eligible && exact.StartsAt == proposed.StartsAt => $"El tramo no coincide con la duración vigente: debe terminar a las {Hour(exact.DueAt)}.",
            _ when exact.Eligible => $"La agenda permite iniciar a las {Hour(exact.StartsAt)}, fuera del tramo solicitado.",
            _ => "El tramo solicitado no cumple las reglas de la agenda vigente." };
        return [new("Horario incompatible", detail, "Revisa otro día o responsable.")];
    }

    public async Task ResolveItemAsync(Guid actor, CarryoverDecision command, string fingerprint, CancellationToken ct)
    {
        if (command.Intent == Guid.Empty || string.IsNullOrWhiteSpace(command.Comment) || command.Comment.Trim().Length > 4000) throw Error("ARRASTRE_DATOS_INVALIDOS", 422);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try {
            await OperationalTaskService.LockAsync(db, ct);
            var (snapshot, _, _) = await ResolutionSource(actor, command, ct);
            var commandHash = Hash(command);
            var prior = snapshot.Results().SingleOrDefault(r => r.AssignmentId == command.AssignmentId);
            if (prior != null) {
                if (prior.Intent == command.Intent && prior.Actor == actor && prior.CommandHash == commandHash) { await tx.CommitAsync(ct); return; }
                throw Error("ARRASTRE_CONFLICTO");
            }
            var preview = await PreviewDecisionAsync(actor, command, ct);
            if (preview.Conflicts.Length > 0 || string.IsNullOrEmpty(fingerprint) || fingerprint != preview.Fingerprint) throw Error("ARRASTRE_CONFLICTO");
            var request = await db.Set<OperationalCarryoverRequest>().SingleAsync(r => r.Id == command.RequestId, ct);
            var row = await db.OperationalAssignments.SingleAsync(a => a.Id == command.AssignmentId, ct);
            var before = row.DueAt;
            Guid? transferId = null;
            var kind = command.Action == "REJECT" ? "RECHAZO" : row.CompletedAt != null ? "ENTREGADA_SIN_CAMBIOS" : command.Action switch { "REASSIGN" => "REASIGNACION", "RESCHEDULE" => "REPROGRAMACION", _ => "POSPOSICION" };
            if (preview.Proposed is {} proposed && command.Action != "REJECT") {
                if (command.Action == "REASSIGN") {
                    var receipt = new OperationalTransfer { Id = Guid.CreateVersion7(), AssignmentId = row.Id, TaskName = row.Name,
                        FromPersonId = row.PersonId, FromName = row.PersonName, ToPersonId = proposed.PersonId, ToName = proposed.PersonName,
                        ActorId = actor, At = clock.UtcNow, PreviousStartsAt = row.StartsAt, PreviousDueAt = row.DueAt, NewDueAt = proposed.DueAt,
                        ProgressUnits = row.ProgressUnits, RemainingMinutes = row.RemainingMinutes,
                        Reason = command.Comment.Trim()[..Math.Min(1000, command.Comment.Trim().Length)], SnapshotJson = JsonSerializer.Serialize(row) };
                    db.Add(receipt); transferId = receipt.Id;
                    row.TransferRemainingMinutes = row.RemainingMinutes; row.TransferProgressUnits = row.ProgressUnits;
                    row.PersonId = proposed.PersonId; row.PersonName = proposed.PersonName; row.AssignedRole = proposed.AssignedRole;
                }
                row.StartsAt = proposed.StartsAt; row.DueAt = proposed.DueAt; row.NominalStartsAt = proposed.NominalStartsAt;
                row.TemporalDecisionJson = proposed.TemporalDecisionJson; row.Revision++;
            }
            var result = new CarryoverResult(row.Id, before, row.DueAt, kind is "POSPOSICION" or "REPROGRAMACION" ? row.DueAt : before, row.CompletedAt, kind) {
                Intent = command.Intent, Actor = actor, At = clock.UtcNow, Comment = command.Comment.Trim(), Action = command.Action, CommandHash = commandHash,
                StartsAt = row.StartsAt, PersonId = row.PersonId, PersonName = row.PersonName, TransferId = transferId, AssignmentRevision = row.Revision };
            request.ResultsJson = JsonSerializer.Serialize(request.Results().Append(result));
            request.Revision++;
            if (request.PendingItems().Length == 0) { request.State = "RESUELTA"; request.DecidedBy = actor; request.DecidedAt = clock.UtcNow; request.DecisionComment = "Decisiones individuales; consulta cada tarea."; }
            Audit(actor, request, "OPERATIONAL_POSTPONEMENT_ITEM_RESOLVED");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        } catch { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); throw; }
    }
}

