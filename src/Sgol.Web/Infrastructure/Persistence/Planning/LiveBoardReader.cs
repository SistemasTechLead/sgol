using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record LiveBoardPlan(Guid Id, string Name, string Origin = "");
public sealed record LiveBoardTask(Guid Id, string Name, string PersonName, string Role,
    DateTimeOffset StartsAt, DateTimeOffset DueAt, DateTimeOffset? CompletedAt,
    int GoalUnits = 0, int ProgressUnits = 0, string ProgressUnit = "unidades",
    Guid? PlanId = null, string PlanName = "", string Origin = "",
    Guid TaskId = default, int LoadMinutes = 0, string TaskCode = "", Guid PersonId = default, string? OperationalCriticality = null)
{
    public static LiveBoardTask FromAssignment(OperationalAssignmentDetails task) =>
        new(task.Id, task.Name, task.PersonName, task.AssignedRole ?? "", task.StartsAt, task.DueAt,
            task.CompletedAt, task.GoalUnits, task.ProgressUnits, task.ProgressUnit, OperationalCriticality: task.OperationalCriticality);

    public string Color(DateTimeOffset now) => CompletedAt is not null ? "delivered" :
        now < StartsAt ? "scheduled" : now >= DueAt ? "urgent" :
        (decimal)(DueAt - now).Ticks <= (DueAt - StartsAt).Ticks * 0.10m ? "urgent" :
        (decimal)(DueAt - now).Ticks <= (DueAt - StartsAt).Ticks * 0.30m ? "warning" : "active";
    public int Priority(DateTimeOffset now) => Color(now) switch {
        "urgent" => 0, "active" => 1, "warning" => 2, "scheduled" => 3, _ => 4 };
    public string State(DateTimeOffset now) => Color(now) == "urgent"
        ? now >= DueAt ? "overdue" : "urgent" : Color(now);
}

public sealed record LiveBoardSnapshot(DateTimeOffset Now, DateOnly Day, Guid? PlanId,
    IReadOnlyList<LiveBoardPlan> Plans, IReadOnlyList<LiveBoardTask> Tasks,
    IReadOnlyList<LiveBoardTask>? DailyTasks = null, Guid? TaskId = null,
    IReadOnlyList<LiveBoardDeadline>? WeeklyDeadlines = null, string? StateFilter = null,
    IReadOnlyList<LiveBoardTask>? AllTasks = null)
{
    public static IReadOnlyList<string> States { get; } = ["scheduled", "active", "warning", "urgent", "overdue", "delivered"];
    public static string StateLabel(string state) => state switch {
        "scheduled" => "Por activar", "active" => "Activa", "warning" => "Último 30% del tiempo",
        "urgent" => "Último 10% del tiempo", "overdue" => "Vencida", "delivered" => "Entregada", _ => "" };
    public string? TaskFilter => StateFilter is null ? TaskId?.ToString() : $"state:{StateFilter}";
    public IEnumerable<LiveBoardTask> VisibleTasks => Tasks.Where(t => StateFilter is null || t.State(Now) == StateFilter);
    public IReadOnlyList<LiveBoardTask> DayTasks => DailyTasks ?? Tasks;
    public IReadOnlyList<LiveBoardTask> BoardTasks => AllTasks ?? DayTasks;
    public IEnumerable<LiveBoardTask> TaskOptions => BoardTasks.Where(t => PlanId == null || t.PlanId == PlanId)
        .OrderBy(t => t.Name).ThenBy(t => t.PersonName).ThenBy(t => t.DueAt).ThenBy(t => t.Id);
    public int Delivered => DayTasks.Count(t => t.CompletedAt is not null);
    public IEnumerable<LiveBoardTask> OrderedTasks => Tasks.OrderBy(t => t.Priority(Now)).ThenBy(t => t.DueAt).ThenBy(t => t.Id);
    public int Percent => DayTasks.Count == 0 ? 0 : (int)(100L * Delivered / DayTasks.Count);
    public LiveBoardPace Pace => LiveBoardPace.For(DayTasks, Now);
    public IReadOnlyList<LiveBoardDeadline> DayDeadlines => DayTasks.Select(t => new LiveBoardDeadline(t.DueAt, t.CompletedAt)).ToArray();
    public IEnumerable<IGrouping<Guid, LiveBoardTask>> Collaborators => DayTasks
        .Where(t => t.Role != CanonicalRole.Direction).GroupBy(t => t.PersonId)
        .OrderBy(g => g.First().PersonName).ThenBy(g => g.Key);
    public DateOnly WeekStartsOn => WeeklyBoardSnapshot.Monday(Day);
    public string DayName => CultureInfo.GetCultureInfo("es-MX").TextInfo.ToTitleCase(
        Day.ToDateTime(TimeOnly.MinValue).ToString("dddd", CultureInfo.GetCultureInfo("es-MX")));
    public string Time(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, OperationalSchedule.Zone)
        .ToString("dd/MM/yyyy · HH:mm", CultureInfo.InvariantCulture);
    public string Version => System.Text.Json.JsonSerializer.Serialize(new { Day, PlanId, TaskId, Plans, Tasks, DayTasks, WeeklyDeadlines, BoardTasks });
}

