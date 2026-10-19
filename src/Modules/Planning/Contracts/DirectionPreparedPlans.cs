using System.Globalization;
using System.Text;

namespace Sgol.Planning.Contracts;

public sealed class DirectionPlanException(string code, int status) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed record DirectionPlanInput(string Name, string Area, string PeriodType, int Year,
    int? PeriodNumber, IReadOnlyList<string> TaskCodes,
    IReadOnlyList<PlanCapacityAdjustment>? CapacityAdjustments = null, string? CapacityFingerprint = null,
    bool IsWeeklyOperationsPlan = false);
public sealed record PlanCapacityAdjustment(Guid PersonId, int Percent);
public sealed record DirectionPlanTask(string TaskCode, string Name);
public sealed record MonthlyPlanPending(string ScheduleKey, string TaskCode, string Name, int Year, int Month, int PreferredDay, string Reason);
public sealed record OperationalAssignmentDetails(Guid Id, string TaskCode, string Name, string Requirement,
    int LoadMinutes, Guid PersonId, string PersonName, DateTimeOffset StartsAt, DateTimeOffset DueAt,
    DateTimeOffset? CompletedAt, long PreviousLoadMinutes, string? Comment, string? FileName, string? AssignedRole = null,
    string? Conclusion = null, string? ReviewStatus = null, string? ReviewComment = null,
    Guid? PlanId = null, string? PlanName = null, string? PlanPeriodType = null,
    DateOnly? PlanStartsOn = null, DateOnly? PlanEndsOn = null,
    string ScheduleMode = "DISTRIBUIDA", int GoalUnits = 0, int ProgressUnits = 0, string ProgressUnit = "unidades", long Revision = 1,
    Guid? SpecializationId = null, string? SpecializationName = null, int MinimumPhotos = 1, int MaximumPhotos = 20, long MaximumPhotoBytes = 31457280,
    Guid? OccasionId = null, string? Description = null, string? TemporalPolicyJson = null, string? TemporalDecisionJson = null);
public sealed record DirectionPlanDetails(Guid PlanId, string BranchCode, string Name, string Area,
    string PeriodType, int Year, int? PeriodNumber, DateOnly StartsOn, DateOnly EndsOn,
    int StartIsoYear, int StartIsoWeek, int EndIsoYear, int EndIsoWeek, string Status,
    DateTimeOffset CreatedAt, IReadOnlyList<DirectionPlanTask> Tasks,
    IReadOnlyList<OperationalAssignmentDetails>? Assignments = null, bool IsWeeklyOperationsPlan = false,
    IReadOnlyList<MonthlyPlanPending>? MonthlyPending = null);
public sealed record DirectionPlanSaved(string Result, DirectionPlanDetails Plan);
public sealed record DirectionPlanCursor(Guid Actor, string Role, int Limit, DateTimeOffset CreatedAt, Guid PlanId);
public sealed record DirectionPlanPage(IReadOnlyList<DirectionPlanDetails> Plans, DirectionPlanCursor? NextCursor);
public sealed record DirectionPlanDeletionPreview(Guid PlanId, string Name, int Tasks, int Assignments,
    int CompletedAssignments, int EvidenceFiles, int ProgressEntries, int CapacityAdjustments, string Fingerprint);
public sealed record DirectionPlanDeleted(Guid PlanId, string Result);
public sealed record DirectionPlanArchive(Stream Content, string FileName, string Fingerprint);

public interface IDirectionPreparedPlanService
{
    Task AuthorizeAsync(Guid actor, CancellationToken token = default);
    Task AuthorizeWeeklyOperationsAsync(Guid actor, CancellationToken token = default);
    Task<DirectionPlanSaved> CreateAsync(Guid actor, Guid key, Guid correlation, DirectionPlanInput input, CancellationToken token = default);
    Task<DirectionPlanDetails> ReadAsync(Guid actor, Guid id, CancellationToken token = default);
    Task<DirectionPlanPage> ListAsync(Guid actor, int limit, DirectionPlanCursor? cursor, CancellationToken token = default);
    Task<DirectionPlanDeletionPreview> PreviewDeletionAsync(Guid actor, Guid id, CancellationToken token = default);
    Task<DirectionPlanArchive> DownloadArchiveAsync(Guid actor, Guid id, string fingerprint, CancellationToken token = default);
    Task<DirectionPlanDeleted> DeleteAsync(Guid actor, Guid id, Guid key, Guid correlation, string fingerprint, CancellationToken token = default);
}

