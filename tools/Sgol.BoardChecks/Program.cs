using Sgol.Web.Infrastructure.Persistence.Planning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.Components;

var firstPerson = Guid.Parse("10000000-0000-0000-0000-000000000001");
var namesakePerson = Guid.Parse("10000000-0000-0000-0000-000000000002");
var firstAvatar = BoardPersonAvatar.For(firstPerson, "Ana López");
Check(firstAvatar == BoardPersonAvatar.For(firstPerson, "Ana López"), "Person avatar remains stable on refresh");
Check(firstAvatar == BoardPersonAvatar.For(firstPerson, "Ana García"), "Person avatar survives a display name change");
Check(firstAvatar != BoardPersonAvatar.For(namesakePerson, "Ana López"), "Namesakes use their own person identity for the avatar");

var day = new DateOnly(2026, 10, 12);
var start = OperationalSchedule.Instant(day, 17);
var due = OperationalSchedule.Instant(day, 19);
var task = new LiveBoardTask(Guid.NewGuid(), "Cerrar cortinas", "Responsable", "PISO_VENTAS", start, due, null);
Check(task.Color(start.AddTicks(-1)) == "scheduled", "Gray before activation");
Check(task.Color(start) == "active", "Green at activation");
foreach (var duration in new[] { TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromDays(3) })
{
    var sample = task with { StartsAt = due - duration };
    var yellowAt = due.AddTicks(-duration.Ticks * 3 / 10);
    var redAt = due.AddTicks(-duration.Ticks / 10);
    Check(sample.Color(sample.StartsAt.AddTicks(-1)) == "scheduled", "Gray before activation regardless of duration");
    Check(sample.Color(sample.StartsAt) == "active", "Green at activation regardless of duration");
    Check(sample.Color(yellowAt.AddTicks(-1)) == "active", "Green above 30 percent remaining");
    Check(sample.Color(yellowAt) == "warning", "Yellow at exactly 30 percent remaining");
    Check(sample.Color(redAt.AddTicks(-1)) == "warning", "Yellow above 10 percent remaining");
    Check(sample.Color(redAt) == "urgent", "Red at exactly 10 percent remaining");
    Check(sample.Color(due) == "urgent", "Red at deadline");
    Check((sample with { CompletedAt = sample.StartsAt }).Color(sample.StartsAt.AddTicks(-1)) == "delivered",
        "Delivery overrides scheduled state");
}
Check((task with { StartsAt = due }).Color(due) == "urgent", "Zero duration is red at activation");
Check(task.Color(due.AddDays(3)) == "urgent", "Overdue tasks stay red across days");
Check((task with { CompletedAt = due }).Color(due.AddDays(1)) == "delivered", "Delivery overrides urgency");
var globalTasks = new[] { task, task with { Id = Guid.NewGuid(), CompletedAt = start } };
var stateTasks = new[] {
    task with { StartsAt = start.AddHours(1) }, task,
    task with { StartsAt = start.AddHours(-2), DueAt = start.AddMinutes(45) },
    task with { StartsAt = start.AddHours(-4), DueAt = start.AddMinutes(20) },
    task with { DueAt = start }, task with { CompletedAt = start } };
var stateBoard = new LiveBoardSnapshot(start, day, null, [], stateTasks);
foreach (var state in LiveBoardSnapshot.States)
{
    var filtered = stateBoard with { StateFilter = state };
    Check(filtered.VisibleTasks.Count() == 1 && filtered.TaskFilter == $"state:{state}", $"State filter selects only {state}");
    Check(filtered.DayTasks.Count == 6 && filtered.Delivered == 1 && filtered.Percent == stateBoard.Percent,
        $"State filter {state} preserves global progress");
}
Check((stateBoard with { StateFilter = "active", Now = due }).VisibleTasks.Count() == 0,
    "State filter follows the clock and supports empty results");
var filteredBoard = new LiveBoardSnapshot(start, day, Guid.NewGuid(), [], [task], globalTasks, task.Id);
Check(filteredBoard.Delivered == 1 && filteredBoard.Percent == 50 && filteredBoard.DayTasks.Count == 2,
    "Plan and task filters preserve global day progress");
Check((filteredBoard with { Tasks = [] }).Percent == 50, "Empty selected plan preserves global progress");
Check(filteredBoard.Version != (filteredBoard with { DailyTasks = [task, globalTasks[1] with { CompletedAt = null }] }).Version,
    "Delivery outside selected filter triggers realtime update");
