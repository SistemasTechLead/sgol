using System.Globalization;
using System.ComponentModel.DataAnnotations.Schema;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class OperationalTask
{
    public string? OperationalCriticality { get; set; }
    public Guid ProcessId { get; set; } = OperationalSequence.GeneralId;
    public Guid Id { get; set; }
    public string TaskCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int MinimumPhotos { get; set; } = 1;
    public int MaximumPhotos { get; set; } = 20;
    public long MaximumPhotoBytes { get; set; } = 31457280;
    public string ChecklistJson { get; set; } = "[]";
    public string Requirement { get; set; } = "NINGUNO";
    public string AssignedRole { get; set; } = CanonicalRole.SalesFloor;
    public string PeriodType { get; set; } = "SEMANAL";
    public int Repetitions { get; set; } = 1;
    public int DeadlineHours { get; set; } = 24;
    public int LoadMinutes { get; set; } = 30;
    public string ScheduleMode { get; set; } = "DISTRIBUIDA";
    public int WeekDays { get; set; }
    // Comma separated unique day numbers; independent from the weekly bitmask.
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string PreferredMonthDays { get; set; } = "";
    public int StartHour { get; set; }
    public int StartMinute { get; set; }
    public int? SundayStartMinute { get; set; }
    [NotMapped] public TimeOnly? SundayStartTime { get => SundayStartMinute is >= 0 and < 1440 ? new TimeOnly(SundayStartMinute.Value / 60, SundayStartMinute.Value % 60) : null; set => SundayStartMinute = value is {} time ? time.Hour * 60 + time.Minute : null; }
    [NotMapped] public TimeOnly StartTime { get => StartHour is >= 0 and <= 23 && StartMinute is >= 0 and <= 59 ? new(StartHour, StartMinute) : TimeOnly.MinValue; set { StartHour = value.Hour; StartMinute = value.Minute; } }
    public DateTimeOffset StartOn(DateOnly day)
    {
        return TemporalEligibilityContext.At(day, day.DayOfWeek == DayOfWeek.Sunday && SundayStartMinute is {} minute ? minute : StartHour * 60 + StartMinute);
    }
    public bool DifferentPeople { get; set; }
    public int? GoalUnits { get; set; } = 100;
    public string ProgressUnit { get; set; } = "unidades";
    public bool Active { get; set; } = true;
    public bool IsWeeklyOperationsTask { get; set; }
    public Guid? SpecializationId { get; set; }
    public bool Deleted { get; set; }
    public long Revision { get; set; } = 1;

    public void Validate()
    {
        if (OperationalCriticality != null && !OperationalAgenda.IsDefined(OperationalCriticality)) throw new DirectionPlanException("SCHEDULING_CRITICALITY_REQUIRED", 422);
        if (StartMinute is < 0 or > 59 || SundayStartMinute is < 0 or > 1439)
            throw new DirectionPlanException("TASK_PARAMETERS_INVALID", 422);
        if (IsWeeklyOperationsTask && (ProcessId != OperationalSequence.GeneralId || PeriodType != "SEMANAL"))
            throw new DirectionPlanException("WEEKLY_OPERATIONS_TASK_INVALID", 422);
        ChecklistJson = OperationalChecklist.Normalize(ChecklistJson, Requirement == "CHECKLIST");
        Name = Name.Trim();
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        if (Description?.Length > 4000 || Description?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new DirectionPlanException("TASK_DESCRIPTION_INVALID", 422);
        ProgressUnit = (ProgressUnit ?? "").Trim();
        var monthDays = MonthlyTaskCalendar.Parse(PreferredMonthDays);
        if (monthDays.Length > 0 && (PeriodType != "MENSUAL" || ScheduleMode != "DISTRIBUIDA"))
            throw new DirectionPlanException("TASK_MONTH_DAYS_INVALID", 422);
        PreferredMonthDays = string.Join(',', monthDays);
        if (monthDays.Length > 0) Repetitions = monthDays.Length;
        if (PeriodType == "OCASIONAL")
        {
            Repetitions = 1; ScheduleMode = "DISTRIBUIDA"; WeekDays = 0;
            DifferentPeople = false; StartHour = 0; StartMinute = 0; SundayStartMinute = null;
        }
        if (ScheduleMode == "DIAS_FIJOS") Repetitions = System.Numerics.BitOperations.PopCount((uint)WeekDays);
        if (ScheduleMode == "PROGRESO") Repetitions = 1;
        if (MinimumPhotos is < 1 or > 20 || MaximumPhotos < MinimumPhotos || MaximumPhotos > 20 ||
            MaximumPhotoBytes is < 1048576 or > 31457280 || Name.Length is < 1 or > 500 || Name.Any(char.IsControl) ||
            !OperationalSchedule.Periods.Contains(PeriodType) ||
            !OperationalSchedule.Requirements.Contains(Requirement) ||
            !CanonicalRole.IsDefined(AssignedRole) ||
            Repetitions is < 1 or > 100 || DeadlineHours is < 1 or > 8760 || LoadMinutes is < 1 or > 4800 ||
            ScheduleMode is not ("DISTRIBUIDA" or "DIAS_FIJOS" or "PROGRESO") || StartHour is < 0 or > 23 ||
            WeekDays is < 0 or > 127 || ScheduleMode == "DIAS_FIJOS" && (PeriodType != "SEMANAL" || WeekDays == 0) ||
            ScheduleMode == "PROGRESO" && (PeriodType != "SEMANAL" || Active && DifferentPeople) ||
            GoalUnits is < 1 or > 1000000 || ProgressUnit.Length > 60 ||
            (Active || ScheduleMode != "PROGRESO") && (GoalUnits == null || ProgressUnit.Length == 0))
            throw new DirectionPlanException("TASK_PARAMETERS_INVALID", 422);
    }
}

public sealed class OperationalAssignment
{
    public Guid? ItemLoadSourceAttemptId { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long? PendingItemDemand { get; set; }
    public string? OperationalCriticality { get; set; }
    public Guid? SchedulingPolicyId { get; set; }
    public int? ClosureMarginPercent { get; set; }
    public DateTimeOffset? NominalStartsAt { get; set; }
    public string? TemporalPolicyJson { get; set; }
    public string? TemporalDecisionJson { get; set; }
    public Guid? OccasionId { get; set; }
    public Guid? SpecializationId { get; set; }
    public string? SpecializationName { get; set; }
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid TaskId { get; set; }
    public long? TaskRevision { get; set; }
    public string? DefinitionSnapshotJson { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public OperationalAttachment? Attachment { get; set; }
    public string TaskCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int MinimumPhotos { get; set; } = 1;
    public int MaximumPhotos { get; set; } = 20;
    public long MaximumPhotoBytes { get; set; } = 31457280;
    public string ChecklistJson { get; set; } = "[]";
    public string Requirement { get; set; } = "";
    public string? AssignedRole { get; set; }
    public int LoadMinutes { get; set; }
    public string PeriodType { get; set; } = "SEMANAL";
    public int Repetitions { get; set; } = 1;
    public Guid PersonId { get; set; }
    public string PersonName { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedBy { get; set; }
    public string? CompletionKind { get; set; }
    public string ChecklistDraftJson { get; set; } = "null";
    public string? Comment { get; set; }
    // Transient download/view metadata; evidence is stored in child tables.
    [NotMapped] public string? FileName { get; set; }
    [NotMapped] public string? ContentType { get; set; }
    [NotMapped] public byte[]? FileContent { get; set; }
    public long PreviousLoadMinutes { get; set; }
    public string? Conclusion { get; set; }
    public string? ReviewStatus { get; set; }
    public string? ReviewComment { get; set; }
    public string? ReviewDecision { get; set; }
    public string? ReassignmentComment { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public Guid? ReassignedFromId { get; set; }
    public long Revision { get; set; } = 1;
    public string? ScheduleKey { get; set; }
    public string PreferredMonthDays { get; set; } = "";
    public string ScheduleMode { get; set; } = "DISTRIBUIDA";
    public bool DifferentPeople { get; set; }
    public int GoalUnits { get; set; }
    public int ProgressUnits { get; set; }
    public int? TransferRemainingMinutes { get; set; }
    public int TransferProgressUnits { get; set; }
    public string ProgressUnit { get; set; } = "unidades";
    public int RemainingMinutes => CompletedAt != null || RetiredAt != null ? 0 : TransferRemainingMinutes is { } remaining
        ? GoalUnits > TransferProgressUnits ? (int)Math.Ceiling(remaining * Math.Max(0m, (decimal)(GoalUnits - ProgressUnits) / (GoalUnits - TransferProgressUnits))) : remaining
        : GoalUnits > 0
        ? (int)Math.Ceiling(LoadMinutes * Math.Max(0m, 1m - (decimal)ProgressUnits / GoalUnits)) : LoadMinutes;
    public OperationalAssignmentDetails Details() => new(Id, TaskCode, Name, Requirement, LoadMinutes,
        PersonId, PersonName, StartsAt, DueAt, CompletedAt, PreviousLoadMinutes, Comment, FileName, AssignedRole,
        Conclusion, ReviewStatus, ReviewComment ?? ReassignmentComment, ScheduleMode: ScheduleMode, GoalUnits: GoalUnits,
        ProgressUnits: ProgressUnits, ProgressUnit: ProgressUnit, Revision: Revision, SpecializationId: SpecializationId, SpecializationName: SpecializationName, MinimumPhotos: MinimumPhotos, MaximumPhotos: MaximumPhotos, MaximumPhotoBytes: MaximumPhotoBytes, OccasionId: OccasionId, Description: Description, TemporalPolicyJson: TemporalPolicyJson, TemporalDecisionJson: TemporalDecisionJson, OperationalCriticality: OperationalCriticality, NominalStartsAt: NominalStartsAt, SchedulingPolicyId: SchedulingPolicyId,
        TransferRemainingMinutes: TransferRemainingMinutes, TransferProgressUnits: TransferProgressUnits, ChecklistJson: ChecklistJson, ChecklistDraftJson: ChecklistDraftJson);
}

public static class OperationalSchedule
{
    public static readonly string[] Periods = ["SEMANAL", "MENSUAL", "TRIMESTRAL", "ANUAL", "OCASIONAL"];
    public static readonly string[] Requirements = ["FOTOGRAFIA", "CAPTURA_PANTALLA", "COMENTARIO", "DOCUMENTO", "EXCEL", "CHECKLIST", "NINGUNO"];
    public static readonly string[] Roles = [CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor];
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    public static DateTimeOffset Instant(DateOnly day) => new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone), TimeSpan.Zero);
    public static DateTimeOffset Instant(DateOnly day, int hour) => new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, 0)), Zone), TimeSpan.Zero);
    public static (DateOnly From, DateOnly Through) LoadRange(DateOnly day, string period)
    {
        var from = period switch {
            "MENSUAL" => new DateOnly(day.Year, day.Month, 1),
            "TRIMESTRAL" => new DateOnly(day.Year, (day.Month - 1) / 3 * 3 + 1, 1),
            "ANUAL" => new DateOnly(day.Year, 1, 1),
            _ => day.AddDays(-(((int)day.DayOfWeek + 6) % 7))
        };
        var count = period switch {
            "MENSUAL" => DateTime.DaysInMonth(from.Year, from.Month),
            "TRIMESTRAL" => Enumerable.Range(from.Month, 3).Sum(m => DateTime.DaysInMonth(from.Year, m)),
            "ANUAL" => DateTime.IsLeapYear(from.Year) ? 366 : 365,
            _ => 7
        };
        return (from, DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, from.DayNumber + count - 1)));
    }
    public static IEnumerable<DateOnly> Dates(DateOnly start, DateOnly end, OperationalTask task)
    {
        if (task.PeriodType == "OCASIONAL") yield break;
        if (task.PreferredMonthDays.Length > 0)
        {
            foreach (var occurrence in MonthlyTaskCalendar.Occurrences(task, start, end))
                if (occurrence.ValidDate is { } date) yield return date;
            yield break;
        }
        if (task.ScheduleMode == "DIAS_FIJOS")
        {
            for (var number = start.DayNumber; number <= end.DayNumber; number++)
            {
                var day = DateOnly.FromDayNumber(number);
                if ((task.WeekDays & (1 << (int)day.DayOfWeek)) != 0) yield return day;
            }
        }
        else foreach (var day in Dates(start, end, task.PeriodType, task.Repetitions)) yield return day;
    }

    // Repetitions are spaced across each calendar cycle, including partial cycles at plan edges.
    public static IEnumerable<DateOnly> Dates(DateOnly start, DateOnly end, string period, int repetitions)
    {
        var cycle = period switch
        {
            "SEMANAL" => start.AddDays(-(((int)start.DayOfWeek + 6) % 7)),
            "MENSUAL" => new DateOnly(start.Year, start.Month, 1),
            "TRIMESTRAL" => new DateOnly(start.Year, (start.Month - 1) / 3 * 3 + 1, 1),
            "ANUAL" => new DateOnly(start.Year, 1, 1),
            _ => throw new DirectionPlanException("TASK_PARAMETERS_INVALID", 422)
        };
        while (cycle <= end)
        {
            // Avoid overflowing DateOnly in year 9999.
            var days = period switch
            {
                "SEMANAL" => 7,
                "MENSUAL" => DateTime.DaysInMonth(cycle.Year, cycle.Month),
                "TRIMESTRAL" => Enumerable.Range(cycle.Month, 3).Sum(m => DateTime.DaysInMonth(cycle.Year, m)),
                _ => DateTime.IsLeapYear(cycle.Year) ? 366 : 365
            };
            for (var i = 0; i < repetitions; i++)
            {
                var number = cycle.DayNumber + i * days / repetitions;
                if (number > DateOnly.MaxValue.DayNumber) break;
                var day = DateOnly.FromDayNumber(number);
                if (day >= start && day <= end) yield return day;
            }
            if (cycle.DayNumber + days > DateOnly.MaxValue.DayNumber) break;
            cycle = cycle.AddDays(days);
        }
    }
}

