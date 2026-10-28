using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

// All minutes are local wall-clock minutes in the operational zone. Null day = unknown; [] = rest.
public sealed record WorkInterval(int Entry, int Exit);
public sealed class PersonalScheduleVersion
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public long Revision { get; set; }
    public DateOnly From { get; set; }
    public DateOnly? Through { get; set; }
    public bool Superseded { get; set; }
    public string Zone { get; set; } = "America/Mexico_City";
    public string DaysJson { get; set; } = "{}";
    public Guid ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public Guid IntentKey { get; set; }
    public string RequestHash { get; set; } = "";
    public Dictionary<int, WorkInterval[]> Days() => JsonSerializer.Deserialize<Dictionary<int, WorkInterval[]>>(DaysJson)!;
}
public sealed class PersonalScheduleException
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public DateOnly Day { get; set; }
    public long Revision { get; set; }
    public string IntervalsJson { get; set; } = "[]";
    public string Reason { get; set; } = "";
    public Guid ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public Guid IntentKey { get; set; }
    public string RequestHash { get; set; } = "";
}
public sealed class ClosingRotationVersion
{
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public DateOnly From { get; set; }
    public DateOnly Anchor { get; set; }
    public int WeekDay { get; set; } = 3;
    public int ExtendedExit { get; set; } = 1200;
    public Guid RegularPersonId { get; set; }
    public Guid ExcludedPersonId { get; set; }
    public string ParticipantsJson { get; set; } = "[]";
    public Guid ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public Guid IntentKey { get; set; }
    public Guid Person(DateOnly day)
    {
        var people = JsonSerializer.Deserialize<Guid[]>(ParticipantsJson)!;
        // Floor division matters for dates before the anchor; ISO year boundaries are irrelevant.
        var weeks = (int)Math.Floor((day.DayNumber - Anchor.DayNumber) / 7d);
        return day.DayOfWeek == (DayOfWeek)WeekDay ? people[((weeks % people.Length) + people.Length) % people.Length] : RegularPersonId;
    }
}
public sealed class ClosingCoverageVersion
{
    public Guid Id { get; set; }
    public DateOnly Day { get; set; }
    public Guid PersonId { get; set; }
    public long Revision { get; set; }
    public string Reason { get; set; } = "";
    public Guid ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public Guid IntentKey { get; set; }
}
public sealed class TemporalAssignmentState
{
    public Guid? ClosingTaskId { get; set; }
    public string SundayTaskIdsJson { get; set; } = "[]";
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    public long Revision { get; set; } = 1;
}
public sealed record TemporalDecision(bool Eligible, string Reason, Guid? ScheduleId = null, Guid? ExceptionId = null,
    Guid? RotationId = null, Guid? CoverageId = null, DateTimeOffset? Limit = null,
    IReadOnlyList<TemporalWindow>? Windows = null);
public sealed record TemporalWindow(DateOnly Day, DateTimeOffset Entry, DateTimeOffset Exit, Guid? ScheduleId = null,
    Guid? ExceptionId = null, Guid? RotationId = null, Guid? CoverageId = null);