Check((task with { StartsAt = due.AddHours(-1) }).Color(due.AddHours(-1)) == "active", "One-hour task starts green");
var predicate = LiveBoardReader.ForDay(day).Compile();
var assignment = new OperationalAssignment { StartsAt = start, DueAt = due };
Check(predicate(assignment), "Today's future activation included");
Check(!predicate(new() { StartsAt = start.AddDays(-3), DueAt = due.AddDays(2) }), "Active task with future deadline excluded");
Check(!predicate(new() { StartsAt = start.AddDays(-3), DueAt = due.AddDays(-2) }), "Old overdue task excluded from board");
var overnight = new OperationalAssignment { StartsAt = start.AddDays(-1), DueAt = start };
Check(!LiveBoardReader.ForDay(day.AddDays(-1)).Compile()(overnight) && predicate(overnight), "24-hour task appears only on deadline day");
var tomorrowDeadline = new OperationalAssignment { StartsAt = start, DueAt = start.AddDays(1) };
Check(!predicate(tomorrowDeadline) && LiveBoardReader.ForDay(day.AddDays(1)).Compile()(tomorrowDeadline), "Task activated today and due tomorrow waits for tomorrow");
Check(!predicate(new() { StartsAt = start.AddDays(1), DueAt = due.AddDays(1) }), "Future task excluded");
Check(predicate(new() { StartsAt = start.AddDays(-3), DueAt = due, CompletedAt = due.AddDays(-1) }), "Early delivery remains visible on deadline day");
Check(!predicate(new() { StartsAt = start.AddDays(-3), DueAt = due.AddDays(-2), CompletedAt = start }), "Late delivery does not bring an old deadline back to board");
Check(predicate(new() { StartsAt = start.AddDays(-1), DueAt = OperationalSchedule.Instant(day) }), "Deadline at local midnight included");
Check(predicate(new() { StartsAt = start, DueAt = OperationalSchedule.Instant(day.AddDays(1)).AddTicks(-1) }), "Last instant before next local midnight included");
Check(!predicate(new() { StartsAt = start, DueAt = OperationalSchedule.Instant(day.AddDays(1)) }), "Deadline at next local midnight excluded even if activated today");
Check(!predicate(new() { StartsAt = start, DueAt = due, ReviewStatus = "REASIGNADA" }), "Reassigned original excluded");
Check(!predicate(new() { StartsAt = start, DueAt = due, ReviewStatus = "NO_SATISFECHA", CompletedAt = start }), "Rejected terminal state is not a delivery");
Check(!LiveBoardReader.ForDay(day.AddDays(1)).Compile()(new() { StartsAt = start, DueAt = due, CompletedAt = start }), "Delivered card leaves at local midnight");
Check(predicate(new() { StartsAt = OperationalSchedule.Instant(day.AddDays(1)), DueAt = due.AddDays(1), CompletedAt = null }) == false, "Next midnight excluded");
foreach (var criticality in new string?[] { "CRITICA", "IMPORTANTE", "FLEXIBLE", null })
foreach (var completed in new[] { false, true })
foreach (var status in new string?[] { null, "REASIGNADA", "NO_SATISFECHA" })
{
    var old = new OperationalAssignment { StartsAt = start.AddDays(-10), DueAt = due.AddDays(-8),
        OperationalCriticality = criticality, CompletedAt = completed ? start : null, ReviewStatus = status };
    var expected = criticality == "CRITICA" && !completed && status is null;
    Check(predicate(old) == expected, $"Previous deadlines: {criticality ?? "unclassified"}, delivered={completed}, status={status ?? "pending"}");
    Check(!LiveBoardReader.ForRange(day, day.AddDays(1)).Compile()(old), "Historical critical deadlines do not change daily/weekly ranges");
}
Check(predicate(new() { DueAt = OperationalSchedule.Instant(day).AddTicks(-1), OperationalCriticality = "CRITICA" }),
    "Pending critical work crosses the Mexico City midnight boundary");
Check(!predicate(new() { DueAt = OperationalSchedule.Instant(day.AddDays(1)), OperationalCriticality = "CRITICA" }),
    "Critical work due tomorrow stays outside the board");
var overdueCritical = task with { Id = Guid.NewGuid(), PlanId = Guid.NewGuid(),
    StartsAt = start.AddDays(-10), DueAt = due.AddDays(-8), OperationalCriticality = "CRITICA" };
var backlogBoard = new LiveBoardSnapshot(start, day, null, [], [.. globalTasks, overdueCritical], globalTasks,
    AllTasks: [.. globalTasks, overdueCritical]);
Check(backlogBoard.TaskOptions.Any(t => t.Id == overdueCritical.Id) && backlogBoard.OrderedTasks.First().Id == overdueCritical.Id,
    "Outstanding critical work is selectable and sorts first by its original deadline");
Check(backlogBoard.Percent == 50 && backlogBoard.DayDeadlines.Count == 2 && backlogBoard.Collaborators.Sum(g => g.Count()) == 2,
    "Previous critical deadlines preserve daily and collaborator progress");
Check((backlogBoard with { Tasks = [overdueCritical], PlanId = overdueCritical.PlanId }).TaskOptions.Single().Id == overdueCritical.Id,
    "Plan filters include outstanding critical work from older plans");
