using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public static class CarryoverCauses
{
    public const int Version = 1;
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string> {
        ["FALTA_TIEMPO_OPERATIVO"] = "Sin capacidad operativa suficiente.",
        ["ALTA_AFLUENCIA_CLIENTES"] = "La atención al cliente impidió terminar.",
        ["PRIORIDAD_COMERCIAL"] = "Actividad comercial prioritaria autorizada.",
        ["FALTA_RESPONSABLE"] = "Responsable ausente o no disponible.",
        ["OMISION_RESPONSABLE"] = "Incumplimiento atribuible al responsable.",
        ["FALTA_EVIDENCIA"] = "Evidencia insuficiente.",
        ["ERROR_DE_ASIGNACION"] = "Error de puesto, persona, fecha o capacidad.",
        ["DEPENDENCIA_EXTERNA"] = "Dependencia de proveedor, cliente, sistema o tercero.",
        ["FALLA_SISTEMA_HERRAMIENTA"] = "Falla técnica o falta de herramienta.",
        ["CAUSA_NO_IDENTIFICADA"] = "No fue posible determinar la causa." };
}
public sealed record CarryoverItem(Guid AssignmentId, Guid PersonId, Guid PlanId, long Revision, string Task,
    string? Role, DateTimeOffset Start, DateTimeOffset Due, DateTimeOffset Proposed, bool Direct,
    Guid Chain, string? ReviewStatus, string Warning, string Structure, string PersonName)
{
    public DateTimeOffset? ProposedStart { get; init; }
    public int WorkMinutes { get; init; }
    public string? LoadSummary { get; init; }
    public DateOnly? BeforeDate { get; init; }
    public double Minutes => (Proposed - Due).TotalMinutes;
}
public sealed record CarryoverPreview(Guid PersonId, DateTimeOffset End, int Minutes, string Cause,
    string Explanation, CarryoverItem[] Items, string[] Conflicts, string Fingerprint);
public sealed record CarryoverResult(Guid AssignmentId, DateTimeOffset Before, DateTimeOffset After,
    DateTimeOffset RecognizedDue, DateTimeOffset? CompletedAt, string Kind);
public sealed class OperationalCarryoverRequest
{
    public string Kind { get; set; } = "INCIDENTE";
    public DateOnly? From { get; set; }
    public DateOnly? Through { get; set; }
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid PersonId { get; set; }
    public Guid RequestedBy { get; set; }
    public Guid IntentKey { get; set; }
    public string Cause { get; set; } = "";
    public string CauseDescription { get; set; } = "";
    public int CauseVersion { get; set; } = CarryoverCauses.Version;
    public int AlgorithmVersion { get; set; } = 1;
    public string Explanation { get; set; } = "";
    public DateTimeOffset IncidentStart { get; set; }
    public DateTimeOffset IncidentEnd { get; set; }
    public int Minutes { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string State { get; set; } = "PENDIENTE";
    public long Revision { get; set; } = 1;
    public string Fingerprint { get; set; } = "";
    public string SnapshotJson { get; set; } = "[]";
    public string ScheduleFingerprint { get; set; } = "";
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }
    public string ResultsJson { get; set; } = "[]";
    public CarryoverItem[] Items() => JsonSerializer.Deserialize<CarryoverItem[]>(SnapshotJson)!;
    public CarryoverResult[] Results() => JsonSerializer.Deserialize<CarryoverResult[]>(ResultsJson)!;
}

