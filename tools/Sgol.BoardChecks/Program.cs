using Sgol.Web.Infrastructure.Persistence.Planning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Sgol.Planning.Contracts;

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
Check(snapshot.DayName == "Lunes" && snapshot.Time(start).EndsWith("17:00"), "Mexico City date and time");
Check(snapshot.Version == (snapshot with { Now = start.AddSeconds(2) }).Version, "Clock ticks do not trigger data broadcasts");
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
    .SequenceEqual(["urgent", "warning", "active", "scheduled", "delivered"]),
    "My Work sorts today's deadlines chronologically with deliveries last");
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
Check(todayCombined.Select(t => t.Id).SequenceEqual([urgentToday.Id, earlierGreen.Id, occasional.Id, grayToday.Id, lateGreen.Id, blueToday.Id]),
    "Daily list prioritizes today's deadlines across occasional and recurring plans, before other active work and deliveries");
var midnight = OperationalSchedule.Instant(day);
var nextMidnight = OperationalSchedule.Instant(day.AddDays(1));
foreach (var (sample, expected) in new[] {
    (ownTask with { DueAt = midnight }, 0),
    (ownTask with { DueAt = nextMidnight.AddTicks(-1) }, 0),
    (ownTask with { DueAt = nextMidnight }, 1),
    (ownTask with { StartsAt = start.AddDays(-2), DueAt = midnight.AddTicks(-1) }, 1),
    (ownTask with { StartsAt = start.AddHours(1), DueAt = nextMidnight }, 2),
    (futureWork, 4),
    (ownTask with { CompletedAt = start }, 3),
    (ownTask with { ReviewStatus = "CONCLUIDA" }, 3),
    (ownTask with { ReviewStatus = "NO_SATISFECHA" }, 3),
    (ownTask with { ReviewStatus = "REASIGNADA" }, 3) })
    Check(OperationalWorkPlan.Priority(sample, day, start) == expected,
        "My Work priority respects Mexico City day boundaries, availability and terminal states");
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
            [new(firstPlan, "Operación semanal · Sucursal"), new(secondPlan, "Inventario mensual")], selectedPlan == firstPlan ? samples : [samples[1]]);
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