public sealed class LiveBoardReader(SgolDbContext db, IClock clock)
{
    // Keep today's deadlines (including early deliveries) and outstanding critical work
    // from earlier local days. Historical deadlines and scheduling snapshots stay unchanged.
    public static Expression<Func<OperationalAssignment, bool>> ForDay(DateOnly day)
    {
        var start = OperationalSchedule.Instant(day);
        var end = OperationalSchedule.Instant(day.AddDays(1));
        return a => a.ReviewStatus != "REASIGNADA" && a.ReviewStatus != "NO_SATISFECHA" &&
            (a.DueAt >= start && a.DueAt < end ||
             a.DueAt < start && a.CompletedAt == null && a.OperationalCriticality == "CRITICA");
    }

    public static Expression<Func<OperationalAssignment, bool>> ForRange(DateOnly from, DateOnly throughExclusive)
    {
        var start = OperationalSchedule.Instant(from);
        var end = OperationalSchedule.Instant(throughExclusive);
        return a => a.ReviewStatus != "REASIGNADA" && a.ReviewStatus != "NO_SATISFECHA" &&
            a.DueAt >= start && a.DueAt < end;
    }

    public Task<LiveBoardSnapshot> ReadAsync(Guid actor, Guid? selectedPlan, CancellationToken token = default, Guid? selectedTask = null) =>
        ReadRangeAsync(actor, selectedPlan, null, token, selectedTask);

    public async Task<WeeklyBoardSnapshot> ReadWeekAsync(Guid actor, DateOnly? week, CancellationToken token = default)
    {
        var monday = WeeklyBoardSnapshot.Monday(week ?? WeekContract.LocalToday(clock.UtcNow));
        var snapshot = await ReadRangeAsync(actor, null, monday, token, null);
        return new(snapshot.Now, monday, snapshot.Tasks.Where(t => WeekContract.LocalToday(t.DueAt) >= monday &&
            WeekContract.LocalToday(t.DueAt) <= monday.AddDays(6)).ToArray(), snapshot.Tasks);
    }

    private async Task<LiveBoardSnapshot> ReadRangeAsync(Guid actor, Guid? selectedPlan, DateOnly? week,
        CancellationToken token, Guid? selectedTask)
    {
        var now = clock.UtcNow;
        var authorized = await (from u in db.AppUsers.AsNoTracking()
            join e in db.EmploymentVersions.AsNoTracking() on u.PersonId equals e.PersonId
            join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
            where u.Id == actor && u.Status == AccountStatus.Active &&
                e.BranchId == BranchScope.LorettaId && e.Status == EmploymentStatus.Active &&
                e.ValidFrom <= now && (e.ValidTo == null || e.ValidTo > now) &&
                r.BranchId == BranchScope.LorettaId && r.Status == RoleAssignmentStatus.Active &&
                r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now) &&
                OperationalSchedule.Roles.Contains(r.RoleCode)
            select u.Id).AnyAsync(token);
        if (!authorized) authorized = await db.Set<Sgol.Web.Infrastructure.Persistence.Identity.ScreenAccount>().AsNoTracking()
            .AnyAsync(x => x.Id == actor && x.BranchId == BranchScope.LorettaId &&
                x.Status == Sgol.Web.Infrastructure.Persistence.Identity.ScreenAccount.Active &&
                x.Purpose == Sgol.Web.Infrastructure.Persistence.Identity.ScreenAccount.BoardReadOnly &&
                x.CredentialHash != null && x.SessionStamp != null, token);
        if (!authorized) throw new DirectionPlanException("ACCESO_DENEGADO", 403);