// The calculator consumes a single day's pending ordinary tasks. Closed and long-window work cannot bridge chains.
public static class OperationalCarryoverCalculator
{
    public static string Structure(OperationalAssignment a) => JsonSerializer.Serialize(new { a.ItemLoadSourceAttemptId, a.TaskId, a.TaskRevision, a.PersonId, a.PlanId, a.StartsAt, a.DueAt, a.AssignedRole, a.SpecializationId, a.ScheduleMode, a.OperationalCriticality, a.SchedulingPolicyId, a.TemporalPolicyJson, a.TemporalDecisionJson, a.ReassignedFromId, a.LoadMinutes, a.Requirement, a.MinimumPhotos, a.MaximumPhotos, a.MaximumPhotoBytes });
    public static CarryoverItem[] Calculate(IEnumerable<OperationalAssignment> source, DateTimeOffset end, int minutes,
        IReadOnlyList<WorkInterval> availability)
    {
        var day = WeekContract.LocalToday(end);
        var start = end.AddMinutes(-minutes);
        var intervals = availability.OrderBy(w => w.Entry).ToArray();
        double Available(DateTimeOffset a, DateTimeOffset b) => intervals.Sum(w => Math.Max(0,
            (new[] { b, TemporalEligibilityContext.At(day, w.Exit) }.Min() - new[] { a, TemporalEligibilityContext.At(day, w.Entry) }.Max()).TotalMinutes));
        DateTimeOffset? f = null; double delay = 0; Guid chain = Guid.Empty;
        var output = new List<CarryoverItem>();
        foreach (var group in source.Where(a => a.CompletedAt == null && a.ReviewStatus is not ("REASIGNADA" or "CONCLUIDA" or "NO_SATISFECHA") &&
            WeekContract.LocalToday(a.StartsAt) == day && a.ScheduleMode != "PROGRESO" && WeekContract.LocalToday(a.DueAt) == day)
            .GroupBy(a => a.StartsAt).OrderBy(g => g.Key))
        {
            var propagated = f is {} frontier ? Math.Max(0, delay - Available(frontier, group.Key)) : 0;
            if (propagated <= 0) { f = null; delay = 0; chain = Guid.Empty; }
            var prior = f; var nextF = f; double nextDelay = 0;
            foreach (var a in group.OrderBy(a => a.Id))
            {
                var direct = a.StartsAt < end && a.DueAt > start;
                var extra = Math.Max(direct ? minutes : 0, propagated);
                if (extra <= 0) continue;
                if (chain == Guid.Empty) chain = a.Id;
                var proposed = a.DueAt.AddMinutes(extra);
                var warning = a.OperationalCriticality == "CRITICA" ? "Crítica: la fecha límite real puede seguir incumplida. " : "";
                if (intervals.Length > 0 && proposed > TemporalEligibilityContext.At(day, intervals.Max(w => w.Exit))) warning += "Excepción de vencimiento fuera de turno; no autoriza ampliar la jornada. ";
                if (WeekContract.LocalToday(proposed) != day) warning += "CONFLICTO: cruza al siguiente día. ";
                output.Add(new(a.Id, a.PersonId, a.PlanId, a.Revision, a.TaskCode + " · " + a.Name, a.AssignedRole,
                    a.StartsAt, a.DueAt, proposed, direct, chain, a.ReviewStatus, warning, Structure(a), a.PersonName));
                if (nextF == null || a.DueAt > nextF) nextF = a.DueAt;
            }
            if (nextF is {} nf)
            {
                // Delay is work time still outstanding at the old frontier; breaks never absorb it.
                nextDelay = prior is {} pf ? Math.Max(0, delay - Available(pf, nf)) : 0;
                foreach (var item in output.Where(i => i.Start == group.Key))
                    nextDelay = Math.Max(nextDelay, item.Minutes - Available(item.Due, nf));
                f = nf; delay = nextDelay;
            }
        }
        return output.ToArray();
    }
}