Check((backlogBoard with { Tasks = [], AllTasks = globalTasks }).Version != (backlogBoard with { Tasks = [] }).Version,
    "Critical backlog delivery outside the selected cards triggers a refresh");
var ownTask = new OperationalAssignmentDetails(Guid.NewGuid(), "TEST", "Trabajo del día", "NINGUNO", 10,
    Guid.NewGuid(), "Responsable", start, due, null, 0, null, null,
    PlanId: Guid.NewGuid(), PlanPeriodType: "ANUAL");
foreach (var (sample, expected) in new[] {
    (ownTask, true),
    (ownTask with { StartsAt = start.AddDays(-3), DueAt = due.AddDays(2) }, true),
    (ownTask with { StartsAt = start.AddDays(-3), DueAt = due.AddDays(-2) }, true),
    (ownTask with { StartsAt = OperationalSchedule.Instant(day.AddDays(1)), DueAt = due.AddDays(1) }, false),
    (ownTask with { CompletedAt = OperationalSchedule.Instant(day).AddTicks(-1) }, false),
    (ownTask with { CompletedAt = OperationalSchedule.Instant(day) }, true),
    (ownTask with { CompletedAt = OperationalSchedule.Instant(day.AddDays(1)) }, false),
    (ownTask with { ReviewStatus = "CONCLUIDA", CompletedAt = start }, true),
    (ownTask with { ReviewStatus = "REASIGNADA" }, false),
    (ownTask with { ReviewStatus = "NO_SATISFECHA", CompletedAt = start }, false) })
    Check(OperationalWorkPlan.IsForDay(sample, day) == expected,
        "My Work retains its existing day membership");
Check(OperationalWorkPlan.IsForDay(ownTask with { StartsAt = start.AddDays(-1), DueAt = due.AddDays(-1) }, day),
    "My Work still exposes old overdue assignments");
Check(OperationalWorkPlan.IsForDay(ownTask with { DueAt = due.AddDays(1) }, day),
    "My Work still exposes activated tasks with tomorrow deadline");
var futureWork = ownTask with { Id = Guid.NewGuid(), PlanId = Guid.NewGuid(), PlanPeriodType = "SEMANAL",
    StartsAt = start.AddDays(1), DueAt = due.AddDays(1) };
var pastDelivery = ownTask with { Id = Guid.NewGuid(), CompletedAt = start.AddDays(-1) };
var workPlans = OperationalWorkPlan.Group([futureWork, pastDelivery, ownTask], day);
Check(workPlans[0].Id == ownTask.PlanId, "Today's annual plan precedes future weekly plan");
Check(workPlans.Sum(p => p.Tasks.Count) == 3 && workPlans[0].Tasks.Count == 2, "Daily priority preserves all tasks and plan grouping");
Check(OperationalWorkPlan.Group([futureWork, ownTask])[0].Id == futureWork.PlanId, "Default period ordering remains supported");
var snapshot = new LiveBoardSnapshot(start, day, null, [], [task, task with { CompletedAt = start }]);
Check(snapshot.Percent == 50 && snapshot.Delivered == 1, "Progress counts delivery before approval");
Check((snapshot with { Tasks = [task with { CompletedAt = start }] }).Percent == 100, "All deliveries yield 100 percent");
Check((snapshot with { Tasks = [] }).Percent == 0, "Empty board yields zero safely");
Check(snapshot.Pace.Color == "neutral" && snapshot.Pace.Expected == 0 && snapshot.Pace.ExpectedPercent == 0,
    "Pace stays neutral before the first deadline, even with early deliveries");
var paceTasks = Enumerable.Range(0, 20).Select(i => task with {
    Id = Guid.NewGuid(), DueAt = i < 10 ? due : due.AddHours(2), CompletedAt = i < 9 ? start : null }).ToArray();
var paceBoard = new LiveBoardSnapshot(due, day, null, [], paceTasks);
Check(paceBoard.Pace is { Expected: 10, Delivered: 9, ExpectedPercent: 50, Color: "green" } && paceBoard.Percent == 45,
    "At the exact deadline, nine of ten due tasks is green with a 50 percent reference and 45 percent fill");
Check((paceBoard with { Now = due.AddTicks(-1) }).Pace.Expected == 0, "The reference does not move before a deadline");
foreach (var (covered, expectedColor) in new[] { (10, "green"), (9, "green"), (8, "yellow"), (7, "yellow"), (6, "red"), (0, "red") })
{
    var sample = paceTasks.Select((t, i) => t with { CompletedAt = i < covered ? start : null }).ToArray();
    Check(LiveBoardPace.For(sample, due).Color == expectedColor, $"Pace threshold: {covered} of ten is {expectedColor}");
}
var earlyOnly = paceTasks.Select((t, i) => t with { CompletedAt = i >= 10 ? start : null }).ToArray();
var earlyBoard = paceBoard with { Tasks = earlyOnly };
Check(earlyBoard.Percent == 50 && earlyBoard.Pace is { Delivered: 10, Expected: 10, Color: "green" },
    "Early deliveries count consistently in fill, pace color and detail");