        var day = week ?? WeekContract.LocalToday(now);
        var through = day.AddDays(week is null ? 1 : 7);
        var daily = db.OperationalAssignments.AsNoTracking().Where(ForDay(day));
        if (week != null)
        {
            var fromInstant = OperationalSchedule.Instant(day);
            var throughInstant = OperationalSchedule.Instant(through);
            daily = db.OperationalAssignments.AsNoTracking().Where(a => a.ReviewStatus != "REASIGNADA" &&
                a.ReviewStatus != "NO_SATISFECHA" && (a.DueAt >= fromInstant && a.DueAt < throughInstant ||
                a.StartsAt >= fromInstant && a.StartsAt < throughInstant));
        }
        // Include expired plans when they still have outstanding critical work.
        var plans = await db.DirectionPreparedPlans.AsNoTracking()
            .Where(p => p.BranchId == BranchScope.LorettaId && p.Status == "ASIGNADO" &&
                (p.StartsOn < through && p.EndsOn >= day || daily.Any(a => a.PlanId == p.Id)))
            .OrderBy(p => p.IsWeeklyOperationsPlan ? 0 : p.PeriodType == "SEMANAL" ? 1 : p.PeriodType == "MENSUAL" ? 2 :
                p.PeriodType == "TRIMESTRAL" ? 3 : p.PeriodType == "SEMESTRAL" ? 4 : p.PeriodType == "ANUAL" ? 5 : 6)
            .ThenByDescending(p => daily.Any(a => a.PlanId == p.Id)).ThenByDescending(p => p.StartsOn)
            .ThenBy(p => p.Name).ThenBy(p => p.Id).Select(p => new LiveBoardPlan(p.Id, p.Name,
                p.Area == "Procesos de trabajo" || db.Set<OperationalSequenceRun>().Any(r => r.PlanId == p.Id) ? "Proceso de trabajo" :
                p.IsWeeklyOperationsPlan ? "Plan de Operaciones Semanal" :
                p.PeriodType == "SEMANAL" ? "Plan semanal" : p.PeriodType == "MENSUAL" ? "Plan mensual" :
                p.PeriodType == "TRIMESTRAL" ? "Plan trimestral" : p.PeriodType == "SEMESTRAL" ? "Plan semestral" :
                p.PeriodType == "ANUAL" ? "Plan anual" : "Tarea ocasional")).ToArrayAsync(token);
        var planId = plans.FirstOrDefault(p => p.Id == selectedPlan)?.Id;
        var planIds = plans.Select(p => p.Id).ToArray();
        var assignments = await (from a in daily
                join t in db.OperationalTasks.AsNoTracking() on a.TaskId equals t.Id
                where planIds.Contains(a.PlanId) && (a.AssignedRole ?? t.AssignedRole) != CanonicalRole.Direction
                orderby a.DueAt, a.Id
                // No evidence, comments, or file bytes are read for this display.
                select new LiveBoardTask(a.Id, a.Name, a.PersonName, a.AssignedRole ?? t.AssignedRole,
                    a.StartsAt, a.DueAt, a.CompletedAt, a.GoalUnits, a.ProgressUnits, a.ProgressUnit, a.PlanId, "", "",
                    a.TaskId, a.LoadMinutes, a.TaskCode, a.PersonId, a.OperationalCriticality)).ToArrayAsync(token);
        var origins = plans.ToDictionary(p => p.Id);
        var boardTasks = assignments.Select(t => t with { PlanName = origins[t.PlanId!.Value].Name,
            Origin = origins[t.PlanId.Value].Origin }).ToArray();
        var dayTasks = week is null ? boardTasks.Where(t => t.DueAt >= OperationalSchedule.Instant(day) &&
            t.DueAt < OperationalSchedule.Instant(through)).ToArray() : boardTasks;
        var scoped = boardTasks.Where(t => planId == null || t.PlanId == planId).ToArray();
        var taskId = scoped.FirstOrDefault(t => t.Id == selectedTask)?.Id;
        var tasks = scoped.Where(t => taskId == null || t.Id == taskId).ToArray();
        IReadOnlyList<LiveBoardDeadline>? weeklyDeadlines = null;
        if (week is null)
        {
            var monday = WeeklyBoardSnapshot.Monday(day);
            // Use the same role exclusion as daily cards, independently of card filters.
            // Read only deadline/delivery timestamps; the existing weekly workload view is unchanged.
            weeklyDeadlines = await (from a in db.OperationalAssignments.AsNoTracking().Where(ForRange(monday, monday.AddDays(7)))
                join p in db.DirectionPreparedPlans.AsNoTracking() on a.PlanId equals p.Id
                join t in db.OperationalTasks.AsNoTracking() on a.TaskId equals t.Id
                where p.BranchId == BranchScope.LorettaId && p.Status == "ASIGNADO" &&
                    (a.AssignedRole ?? t.AssignedRole) != CanonicalRole.Direction
                orderby a.DueAt, a.Id
                select new LiveBoardDeadline(a.DueAt, a.CompletedAt)).ToArrayAsync(token);
        }
        return new(now, day, planId, plans, tasks, dayTasks, taskId, weeklyDeadlines, AllTasks: boardTasks);
    }
}
