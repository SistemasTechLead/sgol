using System.Text.Json;
using Sgol.Planning.Contracts;
namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class ProcessStartSchedule
{
    public string Mode { get; set; } = "DISTRIBUIDA";
    public int Repetitions { get; set; } = 1;
    public int WeekDays { get; set; }
    public string MonthDays { get; set; } = "";
    public TimeOnly Time { get; set; } = new(9, 0);
    public bool WeeklyOperations { get; set; }
    public static ProcessStartSchedule Read(string? json) => json == null ? new() : JsonSerializer.Deserialize<ProcessStartSchedule>(json)!;
    public static bool Recurring(string kind) => kind is "SEMANAL" or "MENSUAL" or "TRIMESTRAL" or "ANUAL";
    public OperationalTask Calendar(string kind)
    {
        if (!Recurring(kind) || Mode is not ("DISTRIBUIDA" or "DIAS_FIJOS") || Time.Ticks % TimeSpan.TicksPerMinute != 0 || WeeklyOperations && kind != "SEMANAL")
            throw new DirectionPlanException("PROCESS_START_SCHEDULE_INVALID", 422);
        var task = new OperationalTask { Name = "Inicio del proceso", PeriodType = kind, ScheduleMode = Mode,
            Repetitions = Repetitions, WeekDays = WeekDays, PreferredMonthDays = MonthDays ?? "", StartTime = Time };
        task.Validate();
        if (task.Repetitions > (kind == "SEMANAL" ? 7 : kind == "MENSUAL" ? 28 : kind == "TRIMESTRAL" ? 90 : 100) && task.PreferredMonthDays.Length == 0)
            throw new DirectionPlanException("PROCESS_START_SCHEDULE_INVALID", 422);
        return task;
    }
    public string Capture(string kind)
    {
        if (Recurring(kind)) { var task = Calendar(kind); Repetitions = task.Repetitions; MonthDays = task.PreferredMonthDays; }
        else if (WeeklyOperations) throw new DirectionPlanException("PROCESS_START_SCHEDULE_INVALID", 422);
        return JsonSerializer.Serialize(this);
    }
}