var reportedTasks = Enumerable.Range(0, 35).Select(i => task with {
    Id = Guid.NewGuid(), DueAt = i < 8 ? due : due.AddHours(2), CompletedAt = i < 6 || i >= 8 && i < 13 ? start : null }).ToArray();
var reportedBoard = paceBoard with { Tasks = reportedTasks };
Check(reportedBoard.Percent == 31 && reportedBoard.Pace is { Delivered: 11, Expected: 8, Color: "green" } &&
    reportedBoard.Pace.ExpectedPercent == 800m / 35,
    "Regression: 31 percent fill past eight of 35 expected tasks is green and shows eleven of eight");
var sixDelivered = reportedTasks.Select((t, i) => t with { CompletedAt = i < 6 ? start : null }).ToArray();
var sixBoard = reportedBoard with { Tasks = sixDelivered };
Check(sixBoard.Percent == 17 && sixBoard.Pace is { Delivered: 6, Expected: 8, Color: "yellow" },
    "Six deliveries versus eight expected places the fill before the reference and is yellow");
Check((paceBoard with { Tasks = [paceTasks[0]], DailyTasks = paceTasks, TaskId = paceTasks[0].Id }).Pace == paceBoard.Pace,
    "Task and plan filters preserve the global pace and reference");
Check((paceBoard with { Now = due.AddHours(2) }).Pace.ExpectedPercent == 100, "The final deadline puts the reference at 100 percent");
Check(LiveBoardPace.For([], due) is { Color: "neutral", ExpectedPercent: 0 }, "Empty-day pace has no division by zero");
Check(LiveBoardPace.For([task], due).Color == "red" && LiveBoardPace.For([task with { CompletedAt = due.AddMinutes(1) }], due.AddMinutes(1)).Color == "green",
    "A late delivery restores current coverage without changing its recorded deadline");
Check(snapshot.DayName == "Lunes" && snapshot.Time(start).EndsWith("17:00"), "Mexico City date and time");
Check(snapshot.Version == (snapshot with { Now = start.AddSeconds(2) }).Version, "Clock ticks do not trigger data broadcasts");
var monday = WeeklyBoardSnapshot.Monday(day);
var weekSource = new[] {
    new LiveBoardDeadline(OperationalSchedule.Instant(monday).AddTicks(-1), start),
    new LiveBoardDeadline(OperationalSchedule.Instant(monday), start),
    new LiveBoardDeadline(due, start),
    new LiveBoardDeadline(due.AddHours(1), null),
    new LiveBoardDeadline(OperationalSchedule.Instant(monday.AddDays(6), 20), start),
    new LiveBoardDeadline(OperationalSchedule.Instant(monday.AddDays(7)), null) };
var weekStartInstant = OperationalSchedule.Instant(monday);
var weekEndInstant = OperationalSchedule.Instant(monday.AddDays(7));
var weekDeadlines = weekSource.Where(d => d.DueAt >= weekStartInstant && d.DueAt < weekEndInstant).ToArray();
var weekProgress = new BoardProgressViewModel("week", "Semana", monday, weekDeadlines, due);
Check(weekProgress is { Delivered: 3, Percent: 75 } && weekProgress.Pace is { Total: 4, Expected: 2, Delivered: 3, ExpectedPercent: 50, Color: "green" },
    "Weekly progress uses Monday-Sunday deadlines, includes past days and early weekly deliveries, and excludes adjacent weeks");
var weekBoard = snapshot with { WeeklyDeadlines = weekDeadlines };
Check((weekBoard with { Tasks = [], TaskId = Guid.NewGuid() }).WeeklyDeadlines == weekBoard.WeeklyDeadlines,
    "Filtering daily cards does not narrow weekly progress");
Check(weekBoard.Version != (weekBoard with { WeeklyDeadlines = weekDeadlines.Select(d => d with { CompletedAt = start }).ToArray() }).Version,
    "A weekly delivery outside today's cards triggers the board broadcast");
Check(weekBoard.Version == (weekBoard with { Now = start.AddSeconds(2) }).Version,
    "Weekly clock ticks move the local reference without data broadcasts");
Check(new BoardProgressViewModel("week", "Semana", monday, [], due) is { Delivered: 0, Percent: 0, Pace.Color: "neutral" },
    "An empty week renders a neutral zero progress safely");
var collaboratorTasks = new[] {
    task with { PersonId = firstPerson, PersonName = "Ana López", DueAt = due, CompletedAt = start },
    task with { Id = Guid.NewGuid(), PersonId = firstPerson, PersonName = "Ana López", DueAt = due.AddHours(1), CompletedAt = null },
    task with { Id = Guid.NewGuid(), PersonId = namesakePerson, PersonName = "Ana López", DueAt = due, CompletedAt = null },
    task with { Id = Guid.NewGuid(), PersonId = Guid.NewGuid(), PersonName = "Dirección", Role = "DIRECCION", DueAt = due, CompletedAt = null } };
