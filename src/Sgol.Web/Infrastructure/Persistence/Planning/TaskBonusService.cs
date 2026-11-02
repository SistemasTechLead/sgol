using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class TaskBonusService(SgolDbContext db, IClock clock, OperationalSupervisionService supervision)
{
    public DateOnly CurrentWeek => TaskBonusCalculator.Monday(WeekContract.LocalToday(clock.UtcNow));
    public async Task<TaskBonusResult[]> ReadAsync(Guid actor, DateOnly? requestedWeek, bool ownOnly, CancellationToken token)
    {
        var week = requestedWeek ?? CurrentWeek;
        if (TaskBonusCalculator.Monday(week) != week || week > CurrentWeek || week < TaskBonusCalculator.FirstWeek.AddDays(-7))
            throw new DirectionPlanException("BONUS_WEEK_INVALID", 400);
        // Screen accounts are not AppUsers. This check also protects direct handler requests.
        var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor && u.Status == AccountStatus.Active, token)
            ?? throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        var role = ownOnly ? null : await supervision.AuthorizeAsync(actor, token);
        var allowed = role == null ? [] : OperationalSupervisionService.RolesFor(role);
        var eligible = await (from u in db.AppUsers.AsNoTracking()
            join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
            join e in db.EmploymentVersions.AsNoTracking() on u.PersonId equals e.PersonId
            where e.BranchId == BranchScope.LorettaId && e.Status == EmploymentStatus.Active && e.ValidFrom <= clock.UtcNow &&
                (e.ValidTo == null || e.ValidTo > clock.UtcNow) && r.BranchId == BranchScope.LorettaId &&
                r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= clock.UtcNow && (r.ValidTo == null || r.ValidTo > clock.UtcNow) &&
                (ownOnly ? u.Id == actor : allowed.Contains(r.RoleCode) && (role == CanonicalRole.Direction || u.Id != actor))
            select u.PersonId).Distinct().ToArrayAsync(token);
        if (ownOnly && !eligible.Contains(user.PersonId)) throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        // The same transaction lock as submission/review/transfer: a closure never observes half an operation.
        await OperationalTaskService.LockAsync(db, token);
        var results = await CalculateAsync(week, eligible, token);
        if (!ownOnly)
        {
            var histories = await db.Set<TaskBonusFact>().AsNoTracking().ToArrayAsync(token);
            var states = histories.Where(f => f.Kind != "HANDOFF").Select(f => JsonSerializer.Deserialize<TaskBonusAssignmentSnapshot>(f.SnapshotJson)!).ToArray();
            // Never present a subset of someone's obligations as their complete weekly score.
            results = results.Where(r => r.Details.All(d => states.Where(s => s.Id == d.AssignmentId)
                .All(s => s.Role != null && allowed.Contains(s.Role)))).ToArray();
        }
        await tx.CommitAsync(token);
        return results;
    }

    public async Task<Dictionary<Guid, TaskBonusResult>> BoardAsync(Guid actor, DateOnly week, CancellationToken token)
    {
        var own = await ReadAsync(actor, week, true, token);
        TaskBonusResult[] others = [];
        try { others = await ReadAsync(actor, week, false, token); }
        catch (DirectionPlanException e) when (e.Status == 403) { }
        return own.Concat(others).DistinctBy(r => r.PersonId).ToDictionary(r => r.PersonId,
            r => r with { Details = [], Incidences = [] });
    }

    public async Task ExcludeAsync(Guid actor, Guid person, Guid obligation, DateOnly week, string reason, CancellationToken token)
    {
        if (await supervision.AuthorizeAsync(actor, token) != CanonicalRole.Direction)
            throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        reason = reason?.Trim() ?? "";
        if (reason.Length is < 5 or > 1000) throw new DirectionPlanException("BONUS_REASON_REQUIRED", 422);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        var allowed = OperationalSupervisionService.RolesFor(CanonicalRole.Direction);
        var visible = await (from u in db.AppUsers.AsNoTracking()
            join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
            join e in db.EmploymentVersions.AsNoTracking() on u.PersonId equals e.PersonId
            where u.PersonId == person && u.Status == AccountStatus.Active &&
                e.BranchId == BranchScope.LorettaId && e.Status == EmploymentStatus.Active && e.ValidFrom <= clock.UtcNow &&
                (e.ValidTo == null || e.ValidTo > clock.UtcNow) && r.BranchId == BranchScope.LorettaId &&
                r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= clock.UtcNow && (r.ValidTo == null || r.ValidTo > clock.UtcNow) && allowed.Contains(r.RoleCode)
            select u.Id).AnyAsync(token);
        if (!visible) throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        if (week < TaskBonusCalculator.FirstWeek || TaskBonusCalculator.Monday(week) != week ||
            clock.UtcNow >= TaskBonusCalculator.Period(week).ReviewCutoffAt ||
            await db.Set<TaskBonusClosure>().AnyAsync(c => c.PersonId == person && c.Week == week, token))
            throw new DirectionPlanException("BONUS_CLOSED", 409);
        var result = (await CalculateAsync(week, [person], token)).Single();
        if (!result.Details.Any(d => d.ObligationId == obligation)) throw new DirectionPlanException("BONUS_OBLIGATION_NOT_FOUND", 404);
        var prior = await db.Set<TaskBonusExclusion>().SingleOrDefaultAsync(e => e.ObligationId == obligation && e.PersonId == person && e.Week == week, token);
        if (prior != null)
        {
            if (prior.Reason != reason || prior.ActorId != actor) throw new DirectionPlanException("BONUS_EXCLUSION_EXISTS", 409);
            await tx.CommitAsync(token); return;
        }
        var exclusion = new TaskBonusExclusion { ObligationId = obligation, PersonId = person, Week = week,
            ActorId = actor, At = clock.UtcNow, Reason = reason };
        db.Add(exclusion);
        db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow,
            ResourceId = obligation, ResourceType = "TASK_BONUS", BranchId = BranchScope.LorettaId,
            Action = "TASK_BONUS_EXCLUDED", Outcome = "SUCCESS", CorrelationId = Guid.CreateVersion7(),
            AfterData = JsonSerializer.SerializeToDocument(exclusion) });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    // Caller holds the operational lock. Used by authorized reads and the catch-up worker.
    public async Task<TaskBonusResult[]> CalculateAsync(DateOnly week, Guid[] people, CancellationToken token)
    {
        var now = clock.UtcNow;
        var period = TaskBonusCalculator.Period(week);
        var cutoff = now < period.ReviewCutoffAt ? now : period.ReviewCutoffAt;
        var names = await db.People.AsNoTracking().Where(p => people.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.DisplayName, token);
        var closed = await db.Set<TaskBonusClosure>().AsNoTracking().Where(c => people.Contains(c.PersonId) && c.Week == week &&
            c.RulesVersion == TaskBonusCalculator.RulesVersion).ToDictionaryAsync(c => c.PersonId, token);
        if (people.All(closed.ContainsKey)) return people.Select(p => JsonSerializer.Deserialize<TaskBonusResult>(closed[p].ResultJson)!).ToArray();
        var facts = await db.Set<TaskBonusFact>().AsNoTracking().OrderBy(f => f.RecordedAt).ThenBy(f => f.Revision).ToArrayAsync(token);
        var transfers = await db.Set<OperationalTransfer>().AsNoTracking().OrderBy(t => t.At).ToArrayAsync(token);
        var carryovers = await db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r => r.State == "APROBADA")
            .OrderBy(r => r.DecidedAt).ToArrayAsync(token);
        var exclusions = await db.Set<TaskBonusExclusion>().AsNoTracking().Where(e => e.Week == week && e.At < period.ReviewCutoffAt && e.At <= now).ToArrayAsync(token);
        var obligations = Materialize(facts, transfers, carryovers, cutoff, period.ReviewCutoffAt);
        obligations = obligations.Select(o => o with { Events = o.Events.Concat(exclusions.Where(e => e.ObligationId == o.Id && e.PersonId == o.PersonId)
            .Select(e => new TaskBonusEvent("EXCLUDED", e.At, Actor: e.ActorId, Reason: e.Reason))).ToArray() }).ToArray();
        var schedules = await db.Set<PersonalScheduleVersion>().AsNoTracking().Where(s => people.Contains(s.PersonId) && s.ChangedAt < period.ReviewCutoffAt && s.ChangedAt <= now).ToArrayAsync(token);
        var exceptions = await db.Set<PersonalScheduleException>().AsNoTracking().Where(s => people.Contains(s.PersonId) && s.ChangedAt < period.ReviewCutoffAt && s.ChangedAt <= now && s.Day >= week && s.Day <= period.EndsOn).ToArrayAsync(token);
        var availability = await db.Set<TaskBonusAvailabilityFact>().AsNoTracking().Where(s => people.Contains(s.PersonId) && s.RecordedAt < period.ReviewCutoffAt && s.RecordedAt <= now && s.Day >= week && s.Day <= period.EndsOn).ToArrayAsync(token);
        var results = new List<TaskBonusResult>();
        foreach (var person in people)
        {
            if (closed.TryGetValue(person, out var saved)) { results.Add(JsonSerializer.Deserialize<TaskBonusResult>(saved.ResultJson)!); continue; }
            bool? worked = false;
            foreach (var day in CapacityLedger.Days(week, period.EndsOn))
            {
                var available = availability.Where(a => a.PersonId == person && a.Day == day).OrderByDescending(a => a.RecordedAt).FirstOrDefault();
                if (available is { Available: false }) continue;
                var exception = exceptions.Where(e => e.PersonId == person && e.Day == day).OrderByDescending(e => e.Revision).FirstOrDefault();
                var schedule = schedules.Where(s => s.PersonId == person && s.From <= day && (s.Through == null || s.Through >= day)).OrderByDescending(s => s.Revision).FirstOrDefault();
                var spans = exception != null ? JsonSerializer.Deserialize<WorkInterval[]>(exception.IntervalsJson) : schedule?.Days().GetValueOrDefault((int)day.DayOfWeek);
                if (spans is { Length: > 0 }) { worked = true; break; }
                if (spans == null) worked = null;
            }
            var result = TaskBonusCalculator.Calculate(person, week, now, obligations, worked) with { PersonName = names.GetValueOrDefault(person, "Colaborador") };
            result = result with { ResultVersion = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
                result.PersonId, result.Period, result.Closed, result.CalculationState, result.ScorePercent, result.BonusPercent,
                result.EvaluablePoints, result.DeductedPoints, result.Details, result.Incidences, result.RulesVersion }))) };
            if (result.Closed)
            {
                result = result with { ClosedAt = period.ReviewCutoffAt };
                db.Add(new TaskBonusClosure { PersonId = person, Week = week, ClosedAt = period.ReviewCutoffAt, ResultJson = JsonSerializer.Serialize(result) });
            }
            results.Add(result);
        }
        await db.SaveChangesAsync(token);
        return results.ToArray();
    }

    public static TaskBonusObligation[] Materialize(TaskBonusFact[] facts, OperationalTransfer[] transfers,
        OperationalCarryoverRequest[] carryovers, DateTimeOffset now, DateTimeOffset reviewCutoff)
    {
        transfers = transfers.Concat(facts.Where(f => f.Kind == "HANDOFF").Select(f => JsonSerializer.Deserialize<OperationalTransfer>(f.SnapshotJson)!)).DistinctBy(t => t.Id).ToArray();
        var byAssignment = facts.Where(f => f.Kind != "HANDOFF" && (f.Kind == "BASELINE" || f.RecordedAt <= now && f.RecordedAt < reviewCutoff))
            .GroupBy(f => f.AssignmentId).ToDictionary(g => g.Key, g => g.OrderBy(f => f.Kind == "BASELINE" ? 0 : 1).ThenBy(f => f.Revision).ToArray());
        var states = byAssignment.ToDictionary(g => g.Key, g => JsonSerializer.Deserialize<TaskBonusAssignmentSnapshot>(g.Value[0].SnapshotJson)!);
        var stages = states.Values.Where(s => s.ProcessStageId != null).GroupBy(s => s.ProcessStageId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => byAssignment[s.Id][0].RecordedAt).ThenBy(s => s.StartsAt).First().Id);
        Guid Root(Guid id)
        {
            var seen = new HashSet<Guid>();
            while (seen.Add(id) && states.TryGetValue(id, out var state))
            {
                if (state.ProcessStageId is {} stage) return stages[stage];
                if (state.ReassignedFromId is {} parent) id = parent; else break;
            }
            return id;
        }
        var output = new List<TaskBonusObligation>();
        foreach (var group in states.Values.GroupBy(s => Root(s.Id)))
        {
            if (!states.TryGetValue(group.Key, out var original)) continue;
            var history = group.SelectMany(s => byAssignment[s.Id]).OrderBy(f => f.RecordedAt).ThenBy(f => f.Revision).ToArray();
            var receipt = transfers.Where(t => t.AssignmentId == original.Id).OrderBy(t => t.At).FirstOrDefault();
            if (receipt != null && byAssignment[original.Id][0].Kind == "BASELINE")
                original = TaskBonusAssignmentSnapshot.From(JsonSerializer.Deserialize<OperationalAssignment>(receipt.SnapshotJson)!) with { ProcessStageId = original.ProcessStageId };
            var carried = carryovers.SelectMany(r => r.Items()).FirstOrDefault(i => i.AssignmentId == original.Id);
            if (carried != null && byAssignment[original.Id][0].Kind == "BASELINE") original = original with { StartsAt = carried.Start, DueAt = carried.Due };
            var day = EvaluationDay(original);
            if (day < TaskBonusCalculator.FirstWeek) continue;
            var person = original.PersonId;
            var events = new List<TaskBonusEvent>();
            var issue = original.Criticality is not ("FLEXIBLE" or "IMPORTANTE" or "CRITICA") ? "Clasificación original no acreditada." : null;
            if (history.Any(f => f.Kind == "BASELINE" && f.RecordedAt >= reviewCutoff)) issue = "La captura inicial ocurrió después del corte; falta acreditar el historial al cierre.";
            var implicitMoves = new List<OperationalTransfer>();
            foreach (var next in group.Where(s => s.ProcessStageId != null && s.ReassignedFromId != null))
            {
                var previous = history.Where(f => f.AssignmentId == next.ReassignedFromId)
                    .Select(f => JsonSerializer.Deserialize<TaskBonusAssignmentSnapshot>(f.SnapshotJson)!)
                    .LastOrDefault(s => s.CompletionKind == "SUPERVISION" && s.ReviewStatus == "REASIGNADA");
                if (previous?.ReviewedAt is {} handoff && previous.PersonId != next.PersonId)
                    implicitMoves.Add(new() { AssignmentId = previous.Id, At = handoff, FromPersonId = previous.PersonId,
                        ToPersonId = next.PersonId, NewDueAt = next.DueAt, ActorId = previous.ReviewedBy ?? Guid.Empty, Reason = "Reasignación de etapa disponible" });
            }
            var movements = transfers.Concat(implicitMoves).Where(t => group.Any(s => s.Id == t.AssignmentId) && t.At <= now && t.At < reviewCutoff).OrderBy(t => t.At).ToArray();
            foreach (var fact in history)
            {
                var s = JsonSerializer.Deserialize<TaskBonusAssignmentSnapshot>(fact.SnapshotJson)!;
                if (s.CompletedAt is {} submitted && s.CompletionKind == "ENTREGA")
                    events.Add(new("SUBMITTED", submitted, fact.Revision * 2, s.CompletedBy));
                if (s.ReviewedAt is {} reviewed)
                {
                    var kind = s.ReviewStatus switch { "REASIGNADA" => "RETURNED", "NO_SATISFECHA" => "REJECTED",
                        "CONCLUIDA" when s.CompletionKind == "SUPERVISION" => "ADMIN_COMPLETION", _ => "APPROVED" };
                    events.Add(new(kind, reviewed, fact.Revision * 2 + 1, s.ReviewedBy));
                }
                if (s.RetiredAt is {} retired) events.Add(new("CANCELLED", retired));
                if (fact.Kind == "DELETED") events.Add(new("CANCELLED", fact.RecordedAt));
            }
            var frozen = false;
            foreach (var movement in movements)
            {
                var end = OperationalSchedule.Instant(day.AddDays(1));
                if (implicitMoves.Contains(movement)) events.RemoveAll(e => e.At == movement.At && e.Kind == "RETURNED");
                var priorReturn = events.Any(e => e.At <= movement.At && e.Kind is "RETURNED" or "REJECTED");
                if (movement.At < end && !priorReturn)
                {
                    person = movement.ToPersonId;
                    day = WeekContract.LocalToday(movement.NewDueAt.AddTicks(-1));
                    events.Add(new("TRANSFERRED", movement.At, Actor: movement.ActorId, Reason: movement.Reason));
                }
                else
                {
                    events = events.Where(e => e.At <= movement.At).ToList();
                    events.Add(new("TRANSFERRED_OVERDUE", movement.At, Reason: "Transferencia después del incumplimiento: recuperación sin puntos nuevos"));
                    frozen = true; break;
                }
            }
            // A correction by another person cannot erase the original person's breach.
            if (!frozen)
            {
                var other = group.Where(s => s.Id != original.Id && s.PersonId != person).OrderBy(s => s.StartsAt).FirstOrDefault();
                if (other != null)
                {
                    var handoff = events.Where(e => e.Kind == "RETURNED").OrderBy(e => e.At).FirstOrDefault();
                    if (handoff != null) events = events.Where(e => e.At <= handoff.At).ToList();
                }
            }
            var points = issue != null && original.Criticality is not ("FLEXIBLE" or "IMPORTANTE" or "CRITICA") ? 0m : TaskBonusCalculator.Points(original.Criticality!);
            output.Add(new(original.Id, original.Id, person, original.TaskCode, original.Name, day, original.Criticality ?? "SIN_CLASIFICACION",
                points, events.DistinctBy(e => (e.Kind, e.At, e.Actor)).OrderBy(e => e.At).ThenBy(e => e.Order).ToArray(), issue));
        }
        return output.ToArray();
    }

    private static DateOnly EvaluationDay(TaskBonusAssignmentSnapshot s)
    {
        var start = WeekContract.LocalToday(s.StartsAt);
        var due = WeekContract.LocalToday(s.DueAt.AddTicks(-1));
        return s.GoalUnits > 0 || due > start ? due : start;
    }
}

public sealed class TaskBonusClosingWorker(IServiceScopeFactory scopes, ILogger<TaskBonusClosingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                var service = scope.ServiceProvider.GetRequiredService<TaskBonusService>();
                for (var week = TaskBonusCalculator.FirstWeek; TaskBonusCalculator.Period(week).ReviewCutoffAt <= clock.UtcNow; week = week.AddDays(7))
                {
                    var period = TaskBonusCalculator.Period(week);
                    var startsAt = OperationalSchedule.Instant(week);
                    var people = await db.EmploymentVersions.Where(e => e.BranchId == BranchScope.LorettaId &&
                        e.Status == EmploymentStatus.Active && e.ValidFrom < period.ExecutionCutoffAt &&
                        (e.ValidTo == null || e.ValidTo > startsAt)).Select(e => e.PersonId).Distinct().ToArrayAsync(stoppingToken);
                    await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                    await OperationalTaskService.LockAsync(db, stoppingToken);
                    await service.CalculateAsync(week, people, stoppingToken);
                    await tx.CommitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError(e, "No se pudo recuperar el cierre del bono de tareas."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