public sealed partial class OperationalCarryoverService(SgolDbContext db, IClock clock, OperationalSupervisionService supervision)
{
    private static DirectionPlanException Error(string code, int status = 409) => new(code, status);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private async Task<Guid> Person(Guid actor, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor && u.Status == AccountStatus.Active, ct);
        if (user == null || !await db.EmploymentVersions.AnyAsync(e => e.PersonId == user.PersonId && e.BranchId == BranchScope.LorettaId && e.Status == EmploymentStatus.Active && e.ValidFrom <= now && (e.ValidTo == null || e.ValidTo > now), ct) ||
            !await db.RoleAssignmentVersions.AnyAsync(r => r.UserId == actor && r.BranchId == BranchScope.LorettaId && r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now), ct)) throw Error("ACCESO_DENEGADO", 403);
        return user.PersonId;
    }
    private async Task<(WorkInterval[] Windows, string Hash)> Schedule(Guid person, DateOnly day, CancellationToken ct)
    {
        var context = await TemporalEligibilityContext.LoadAsync(db, clock.UtcNow, ct);
        var captured = context.Day(person, day, false);
        var unavailable = await db.AvailabilityDayVersions.AnyAsync(a => a.PersonId == person && a.LocalDate == day && a.BranchId == BranchScope.LorettaId && a.Status == AvailabilityVersionStatus.Current && !a.IsAvailable, ct);
        return (unavailable ? [] : captured.Intervals ?? [], Hash(new { captured.Schedule, captured.Exception, unavailable }));
    }
    public async Task<CarryoverPreview> PreviewAsync(Guid actor, DateTimeOffset end, int minutes, string cause, string? explanation, CancellationToken ct)
    {
        var person = await Person(actor, ct);
        var day = WeekContract.LocalToday(end); explanation = explanation?.Trim() ?? "";
        if (minutes <= 0 || end > clock.UtcNow || day != WeekContract.LocalToday(clock.UtcNow) ||
            minutes > (end - OperationalSchedule.Instant(day)).TotalMinutes || !CarryoverCauses.All.ContainsKey(cause) || explanation.Length > 4000 ||
            cause is "CAUSA_NO_IDENTIFICADA" or "PRIORIDAD_COMERCIAL" && explanation.Length == 0) throw Error("ARRASTRE_DATOS_INVALIDOS", 422);
        var startDay = OperationalSchedule.Instant(day); var nextDay = startDay.AddDays(1);
        var rows = await db.OperationalAssignments.AsNoTracking().Where(a => a.PersonId == person && a.CompletedAt == null && a.StartsAt < nextDay && a.DueAt > startDay &&
            db.DirectionPreparedPlans.Any(p => p.Id == a.PlanId && p.BranchId == BranchScope.LorettaId)).OrderBy(a => a.StartsAt).ThenBy(a => a.Id).ToArrayAsync(ct);
        var schedule = await Schedule(person, day, ct);
        var conflicts = new List<string>();
        if (schedule.Windows.Length == 0) conflicts.Add("No hay disponibilidad verificable para esta jornada.");
        var incidentStart = end.AddMinutes(-minutes);
        var availableMinutes = schedule.Windows.Sum(w => Math.Max(0, (new[] { end, TemporalEligibilityContext.At(day, w.Exit) }.Min() - new[] { incidentStart, TemporalEligibilityContext.At(day, w.Entry) }.Max()).TotalMinutes));
        if (availableMinutes < minutes) conflicts.Add("El incidente incluye descanso o tiempo fuera de turno. Solicita únicamente el tramo de trabajo afectado.");
        if (rows.Any(a => a.StartsAt < end && a.DueAt > end.AddMinutes(-minutes) && (a.ScheduleMode == "PROGRESO" || a.StartsAt < startDay || a.DueAt >= nextDay)))
            conflicts.Add("Hay una tarea de ventana larga o PROGRESO: requiere revisión de su regla específica.");
        var processIds = await db.Set<OperationalSequenceAttempt>().Where(p => p.AssignmentId != null).Select(p => p.AssignmentId!.Value).ToArrayAsync(ct);
        var items = OperationalCarryoverCalculator.Calculate(rows, end, minutes, schedule.Windows);
        if (items.Any(i => processIds.Contains(i.AssignmentId))) conflicts.Add("Un proceso requiere revisar sus dependencias; no admite este arrastre.");
        if (items.Any(i => WeekContract.LocalToday(i.Proposed) != day)) conflicts.Add("El arrastre cruza al siguiente día; requiere una regla explícita.");
        if (items.Length == 0) conflicts.Add("No hay asignaciones pendientes afectadas.");
        if (await db.Set<OperationalCarryoverRequest>().AnyAsync(r => r.Kind == "INCIDENTE" && r.PersonId == person && r.State != "RECHAZADA" && r.IncidentStart < end && r.IncidentEnd > end.AddMinutes(-minutes), ct))
            conflicts.Add("El incidente se solapa con una petición pendiente o aprobada.");
        // Verify that at least one independent active supervisor can decide the entire snapshot.
        if (items.Length > 0 && !await HasSupervisor(items, person, ct)) conflicts.Add("No hay un supervisor vigente autorizado sobre todo el alcance.");
        if (await PendingPostponement(person, items.Select(i => i.AssignmentId).ToArray(), ct)) conflicts.Add("Hay tareas incluidas en una petición de posposición pendiente.");
        return new(person, end, minutes, cause, explanation, items, conflicts.ToArray(), Hash(new { person, end, minutes, cause, explanation, items, schedule.Hash, conflicts }));
    }
    private async Task<bool> HasSupervisor(CarryoverItem[] items, Guid person, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var roles = await (from u in db.AppUsers.AsNoTracking() join e in db.EmploymentVersions on u.PersonId equals e.PersonId join r in db.RoleAssignmentVersions on u.Id equals r.UserId
            where u.PersonId != person && u.Status == AccountStatus.Active && e.BranchId == BranchScope.LorettaId && e.Status == EmploymentStatus.Active && e.ValidFrom <= now && (e.ValidTo == null || e.ValidTo > now) &&
            r.BranchId == BranchScope.LorettaId && r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now) select r.RoleCode).ToArrayAsync(ct);
        var definitions = await db.OperationalTasks.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        var ids = items.Select(i => i.AssignmentId).ToArray();
        var assignments = await db.OperationalAssignments.AsNoTracking().Where(a => ids.Contains(a.Id)).ToArrayAsync(ct);
        return roles.Any(role => assignments.All(a => OperationalSupervisionService.RolesFor(role).Contains(a.AssignedRole ?? definitions[a.TaskId].AssignedRole)));
    }
    public async Task<Guid> SubmitAsync(Guid actor, DateTimeOffset end, int minutes, string cause, string? explanation, string fingerprint, Guid intent, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await OperationalTaskService.LockAsync(db, ct);
        var person = await Person(actor, ct);
        var old = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleOrDefaultAsync(r => r.IntentKey == intent, ct);
        if (old != null) {
            if (old.RequestedBy != actor || old.Fingerprint != fingerprint) throw Error("ARRASTRE_CONFLICTO");
            await tx.CommitAsync(ct); return old.Id;
        }
        if (intent == Guid.Empty) throw Error("ARRASTRE_DATOS_INVALIDOS", 422);
        var preview = await PreviewAsync(actor, end, minutes, cause, explanation, ct);
        if (preview.Conflicts.Length > 0 || preview.Fingerprint != fingerprint) throw Error("ARRASTRE_CONFLICTO");
        var schedule = await Schedule(person, WeekContract.LocalToday(end), ct);
        var request = new OperationalCarryoverRequest { Id = Guid.CreateVersion7(), BranchId = BranchScope.LorettaId, PersonId = person, RequestedBy = actor, IntentKey = intent,
            Cause = cause, CauseDescription = CarryoverCauses.All[cause], Explanation = preview.Explanation, IncidentStart = end.AddMinutes(-minutes), IncidentEnd = end, Minutes = minutes,
            SubmittedAt = clock.UtcNow, Fingerprint = fingerprint, SnapshotJson = JsonSerializer.Serialize(preview.Items), ScheduleFingerprint = schedule.Hash };
        db.Add(request); Audit(actor, request, "OPERATIONAL_CARRYOVER_SUBMITTED");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return request.Id;
    }
    public async Task<OperationalCarryoverRequest[]> MineAsync(Guid actor, CancellationToken ct) {
        var person = await Person(actor, ct);
        return await db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r => r.PersonId == person && r.BranchId == BranchScope.LorettaId).OrderByDescending(r => r.SubmittedAt).ToArrayAsync(ct);
    }
    public async Task<OperationalCarryoverRequest[]> InboxAsync(Guid actor, CancellationToken ct) {
        return await (await AuthorizedInboxAsync(actor, ct)).OrderBy(r => r.SubmittedAt).ThenBy(r => r.Id).ToArrayAsync(ct);
    }
    public async Task DecideAsync(Guid actor, Guid id, long revision, bool approve, string comment, CancellationToken ct)
    {
        comment = comment?.Trim() ?? ""; if (comment.Length is < 1 or > 4000) throw Error("ARRASTRE_COMENTARIO_REQUERIDO", 422);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await OperationalTaskService.LockAsync(db, ct);
        var person = await Person(actor, ct);
        var r = await db.Set<OperationalCarryoverRequest>().SingleOrDefaultAsync(r => r.Id == id && r.BranchId == BranchScope.LorettaId, ct) ?? throw Error("TASK_NOT_FOUND", 404);
        if (r.PersonId == person) throw Error("ACCESO_DENEGADO", 403);
        foreach (var item in r.Items()) await supervision.AuthorizeAssignmentAsync(actor, item.AssignmentId, ct);
        if (r.State != "PENDIENTE") {
            if (r.DecidedBy == actor && r.State == (approve ? "APROBADA" : "RECHAZADA") && r.DecisionComment == comment) { await tx.CommitAsync(ct); return; }
            throw Error("ARRASTRE_CONFLICTO");
        }
        if (r.Revision != revision) throw Error("ARRASTRE_CONFLICTO");
        var results = new List<CarryoverResult>();
        if (r.Kind == "POSPONER") {
            await RequireImmediateSupervisor(actor, r.PersonId, ct);
            if (approve) results.AddRange(await ApplyPostponement(r, ct));
        }
        else if (approve) {
            await Person(r.RequestedBy, ct);
            if ((await Schedule(r.PersonId, WeekContract.LocalToday(r.IncidentEnd), ct)).Hash != r.ScheduleFingerprint) throw Error("ARRASTRE_CONFLICTO");
            if (await db.Set<OperationalCarryoverRequest>().AnyAsync(x => x.Kind == "INCIDENTE" && x.Id != id && x.PersonId == r.PersonId && x.State == "APROBADA" && x.IncidentStart < r.IncidentEnd && x.IncidentEnd > r.IncidentStart, ct)) throw Error("ARRASTRE_CONFLICTO");
            var snapshot = r.Items();
            var ids = snapshot.Select(i => i.AssignmentId).ToArray();
            var current = await db.OperationalAssignments.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
            foreach (var item in snapshot) {
                if (!current.TryGetValue(item.AssignmentId, out var a)) throw Error("ARRASTRE_CONFLICTO");
                if (OperationalCarryoverCalculator.Structure(a) != item.Structure || a.ReviewStatus == "REASIGNADA" ||
                    a.CompletedAt == null && a.ReviewStatus is "CONCLUIDA" or "NO_SATISFECHA") throw Error("ARRASTRE_CONFLICTO");
            }
            foreach (var item in snapshot) {
                var a = current[item.AssignmentId];
                // Delivery/progress/review revisions are nonstructural. Only the persisted due changes for pending work.
                var before = a.DueAt;
                if (a.CompletedAt == null) { a.DueAt = item.Proposed; a.Revision++; }
                results.Add(new(a.Id, before, a.DueAt, item.Proposed, a.CompletedAt, a.CompletedAt == null ? "AJUSTE" : "RECONOCIMIENTO"));
            }
        }
        r.ResultsJson = JsonSerializer.Serialize(results); r.State = approve ? "APROBADA" : "RECHAZADA";
        r.DecidedBy = actor; r.DecidedAt = clock.UtcNow; r.DecisionComment = comment; r.Revision++;
        Audit(actor, r, approve ? "OPERATIONAL_CARRYOVER_APPROVED" : "OPERATIONAL_CARRYOVER_REJECTED");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private void Audit(Guid actor, OperationalCarryoverRequest r, string action) => db.AuditEvents.Add(new AuditEvent {
        Id = Guid.CreateVersion7(), OccurredAt = clock.UtcNow, ActorUserId = actor, ActorType = "USER", Action = action,
        ResourceType = "OPERATIONAL_CARRYOVER", ResourceId = r.Id, BranchId = r.BranchId, CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS",
        AfterData = JsonSerializer.SerializeToDocument(r) });
}