var collaboratorBoard = snapshot with { Now = due, Tasks = collaboratorTasks };
var collaborators = collaboratorBoard.Collaborators.ToArray();
Check(collaborators.Length == 2 && collaborators.All(g => g.All(t => t.Role != "DIRECCION")) && collaboratorBoard.DayTasks.Count == 4,
    "Individual bars exclude Direction while keeping the global daily total unchanged");
Check(collaborators.Select(g => g.Key).Distinct().Count() == 2 && collaborators.SelectMany(g => g).Count() == 3,
    "Namesakes remain separate people and each individual assignment is counted once");
var individualTasks = collaborators.Single(g => g.Key == firstPerson).ToArray();
var individualProgress = new BoardProgressViewModel($"person-{firstPerson:N}", "Ana López", day,
    individualTasks.Select(t => new LiveBoardDeadline(t.DueAt, t.CompletedAt)).ToArray(), due, firstPerson);
Check(individualProgress is { Percent: 50, Delivered: 1, Pace.Total: 2, Pace.Expected: 1, Pace.Color: "green" },
    "Individual fill, reference and color use only that person's daily assignments");
Check((collaboratorBoard with { Tasks = [collaboratorTasks[0]], DailyTasks = collaboratorTasks }).Collaborators.Sum(g => g.Count()) == 3,
    "Filtering cards keeps every colleague's complete daily indicator");
Check(!(collaboratorBoard with { Tasks = [] }).Collaborators.Any(), "An empty day has no fabricated individual assignments");
Check(snapshot.Version != (snapshot with { Tasks = [task with { GoalUnits = 20, ProgressUnits = 5 }] }).Version, "Unit progress triggers realtime broadcast");
Check(snapshot.Version != (snapshot with { Tasks = [task with { CompletedAt = start }] }).Version, "Delivery triggers realtime broadcast");
var sorting = new LiveBoardSnapshot(start, day, null, [], [
    task with { StartsAt = start.AddHours(1) },
    task with { CompletedAt = start },
    task with { StartsAt = start.AddHours(-2), DueAt = start.AddMinutes(45) },
    task,
    task with { StartsAt = start.AddHours(-4), DueAt = start.AddMinutes(20) }]);
Check(sorting.OrderedTasks.Select(t => t.Color(start)).SequenceEqual(["urgent", "active", "warning", "scheduled", "delivered"]),
    "Board sorts red, green, yellow, gray, then delivered");
var ownSorting = sorting.Tasks.Select(t => ownTask with { Id = Guid.NewGuid(), StartsAt = t.StartsAt,
    DueAt = t.StartsAt > start ? t.DueAt.AddHours(1) : t.DueAt, CompletedAt = t.CompletedAt }).Append(futureWork).ToArray();
var ownOrdered = OperationalWorkPlan.Group(ownSorting, day, start).Single(p => p.Id == ownTask.PlanId).Tasks;
Check(ownOrdered.Select(t => LiveBoardTask.FromAssignment(t).Color(start))
    .SequenceEqual(["urgent", "active", "warning", "scheduled", "delivered"]),
    "My Work sorts by color with deliveries last");
Check(OperationalWorkPlan.Group(ownSorting, day, start)[0].Id == ownTask.PlanId,
    "Day work stays above future plans");
var occasional = ownTask with { Id = Guid.NewGuid(), PlanId = Guid.NewGuid(), PlanName = "Publicación ocasional",
    PlanPeriodType = "OCASIONAL", OccasionId = Guid.NewGuid(), StartsAt = start.AddMinutes(-1), DueAt = start.AddHours(3) };
var earlierGreen = ownTask with { Id = Guid.NewGuid(), DueAt = start.AddHours(2) };
var lateGreen = ownTask with { Id = Guid.NewGuid(), DueAt = start.AddHours(8) };
var grayToday = ownTask with { Id = Guid.NewGuid(), StartsAt = start.AddHours(1), DueAt = due.AddHours(4) };
var blueToday = ownTask with { Id = Guid.NewGuid(), CompletedAt = start };
var urgentToday = ownTask with { Id = Guid.NewGuid(), DueAt = start.AddMinutes(15) };
var dailySource = new[] { grayToday, blueToday, lateGreen, occasional, futureWork, pastDelivery, earlierGreen, urgentToday };
var todayCombined = OperationalWorkPlan.TodayTasks(dailySource, day, start);
Check(todayCombined.Select(t => t.Id).SequenceEqual([urgentToday.Id, earlierGreen.Id, occasional.Id, lateGreen.Id, grayToday.Id, blueToday.Id]),
    "Daily list sorts by color across occasional and recurring plans");