public sealed class TemporalEligibilityContext
{
    public static string Message(string code) => code switch {
        "SCHEDULING_INFEASIBLE" => "Las restricciones de esta agenda son incompatibles. Revisa el diagnóstico por tarea, turno y duración.",
        "SCHEDULING_SEARCH_INCONCLUSIVE" => "Se alcanzó el límite de búsqueda sin completar la agenda. No se ha demostrado inviabilidad; no se publicó trabajo parcial.",
        "SCHEDULING_CIRCUIT_ORDER" => "El precorte debe terminar antes de conciliación, y conciliación antes de alarma.",
        "PLAN_CAPACITY_EXCEEDED" => "La carga pendiente supera la capacidad disponible; reacomodar horas no crea más capacidad.",
        "SCHEDULING_CRITICAL_OVERLAP" => "Dos tareas críticas se superponen; conserva sus horas y revisa los responsables.",
        "SCHEDULING_CRITICAL_ANCHOR_PASSED" => "La hora fija de esta tarea crítica ya pasó; no se puede activar retroactivamente.",
        "SCHEDULING_LEGACY_RESERVATION" => "Hay compromisos anteriores sin bloques de ejecución verificables. Revisa otro responsable o día permitido sin modificar esos compromisos.",
        "SCHEDULING_NO_CONTIGUOUS_SLOT" => "No se encontró un hueco continuo hasta el cierre calculado dentro del turno y del mismo día.",
        "SCHEDULE_EMPLOYMENT_END" => "El empleo o el rol termina antes del tiempo necesario para realizar la tarea.",
        "SCHEDULE_ACCOUNT_INACTIVE" => "La cuenta, el empleo o el rol ya no está activo para recibir trabajo.",
        "SCHEDULE_ROLE_CONFLICT" => "La persona de cobertura no tiene el rol requerido por esta tarea.",
        "SCHEDULE_STALE" => "El horario o el trabajo programado cambió. Recarga y revisa el impacto antes de guardar.",
        "SCHEDULE_DATE_INVALID" => "Indica una fecha desde hoy y un motivo para la excepción.",
        "SCHEDULE_INTERVAL_INVALID" => "Completa entrada y salida de cada tramo. No pueden solaparse ni cruzar medianoche; máximo cuatro por día.",
        "CLOSING_COVERAGE_REQUIRED" => "Revisa la cobertura de esta fecha en Planificación. La persona debe tener un turno cercano al cierre y estar disponible.",
        "SCHEDULE_MISSING" => "La persona no tiene horario configurado para este día de la semana.",
        "SCHEDULE_ABSENT" => "La persona está marcada como no disponible.",
        "SCHEDULE_DEADLINE_PASSED" => "El plazo de esta ocurrencia ya venció; no es un conflicto con el horario de la persona.",
        "SCHEDULE_NOT_PRESENT_AT_ACTIVATION" => "La persona no está en turno a la hora de activación de la tarea.",
        "SCHEDULE_NOT_PRESENT_AT_RECEPTION" => "La persona no está en turno al recibir la tarea.",
        _ => "La persona no está disponible a la hora de activación de la tarea."
    };
    public Guid? ClosingTaskId { get; init; }
    public int EligibilityPolicyVersion => 4;
    public Guid[] BaseSchedulePolicyIds { get; init; } = [];
    public bool BaseSchedulesForCapacity { get; init; }
    public bool UsesBaseSchedule(OperationalAssignment task) => task.SchedulingPolicyId is {} id && BaseSchedulePolicyIds.Contains(id);
    // Closing honors its explicitly selected coverage even under the base-schedule agenda.
    public bool UsesCoverageSchedule(OperationalAssignment task) => task.TaskId == ClosingTaskId || !UsesBaseSchedule(task);
    public bool Enabled { get; init; }
    public long StateRevision { get; init; }
    public PersonalScheduleVersion[] Schedules { get; init; } = [];
    public PersonalScheduleException[] Exceptions { get; init; } = [];
    public ClosingRotationVersion[] Rotations { get; init; } = [];
    public ClosingCoverageVersion[] Coverages { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore] public DateTimeOffset Now { get; init; }
    public static void ValidateIntervals(IEnumerable<WorkInterval> source)
    {
        var intervals = source.ToArray();
        if (intervals.Length > 4 || intervals.Any(i => i.Entry < 0 || i.Exit > 1440 || i.Entry >= i.Exit) ||
            intervals.OrderBy(i => i.Entry).Zip(intervals.OrderBy(i => i.Entry).Skip(1)).Any(p => p.First.Exit > p.Second.Entry))
            throw new DirectionPlanException("SCHEDULE_INTERVAL_INVALID", 422);
    }
    public static DateTimeOffset At(DateOnly day, int minutes) => OperationalSchedule.Instant(day).AddMinutes(minutes);
    public ClosingRotationVersion? Rotation(DateOnly day) => Rotations.Where(r => r.From <= day).OrderByDescending(r => r.Revision).FirstOrDefault();
    // A context is a captured request snapshot. Private caches never enter fingerprints.
    private readonly Dictionary<(Guid Person, DateOnly Day, bool Coverage), (PersonalScheduleVersion? Schedule, PersonalScheduleException? Exception, WorkInterval[]? Intervals)> dayCache = [];
    private readonly Dictionary<Guid, PersonalScheduleVersion?> scheduleCache = [];
    private readonly Dictionary<PersonalScheduleVersion, Dictionary<int, WorkInterval[]>> weekCache = [];
    private readonly Dictionary<DateOnly, Guid?> closingCache = [];
    public (PersonalScheduleVersion? Schedule, PersonalScheduleException? Exception, WorkInterval[]? Intervals) Day(Guid person, DateOnly day, bool applyCoverage = true)
    {
        var key = (person, day, applyCoverage);
        if (dayCache.TryGetValue(key, out var cached)) return cached;
        if (applyCoverage) {
            var original = Day(person, day, false);
            var intervals = original.Intervals;
            if (ClosingPerson(day) == person && intervals is { Length: > 0 }) {
                intervals = intervals.ToArray(); var last = intervals.MaxBy(i => i.Exit)!;
                intervals[Array.IndexOf(intervals, last)] = last with { Exit = Math.Max(last.Exit, Math.Min(1260, last.Exit + 60)) };
            }
            var effective = (original.Schedule, original.Exception, intervals); dayCache[key] = effective; return effective;
        }
        // Latest weekly version is global to every plan date, as in the existing policy.
        if (!scheduleCache.TryGetValue(person, out var schedule)) {
            schedule = Schedules.Where(s => !s.Superseded && s.PersonId == person).OrderByDescending(s => s.Revision).FirstOrDefault(); scheduleCache[person] = schedule;
        }
        var exception = Exceptions.Where(e => e.PersonId == person && e.Day == day).OrderByDescending(e => e.Revision).FirstOrDefault();
        WorkInterval[]? spans;
        if (exception != null) spans = JsonSerializer.Deserialize<WorkInterval[]>(exception.IntervalsJson);
        else if (schedule == null) spans = null;
        else {
            if (!weekCache.TryGetValue(schedule, out var week)) { week = schedule.Days(); weekCache[schedule] = week; }
            spans = week.GetValueOrDefault((int)day.DayOfWeek);
        }
        var result = (schedule, exception, spans); dayCache[key] = result; return result;
    }
    public TemporalDecision Evaluate(Guid person, OperationalAssignment task, HashSet<(Guid, DateOnly)> unavailable, DateTimeOffset? reception = null)
    {
        if (!Enabled) return new(true, "SCHEDULE_POLICY_INACTIVE");
        var day = WeekContract.LocalToday(task.StartsAt);
        var received = reception > task.StartsAt ? reception.Value : task.StartsAt;
        var due = task.TaskId == ClosingTaskId && task.SchedulingPolicyId == null ? At(day, day.DayOfWeek == DayOfWeek.Sunday ? 900 : 1260) : task.DueAt;
        if (received >= due) return new(false, "SCHEDULE_DEADLINE_PASSED");
        var coverage = Coverages.Where(c => c.Day == day).OrderByDescending(c => c.Revision).FirstOrDefault();
        if (task.TaskId == ClosingTaskId && person != ClosingPerson(day)) return new(false, "CLOSING_COVERAGE_REQUIRED", CoverageId: coverage?.Id);
        // A deadline is a completion margin, never permission to wait for a later shift.
        var effective = Day(person, day, UsesCoverageSchedule(task));
        if (unavailable.Contains((person, day))) return new(false, "SCHEDULE_ABSENT");
        if (effective.Intervals == null) return new(false, "SCHEDULE_MISSING");
        if (!effective.Intervals.Any(i => At(day, i.Entry) <= task.StartsAt && task.StartsAt < At(day, i.Exit)))
            return new(false, "SCHEDULE_NOT_PRESENT_AT_ACTIVATION", effective.Schedule?.Id, effective.Exception?.Id);
        var receptionDay = WeekContract.LocalToday(received);
        if (unavailable.Contains((person, receptionDay))) return new(false, "SCHEDULE_ABSENT");
        var receptionSchedule = Day(person, receptionDay, UsesCoverageSchedule(task));
        foreach (var interval in receptionSchedule.Intervals ?? [])
        {
            var entry = At(receptionDay, interval.Entry); var exit = At(receptionDay, interval.Exit);
            if (entry > received || received >= exit) continue;
            if (exit > due) exit = due;
            return new(true, "ELIGIBLE", effective.Schedule?.Id, effective.Exception?.Id, CoverageId: coverage?.Id,
                Limit: task.TaskId == ClosingTaskId ? due : null,
                Windows: [new(receptionDay, received, exit, receptionSchedule.Schedule?.Id, receptionSchedule.Exception?.Id, CoverageId: coverage?.Id)]);
        }
        return new(false, "SCHEDULE_NOT_PRESENT_AT_RECEPTION", effective.Schedule?.Id, effective.Exception?.Id);
    }
    public Guid? ClosingPerson(DateOnly day)
    {
        if (closingCache.TryGetValue(day, out var cached)) return cached;
        var selected = Coverages.Where(c => c.Day == day).OrderByDescending(c => c.Revision).FirstOrDefault();
        if (selected != null) { closingCache[day] = selected.PersonId; return selected.PersonId; }
        var boundary = day.DayOfWeek == DayOfWeek.Sunday ? 840 : 1200;
        var excluded = Rotation(day)?.ExcludedPersonId;
        var result = Schedules.Select(s => s.PersonId).Distinct().Where(p => p != excluded &&
            (Day(p, day, false).Intervals ?? []).Any(i => i.Entry < boundary && i.Exit >= boundary))
            .OrderBy(p => p).Select(p => (Guid?)p).FirstOrDefault();
        closingCache[day] = result; return result;
    }
    public static async Task<TemporalEligibilityContext> LoadAsync(SgolDbContext db, DateTimeOffset now, CancellationToken ct) => new()
    {
        Now = now,
        BaseSchedulePolicyIds = await db.Set<OperationalSchedulingPolicy>().Where(p => p.AgendaVersion >= 2).OrderBy(p => p.Revision).Select(p => p.Id).ToArrayAsync(ct),
        BaseSchedulesForCapacity = await db.Set<OperationalSchedulingSettings>().Join(db.Set<OperationalSchedulingPolicy>(), s => s.CurrentPolicyId, p => p.Id, (s,p) => p.AgendaVersion >= 2).SingleOrDefaultAsync(ct),
        ClosingTaskId = await db.Set<TemporalAssignmentState>().Where(s=>s.Id==1).Select(s=>s.ClosingTaskId).SingleOrDefaultAsync(ct),
        Enabled = await db.Set<TemporalAssignmentState>().AnyAsync(s => s.Id == 1 && s.Enabled, ct),
        StateRevision = await db.Set<TemporalAssignmentState>().Where(s => s.Id == 1).Select(s => (long?)s.Revision).SingleOrDefaultAsync(ct) ?? 0,
        Schedules = await db.Set<PersonalScheduleVersion>().AsNoTracking().OrderBy(s => s.PersonId).ThenBy(s => s.Revision).ToArrayAsync(ct),
        Exceptions = await db.Set<PersonalScheduleException>().AsNoTracking().OrderBy(s => s.PersonId).ThenBy(s => s.Day).ThenBy(s => s.Revision).ToArrayAsync(ct),
        Rotations = await db.Set<ClosingRotationVersion>().AsNoTracking().OrderBy(s => s.Revision).ToArrayAsync(ct),
        Coverages = await db.Set<ClosingCoverageVersion>().AsNoTracking().OrderBy(s => s.Day).ThenBy(s => s.Revision).ToArrayAsync(ct),
    };
}

