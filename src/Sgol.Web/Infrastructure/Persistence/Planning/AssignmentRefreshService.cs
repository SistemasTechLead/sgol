using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record AssignmentRefreshRequest(Guid TaskId, Guid Key, DateTimeOffset NotBefore,
    Guid? PersonId = null, bool ReplaceRemainingEstimate = false, bool ChangeAuthorizedDeadline = false, bool AdoptLegacyCalendar = false, bool RetireInactiveAssignments = false, bool RetireExcludedAssignments = false);
public sealed record AssignmentRefreshDifference(OperationalAssignment? Before, OperationalAssignment? After,
    string[] Fields, bool ProcessStage = false, bool Retire = false)
{
    public bool MetadataOnly => !Retire && Before != null && After != null && Fields.Length > 0 && Fields.All(AssignmentDefinition.IsMetadataField);
}
public sealed record AssignmentRefreshDecision(string Code, string Title, string Help, string[] TaskCodes, bool Accepted);
public sealed record AssignmentRefreshPreview(AssignmentRefreshRequest Request, string TaskName, string Fingerprint,
    AssignmentRefreshDifference[] Differences, string[] Conflicts, int Excluded)
{
    public AssignmentRefreshDecision[] Decisions { get; init; } = [];
    public string[] DecisionConflicts { get; init; } = [];
    public string[] Notices { get; init; } = [];
    public bool CanApply => Conflicts.Length == 0 && Differences.Length > 0;
}