var oldOverdue = ownTask with { Id = Guid.NewGuid(), StartsAt = start.AddDays(-3), DueAt = start.AddDays(-2) };
var globalSource = dailySource.Append(oldOverdue).ToArray();
var globalOrdered = OperationalWorkPlan.OrderedTasks(globalSource, day, start);
Check(globalOrdered.First().Id == oldOverdue.Id, "Older overdue work comes before today's red tasks");
Check(globalOrdered.Select(t => LiveBoardTask.FromAssignment(t).Priority(start)).SequenceEqual(
    globalOrdered.Select(t => LiveBoardTask.FromAssignment(t).Priority(start)).Order()),
    "All dates and plans share red, green, yellow, gray, blue ordering");
Check(globalOrdered.Count == globalSource.Length && globalOrdered.Select(t => t.Id).Distinct().Count() == globalSource.Length,
    "Global ordering retains every assignment exactly once, including future work and old deliveries");
var midnight = OperationalSchedule.Instant(day);
var nextMidnight = OperationalSchedule.Instant(day.AddDays(1));
foreach (var (sample, expected) in new[] {
    (ownTask with { DueAt = midnight }, 0),
    (ownTask with { DueAt = nextMidnight.AddTicks(-1) }, 1),
    (ownTask with { DueAt = nextMidnight }, 1),
    (ownTask with { StartsAt = start.AddDays(-2), DueAt = midnight.AddTicks(-1) }, 0),
    (ownTask with { StartsAt = start.AddHours(1), DueAt = nextMidnight }, 3),
    (futureWork, 3),
    (ownTask with { CompletedAt = start }, 4),
    (ownTask with { ReviewStatus = "CONCLUIDA", CompletedAt = start }, 4),
    (ownTask with { ReviewStatus = "NO_SATISFECHA" }, 0),
    (ownTask with { ReviewStatus = "REASIGNADA" }, 3) })
    Check(OperationalWorkPlan.Priority(sample, day, start) == expected,
        "My Work priority follows colors regardless of deadline day");
var activatingToday = ownTask with { Id = Guid.NewGuid(), StartsAt = start.AddHours(1), DueAt = nextMidnight.AddDays(1) };
Check(OperationalWorkPlan.Priority(activatingToday, day, activatingToday.StartsAt) == 1,
    "Scheduled work becomes active exactly at activation");
Check(todayCombined.All(t => t.Id != futureWork.Id && t.Id != pastDelivery.Id),
    "Future work and previous deliveries remain outside the daily list");
var nonDaily = OperationalWorkPlan.Group(dailySource, day, start).SelectMany(p => p.Tasks.Where(t => !OperationalWorkPlan.IsForDay(t, day)));
Check(todayCombined.Concat(nonDaily).Select(t => t.Id).Distinct().Count() == dailySource.Length &&
    todayCombined.Count + nonDaily.Count() == dailySource.Length,
    "Daily section and plan history preserve every assignment exactly once");
Check(LiveBoardTask.FromAssignment(ownTask with { DueAt = start.AddMinutes(-1) }).Color(start) == "urgent",
    "Overdue My Work task is red");
Check(LiveBoardTask.FromAssignment(ownTask with { CompletedAt = start, ReviewStatus = "CONCLUIDA" }).Color(start) == "delivered",
    "Approved and unreviewed deliveries share board blue");
var weeklyTaskId = Guid.NewGuid();
var weeklyPlanId = Guid.NewGuid();
var weeklyTask = task with { TaskId = weeklyTaskId, PlanId = weeklyPlanId, PlanName = "Operación semanal", LoadMinutes = 15 };
var weekly = new WeeklyBoardSnapshot(start, day, [weeklyTask,
    weeklyTask with { Id = Guid.NewGuid(), PersonName = "Segunda persona", CompletedAt = start },
    weeklyTask with { Id = Guid.NewGuid(), DueAt = OperationalSchedule.Instant(day.AddDays(6), 23) },
    weeklyTask with { Id = Guid.NewGuid(), PlanId = Guid.NewGuid(), PlanName = "Otro plan" },
    weeklyTask with { Id = Guid.NewGuid(), LoadMinutes = 30 }]);
Check(Enumerable.Range(0, 7).All(i => WeeklyBoardSnapshot.Monday(day.AddDays(i)) == day), "All weekdays resolve to Monday");
Check(WeeklyBoardSnapshot.Monday(new DateOnly(2027, 1, 3)) == new DateOnly(2026, 12, 28), "Weeks cross year boundaries");
Check(weekly.Days.Count() == 7 && weekly.EndsOn == day.AddDays(6), "Weekly columns run Monday through Sunday");
Check(weekly.Rows.Count() == 3, "Weekly rows preserve different plans and historical loads");
var weeklyRow = weekly.Rows.Single(r => r.PlanName == "Operación semanal" && r.LoadMinutes == 15);
Check(weeklyRow.ForDay(day).Count() == 2 && weeklyRow.ForDay(day.AddDays(6)).Count() == 1 && !weeklyRow.ForDay(day.AddDays(1)).Any(),
    "Multiple people stay in their deadline day and empty days remain empty");