internal sealed class OperationalTaskConfiguration : IEntityTypeConfiguration<OperationalTask>
{
    public void Configure(EntityTypeBuilder<OperationalTask> b)
    {
        b.ToTable("operational_task", t => {
            t.HasCheckConstraint("CK_task_criticality", "\"OperationalCriticality\" IS NULL OR \"OperationalCriticality\" IN ('CRITICA','IMPORTANTE','FLEXIBLE')");
            t.HasCheckConstraint("CK_operational_task_month_days", "\"PreferredMonthDays\" = '' OR (\"PeriodType\" = 'MENSUAL' AND \"ScheduleMode\" = 'DISTRIBUIDA' AND \"PreferredMonthDays\" ~ '^([1-9]|[12][0-9]|3[01])(,([1-9]|[12][0-9]|3[01]))*$' AND \"Repetitions\" = cardinality(string_to_array(\"PreferredMonthDays\", ',')) AND cardinality(string_to_array(\"PreferredMonthDays\", ',')) <= 31)");
            t.HasCheckConstraint("CK_operational_task_time", "\"StartMinute\" BETWEEN 0 AND 59 AND (\"SundayStartMinute\" IS NULL OR \"SundayStartMinute\" BETWEEN 0 AND 1439)");
            t.HasCheckConstraint("CK_operational_task_schedule", "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"WeekDays\" BETWEEN 0 AND 127 AND \"StartHour\" BETWEEN 0 AND 23 AND (\"GoalUnits\" IS NULL OR \"GoalUnits\" BETWEEN 1 AND 1000000) AND length(\"ProgressUnit\") <= 60 AND ((NOT \"Active\" AND \"ScheduleMode\" = 'PROGRESO') OR (\"GoalUnits\" IS NOT NULL AND length(\"ProgressUnit\") >= 1)) AND (\"ScheduleMode\" <> 'DIAS_FIJOS' OR (\"PeriodType\" = 'SEMANAL' AND \"WeekDays\" > 0)) AND (\"ScheduleMode\" <> 'PROGRESO' OR (\"PeriodType\" = 'SEMANAL' AND (NOT \"Active\" OR NOT \"DifferentPeople\"))) AND (\"PeriodType\" <> 'OCASIONAL' OR (\"ScheduleMode\" = 'DISTRIBUIDA' AND \"WeekDays\" = 0 AND \"StartHour\" = 0 AND \"Repetitions\" = 1 AND NOT \"DifferentPeople\"))");
            t.HasCheckConstraint("CK_operational_task_parameters", "\"Repetitions\" BETWEEN 1 AND 100 AND \"DeadlineHours\" BETWEEN 1 AND 8760 AND \"LoadMinutes\" BETWEEN 1 AND 4800");
            t.HasCheckConstraint("CK_operational_task_photo_limits", "\"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280");
            t.HasCheckConstraint("CK_operational_task_period", "\"PeriodType\" IN ('SEMANAL','MENSUAL','TRIMESTRAL','ANUAL','OCASIONAL')");
            t.HasCheckConstraint("CK_operational_task_requirement", "\"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','CHECKLIST','NINGUNO')");
            t.HasCheckConstraint("CK_operational_task_role", "\"AssignedRole\" IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS')");
        });
        b.Property(t => t.OperationalCriticality).HasMaxLength(12);
        b.Property(t => t.ChecklistJson).HasDefaultValue("[]");
        b.HasKey(t => t.Id); b.HasAlternateKey(t => t.TaskCode);
        b.Property(t => t.ProcessId).HasDefaultValue(OperationalSequence.GeneralId);
        b.HasOne<OperationalSequence>().WithMany().HasForeignKey(t => t.ProcessId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().WithMany().HasForeignKey(t => t.SpecializationId).OnDelete(DeleteBehavior.Restrict);
        b.Property(t => t.TaskCode).HasMaxLength(16); b.Property(t => t.Name).HasMaxLength(500);
        b.Property(t => t.Description).HasMaxLength(4000);
        b.Property(t => t.PreferredMonthDays).HasMaxLength(83).HasDefaultValue("");
        b.Property(t => t.AssignedRole).HasMaxLength(20).HasDefaultValue(CanonicalRole.SalesFloor);
        b.Property(t => t.MinimumPhotos).HasDefaultValue(1);
        b.Property(t => t.MaximumPhotos).HasDefaultValue(20);
        b.Property(t => t.MaximumPhotoBytes).HasDefaultValue(31457280L);
        b.Property(t => t.Revision).IsConcurrencyToken();
        b.Property(t => t.IsWeeklyOperationsTask).HasDefaultValue(false);
        b.Property(t => t.ScheduleMode).HasMaxLength(20).HasDefaultValue("DISTRIBUIDA");
        b.Property(t => t.GoalUnits).HasDefaultValue(100).ValueGeneratedNever();
        b.Property(t => t.ProgressUnit).HasMaxLength(60).HasDefaultValue("unidades");
        b.HasData(TaskDefinitionCatalog.All.Select((t, i) => new OperationalTask {
            Id = t.Id, TaskCode = t.TaskCode, Name = t.Name, MaximumPhotos = 5, // Preserve the original seeded catalogue.
            Requirement = t.TaskCode == "TAR-0018" ? "FOTOGRAFIA" : t.TaskCode == "TAR-0026" ? "DOCUMENTO" : "COMENTARIO",
            PeriodType = t.TaskCode == "TAR-0026" ? "MENSUAL" : "SEMANAL",
            LoadMinutes = t.TaskCode == "TAR-0018" ? 60 : 30
        }));
    }
}

internal sealed class OperationalAssignmentConfiguration : IEntityTypeConfiguration<OperationalAssignment>
{
    public void Configure(EntityTypeBuilder<OperationalAssignment> b)
    {
        b.ToTable("operational_assignment", t => {
            t.HasCheckConstraint("CK_assignment_transfer_load", "\"TransferProgressUnits\" BETWEEN 0 AND \"ProgressUnits\" AND (\"TransferRemainingMinutes\" IS NULL OR \"TransferRemainingMinutes\" BETWEEN 1 AND 4800)");
            t.HasCheckConstraint("CK_assignment_scheduling", "(\"SchedulingPolicyId\" IS NULL AND \"ClosureMarginPercent\" IS NULL AND \"NominalStartsAt\" IS NULL) OR (\"SchedulingPolicyId\" IS NOT NULL AND \"ClosureMarginPercent\" IS NOT NULL AND \"ClosureMarginPercent\" BETWEEN 0 AND 500 AND \"NominalStartsAt\" IS NOT NULL AND (\"StartsAt\" >= \"NominalStartsAt\" OR \"OperationalCriticality\" = 'FLEXIBLE') AND \"OperationalCriticality\" IS NOT NULL AND \"OperationalCriticality\" IN ('CRITICA','IMPORTANTE','FLEXIBLE'))");
            t.HasCheckConstraint("CK_operational_assignment_photo_limits", "\"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280");
            t.HasCheckConstraint("CK_operational_assignment_range", "\"DueAt\" > \"StartsAt\"");
            t.HasCheckConstraint("CK_operational_assignment_progress", "\"GoalUnits\" BETWEEN 0 AND 1000000 AND \"ProgressUnits\" BETWEEN 0 AND 1000000 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND (\"TaskRevision\" IS NULL OR \"TaskRevision\" > 0)");
            t.HasCheckConstraint("CK_operational_assignment_schedule", "\"ScheduleMode\" IN ('DISTRIBUIDA','DIAS_FIJOS','PROGRESO') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','CHECKLIST','NINGUNO') AND (\"AssignedRole\" IS NULL OR \"AssignedRole\" IN ('DIRECCION','ADMINISTRACION','SUBCOORDINACION','PISO_VENTAS'))");
        }); b.HasKey(t => t.Id);
        b.Property(t => t.OperationalCriticality).HasMaxLength(12);
        b.HasQueryFilter(t => t.RetiredAt == null);
        b.Property(t => t.ChecklistJson).HasDefaultValue("[]");
        b.Property(t => t.ChecklistDraftJson).HasDefaultValue("null");
        b.Property(t => t.DefinitionSnapshotJson).HasColumnType("jsonb");
        b.HasOne<OperationalSchedulingPolicy>().WithMany().HasForeignKey(t => t.SchedulingPolicyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(t => new { t.PersonId, t.StartsAt, t.DueAt });
        // Author FKs on split fragments are created explicitly by the lifecycle migration.
        b.HasIndex(t => t.ReviewedBy);
        b.HasIndex(t => t.CompletedBy);
        b.SplitToTable("operational_submission", fragment => {
            fragment.Property(t => t.Id).HasColumnName("AssignmentId");
            fragment.Property(t => t.CompletedAt);
            fragment.Property(t => t.CompletedBy);
            fragment.Property(t => t.CompletionKind);
            fragment.Property(t => t.Comment);
            fragment.Property(t => t.Conclusion);
        });
        b.SplitToTable("operational_review", fragment => {
            fragment.Property(t => t.Id).HasColumnName("AssignmentId");
            fragment.Property(t => t.ReviewStatus);
            fragment.Property(t => t.ReviewDecision);
            fragment.Property(t => t.ReviewComment);
            fragment.Property(t => t.ReviewedAt);
            fragment.Property(t => t.ReviewedBy);
        });
        b.HasOne<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().WithMany().HasForeignKey(t => t.SpecializationId).OnDelete(DeleteBehavior.Restrict);
        b.Property(t => t.SpecializationName).HasMaxLength(120);
        b.Property(t => t.PreferredMonthDays).HasMaxLength(83).HasDefaultValue("");
        b.HasIndex(t => t.PersonId); b.HasIndex(t => t.CompletedAt); b.HasIndex(t => t.PlanId);
        b.HasOne<OperationalTask>().WithMany().HasForeignKey(t => t.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DirectionPreparedPlan>().WithMany().HasForeignKey(t => t.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperationalOccasion>().WithMany().HasForeignKey(t => new { t.OccasionId, t.PlanId }).HasPrincipalKey(o => new { o.Id, o.PlanId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Person>().WithMany().HasForeignKey(t => t.PersonId).OnDelete(DeleteBehavior.Restrict);
        b.Property(t => t.Name).HasMaxLength(500); b.Property(t => t.Comment).HasMaxLength(4000);
        b.Property(t => t.Description).HasMaxLength(4000);
        b.Property(t => t.AssignedRole).HasMaxLength(20);
        b.Property(t => t.Conclusion).HasMaxLength(4000);
        b.Property(t => t.ReviewComment).HasMaxLength(4000);
        b.Property(t => t.ReassignmentComment).HasMaxLength(4000);
        b.Property(t => t.CompletionKind).HasMaxLength(16);
        b.Property(t => t.ReviewDecision).HasMaxLength(24);
        b.Property(t => t.ReviewStatus).HasMaxLength(24);
        b.Property(t => t.PeriodType).HasMaxLength(16);
        b.Property(t => t.MinimumPhotos).HasDefaultValue(1);
        b.Property(t => t.MaximumPhotos).HasDefaultValue(20);
        b.Property(t => t.MaximumPhotoBytes).HasDefaultValue(31457280L);
        b.Property(t => t.Revision).IsConcurrencyToken();
        b.Property(t => t.ScheduleKey).HasMaxLength(100);
        b.HasIndex(t => t.ScheduleKey).IsUnique().HasFilter("\"RetiredAt\" IS NULL");
        b.Property(t => t.ScheduleMode).HasMaxLength(20).HasDefaultValue("DISTRIBUIDA");
        b.Property(t => t.ProgressUnit).HasMaxLength(60).HasDefaultValue("unidades");
        b.Ignore(t => t.RemainingMinutes);
        b.HasOne<OperationalAssignment>().WithMany().HasForeignKey(t => t.ReassignedFromId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(t => t.ReassignedFromId).IsUnique();
    }
}