// Explicit adoption, sharing the operational lock with delivery, progress, transfers and planning.
public sealed class AssignmentRefreshService(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock)
{
    private const string Action = "OPERATIONAL_ASSIGNMENTS_REFRESHED";
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static object TemporalState(TemporalEligibilityContext context)
    {
        var json = JsonSerializer.SerializeToNode(context)!;
        json.AsObject().Remove("Now");
        return json;
    }
    private static Guid StableId(Guid key, string value) => new(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"refresh:{key}:{value}"))[..16]);

    public async Task<Dictionary<Guid, int>> CountsAsync(Guid actor, CancellationToken ct)
    {
        await direction.AuthorizeAsync(actor, ct);
        var tasks = await db.OperationalTasks.AsNoTracking().Where(t => !t.Deleted).ToDictionaryAsync(t => t.Id, ct);
        var plans = await db.DirectionPreparedPlans.Where(p => p.BranchId == BranchScope.LorettaId && p.Status == "ASIGNADO").Select(p => p.Id).ToArrayAsync(ct);
        var weeklyPlans = await db.DirectionPreparedPlans.Where(p => plans.Contains(p.Id) && p.IsWeeklyOperationsPlan).Select(p => p.Id).ToArrayAsync(ct);
        var rows = await db.OperationalAssignments.AsNoTracking().Where(a => plans.Contains(a.PlanId) && a.CompletedAt == null).ToArrayAsync(ct);
        var stages = await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s => db.Set<OperationalSequenceRun>().Any(r => r.Id == s.RunId && r.Status == "EN_CURSO" && plans.Contains(r.PlanId))).ToArrayAsync(ct);
        var attempts = await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a => a.AssignmentId != null).ToArrayAsync(ct);
        var linked = attempts.Where(a => stages.Any(s => s.CurrentAttemptId == a.Id)).Select(a => a.AssignmentId!.Value).ToHashSet();
        var result = rows.Where(AssignmentDefinition.Pending).Where(a => !linked.Contains(a.Id) && tasks.ContainsKey(a.TaskId) &&
            (AssignmentDefinition.Differences(a, tasks[a.TaskId]).Length > 0 || !tasks[a.TaskId].Active || weeklyPlans.Contains(a.PlanId) && !tasks[a.TaskId].IsWeeklyOperationsTask)).GroupBy(a => a.TaskId).ToDictionary(g => g.Key, g => g.Count());
        foreach (var stage in stages.Where(s => s.Status is not ("CONCLUIDA" or "CANCELADA")))
        {
            if (!tasks.TryGetValue(stage.TaskId, out var task)) continue;
            var a = OperationalProcessService.Reservation(stage, Guid.Empty);
            a.DefinitionSnapshotJson = stage.DefinitionSnapshotJson;
            a.AssignedRole = stage.Role;
            var assignmentId = attempts.FirstOrDefault(x => x.Id == stage.CurrentAttemptId)?.AssignmentId;
            var executable = assignmentId is {} id ? rows.SingleOrDefault(r => r.Id == id && AssignmentDefinition.Pending(r)) : null;
            if (assignmentId != null && executable == null) continue;
            if (!task.Active || AssignmentDefinition.Differences(a, task, true).Length > 0 || executable != null && AssignmentDefinition.Differences(executable, task, true).Length > 0) result[task.Id] = result.GetValueOrDefault(task.Id) + 1;
        }
        return result;
    }

    public async Task<AssignmentRefreshPreview> PreviewAsync(Guid actor, AssignmentRefreshRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        await direction.AuthorizeAsync(actor, ct);
        var result = await BuildAsync(request, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<AssignmentRefreshPreview> BuildAsync(AssignmentRefreshRequest request, CancellationToken ct)
    {
        if (request.Key == Guid.Empty || request.NotBefore.Offset != TimeSpan.Zero || request.NotBefore > clock.UtcNow.AddHours(1))
            throw new DirectionPlanException("REFRESH_INVALID", 422);
        if (request.NotBefore < clock.UtcNow.AddMinutes(-10)) throw new DirectionPlanException("REFRESH_STALE", 409);
        var definitions = await db.OperationalTasks.AsNoTracking().OrderBy(t => t.Id).ToDictionaryAsync(t => t.Id, ct);
        var selected = definitions.Values.Where(t => !t.Deleted && (request.TaskId == Guid.Empty || t.Id == request.TaskId)).ToDictionary(t => t.Id);
        if (request.TaskId != Guid.Empty && selected.Count == 0) throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        if (request.TaskId == Guid.Empty && request.PersonId != null) throw new DirectionPlanException("REFRESH_INVALID", 422);
        var plans = await db.DirectionPreparedPlans.AsNoTracking().Where(p => p.BranchId == BranchScope.LorettaId && p.Status == "ASIGNADO").OrderBy(p => p.Id).ToArrayAsync(ct);
        var planIds = plans.Select(p => p.Id).ToArray();
        var all = await db.OperationalAssignments.AsNoTracking().Where(a => planIds.Contains(a.PlanId)).OrderBy(a => a.Id).ToArrayAsync(ct);
        var runs = await db.Set<OperationalSequenceRun>().AsNoTracking().Where(r => planIds.Contains(r.PlanId) && r.Status == "EN_CURSO").OrderBy(r => r.Id).ToArrayAsync(ct);
        var runIds = runs.Select(r => r.Id).ToArray();
        var stages = await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s => runIds.Contains(s.RunId)).OrderBy(s => s.RunId).ThenBy(s => s.Position).ToArrayAsync(ct);
        var stageIds = stages.Select(s => s.Id).ToArray();
        var attempts = await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a => stageIds.Contains(a.StageId)).OrderBy(a => a.Id).ToArrayAsync(ct);
        var linked = attempts.Where(a => a.AssignmentId != null).ToDictionary(a => a.AssignmentId!.Value);
        var conflicts = new List<string>();
        var decisionConflicts = new List<string>();
        var decisions = new Dictionary<string, (string Title, string Help, bool Accepted, HashSet<string> Tasks)>();
        void Decision(string code, string title, string help, bool accepted, string taskCode)
        {
            if (!decisions.TryGetValue(code, out var item)) item = (title, help, accepted, []);
            item.Tasks.Add(taskCode); decisions[code] = item;
            if (!accepted)
            {
                var message = $"{taskCode}: marca «{title}» y pulsa Revisar cambios para continuar.";
                conflicts.Add(message); decisionConflicts.Add(message);
            }
        }
        var cancelRuns = stages.Where(s => selected.TryGetValue(s.TaskId, out var t) && !t.Active && s.Status is not ("CONCLUIDA" or "CANCELADA") &&
            attempts.Any(a => a.Id == s.CurrentAttemptId && a.Status is "PREPARADO" or "ACTIVO" &&
                (a.AssignmentId == null || all.Any(row => row.Id == a.AssignmentId && AssignmentDefinition.Pending(row)))))
            .Select(s => s.RunId).ToHashSet();
        var notices = runs.Where(r => cancelRuns.Contains(r.Id)).Select(r => $"Se cancelará el proceso «{r.Name}» y se retirarán sus pendientes. Las etapas ya entregadas conservarán su historial.").ToArray();
        var diffs = new List<AssignmentRefreshDifference>();
        var stageRows = new Dictionary<Guid, OperationalSequenceStage>();
        var targets = all.Where(a => selected.ContainsKey(a.TaskId) && AssignmentDefinition.Pending(a) && !linked.ContainsKey(a.Id)).ToList();
        foreach (var stage in stages.Where(s => (selected.ContainsKey(s.TaskId) || cancelRuns.Contains(s.RunId)) && s.Status is not ("CONCLUIDA" or "CANCELADA")))
        {
            var attempt = attempts.SingleOrDefault(a => a.Id == stage.CurrentAttemptId);
            var assignment = attempt?.AssignmentId is {} id ? all.SingleOrDefault(a => a.Id == id) : null;
            if (assignment != null && !AssignmentDefinition.Pending(assignment)) continue;
            if (attempt == null || attempt.Status is not ("PREPARADO" or "ACTIVO")) continue;
            var row = assignment ?? OperationalProcessService.Reservation(stage, runs.Single(r => r.Id == stage.RunId).PlanId);
            row = AssignmentDefinition.Copy(row);
            row.DefinitionSnapshotJson ??= stage.DefinitionSnapshotJson;
            stageRows[row.Id] = stage;
            targets.Add(row);
        }
        var specialties = await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().AsNoTracking().Where(s => s.Active).ToDictionaryAsync(s => s.Id, ct);
        var currentPolicy = await OperationalSchedulingPolicyService.CurrentAsync(db, ct);
        var source = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
        var carryovers = await db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r => r.BranchId == BranchScope.LorettaId && r.State != "RECHAZADA").OrderBy(r => r.Id).ToArrayAsync(ct);
        foreach (var a in targets)
        {
            var task = definitions[a.TaskId];
            var process = stageRows.ContainsKey(a.Id);
            if (!task.Active || process && cancelRuns.Contains(stageRows[a.Id].RunId))
            {
                Decision("inactive", "Retirar asignaciones de tareas inactivas", "Quita sus pendientes del trabajo activo, incluso si tienen avance. Conserva avances, evidencias y entregas como historial.", request.RetireInactiveAssignments, task.TaskCode);
                {
                    var retired = AssignmentDefinition.Copy(a); retired.RetiredAt = request.NotBefore;
                    diffs.Add(new(a, retired, [!task.Active ? "Retiro por tarea inactiva" : "Retiro por cancelación del proceso"], process, true));
                }
                continue;
            }
            if (!process && plans.Single(p => p.Id == a.PlanId).IsWeeklyOperationsPlan && !task.IsWeeklyOperationsTask)
            {
                Decision("excluded", "Retirar pendientes de tareas que salieron del plan", "Estas tareas siguen activas, pero ya no pertenecen al plan de operaciones semanal. Quita sus pendientes de ese plan, incluso correcciones o avances. Conserva el historial.", request.RetireExcludedAssignments, task.TaskCode);
                {
                    var retired = AssignmentDefinition.Copy(a); retired.RetiredAt = request.NotBefore;
                    diffs.Add(new(a, retired, ["Retiro por salida del plan"], false, true));
                }
                continue;
            }
            var fields = AssignmentDefinition.Differences(a, task, process);
            if (fields.Length == 0) continue;
            if (request.PersonId is {} requestedPerson && requestedPerson != a.PersonId) fields = fields.Append("Responsable").ToArray();
            var specialty = task.SpecializationId is {} sid ? specialties.GetValueOrDefault(sid) : null;
            if (task.SpecializationId != null && specialty == null) { conflicts.Add(task.TaskCode + ": la especialización requerida está inactiva."); continue; }
            if (fields.Contains("Pertenencia al proceso") || process && task.ScheduleMode == "PROGRESO")
            { conflicts.Add(a.TaskCode + ": el cambio de proceso o la modalidad de progreso requiere resolver la estructura desde el editor del proceso."); continue; }
            if (fields.Contains("Calendario anterior sin verificar"))
            {
                Decision("calendar", "Usar el calendario actual de las tareas", "Las asignaciones antiguas no tienen un calendario verificable. Esta opción permite usar los días y horarios configurados hoy.", request.AdoptLegacyCalendar, task.TaskCode);
                fields = fields.Concat(new[] { "Recurrencia", "Hora de activación" }).Distinct().ToArray();
            }
            if (process && !runs.Single(r => r.Id == stageRows[a.Id].RunId).UsesTaskEvidence && task.Requirement != "DOCUMENTO")
            { conflicts.Add(a.TaskCode + ": este proceso usa circulación de documentos PDF. Cambiar el tipo de evidencia requiere preparar una versión y resolver su flujo documental."); continue; }
            var next = AssignmentDefinition.Copy(a);
            if (a.ProgressUnits > 0 && (task.ScheduleMode != "PROGRESO" || task.ProgressUnit != a.ProgressUnit))
            { conflicts.Add($"{a.PersonName}: hay {a.ProgressUnits} {a.ProgressUnit}. Cambiar unidad o abandonar PROGRESO requiere una conversión documentada; conserva la unidad y modalidad para actualizar este pendiente."); continue; }
            if (a.TransferRemainingMinutes != null && fields.Any(f => f is "Carga" or "Meta o unidad"))
            {
                Decision("remaining", "Recalcular el tiempo pendiente", "Sustituye la estimación manual anterior por el tiempo calculado con la nueva carga y la meta. El avance registrado se conserva.", request.ReplaceRemainingEstimate, task.TaskCode);
            }
            if (fields.All(AssignmentDefinition.IsMetadataField)) AssignmentDefinition.AdoptMetadata(next,task);
            else AssignmentDefinition.Adopt(next, task, specialty?.Name, process);
            if (a.TransferRemainingMinutes != null && fields.Any(f => f is "Carga" or "Meta o unidad"))
            { next.TransferRemainingMinutes = null; next.TransferProgressUnits = 0; }
            if (request.PersonId is {} recipient)
            {
                var worker = source.Workers.SingleOrDefault(w => w.Id == recipient && w.EligibleFor(task.AssignedRole, task.SpecializationId));
                if (worker == null) { conflicts.Add("El responsable seleccionado no cumple el rol o especialización vigente."); continue; }
                next.PersonId = worker.Id; next.PersonName = worker.Name; next.AssignedRole = worker.Role;
            }
            diffs.Add(new(a, next, fields, process));
        }

        // Calendar reconciliation is bounded by existing plan dates, never by historical repetition.
        foreach (var group in diffs.Where(d => !d.ProcessStage && d.Before!.OccasionId == null &&
            d.Fields.Any(f => f is "Recurrencia" or "Modalidad")).GroupBy(d => (d.Before!.PlanId, d.Before.TaskId)).ToArray())
        {
            var plan = plans.Single(p => p.Id == group.Key.PlanId);
            var task = selected[group.Key.TaskId];
            var today = WeekContract.LocalToday(request.NotBefore);
            var from = plan.StartsOn > today ? plan.StartsOn : today;
            if (from > plan.EndsOn) continue; // Old obligations survive as overdue work.
            var expected = task.PeriodType == "OCASIONAL" ? new List<DateOnly>() : OperationalSchedule.Dates(plan.StartsOn, plan.EndsOn, task).ToList();
            if (MonthlyTaskCalendar.UsesCalendar(task) && MonthlyTaskCalendar.Occurrences(task, from, plan.EndsOn).Any(o => o.ValidDate == null))
                conflicts.Add(task.TaskCode + ": el nuevo calendario contiene una fecha inexistente. Corrige sus días del mes antes de retirar o agregar ocurrencias.");
            var protectedRows = all.Where(a => a.PlanId == plan.Id && a.TaskId == task.Id && !AssignmentDefinition.Pending(a) && a.ReviewStatus != "REASIGNADA").ToArray();
            foreach (var closed in protectedRows)
            {
                var day = WeekContract.LocalToday(closed.NominalStartsAt ?? closed.StartsAt);
                var index = expected.IndexOf(day);
                if (index < 0) index = expected.FindIndex(d => OperationalSchedule.LoadRange(d, task.PeriodType).From == OperationalSchedule.LoadRange(day, task.PeriodType).From);
                if (index >= 0) expected.RemoveAt(index);
            }
            // Other overlapping plans already own their occurrences. A catalogue refresh
            // must not bypass the generator's branch-wide repetition accounting.
            var external = all.Where(a => a.TaskId == task.Id && a.PlanId != plan.Id && a.ReviewStatus != "REASIGNADA")
                .Select(a => diffs.FirstOrDefault(d => d.Before?.Id == a.Id) is {} change ? change.After : a)
                .Where(a => a != null).Select(a => a!)
                .Concat(diffs.Where(d => d.Before == null && d.After!.TaskId == task.Id && d.After.PlanId != plan.Id).Select(d => d.After!));
            foreach (var claim in external)
            {
                var day = WeekContract.LocalToday(claim.NominalStartsAt ?? claim.StartsAt);
                if (day < plan.StartsOn || day > plan.EndsOn) continue;
                var index = expected.IndexOf(day);
                if (index < 0) index = expected.FindIndex(d => OperationalSchedule.LoadRange(d, task.PeriodType).From == OperationalSchedule.LoadRange(day, task.PeriodType).From);
                if (index >= 0) expected.RemoveAt(index);
            }
            // Older pending obligations still count in their cycle. They are not regenerated.
            foreach (var older in group.Where(d => WeekContract.LocalToday(d.Before!.NominalStartsAt ?? d.Before.StartsAt) < from))
            {
                var day = WeekContract.LocalToday(older.Before!.NominalStartsAt ?? older.Before.StartsAt);
                var index = expected.FindIndex(d => OperationalSchedule.LoadRange(d, task.PeriodType).From == OperationalSchedule.LoadRange(day, task.PeriodType).From);
                if (index >= 0) expected.RemoveAt(index);
            }
            expected.RemoveAll(d => d < from);
            var future = group.Where(d => WeekContract.LocalToday(d.Before!.NominalStartsAt ?? d.Before.StartsAt) >= from).OrderBy(d => d.Before!.StartsAt).ThenBy(d => d.Before!.Id).ToArray();
            foreach (var diff in future)
            {
                if (expected.Count == 0)
                {
                    if (diff.Before!.ProgressUnits > 0 || diff.Before.TransferRemainingMinutes != null || diff.Before.ReassignedFromId != null)
                        conflicts.Add($"{diff.Before.TaskCode} · {diff.Before.PersonName}: sobra una ocurrencia con actividad; no puede retirarse automáticamente.");
                    else { diffs.Remove(diff); diffs.Add(diff with { After = null, Fields = ["Retirar ocurrencia sin actividad"] }); }
                    continue;
                }
                var day = expected[0]; expected.RemoveAt(0);
                diff.After!.NominalStartsAt = task.StartOn(day);
                diff.After.StartsAt = diff.After.NominalStartsAt.Value;
            }
            foreach (var day in expected)
            {
                var template = group.First().After!;
                var added = AssignmentDefinition.Copy(template);
                added.Id = StableId(request.Key, $"{plan.Id}:{day}:{expected.Count}:{diffs.Count}");
                added.ScheduleKey = $"refresh:{added.Id:N}";
                added.ProgressUnits = 0; added.TransferRemainingMinutes = null; added.TransferProgressUnits = 0;
                added.ReassignedFromId = null; added.ReassignmentComment = null; added.ReviewStatus = null;
                added.ReviewedAt = null; added.ReviewedBy = null; added.ReviewComment = null; added.ReviewDecision = null;
                added.Comment = null; added.Conclusion = null; added.PreviousLoadMinutes = 0;
                added.NominalStartsAt = task.StartOn(day); added.StartsAt = added.NominalStartsAt.Value;
                diffs.Add(new(null, added, ["Agregar ocurrencia"]));
            }
        }
        var affectedIds = diffs.Where(d => d.Before != null).Select(d => d.Before!.Id).ToHashSet();
        var evidence = await db.OperationalPhotographs.AsNoTracking().Where(p => affectedIds.Contains(p.AssignmentId)).OrderBy(p => p.Id).Select(p => new { p.Id, p.AssignmentId, p.AttachedAt }).ToArrayAsync(ct);
        var attachments = await db.OperationalAttachments.AsNoTracking().Where(p => affectedIds.Contains(p.AssignmentId)).OrderBy(p => p.AssignmentId).Select(p => new { p.AssignmentId, p.CreatedAt, p.FileName }).ToArrayAsync(ct);
        var progress = await db.OperationalProgressEntries.AsNoTracking().Where(p => affectedIds.Contains(p.AssignmentId)).OrderBy(p => p.Id).ToArrayAsync(ct);
        var transfers = await db.Set<OperationalTransfer>().AsNoTracking().Where(t => affectedIds.Contains(t.AssignmentId)).OrderBy(t => t.Id).ToArrayAsync(ct);
        var captureSessions = await db.Set<OperationalCaptureSession>().AsNoTracking().Where(t => affectedIds.Contains(t.AssignmentId)).OrderBy(t => t.Id).Select(t => new { t.Id, t.AssignmentId }).ToArrayAsync(ct);
        var childLinks = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => a.ReassignedFromId != null && affectedIds.Contains(a.ReassignedFromId.Value)).Select(a => a.ReassignedFromId!.Value).ToArrayAsync(ct);
        foreach (var diff in diffs.Where(d => d.After == null))
            if (evidence.Any(p => p.AssignmentId == diff.Before!.Id) || attachments.Any(p => p.AssignmentId == diff.Before!.Id) || progress.Any(p => p.AssignmentId == diff.Before!.Id) || childLinks.Contains(diff.Before!.Id) || transfers.Any(t => t.AssignmentId == diff.Before!.Id) || captureSessions.Any(t => t.AssignmentId == diff.Before!.Id) || carryovers.Any(r => r.Items().Any(i => i.AssignmentId == diff.Before!.Id)))
                conflicts.Add($"{diff.Before!.TaskCode}: la ocurrencia a retirar tiene evidencia, avance o un reemplazo.");

        // Only scheduling changes release neighbouring pending reservations. Metadata-only updates stay put.
        var scheduled = diffs.Where(d => !d.Retire && !d.MetadataOnly && d.After != null && (d.Before == null || d.Fields.Any(f => f is "Carga" or "Criticidad" or "Elegibilidad" or "Recurrencia" or "Modalidad" or "Hora de activación" or "Meta o unidad" or "Participación en planes") || d.Before.PersonId != d.After.PersonId)).ToList();
        var days = scheduled.Select(d => WeekContract.LocalToday(d.After!.NominalStartsAt ?? d.After.StartsAt)).ToHashSet();
        scheduled.AddRange(diffs.Where(d => !d.Retire && !d.MetadataOnly && d.Before != null && d.After != null && !scheduled.Contains(d) && d.After.SchedulingPolicyId != null &&
            d.Before.StartsAt >= request.NotBefore && days.Contains(WeekContract.LocalToday(d.Before.StartsAt))));
        foreach (var neighbour in all.Where(a => !affectedIds.Contains(a.Id) && !linked.ContainsKey(a.Id) && a.OccasionId == null && AssignmentDefinition.Pending(a) && a.SchedulingPolicyId != null &&
            a.StartsAt >= request.NotBefore && days.Contains(WeekContract.LocalToday(a.StartsAt))))
        {
            var diff = new AssignmentRefreshDifference(neighbour, AssignmentDefinition.Copy(neighbour), ["Reacomodo por capacidad"]);
            diffs.Add(diff); scheduled.Add(diff);
        }
        var affectedRuns = scheduled.Where(d => d.ProcessStage).Select(d => stageRows[d.Before!.Id].RunId).ToHashSet();
        foreach (var stage in stages.Where(s => affectedRuns.Contains(s.RunId) && s.Status is not ("CONCLUIDA" or "CANCELADA")))
        {
            var attempt = attempts.SingleOrDefault(a => a.Id == stage.CurrentAttemptId);
            if (attempt == null || attempt.Status is not ("PREPARADO" or "ACTIVO")) continue;
            var row = attempt.AssignmentId is {} assignmentId ? all.SingleOrDefault(a => a.Id == assignmentId) : OperationalProcessService.Reservation(stage, runs.Single(r => r.Id == stage.RunId).PlanId);
            if (row == null || !AssignmentDefinition.Pending(row) || diffs.Any(d => d.Before?.Id == row.Id)) continue;
            row = AssignmentDefinition.Copy(row); row.DefinitionSnapshotJson ??= stage.DefinitionSnapshotJson;
            stageRows[row.Id] = stage;
            var diff = new AssignmentRefreshDifference(row, AssignmentDefinition.Copy(row), ["Reacomodo por capacidad"], true);
            diffs.Add(diff); scheduled.Add(diff);
        }
        foreach (var diff in scheduled.Where(d => d.After!.SchedulingPolicyId == null))
        {
            if (!source.Temporal.Enabled) { conflicts.Add(diff.After!.TaskCode + ": configura los horarios antes de adoptar una agenda con carga y cierre calculados."); continue; }
            var row = diff.After!;
            row.SchedulingPolicyId = currentPolicy.Id; row.ClosureMarginPercent = currentPolicy.MarginPercent;
            row.NominalStartsAt ??= row.StartsAt;
            if (!OperationalAgenda.IsDefined(row.OperationalCriticality)) conflicts.Add(row.TaskCode + ": selecciona una criticidad operativa.");
        }
        var released = scheduled.Where(d => d.Before != null).Select(d => d.Before!.Id).Concat(diffs.Where(d => d.After == null || d.Retire).Select(d => d.Before!.Id)).ToHashSet();
        // Active process reservations can have the stage ID in the ledger as well as the assignment ID.
        foreach (var id in released.ToArray()) if (stageRows.TryGetValue(id, out var stage)) released.Add(stage.Id);
        var ledger = new CapacityLedger(source.Workers, source.Tasks.Where(a => !released.Contains(a.Id)).ToArray(), source.Capacities, source.Unavailable, source.Legacy, source.Today)
        { Temporal = source.Temporal, PlannedChanges = source.PlannedChanges, Override = source.Override };
        var schedulingRows = scheduled.OrderBy(d => d.ProcessStage ? 0 : 1)
            .ThenBy(d => d.ProcessStage ? stageRows[d.Before!.Id].Position : OperationalAgenda.Priority(d.After!.OperationalCriticality))
            .ThenBy(d => d.After!.StartsAt).ThenBy(d => d.After!.Id).ToArray();
        if (schedulingRows.Length > 500) conflicts.Add("La propuesta supera 500 bloques de agenda. Reduce el alcance operativo antes de volver a calcular.");
        var contributions = all.Where(a => a.CompletedAt != null && a.ReviewStatus != "REASIGNADA")
            .GroupBy(a => (a.TaskId, a.PersonId, Week: WeeklyBoardSnapshot.Monday(WeekContract.LocalToday(a.StartsAt))))
            .ToDictionary(g => g.Key, g => g.Count());
        var budget = 0;
        bool Search(CapacityLedger state, int index)
        {
            ct.ThrowIfCancellationRequested();
            if (index == schedulingRows.Length) return true;
            if (++budget > 20000) throw new DirectionPlanException("SCHEDULING_SEARCH_INCONCLUSIVE", 422);
            var diff = schedulingRows[index]; var row = diff.After!;
            var target = selected.ContainsKey(row.TaskId) && !diff.Fields.SequenceEqual(new[] { "Reacomodo por capacidad" });
            var definition = definitions[row.TaskId];
            var requiredRole = target ? definition.AssignedRole : AssignmentDefinition.Baseline(row, definition).AssignedRole;
            var week = WeeklyBoardSnapshot.Monday(WeekContract.LocalToday(row.NominalStartsAt ?? row.StartsAt));
            var people = state.Workers.Where(w => w.EligibleFor(requiredRole, row.SpecializationId) &&
                (!target || request.PersonId == null || w.Id == request.PersonId) && (target || w.Id == row.PersonId))
                .OrderBy(w => row.DifferentPeople ? contributions.GetValueOrDefault((row.TaskId, w.Id, week)) +
                    state.Tasks.Count(a => a.CompletedAt == null && AssignmentDefinition.Pending(a) && a.TaskId == row.TaskId && a.PersonId == w.Id &&
                        WeeklyBoardSnapshot.Monday(WeekContract.LocalToday(a.StartsAt)) == week) : 0)
                .ThenBy(w => w.Id != (diff.Before?.PersonId ?? row.PersonId)).ThenBy(w => w.Code, StringComparer.Ordinal).ToArray();
            var original = AssignmentDefinition.Copy(row);
            var preserveAnchor = diff.Before != null && !diff.Fields.Contains("Hora de activación") &&
                (!diff.Fields.Contains("Recurrencia") || original.NominalStartsAt == diff.Before.NominalStartsAt);
            var earliest = request.NotBefore;
            OperationalSequenceStage? processStage = null;
            if (diff.ProcessStage)
            {
                processStage = stageRows[diff.Before!.Id];
                var previousStage = stages.LastOrDefault(s => s.RunId == processStage.RunId && s.Position < processStage.Position);
                var previousProposal = schedulingRows.FirstOrDefault(d => d.ProcessStage && stageRows[d.Before!.Id].Id == previousStage?.Id)?.After;
                earliest = new[] { earliest, processStage.MinimumStartsAt ?? earliest, processStage.DependencySatisfiedAt ?? earliest,
                    previousProposal?.DueAt ?? (previousStage?.Status == "CONCLUIDA" ? earliest : previousStage?.EffectiveDueAt ?? previousStage?.PlannedDueAt ?? earliest) }.Max();
            }
            foreach (var worker in people)
            {
                row.PersonId = worker.Id; row.PersonName = worker.Name; row.AssignedRole = worker.Role;
                // Prefer preserving the current valid block before exploring new placements.
                if (preserveAnchor && diff.Before != null &&
                    (row.StartsAt >= earliest || row.ScheduleMode == "PROGRESO" && worker.Id == diff.Before.PersonId) && state.CanAssign(worker.Id, row) &&
                    Search(state.WithAdded(AssignmentDefinition.Copy(row)), index + 1)) return true;
                // A different duration need not change activation. Try its new closing time
                // at the original start before moving flexible work earlier or later.
                if (preserveAnchor && row.SchedulingPolicyId != null && row.ScheduleMode != "PROGRESO" && row.OccasionId == null && original.StartsAt >= earliest)
                {
                    row.StartsAt = original.StartsAt;
                    row.DueAt = row.StartsAt.AddMinutes(OperationalAgenda.Duration(row.LoadMinutes, row.ClosureMarginPercent!.Value));
                    if ((processStage?.FixedDueAt == null || row.DueAt <= processStage.FixedDueAt) && state.CanAssign(worker.Id, row) &&
                        Search(state.WithAdded(AssignmentDefinition.Copy(row)), index + 1)) return true;
                    row.DueAt = original.DueAt;
                }
                if (row.OccasionId != null || diff.ProcessStage)
                {
                    // Their own windows/dependencies are authoritative; use the current day only.
                    row.StartsAt = original.StartsAt; row.DueAt = original.DueAt; row.NominalStartsAt = original.NominalStartsAt;
                }
                var plan = plans.Single(p => p.Id == row.PlanId);
                var first = WeekContract.LocalToday(original.NominalStartsAt ?? original.StartsAt);
                var today = WeekContract.LocalToday(request.NotBefore);
                if (first < today) first = today;
                var through = target && row.OccasionId == null && !diff.ProcessStage && row.ScheduleMode == "DISTRIBUIDA"
                    ? new[] { plan.EndsOn >= first ? plan.EndsOn : first.AddDays(6), OperationalSchedule.LoadRange(first, row.PeriodType).Through }.Min() : first;
                var candidateDays = target && row.OccasionId == null && !diff.ProcessStage && definition.PreferredMonthDays.Length > 0
                    ? MonthlyTaskCalendar.Candidates(WeekContract.LocalToday(original.NominalStartsAt ?? original.StartsAt), plan.StartsOn, plan.EndsOn, today)
                    : CapacityLedger.Days(first, through).Where(d => definition.PeriodType != "MENSUAL" || d.DayOfWeek != DayOfWeek.Sunday).ToArray();
                foreach (var day in candidateDays)
                {
                    if (row.OccasionId != null || diff.ProcessStage)
                    {
                        row.NominalStartsAt = original.NominalStartsAt ?? original.StartsAt;
                        row.StartsAt = original.StartsAt; row.DueAt = original.DueAt;
                    }
                    else
                    {
                        var nominal = target ? definition.StartOn(day) : original.NominalStartsAt ?? original.StartsAt;
                        row.NominalStartsAt = nominal; row.StartsAt = nominal;
                    }
                    if (row.SchedulingPolicyId == null)
                    {
                        // Missing policy adoption is a conflict, never a free legacy slot.
                        continue;
                    }
                    if (row.ScheduleMode == "PROGRESO")
                    {
                        row.StartsAt = original.StartsAt < request.NotBefore ? request.NotBefore : original.StartsAt;
                        row.NominalStartsAt = row.StartsAt;
                        row.DueAt = original.DueAt > row.StartsAt ? original.DueAt : OperationalSchedule.Instant(today.AddDays(7));
                    }
                    foreach (var placement in OperationalAgenda.Alternatives(state, worker.Id, row, earliest))
                    {
                        if (++budget > 20000) throw new DirectionPlanException("SCHEDULING_SEARCH_INCONCLUSIVE", 422);
                        OperationalAgenda.Apply(row, placement);
                        if (processStage?.FixedDueAt is {} fixedDue && row.DueAt > fixedDue) continue;
                        if (!state.CanAssign(worker.Id, row)) continue;
                        if (Search(state.WithAdded(AssignmentDefinition.Copy(row)), index + 1)) return true;
                    }
                }
            }
            // Restore this node; descendants are copied in the successful proposal only.
            CopyValues(original, row);
            return false;
        }
        if (!conflicts.Except(decisionConflicts).Any() && schedulingRows.Length > 0)
        {
            try { if (!Search(ledger, 0)) conflicts.Add("No hay una agenda compatible con turnos, capacidad, criticidad y responsables elegibles. Revisa los pendientes y las ventanas disponibles."); }
            catch (DirectionPlanException e) when (e.Code == "SCHEDULING_SEARCH_INCONCLUSIVE") { conflicts.Add("La búsqueda alcanzó su límite. No se puede asegurar que la agenda sea imposible; reduce los conflictos y vuelve a calcular."); }
        }
        foreach (var diff in diffs.Where(d => !d.Retire && !d.MetadataOnly && d.Before != null && d.After != null))
        {
            var touched = carryovers.Where(r => r.Items().Any(i => i.AssignmentId == diff.Before!.Id)).ToArray();
            if (touched.Any(r => r.State == "PENDIENTE")) conflicts.Add($"{diff.Before!.TaskCode}: resuelve la solicitud de arrastre pendiente antes de actualizar esta asignación.");
            if (touched.Any(r => r.State == "APROBADA") && diff.Before!.DueAt != diff.After!.DueAt)
                Decision("deadline", "Permitir cambiar plazos con arrastre aprobado", "Usa las nuevas fechas que aparecen en esta revisión. El plazo original y la autorización de arrastre se conservan en el historial.", request.ChangeAuthorizedDeadline, diff.Before.TaskCode);
            if (diff.ProcessStage && stageRows.TryGetValue(diff.Before!.Id, out var stage))
            {
                if (stage.FixedDueAt is {} fixedDue && diff.After!.DueAt > fixedDue) conflicts.Add($"Etapa {stage.Position}: el nuevo cierre supera el plazo fijo del proceso.");
                var previous = stages.LastOrDefault(s => s.RunId == stage.RunId && s.Position < stage.Position);
                var previousAfter = diffs.FirstOrDefault(d => d.ProcessStage && stageRows[d.Before!.Id].Id == previous?.Id)?.After;
                if (previous != null && diff.After!.StartsAt < (previousAfter?.DueAt ?? previous.EffectiveDueAt ?? previous.PlannedDueAt) && previous.Status is not ("CONCLUIDA"))
                    conflicts.Add($"Etapa {stage.Position}: el inicio propuesto rompe la dependencia anterior.");
                if (stages.Any(s => s.RunId == stage.RunId && s.Position > stage.Position && s.OperationalCriticality == "CRITICA" && s.NominalStartsAt < diff.After!.DueAt))
                    conflicts.Add($"Etapa {stage.Position}: el nuevo cierre invade una etapa crítica posterior.");
            }
        }
        // Neighbours that did not actually move need no write or revision bump.
        diffs.RemoveAll(d => d.Fields.SequenceEqual(new[] { "Reacomodo por capacidad" }) && d.Before!.StartsAt == d.After!.StartsAt && d.Before.DueAt == d.After.DueAt &&
            d.Before.SchedulingPolicyId == d.After.SchedulingPolicyId && d.Before.ClosureMarginPercent == d.After.ClosureMarginPercent && d.Before.NominalStartsAt == d.After.NominalStartsAt);
        var fingerprint = Hash(new { request, Selected = selected.Keys.Order(), currentPolicy, definitions = definitions.Values, plans, all, runs, stages, attempts, carryovers,
            source.Workers, source.Tasks, source.Capacities, Temporal = TemporalState(source.Temporal), Unavailable = source.Unavailable.OrderBy(x => x.Item1).ThenBy(x => x.Item2), Legacy = source.Legacy.OrderBy(x => x.Key), evidence, attachments, progress, childLinks, transfers, captureSessions,
            diffs, conflicts, Algorithm = 1 });
        return new(request, request.TaskId == Guid.Empty ? "Todas las tareas con asignaciones pendientes desactualizadas" : selected[request.TaskId].Name, fingerprint, diffs.ToArray(), conflicts.Distinct().ToArray(), all.Count(a => selected.ContainsKey(a.TaskId) && !AssignmentDefinition.Pending(a)))
        {
            Decisions = decisions.Select(d => new AssignmentRefreshDecision(d.Key,d.Value.Title,d.Value.Help,d.Value.Tasks.Order().ToArray(),d.Value.Accepted)).ToArray(),
            DecisionConflicts = decisionConflicts.Distinct().ToArray(), Notices = notices
        };
    }

    public async Task ApplyAsync(Guid actor, AssignmentRefreshRequest request, string fingerprint, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) throw new DirectionPlanException("REFRESH_REASON_REQUIRED", 422);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationalTaskService.LockAsync(db, ct); await direction.AuthorizeAsync(actor, ct);
        var receipt = await db.AuditEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Action == Action && e.ResourceId == request.Key, ct);
        if (receipt != null)
        {
            if (receipt.AfterData!.RootElement.GetProperty("Fingerprint").GetString() != fingerprint || receipt.AfterData.RootElement.GetProperty("Reason").GetString() != reason.Trim()) throw new DirectionPlanException("REFRESH_STALE", 409);
            await tx.CommitAsync(ct); return;
        }
        var preview = await BuildAsync(request, ct);
        if (!preview.CanApply || preview.Fingerprint != fingerprint) throw new DirectionPlanException("REFRESH_STALE", 409);
        if (preview.Differences.Any(d => d.After != null && (d.Before == null || d.After.StartsAt != d.Before.StartsAt) && d.After.ScheduleMode != "PROGRESO" && d.After.StartsAt < clock.UtcNow))
            throw new DirectionPlanException("REFRESH_STALE", 409);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
        foreach (var diff in preview.Differences)
        {
            var before = diff.Before; var after = diff.After;
            if (diff.Retire)
            {
                if (diff.ProcessStage)
                {
                    var currentAttempt = await db.Set<OperationalSequenceAttempt>().SingleOrDefaultAsync(a => a.AssignmentId == before!.Id, ct);
                    var stage = currentAttempt != null ? await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == currentAttempt.StageId, ct)
                        : await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == before!.Id, ct);
                    currentAttempt ??= await db.Set<OperationalSequenceAttempt>().SingleOrDefaultAsync(a => a.Id == stage.CurrentAttemptId, ct);
                    stage.Status = "CANCELADA"; stage.BlockReason = reason.Trim(); stage.Revision++;
                    if (currentAttempt != null) { currentAttempt.Status = "CANCELADO"; currentAttempt.FinishedAt = clock.UtcNow; currentAttempt.Revision++; }
                    var run = await db.Set<OperationalSequenceRun>().SingleAsync(r => r.Id == stage.RunId, ct);
                    if (run.Status == "EN_CURSO")
                    {
                        run.Status = "CANCELADA"; run.ClosedAt = clock.UtcNow; run.ClosedBy = actor;
                        run.ClosureOutcome = "CANCELACION"; run.ClosureComment = reason.Trim(); run.Revision++;
                    }
                    if (currentAttempt != null) db.Add(new OperationalSequenceEvent { Id = Guid.CreateVersion7(), RunId = stage.RunId, StageId = stage.Id,
                        AttemptId = currentAttempt.Id, Kind = "CANCELACION", EventKey = $"retire:{request.Key}:{stage.Id}", ActorId = actor, OccurredAt = clock.UtcNow, Comment = reason.Trim() });
                    AddAudit(actor, stage.Id, "OPERATIONAL_STAGE_RETIRED", before, new { request.Key, Reason = reason.Trim(), stage.RunId });
                    if (!await db.OperationalAssignments.AnyAsync(a => a.Id == before!.Id, ct)) continue;
                }
                var retired = await db.OperationalAssignments.SingleAsync(a => a.Id == before!.Id, ct);
                retired.RetiredAt = clock.UtcNow; retired.Revision++;
                AddAudit(actor, retired.Id, "OPERATIONAL_ASSIGNMENT_RETIRED", before, new { request.Key, Reason = reason.Trim(), retired.RetiredAt, PreservedProgress = retired.ProgressUnits });
                continue;
            }
            if (diff.ProcessStage)
            {
                var attempt = await db.Set<OperationalSequenceAttempt>().SingleOrDefaultAsync(a => a.AssignmentId == before!.Id, ct);
                var stage = attempt != null ? await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == attempt.StageId, ct)
                    : await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == before!.Id, ct);
                var hasAssignment = attempt != null;
                attempt ??= await db.Set<OperationalSequenceAttempt>().SingleAsync(a => a.Id == stage.CurrentAttemptId, ct);
                stage.Name = after!.Name; stage.Description = after.Description; stage.Requirement = after.Requirement; stage.ChecklistJson = after.ChecklistJson;
                stage.MinimumPhotos = after.MinimumPhotos; stage.MaximumPhotos = after.MaximumPhotos; stage.MaximumPhotoBytes = after.MaximumPhotoBytes;
                if (!diff.MetadataOnly)
                {
                stage.Role = after.DefinitionSnapshotJson is {} definition ? JsonSerializer.Deserialize<OperationalTask>(definition)!.AssignedRole : stage.Role;
                stage.SpecializationId = after.SpecializationId; stage.SpecializationName = after.SpecializationName;
                stage.LoadMinutes = after.LoadMinutes; stage.OperationalCriticality = after.OperationalCriticality;
                stage.SchedulingPolicyId = after.SchedulingPolicyId; stage.ClosureMarginPercent = after.ClosureMarginPercent;
                stage.TaskRevision = after.TaskRevision!.Value; stage.DefinitionSnapshotJson = after.DefinitionSnapshotJson;
                stage.ReservedPersonId = after.PersonId; stage.ReservedPersonName = after.PersonName;
                stage.PlannedStartsAt = after.StartsAt; stage.PlannedDueAt = after.DueAt;
                stage.NominalStartsAt = after.NominalStartsAt; stage.Revision++;
                if (hasAssignment) stage.EffectiveDueAt = after.DueAt;
                }
                else { stage.DefinitionSnapshotJson = after.DefinitionSnapshotJson; stage.Revision++; }
                attempt.Revision++;
                var run = await db.Set<OperationalSequenceRun>().SingleAsync(r => r.Id == stage.RunId, ct); run.Revision++;
                db.Add(new OperationalSequenceEvent { Id = Guid.CreateVersion7(), RunId = stage.RunId, StageId = stage.Id,
                    AttemptId = attempt?.Id ?? stage.CurrentAttemptId!.Value, Kind = "ASIGNACION", EventKey = $"refresh:{request.Key}:{stage.Id}",
                    OccurredAt = clock.UtcNow, ActorId = actor, Comment = reason.Trim() });
                if (!hasAssignment) { AddAudit(actor, stage.Id, "OPERATIONAL_STAGE_REFRESHED", before, after); continue; }
            }
            if (after == null)
            {
                var row = await db.OperationalAssignments.SingleAsync(a => a.Id == before!.Id, ct);
                db.Remove(row); AddAudit(actor, row.Id, "OPERATIONAL_ASSIGNMENT_RETIRED", before, new { Reason = reason.Trim(), request.Key });
                continue;
            }
            after.Revision = (before?.Revision ?? 0) + 1;
            if (before == null || before.StartsAt != after.StartsAt || before.DueAt != after.DueAt || before.PersonId != after.PersonId)
                ledger.CaptureTime(after, after.StartsAt <= clock.UtcNow ? clock.UtcNow : null);
            if (before == null) db.Add(after);
            else
            {
                var row = await db.OperationalAssignments.SingleAsync(a => a.Id == before.Id, ct);
                db.Entry(row).CurrentValues.SetValues(after);
                if (before.PersonId != after.PersonId)
                {
                    db.Add(new OperationalTransfer { Id = Guid.CreateVersion7(), AssignmentId = after.Id, TaskName = after.Name,
                        FromPersonId = before.PersonId, FromName = before.PersonName, ToPersonId = after.PersonId, ToName = after.PersonName,
                        ActorId = actor, At = clock.UtcNow, PreviousStartsAt = before.StartsAt, PreviousDueAt = before.DueAt,
                        NewDueAt = after.DueAt, ProgressUnits = before.ProgressUnits, RemainingMinutes = Math.Max(1, after.RemainingMinutes), Reason = reason.Trim(), SnapshotJson = JsonSerializer.Serialize(before) });
                }
            }
            AddAudit(actor, after.Id, "OPERATIONAL_ASSIGNMENT_REFRESHED", before, new { Assignment = after, request.Key, Reason = reason.Trim(), diff.Fields });
        }
        AddAudit(actor, request.Key, Action, null, new { preview.Fingerprint, request, Reason = reason.Trim(), Count = preview.Differences.Length });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private static void CopyValues(OperationalAssignment from, OperationalAssignment to)
    {
        foreach (var property in typeof(OperationalAssignment).GetProperties().Where(p => p.CanWrite)) property.SetValue(to, property.GetValue(from));
    }
    private void AddAudit(Guid actor, Guid id, string action, object? before, object after) => db.AuditEvents.Add(new AuditEvent {
        Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow,
        ResourceId = id, ResourceType = "ASSIGNMENT_REFRESH", BranchId = BranchScope.LorettaId, Action = action,
        Outcome = "SUCCESS", CorrelationId = Guid.CreateVersion7(), BeforeData = before == null ? null : JsonSerializer.SerializeToDocument(before), AfterData = JsonSerializer.SerializeToDocument(after) });
}