Check(weeklyRow.ForDay(day).Select(t => t.Color(start)).Distinct().Count() == 2, "Each person retains their own board status");
Check(weekly.ClosingLoad(day) == 75 && weekly.ClosingLoad(day.AddDays(6)) == 15,
    "Closing day totals sum every assignment including deliveries and different plans");
var activationPerson = Guid.NewGuid();
var activationWeek = weekly with { ActivationTasks = [weeklyTask with { PersonId = activationPerson,
    StartsAt = OperationalSchedule.Instant(day.AddDays(4), 9), DueAt = OperationalSchedule.Instant(day.AddDays(5), 21), LoadMinutes = 120 },
    weeklyTask with { PersonId = Guid.NewGuid(), StartsAt = OperationalSchedule.Instant(day.AddDays(4), 9), LoadMinutes = 20, CompletedAt = start },
    weeklyTask with { StartsAt = OperationalSchedule.Instant(day.AddDays(-1), 9), LoadMinutes = 500 }] };
Check(activationWeek.ActivationLoad(day.AddDays(4), activationPerson) == 120 && activationWeek.ActivationLoad(day.AddDays(4)) == 140 &&
    activationWeek.ActivationLoad(day.AddDays(5)) == 0, "Friday activation includes a Saturday deadline and completed work");
Check(activationWeek.Workers.Count() == 2 && activationWeek.ActivationLoad(day) == 0,
    "Activation totals exclude work started outside the week and preserve distinct people");
var weekFilter = LiveBoardReader.ForRange(day, day.AddDays(7)).Compile();
foreach (var (instant, included) in new[] { (OperationalSchedule.Instant(day).AddTicks(-1), false),
    (OperationalSchedule.Instant(day), true), (OperationalSchedule.Instant(day.AddDays(7)).AddTicks(-1), true),
    (OperationalSchedule.Instant(day.AddDays(7)), false) })
    Check(weekFilter(new OperationalAssignment { DueAt = instant }) == included, "Weekly membership respects Mexico City midnight boundaries");
Check(!weekFilter(new OperationalAssignment { DueAt = due, ReviewStatus = "REASIGNADA" }) &&
    !weekFilter(new OperationalAssignment { DueAt = due, ReviewStatus = "NO_SATISFECHA" }), "Weekly view excludes superseded assignments");