public static class DirectionPlanContract
{
    public static DateOnly OperationsStart(DateOnly today) => today.DayOfWeek switch
    {
        DayOfWeek.Saturday => today.AddDays(2),
        DayOfWeek.Sunday => today.AddDays(1),
        _ => today
    };

    // Both weekend days prepare the following Monday.
    public static (int Year, int Week) OperationsWeek(DateOnly today)
    {
        var day = OperationsStart(today).ToDateTime(TimeOnly.MinValue);
        return (ISOWeek.GetYear(day), ISOWeek.GetWeekOfYear(day));
    }
    public static readonly IReadOnlyList<string> PeriodTypes = ["SEMANAL", "MENSUAL", "TRIMESTRAL", "SEMESTRAL", "ANUAL"];

    public static WeekRange Calculate(string periodType, int year, int? number, DateOnly? operationsToday = null)
    {
        try
        {
            if (year is < 1 or > 9999 || !PeriodTypes.Contains(periodType)) throw PeriodError();
            if (periodType == "SEMANAL")
            {
                var range = WeekContract.Calculate(year, number ?? 0);
                if (operationsToday is not { } today) return range;
                var start = OperationsStart(today);
                if (range.EndsOn < start) throw new DirectionPlanException("PLAN_ENDED", 422);
                return new(DateOnly.FromDayNumber(Math.Max(start.DayNumber, range.StartsOn.DayNumber)), range.EndsOn);
            }
            var month = periodType switch
            {
                "MENSUAL" when number is >= 1 and <= 12 => number.Value,
                "TRIMESTRAL" when number is >= 1 and <= 4 => (number.Value - 1) * 3 + 1,
                "SEMESTRAL" when number is >= 1 and <= 2 => (number.Value - 1) * 6 + 1,
                "ANUAL" when number is null => 1,
                _ => throw PeriodError()
            };
            var months = periodType switch { "MENSUAL" => 1, "TRIMESTRAL" => 3, "SEMESTRAL" => 6, _ => 12 };
            var lastMonth = month + months - 1;
            return new(new(year, month, 1), new(year, lastMonth, DateTime.DaysInMonth(year, lastMonth)));
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or WeekValidationException)
        { throw PeriodError(); }
    }

    public static DirectionPlanInput Normalize(DirectionPlanInput input)
    {
        if (input.IsWeeklyOperationsPlan)
        {
            if (input.PeriodType != "SEMANAL") throw new DirectionPlanException("DIRECTION_PLAN_PERIOD_INVALID", 422);
            Calculate(input.PeriodType, input.Year, input.PeriodNumber);
            input = input with { Name = $"Semana {input.PeriodNumber:00}", Area = "Plan de Operaciones Semanal" };
        }
        if (input.TaskCodes is null || input.TaskCodes.Count > 1000 ||
            input.TaskCodes.Any(string.IsNullOrEmpty) || input.TaskCodes.Distinct(StringComparer.Ordinal).Count() != input.TaskCodes.Count)
            throw new DirectionPlanException("DIRECTION_PLAN_TASK_INVALID", 422);
        if (input.CapacityAdjustments is { } adjustments && (adjustments.Count > 1000 ||
            adjustments.Any(a => a is null || a.PersonId == Guid.Empty || a.Percent is < 0 or > 100) ||
            adjustments.Select(a => a.PersonId).Distinct().Count() != adjustments.Count ||
            adjustments.Count > 0 && (input.CapacityFingerprint?.Length != 64)))
            throw new DirectionPlanException("PLAN_CAPACITY_INVALID", 422);
        return input with { Name = Text(input.Name), Area = Text(input.Area), TaskCodes = input.TaskCodes.ToArray() };
    }

    private static string Text(string? text)
    {
        var value = (text ?? "").Trim().Normalize(NormalizationForm.FormC);
        if (value.Length == 0 || value.EnumerateRunes().Count() > 120 || value.Any(char.IsControl))
            throw new DirectionPlanException("DIRECTION_PLAN_REQUEST_INVALID", 400);
        return value;
    }

    private static DirectionPlanException PeriodError() => new("DIRECTION_PLAN_PERIOD_INVALID", 422);
}

