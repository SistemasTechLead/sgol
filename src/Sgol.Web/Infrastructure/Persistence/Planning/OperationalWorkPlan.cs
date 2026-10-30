using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record OperationalWorkPlan(Guid? Id, string Name, string PeriodType,
    DateOnly? StartsOn, DateOnly? EndsOn, IReadOnlyList<OperationalAssignmentDetails> Tasks)
{
    public static IReadOnlyList<OperationalAssignmentDetails> TodayTasks(
        IEnumerable<OperationalAssignmentDetails> tasks, DateOnly day, DateTimeOffset now) =>
        tasks.Where(t => IsForDay(t, day))
            .OrderBy(t => Priority(t, day, now))
            .ThenBy(t => t.DueAt).ThenBy(t => t.Id).ToArray();

    public static int Priority(OperationalAssignmentDetails task, DateOnly day, DateTimeOffset now)
    {
        if (task.PendingProcessRunId != null) return 3;
        return task.ReviewStatus switch {
            "NO_SATISFECHA" => 0,
            "REASIGNADA" => task.CompletedAt != null && task.CompletionKind == "ENTREGA" ? 4 : 3,
            _ => LiveBoardTask.FromAssignment(task).Priority(now)
        };
    }

    public static IReadOnlyList<OperationalAssignmentDetails> OrderedTasks(
        IEnumerable<OperationalAssignmentDetails> tasks, DateOnly day, DateTimeOffset now) =>
        tasks.OrderBy(t => Priority(t, day, now)).ThenBy(t => t.DueAt).ThenBy(t => t.Id).ToArray();

    // My Work includes active/overdue work and today's deliveries; the board uses deadline day only.
    public static bool IsForDay(OperationalAssignmentDetails task, DateOnly day)
    {
        var start = OperationalSchedule.Instant(day);
        var end = OperationalSchedule.Instant(day.AddDays(1));
        return task.PendingProcessRunId == null && task.ReviewStatus is not ("REASIGNADA" or "NO_SATISFECHA") &&
            (task.CompletedAt is null || task.CompletedAt >= start && task.CompletedAt < end) &&
            (task.StartsAt >= start && task.StartsAt < end || task.DueAt >= start && task.DueAt < end ||
                task.StartsAt < start || task.CompletedAt >= start && task.CompletedAt < end);
    }

    public static IReadOnlyList<OperationalWorkPlan> Group(IEnumerable<OperationalAssignmentDetails> tasks,
        DateOnly? day = null, DateTimeOffset? now = null) =>
        tasks.GroupBy(t => t.PlanId)
            .Select(group => {
                var plan = group.First();
                return new OperationalWorkPlan(plan.PlanId, plan.PlanName ?? "Plan asignado", plan.PlanPeriodType ?? "SEMANAL",
                    plan.PlanStartsOn, plan.PlanEndsOn,
                    group.OrderBy(t => day is not null && IsForDay(t, day.Value) ? 0 : 1)
                        .ThenBy(t => now is not null && day is not null && IsForDay(t, day.Value)
                            ? Priority(t, day.Value, now.Value) : StatusOrder(t))
                        .ThenBy(t => t.DueAt).ThenBy(t => t.Id).ToArray());
            })
            .OrderBy(p => day is not null && p.Tasks.Any(t => IsForDay(t, day.Value)) ? 0 : 1)
            .ThenBy(p => now is not null && day is not null
                ? p.Tasks.Where(t => IsForDay(t, day.Value)).Select(t => Priority(t, day.Value, now.Value)).DefaultIfEmpty(5).Min()
                : 0)
            .ThenBy(p => PeriodOrder(p.PeriodType)).ThenBy(p => p.StartsOn).ThenBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Id).ToArray();

    private static int PeriodOrder(string period) => period switch {
        "SEMANAL" => 0, "MENSUAL" => 1, "TRIMESTRAL" => 2, "SEMESTRAL" => 3, "ANUAL" => 4, _ => 5
    };

    private static int StatusOrder(OperationalAssignmentDetails task) => task.ReviewStatus switch {
        "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA" => 2,
        _ => task.CompletedAt is null ? 0 : 1
    };
}