if (args.Contains("--preview"))
{
    // Isolated visual fixture: no database, accounts, or production authentication.
    var webRoot = Directory.Exists("src/Sgol.Web") ? Path.GetFullPath("src/Sgol.Web") :
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Sgol.Web"));
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
        ApplicationName = typeof(LiveBoardReader).Assembly.GetName().Name,
        ContentRootPath = webRoot, WebRootPath = Path.Combine(webRoot, "wwwroot") });
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
    builder.Services.AddControllersWithViews();
    builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader, PreviewAvatarReader>();
    builder.Services.Configure<RazorViewEngineOptions>(options => options.ViewLocationFormats.Add("/Pages/Shared/{0}.cshtml"));
    var preview = builder.Build();
    preview.UseStaticFiles();
    var firstPlan = Guid.Parse("00000000-0000-0000-0000-000000000001");
    var secondPlan = Guid.Parse("00000000-0000-0000-0000-000000000002");
    preview.MapGet("/tablero/rol-de-tareas", async (HttpContext context, IRazorViewEngine views, ITempDataProvider tempData) => {
        var model = weekly with { Tasks = weekly.Tasks.Concat(Enumerable.Range(1, 22).Select(i => weeklyTask with {
            Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Name = $"Actividad semanal {i}", DueAt = due.AddDays(i % 7),
            PersonName = i % 3 == 0 ? "María Fernanda Hernández López" : "Ana López", CompletedAt = i % 4 == 0 ? start : null })).ToArray() };
        var weekFixtureArg = Array.IndexOf(args, "--week-fixture");
        if (weekFixtureArg >= 0)
            model = System.Text.Json.JsonSerializer.Deserialize<WeeklyBoardSnapshot>(await File.ReadAllTextAsync(args[weekFixtureArg + 1]))!;
        var action = new ActionContext(context, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var result = views.GetView(null, "/Pages/Board/_WeeklyContent.cshtml", false);
        if (!result.Success) throw new Exception(string.Join(",", result.SearchedLocations));
        using var output = new StringWriter();
        await result.View.RenderAsync(new ViewContext(action, result.View,
            new ViewDataDictionary<WeeklyBoardSnapshot>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model },
            new TempDataDictionary(context, tempData), output, new HtmlHelperOptions()));
        var html = output.ToString();
        if (context.Request.Query["handler"] != "Refresh") html = "<!DOCTYPE html><html lang='es-MX' data-theme='" +
            (context.Request.Query["theme"] == "dark" ? "dark" : "light") + "'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='/css/tokens.css'><link rel='stylesheet' href='/css/components.css'><link rel='stylesheet' href='/css/live-board.css'></head><body class='board-page'><main><div id='weekly-board' class='weekly-board'>" +
            html + "</div><p id='weekly-connection' class='board-connection' hidden></p><script src='/js/weekly-board.js'></script></main></body></html>";
        return Results.Content(html, "text/html");
    });
    preview.MapGet("/tablero", async (HttpContext context, IRazorViewEngine views, ITempDataProvider tempData) => {
        var at = DateTimeOffset.UtcNow;
        Guid? selectedPlan = Guid.TryParse(context.Request.Query["plan"], out var parsed) ? parsed : null;
        var samples = new[] {
            new LiveBoardTask(Guid.NewGuid(), "Preparar exhibición", "Ana López", "PISO_VENTAS", at.AddHours(1), at.AddHours(3), null),
            new LiveBoardTask(Guid.NewGuid(), "Revisar inventario", "Luis García", "PISO_VENTAS", at.AddHours(-1), at.AddHours(2), null),
            new LiveBoardTask(Guid.NewGuid(), "Ordenar almacén", "María Pérez", "PISO_VENTAS", at.AddHours(-2), at.AddMinutes(50), null),
            new LiveBoardTask(Guid.NewGuid(), "Cerrar cortinas", "José Ruiz", "PISO_VENTAS", at.AddHours(-4), at.AddMinutes(20), null),
            new LiveBoardTask(Guid.NewGuid(), "Limpiar mostrador", "Ana López", "PISO_VENTAS", at.AddHours(-2), at.AddHours(-1), at),
            new LiveBoardTask(Guid.NewGuid(), "Revisar corte de caja", "Elena Díaz", "ADMINISTRACION", at.AddDays(-1), at.AddHours(-1), null) };
        var model = new LiveBoardSnapshot(at, WeekContract.LocalToday(at), selectedPlan,
            [new(firstPlan, "Operación semanal · Sucursal"), new(secondPlan, "Inventario mensual")], selectedPlan == secondPlan ? [] : samples, samples);
        var fixtureArg = Array.IndexOf(args, "--fixture");
        if (fixtureArg >= 0)
        {
            var fixture = System.Text.Json.JsonSerializer.Deserialize<LiveBoardSnapshot>(await File.ReadAllTextAsync(args[fixtureArg + 1]))!;
            selectedPlan = fixture.Plans.Any(p => p.Id == selectedPlan) ? selectedPlan : null;
            var scoped = fixture.DayTasks.Where(t => selectedPlan == null || t.PlanId == selectedPlan).ToArray();
            Guid? selectedTask = Guid.TryParse(context.Request.Query["task"], out var parsedTask) && scoped.Any(t => t.Id == parsedTask) ? parsedTask : null;
            model = fixture with { PlanId = selectedPlan, TaskId = selectedTask,
                Tasks = scoped.Where(t => selectedTask == null || t.Id == selectedTask).ToArray(), DailyTasks = fixture.DayTasks };
        }
        var stateFilter = context.Request.Query["task"].ToString();
        if (stateFilter.StartsWith("state:", StringComparison.Ordinal) && LiveBoardSnapshot.States.Contains(stateFilter[6..]))
            model = model with { StateFilter = stateFilter[6..] };
        var action = new ActionContext(context, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var result = views.GetView(null, "/Pages/Board/_BoardContent.cshtml", false);
        if (!result.Success) throw new Exception(string.Join(",", result.SearchedLocations));
        using var output = new StringWriter();
        await result.View.RenderAsync(new ViewContext(action, result.View,
            new ViewDataDictionary<LiveBoardSnapshot>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model },
            new TempDataDictionary(context, tempData), output, new HtmlHelperOptions()));
        var html = output.ToString();
        var previewTheme = context.Request.Query["theme"] == "dark" ? "dark" : "light";
        if (context.Request.Query["handler"] != "Refresh") html = "<!DOCTYPE html><html lang='es-MX' data-theme='" + previewTheme + "'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Tablero · Prueba visual</title><link rel='stylesheet' href='/css/tokens.css'><link rel='stylesheet' href='/css/components.css'><link rel='stylesheet' href='/css/live-board.css'></head><body class='board-page'><main><div id='live-board'>" + html + "</div><p id='board-connection' class='board-connection' role='status' hidden></p><script src='/js/live-board.js' defer></script></main></body></html>";
        return Results.Content(html, "text/html");
    });
    await preview.RunAsync(args.Contains("--https") ? "https://localhost:7252" : "http://127.0.0.1:7252");
}
static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }

sealed class PreviewAvatarReader : Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader
{
    public Task<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatar?> FindAsync(Guid id, CancellationToken token = default)
        => Task.FromResult<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatar?>(null);
    public Task<string?> UserNameAsync(Guid id, CancellationToken token = default) => Task.FromResult<string?>(null);
}
