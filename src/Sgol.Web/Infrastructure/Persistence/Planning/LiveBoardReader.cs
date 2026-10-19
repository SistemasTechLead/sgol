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
    Guid TaskId = default, int LoadMinutes = 0, string TaskCode = "", Guid PersonId = default)
{
    public static LiveBoardTask FromAssignment(OperationalAssignmentDetails task) =>
        new(task.Id, task.Name, task.PersonName, task.AssignedRole ?? "", task.StartsAt, task.DueAt,
            task.CompletedAt, task.GoalUnits, task.ProgressUnits, task.ProgressUnit);

    public string Color(DateTimeOffset now) => CompletedAt is not null ? "delivered" :
        now < StartsAt ? "scheduled" : now >= DueAt ? "urgent" :
        (decimal)(DueAt - now).Ticks <= (DueAt - StartsAt).Ticks * 0.10m ? "urgent" :
        (decimal)(DueAt - now).Ticks <= (DueAt - StartsAt).Ticks * 0.30m ? "warning" : "active";
    public int Priority(DateTimeOffset now) => Color(now) switch {
        "urgent" => 0, "active" => 1, "warning" => 2, "scheduled" => 3, _ => 4 };
}

public sealed record LiveBoardSnapshot(DateTimeOffset Now, DateOnly Day, Guid? PlanId,
    IReadOnlyList<LiveBoardPlan> Plans, IReadOnlyList<LiveBoardTask> Tasks,
    IReadOnlyList<LiveBoardTask>? DailyTasks = null, Guid? TaskId = null)
{
    public IReadOnlyList<LiveBoardTask> DayTasks => DailyTasks ?? Tasks;
    public IEnumerable<LiveBoardTask> TaskOptions => DayTasks.Where(t => PlanId == null || t.PlanId == PlanId)
        .OrderBy(t => t.Name).ThenBy(t => t.PersonName).ThenBy(t => t.DueAt).ThenBy(t => t.Id);
    public int Delivered => DayTasks.Count(t => t.CompletedAt is not null);
    public IEnumerable<LiveBoardTask> OrderedTasks => Tasks.OrderBy(t => t.Priority(Now)).ThenBy(t => t.DueAt).ThenBy(t => t.Id);
    public int Percent => DayTasks.Count == 0 ? 0 : (int)(100L * Delivered / DayTasks.Count);
    public string DayName => CultureInfo.GetCultureInfo("es-MX").TextInfo.ToTitleCase(
        Day.ToDateTime(TimeOnly.MinValue).ToString("dddd", CultureInfo.GetCultureInfo("es-MX")));
    public string Time(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, OperationalSchedule.Zone)
        .ToString("dd/MM/yyyy · HH:mm", CultureInfo.InvariantCulture);
    public string Version => System.Text.Json.JsonSerializer.Serialize(new { Day, PlanId, TaskId, Plans, Tasks, DayTasks });
}

public sealed class LiveBoardReader(SgolDbContext db, IClock clock)
{
    // Board membership depends only on the local deadline day, including early deliveries.
    // My Work retains its own broader filter for active and overdue assignments.
    public static Expression<Func<OperationalAssignment, bool>> ForDay(DateOnly day) => ForRange(day, day.AddDays(1));

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
        var daily = db.OperationalAssignments.AsNoTracking().Where(ForRange(day, through));
        if (week != null)
        {
            var fromInstant = OperationalSchedule.Instant(day);
            var throughInstant = OperationalSchedule.Instant(through);
            daily = db.OperationalAssignments.AsNoTracking().Where(a => a.ReviewStatus != "REASIGNADA" &&
                a.ReviewStatus != "NO_SATISFECHA" && (a.DueAt >= fromInstant && a.DueAt < throughInstant ||
                a.StartsAt >= fromInstant && a.StartsAt < throughInstant));
        }
        // Plans outside their original period remain selectable if a task is due today.
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
                where planIds.Contains(a.PlanId)
                orderby a.DueAt, a.Id
                // No evidence, comments, or file bytes are read for this display.
                select new LiveBoardTask(a.Id, a.Name, a.PersonName, a.AssignedRole ?? t.AssignedRole,
                    a.StartsAt, a.DueAt, a.CompletedAt, a.GoalUnits, a.ProgressUnits, a.ProgressUnit, a.PlanId, "", "",
                    a.TaskId, a.LoadMinutes, a.TaskCode, a.PersonId)).ToArrayAsync(token);
        var origins = plans.ToDictionary(p => p.Id);
        var dayTasks = assignments.Select(t => t with { PlanName = origins[t.PlanId!.Value].Name,
            Origin = origins[t.PlanId.Value].Origin }).ToArray();
        var scoped = dayTasks.Where(t => planId == null || t.PlanId == planId).ToArray();
        var taskId = scoped.FirstOrDefault(t => t.Id == selectedTask)?.Id;
        var tasks = scoped.Where(t => taskId == null || t.Id == taskId).ToArray();
        return new(now, day, planId, plans, tasks, dayTasks, taskId);
    }
}