/// <summary>Plan metadata; generated assignments preserve the task parameters at creation.</summary>
public sealed class DirectionPreparedPlan
{
    private DirectionPreparedPlan() { }
    // An occasion uses the existing assignment container without calendar generation.
    public static DirectionPreparedPlan ForOccasion(Guid id, Guid branchId, Guid actor, DateTimeOffset now,
        string name, DateOnly startsOn, DateOnly endsOn) => new() {
        Id = id, BranchId = branchId, CreatedBy = actor, CreatedAt = now, Name = name,
        Area = "Tareas ocasionales", PeriodType = "OCASIONAL", Year = startsOn.Year,
        StartsOn = startsOn, EndsOn = endsOn, Status = "ASIGNADO"
    };
    public DirectionPreparedPlan(Guid id, Guid branchId, Guid actor, DateTimeOffset createdAt, DirectionPlanInput input)
    {
        var normalized = DirectionPlanContract.Normalize(input);
        var range = DirectionPlanContract.Calculate(normalized.PeriodType, normalized.Year, normalized.PeriodNumber,
            normalized.IsWeeklyOperationsPlan ? WeekContract.LocalToday(createdAt) : null);
        if (id == Guid.Empty || branchId == Guid.Empty || actor == Guid.Empty || createdAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Plan identifiers and a UTC instant are required.");
        Id = id; BranchId = branchId; CreatedBy = actor; CreatedAt = createdAt;
        Name = normalized.Name; Area = normalized.Area; PeriodType = normalized.PeriodType;
        IsWeeklyOperationsPlan = normalized.IsWeeklyOperationsPlan;
        Year = normalized.Year; PeriodNumber = normalized.PeriodNumber; StartsOn = range.StartsOn; EndsOn = range.EndsOn;
    }
    public Guid Id { get; private init; }
    public Guid BranchId { get; private init; }
    public Guid CreatedBy { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public string Name { get; private init; } = null!;
    public string Area { get; private init; } = null!;
    public string PeriodType { get; private init; } = null!;
    public int Year { get; private init; }
    public int? PeriodNumber { get; private init; }
    public DateOnly StartsOn { get; private init; }
    public DateOnly EndsOn { get; private init; }
    public string Status { get; private set; } = WorkPlanStatuses.Draft;
    public bool IsWeeklyOperationsPlan { get; private init; }
    public string MonthlyPendingJson { get; private set; } = "[]";
    public void CaptureMonthlyPending(IReadOnlyList<MonthlyPlanPending> pending) =>
        MonthlyPendingJson = System.Text.Json.JsonSerializer.Serialize(pending);
    public void MarkAssigned() => Status = "ASIGNADO";
    public static DirectionPreparedPlan ForProcess(Guid id, Guid branchId, Guid actor, DateTimeOffset now,
        string name, DateOnly startsOn, DateOnly endsOn) => new() {
        Id=id,BranchId=branchId,CreatedBy=actor,CreatedAt=now,Name=name,Area="Procesos de trabajo",PeriodType="OCASIONAL",
        Year=startsOn.Year,StartsOn=startsOn,EndsOn=endsOn,Status="BORRADOR"
    };

    public DirectionPlanDetails Details(IReadOnlyList<DirectionPlanTask> tasks) => new(Id, "LOR-001", Name, Area,
        PeriodType, Year, PeriodNumber, StartsOn, EndsOn,
        ISOWeek.GetYear(StartsOn.ToDateTime(TimeOnly.MinValue)), ISOWeek.GetWeekOfYear(StartsOn.ToDateTime(TimeOnly.MinValue)),
        ISOWeek.GetYear(EndsOn.ToDateTime(TimeOnly.MinValue)), ISOWeek.GetWeekOfYear(EndsOn.ToDateTime(TimeOnly.MinValue)),
        Status, CreatedAt, tasks, IsWeeklyOperationsPlan: IsWeeklyOperationsPlan,
        MonthlyPending: System.Text.Json.JsonSerializer.Deserialize<MonthlyPlanPending[]>(MonthlyPendingJson));
}

public sealed class DirectionPreparedPlanItem
{
    private DirectionPreparedPlanItem() { }
    public DirectionPreparedPlanItem(Guid planId, string taskCode, string name, int position)
    { PlanId = planId; TaskCode = taskCode; Name = name; Position = position; }
    public Guid PlanId { get; private init; }
    public string TaskCode { get; private init; } = null!;
    public string Name { get; private init; } = null!;
    public int Position { get; private init; }
}
