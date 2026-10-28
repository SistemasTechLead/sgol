using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class ScheduleIntervalInput
{
    public TimeOnly? Entry { get; set; }
    public TimeOnly? Exit { get; set; }
}
public sealed class ScheduleDayInput
{
    public string Mode { get; set; } = "UNKNOWN";
    public List<ScheduleIntervalInput> Intervals { get; set; } = [new(), new(), new(), new()];
    public WorkInterval[]? Read()
    {
        if(Intervals.Count>4) throw new DirectionPlanException("SCHEDULE_INTERVAL_INVALID",422);
        if (Mode == "UNKNOWN") return null;
        if (Mode == "REST") return [];
        if (Mode != "WORK") throw new DirectionPlanException("SCHEDULE_INTERVAL_INVALID", 422);
        var used = Intervals.Where(i => i.Entry != null || i.Exit != null).ToArray();
        if (used.Length == 0 || used.Any(i => i.Entry == null || i.Exit == null)) throw new DirectionPlanException("SCHEDULE_INTERVAL_INVALID", 422);
        var result = used.Select(i => new WorkInterval(i.Entry!.Value.Hour * 60 + i.Entry.Value.Minute, i.Exit!.Value.Hour * 60 + i.Exit.Value.Minute)).OrderBy(i => i.Entry).ToArray();
        TemporalEligibilityContext.ValidateIntervals(result); return result;
    }
}
public sealed class PersonalScheduleInput
{
    public Guid UserId { get; set; }
    public Guid Key { get; set; } = Guid.CreateVersion7();
    public long Revision { get; set; }
    public DateOnly From { get; set; }
    public List<ScheduleDayInput> Days { get; set; } = Enumerable.Range(0, 7).Select(_ => new ScheduleDayInput()).ToList();
    public string? Fingerprint { get; set; }
}
public sealed class ScheduleExceptionInput
{
    public string? Fingerprint { get; set; }
    public Guid UserId { get; set; }
    public Guid Key { get; set; } = Guid.CreateVersion7();
    public long Revision { get; set; }
    public DateOnly Day { get; set; }
    public ScheduleDayInput Hours { get; set; } = new() { Mode = "REST" };
    public string Reason { get; set; } = "";
}
public sealed record ScheduleImpact(Guid AssignmentId, string Task, DateTimeOffset StartsAt, string Reason);
public sealed record SchedulePreview(string Fingerprint, ScheduleImpact[] Conflicts);
public sealed partial class PersonalScheduleService(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock)
{
    public async Task<bool> EnabledAsync(Guid actor,CancellationToken ct) { await direction.AuthorizeAsync(actor,ct); return await db.Set<TemporalAssignmentState>().AnyAsync(s=>s.Enabled,ct); }
    public async Task<Guid> PersonAsync(Guid actor, Guid user, CancellationToken ct)
    {
        await direction.AuthorizeAsync(actor, ct);
        return await db.AppUsers.Where(u => u.Id == user).Select(u => u.PersonId).SingleAsync(ct);
    }
    public async Task<PersonalScheduleInput> ReadAsync(Guid actor, Guid user, CancellationToken ct)
    {
        var person = await PersonAsync(actor, user, ct);
        var version = await db.Set<PersonalScheduleVersion>().AsNoTracking().Where(s => s.PersonId == person).OrderByDescending(s => s.Revision).FirstOrDefaultAsync(ct);
        var days = version?.Days() ?? [];
        var today=WeekContract.LocalToday(clock.UtcNow);
        return new() { UserId = user, From = today, Revision = version?.Revision ?? 0,
            Days = Enumerable.Range(0, 7).Select(d => new ScheduleDayInput { Mode = !days.ContainsKey(d) ? "UNKNOWN" : days[d].Length == 0 ? "REST" : "WORK",
                Intervals = Enumerable.Range(0, 4).Select(i => days.GetValueOrDefault(d)?.ElementAtOrDefault(i) is { } span
                    ? new ScheduleIntervalInput { Entry = new(span.Entry / 60, span.Entry % 60), Exit = span.Exit == 1440 ? null : new(span.Exit / 60, span.Exit % 60) } : new()).ToList() }).ToList() };
    }
    public async Task<ScheduleExceptionInput> ReadExceptionAsync(Guid actor, Guid user, DateOnly day, CancellationToken ct)
    {
        var person = await PersonAsync(actor, user, ct);
        var row = await db.Set<PersonalScheduleException>().AsNoTracking().Where(e => e.PersonId == person && e.Day == day).OrderByDescending(e => e.Revision).FirstOrDefaultAsync(ct);
        var intervals = row == null ? [] : JsonSerializer.Deserialize<WorkInterval[]>(row.IntervalsJson)!;
        return new() { UserId = user, Day = day, Revision = row?.Revision ?? 0, Reason = row?.Reason ?? "",
            Hours = new() { Mode = intervals.Length == 0 ? "REST" : "WORK", Intervals = Enumerable.Range(0, 4).Select(i => intervals.ElementAtOrDefault(i) is { } span
                ? new ScheduleIntervalInput { Entry = new(span.Entry/60, span.Entry%60), Exit = new(span.Exit/60, span.Exit%60) } : new()).ToList() } };
    }
    private static string DaysJson(PersonalScheduleInput input)
    {
        if (input.Key == Guid.Empty || input.Days.Count != 7) throw new DirectionPlanException("SCHEDULE_INTERVAL_INVALID", 422);
        return JsonSerializer.Serialize(input.Days.Select((d, i) => (Day: i, Intervals: d.Read())).Where(d => d.Intervals != null).ToDictionary(d => d.Day, d => d.Intervals!));
    }
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public async Task<SchedulePreview> PreviewAsync(Guid actor, PersonalScheduleInput input, CancellationToken ct)
    {
        var person = await PersonAsync(actor, input.UserId, ct);
        var json = DaysJson(input); var today = WeekContract.LocalToday(clock.UtcNow);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
        var latest = ledger.Temporal.Schedules.Where(s => s.PersonId == person).OrderByDescending(s => s.Revision).FirstOrDefault();
        if ((latest?.Revision ?? 0) != input.Revision) throw new DirectionPlanException("SCHEDULE_STALE", 409);
        var proposed = new PersonalScheduleVersion { Id = input.Key, PersonId = person, From = today, Revision = input.Revision + 1, DaysJson = json };
        var context = new TemporalEligibilityContext { Enabled = true, Now = clock.UtcNow, BaseSchedulePolicyIds = ledger.Temporal.BaseSchedulePolicyIds, BaseSchedulesForCapacity = ledger.Temporal.BaseSchedulesForCapacity, StateRevision = ledger.Temporal.StateRevision, Schedules = ledger.Temporal.Schedules.Append(proposed).ToArray(),
            Exceptions = ledger.Temporal.Exceptions, Rotations = ledger.Temporal.Rotations, Coverages = ledger.Temporal.Coverages, ClosingTaskId=ledger.Temporal.ClosingTaskId };
        var relevant = ledger.Tasks.Where(a => a.PersonId == person && WeekContract.LocalToday(a.DueAt) >= today).ToArray();
        var temporalLedger=new CapacityLedger(ledger.Workers,ledger.Tasks,ledger.Capacities,ledger.Unavailable,ledger.Legacy,ledger.Today) { Temporal=context };
        var conflicts = relevant.Select(a => (Task: a, Decision: temporalLedger.CheckTime(person, a))).Where(a => !a.Decision.Eligible)
            .Select(a => new ScheduleImpact(a.Task.Id, a.Task.Name, a.Task.StartsAt, a.Decision.Reason)).ToArray();
        // Clock is deliberately excluded: crossing a second must not invalidate an otherwise identical preview.
        var fingerprint = Hash(new { input.UserId, input.Key, input.Revision, json, ledger.Temporal.Schedules, ledger.Temporal.Exceptions,
            ledger.Temporal.Rotations, ledger.Temporal.Coverages, ledger.Temporal.ClosingTaskId, relevant,
            Absences = ledger.Unavailable.OrderBy(p => p.Item1).ThenBy(p => p.Item2) });
        return new(fingerprint, conflicts);
    }
    public async Task SaveAsync(Guid actor, PersonalScheduleInput input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await OperationalTaskService.LockAsync(db, ct);
        var person = await PersonAsync(actor, input.UserId, ct); var json = DaysJson(input);
        var hash = Hash(new { input.UserId, input.Revision, json });
        var replay = await db.Set<PersonalScheduleVersion>().AsNoTracking().SingleOrDefaultAsync(s => s.IntentKey == input.Key, ct);
        if (replay != null)
        {
            if (replay.ChangedBy != actor || replay.RequestHash != hash) throw new DirectionPlanException("SCHEDULE_STALE", 409);
            await tx.CommitAsync(ct); return;
        }
        var preview = await PreviewAsync(actor, input, ct);
        if (preview.Fingerprint != input.Fingerprint) throw new DirectionPlanException("SCHEDULE_STALE", 409);
        var previous = await db.Set<PersonalScheduleVersion>().Where(s => s.PersonId == person && !s.Superseded).ToArrayAsync(ct);
        foreach (var old in previous) old.Superseded = true;
        var version = new PersonalScheduleVersion { Id = Guid.CreateVersion7(), PersonId = person, From = WeekContract.LocalToday(clock.UtcNow), DaysJson = json,
            Revision = input.Revision + 1, ChangedAt = clock.UtcNow, ChangedBy = actor, IntentKey = input.Key, RequestHash = hash };
        db.Add(version); Audit(actor, version.Id, input.Key, "PERSONAL_SCHEDULE_SAVED", new { version, preview.Conflicts });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task SaveExceptionAsync(Guid actor, ScheduleExceptionInput input, CancellationToken ct)
    {
        var spans = input.Hours.Read() ?? throw new DirectionPlanException("SCHEDULE_INTERVAL_INVALID", 422);
        if (input.Key == Guid.Empty || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000)
            throw new DirectionPlanException("SCHEDULE_DATE_INVALID", 422);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await OperationalTaskService.LockAsync(db, ct);
        var person = await PersonAsync(actor, input.UserId, ct);
        var hash = Hash(new { input.UserId, input.Day, spans, input.Reason, input.Revision });
        var replay = await db.Set<PersonalScheduleException>().AsNoTracking().SingleOrDefaultAsync(e => e.IntentKey == input.Key, ct);
        if (replay != null)
        {
            if (replay.ChangedBy != actor || replay.RequestHash != hash) throw new DirectionPlanException("SCHEDULE_STALE", 409);
            await tx.CommitAsync(ct); return;
        }
        var revision = await db.Set<PersonalScheduleException>().Where(e => e.PersonId == person && e.Day == input.Day).Select(e => (long?)e.Revision).MaxAsync(ct) ?? 0;
        if (input.Day < WeekContract.LocalToday(clock.UtcNow)) throw new DirectionPlanException("SCHEDULE_DATE_INVALID",422);
        if (revision != input.Revision) throw new DirectionPlanException("SCHEDULE_STALE", 409);
        var preview=await PreviewExceptionAsync(actor,input,ct);
        if(input.Fingerprint!=preview.Fingerprint) throw new DirectionPlanException("SCHEDULE_STALE",409);
        var version = new PersonalScheduleException { Id = Guid.CreateVersion7(), PersonId = person, Day = input.Day, Revision = revision + 1,
            IntervalsJson = JsonSerializer.Serialize(spans), Reason = input.Reason.Trim(), ChangedAt = clock.UtcNow, ChangedBy = actor, IntentKey = input.Key, RequestHash = hash };
        db.Add(version); Audit(actor, version.Id, input.Key, "PERSONAL_SCHEDULE_EXCEPTION_SAVED", new { version,preview.Conflicts });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<SchedulePreview> PreviewExceptionAsync(Guid actor,ScheduleExceptionInput input,CancellationToken ct)
    {
        if(input.Day<WeekContract.LocalToday(clock.UtcNow) || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length>1000) throw new DirectionPlanException("SCHEDULE_DATE_INVALID",422);
        var person=await PersonAsync(actor,input.UserId,ct);
        var spans=input.Hours.Read()??throw new DirectionPlanException("SCHEDULE_INTERVAL_INVALID",422);
        var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,ct);
        var version=new PersonalScheduleException { Id=input.Key,PersonId=person,Day=input.Day,Revision=input.Revision+1,IntervalsJson=JsonSerializer.Serialize(spans) };
        var covers=ledger.Temporal.Coverages;

        var context=new TemporalEligibilityContext { Enabled=true,BaseSchedulePolicyIds=ledger.Temporal.BaseSchedulePolicyIds,BaseSchedulesForCapacity=ledger.Temporal.BaseSchedulesForCapacity,StateRevision=ledger.Temporal.StateRevision,Schedules=ledger.Temporal.Schedules,Exceptions=ledger.Temporal.Exceptions.Append(version).ToArray(),
            Rotations=ledger.Temporal.Rotations,Coverages=covers,ClosingTaskId=ledger.Temporal.ClosingTaskId };
        var tasks=ledger.Tasks.Where(t=>WeekContract.LocalToday(t.StartsAt)<=input.Day && WeekContract.LocalToday(t.DueAt)>=input.Day && t.PersonId==person).ToArray();
        var temporalLedger=new CapacityLedger(ledger.Workers,ledger.Tasks,ledger.Capacities,ledger.Unavailable,ledger.Legacy,ledger.Today) { Temporal=context };
        var conflicts=tasks.Select(t=>(Task:t,Decision:temporalLedger.CheckTime(t.PersonId,t))).Where(t=>!t.Decision.Eligible)
            .Select(t=>new ScheduleImpact(t.Task.Id,t.Task.Name,t.Task.StartsAt,t.Decision.Reason)).ToArray();
        return new(Hash(new { input.UserId,input.Key,input.Day,input.Revision,input.Reason,spans,
            ledger.Temporal.Schedules,ledger.Temporal.Exceptions,ledger.Temporal.Rotations,ledger.Temporal.Coverages,tasks,ledger.Temporal.ClosingTaskId,ledger.Temporal.StateRevision,ledger.Temporal.BaseSchedulePolicyIds,ledger.Temporal.BaseSchedulesForCapacity,Absences=ledger.Unavailable.OrderBy(a=>a.Item1).ThenBy(a=>a.Item2) }),conflicts);
    }
    private void Audit(Guid actor, Guid resource, Guid key, string action, object data) => db.AuditEvents.Add(new AuditEvent {
        Id = Guid.CreateVersion7(), ActorUserId = actor, ActorType = "USER", OccurredAt = clock.UtcNow, BranchId = Sgol.Organization.Contracts.BranchScope.LorettaId,
        ResourceId = resource, ResourceType = "PERSONAL_SCHEDULE", Action = action, Outcome = "SUCCESS", CorrelationId = key, AfterData = JsonSerializer.SerializeToDocument(data) });
}