internal sealed class PersonalScheduleConfiguration : IEntityTypeConfiguration<PersonalScheduleVersion>, IEntityTypeConfiguration<PersonalScheduleException>,
    IEntityTypeConfiguration<ClosingRotationVersion>, IEntityTypeConfiguration<ClosingCoverageVersion>, IEntityTypeConfiguration<TemporalAssignmentState>
{
    public void Configure(EntityTypeBuilder<PersonalScheduleVersion> b)
    {
        b.ToTable("personal_schedule_version", t => t.HasCheckConstraint("CK_personal_schedule_range", "\"Through\" IS NULL OR \"Through\" >= \"From\""));
        b.HasKey(s => s.Id); b.HasIndex(s => new { s.PersonId, s.Revision }).IsUnique(); b.HasIndex(s => s.IntentKey).IsUnique();
        b.HasOne<Person>().WithMany().HasForeignKey(s => s.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Property(s => s.Revision).IsConcurrencyToken(); b.Property(s => s.DaysJson).HasColumnType("jsonb");
    }
    public void Configure(EntityTypeBuilder<PersonalScheduleException> b)
    {
        b.ToTable("personal_schedule_exception"); b.HasKey(s => s.Id);
        b.HasIndex(s => new { s.PersonId, s.Day, s.Revision }).IsUnique(); b.HasIndex(s => s.IntentKey).IsUnique();
        b.HasOne<Person>().WithMany().HasForeignKey(s => s.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Property(s => s.IntervalsJson).HasColumnType("jsonb"); b.Property(s => s.Reason).HasMaxLength(1000);
    }
    public void Configure(EntityTypeBuilder<ClosingRotationVersion> b)
    {
        b.ToTable("closing_rotation_version", t => t.HasCheckConstraint("CK_closing_rotation", "\"WeekDay\" BETWEEN 0 AND 6 AND \"ExtendedExit\" BETWEEN 1 AND 1440"));
        b.HasKey(s => s.Id); b.HasIndex(s => s.Revision).IsUnique(); b.HasIndex(s => s.IntentKey).IsUnique();
        b.HasOne<Person>().WithMany().HasForeignKey(s => s.RegularPersonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Person>().WithMany().HasForeignKey(s => s.ExcludedPersonId).OnDelete(DeleteBehavior.Restrict);
        b.Property(s => s.ParticipantsJson).HasColumnType("jsonb");
    }
    public void Configure(EntityTypeBuilder<ClosingCoverageVersion> b)
    {
        b.ToTable("closing_coverage_version"); b.HasKey(s => s.Id); b.HasIndex(s => new { s.Day, s.Revision }).IsUnique(); b.HasIndex(s => s.IntentKey).IsUnique();
        b.HasOne<Person>().WithMany().HasForeignKey(s => s.PersonId).OnDelete(DeleteBehavior.Restrict); b.Property(s => s.Reason).HasMaxLength(1000);
    }
    public void Configure(EntityTypeBuilder<TemporalAssignmentState> b)
    {
        b.ToTable("temporal_assignment_state"); b.HasKey(s => s.Id); b.Property(s => s.Revision).IsConcurrencyToken();
        b.Property(s=>s.SundayTaskIdsJson).HasColumnType("jsonb").HasDefaultValue("[]");
    }
}
