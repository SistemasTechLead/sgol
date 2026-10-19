using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class DistributionChecks
{
    public static async Task RunAsync(SgolDbContext db, Guid actor, Guid a, Guid b)
    {
        var monday = new DateOnly(2026, 10, 26);
        var now = OperationalSchedule.Instant(monday.AddDays(-1), 12);
        var c = Guid.NewGuid();
        CapacityWorker[] workers = [new(a, "A", "A", "PISO_VENTAS", null, null),
            new(b, "B", "B", "PISO_VENTAS", null, null), new(c, "C", "C", "PISO_VENTAS", null, null)];
        var plan = new DirectionPreparedPlan(Guid.CreateVersion7(), BranchScope.LorettaId, actor, now,
            new("Reparto", "Tienda", "SEMANAL", 2026, 44, []));
        OperationalAssignment Load(Guid person, DateOnly day, int minutes) => new() {
            Id = Guid.NewGuid(), PersonId = person, StartsAt = OperationalSchedule.Instant(day, 9),
            DueAt = OperationalSchedule.Instant(day, 17), LoadMinutes = minutes };
        CapacityLedger Ledger(OperationalAssignment[] load, HashSet<(Guid, DateOnly)>? absence = null) =>
            new(workers, load, [], absence ?? [], [], monday);
        async Task<OperationalTask> Task(string code, int minutes, int repeats = 1, string mode = "DISTRIBUIDA", int days = 0)
        {
            var task = new OperationalTask { Id = Guid.NewGuid(), TaskCode = code, Name = code, LoadMinutes = minutes,
                Repetitions = repeats, ScheduleMode = mode, WeekDays = days, StartHour = 9, DeadlineHours = 8,
                DifferentPeople = true };
            db.OperationalTasks.Add(task); await db.SaveChangesAsync(); return task;
        }
        async Task<OperationalAssignment[]> Generate(OperationalTask[] tasks, CapacityLedger ledger) =>
            await OperationalPlanGenerator.GenerateAsync(db, plan, tasks.Select(t => t.TaskCode).ToArray(), now, default, preparedLedger: ledger);
        var fixedTask = await Task("DIST-FIX", 15, mode: "DIAS_FIJOS", days: 1 << 5);
        // A has more week load, but is free Friday; B and C already have work Friday.
        var daily = await Generate([fixedTask], Ledger([Load(a, monday, 120), Load(b, monday.AddDays(4), 45), Load(c, monday.AddDays(4), 60)]));
        Check(daily.Single().PersonId == a, "A free Friday receives work despite a heavier weekly total");
        var distributed = await Task("DIST-SPREAD", 20, 3);
        var baseLoad = Enumerable.Range(0, 7).Where(i => i != 1 && i != 3 && i != 5)
            .SelectMany(i => workers.Select(w => Load(w.Id, monday.AddDays(i), 50))).ToArray();
        var spread = await Generate([distributed], Ledger(baseLoad));
        Check(spread.Select(t => WeekContract.LocalToday(t.StartsAt)).Order().SequenceEqual(new[] { monday.AddDays(1), monday.AddDays(3), monday.AddDays(5) }),
            "Three weekly repetitions choose the three least loaded days instead of fixed offsets");
        Check(spread.Select(t => t.PersonId).Distinct().Count() == 3, "Equal daily loads rotate through all workers");
        var emptySunday = Enumerable.Range(0, 6).SelectMany(i => workers.Select(w => Load(w.Id, monday.AddDays(i), 50))).ToArray();
        var sundayExcluded = await Generate([distributed], Ledger(emptySunday));
        Check(sundayExcluded.Length == 3 && sundayExcluded.All(t => WeekContract.LocalToday(t.StartsAt).DayOfWeek != DayOfWeek.Sunday),
            "Distributed weekly repetitions exclude Sunday even when it has the lowest load");
        var six = await Task("DIST-SIX", 5, 6);
        var weekdays = await Generate([six], Ledger(emptySunday));
        Check(weekdays.Length == 6 && weekdays.Select(t => WeekContract.LocalToday(t.StartsAt)).Distinct().Count() == 6 &&
            weekdays.All(t => WeekContract.LocalToday(t.StartsAt).DayOfWeek != DayOfWeek.Sunday),
            "Six repetitions fill Monday through Saturday without losing frequency");
        var fixedSunday = await Task("DIST-SUNDAY", 5, mode: "DIAS_FIJOS", days: 1);
        var explicitSunday = await Generate([fixedSunday], Ledger([]));
        Check(explicitSunday.Length == 1 && WeekContract.LocalToday(explicitSunday[0].StartsAt).DayOfWeek == DayOfWeek.Sunday,
            "An explicitly selected fixed Sunday is preserved");
        var seven = await Task("DIST-DAILY", 10, 7);
        var everyDay = await Generate([seven], Ledger([]));
        Check(everyDay.Length == 7 && everyDay.Select(t => t.StartsAt).Distinct().Count() == 7, "Seven repetitions retain one task per day");
        Check(everyDay.Count(t => WeekContract.LocalToday(t.StartsAt).DayOfWeek == DayOfWeek.Sunday) == 1,
            "Exactly seven weekly repetitions explicitly retain Sunday");
        Check(everyDay.GroupBy(t => t.PersonId).Max(g => g.Count()) - everyDay.GroupBy(t => t.PersonId).Min(g => g.Count()) <= 1,
            "Equal workers receive balanced weekly rotation");
        var sixteen = await Task("DIST-MANY", 5, 16);
        var many = await Generate([sixteen], Ledger([]));
        Check(many.Length == 16 && many.GroupBy(t => t.StartsAt).All(g => g.Count() is >= 2 and <= 3) &&
            many.Select(t => t.ScheduleKey).Distinct().Count() == 16 &&
            many.All(t => WeekContract.LocalToday(t.StartsAt).DayOfWeek != DayOfWeek.Sunday),
            "More than seven repetitions preserve frequency and unique slots without implicitly enabling Sunday");
        var thirteen = await Task("DIST-THIRTEEN", 5, 13);
        var sixDaySlots = await Generate([thirteen], Ledger([]));
        Check(sixDaySlots.Length == 13 && sixDaySlots.All(t => WeekContract.LocalToday(t.StartsAt).DayOfWeek != DayOfWeek.Sunday),
            "Thirteen repetitions fit across six days rather than retaining a seven-day slot limit");
        var constrained = await Task("DIST-BASE", 100, mode: "DIAS_FIJOS", days: 1 << 1);
        var mixed = await Generate([distributed, constrained], Ledger([]));
        Check(mixed.Where(t => t.TaskId == constrained.Id).All(t => WeekContract.LocalToday(t.StartsAt) == monday) &&
            mixed.Where(t => t.TaskId == distributed.Id).All(t => WeekContract.LocalToday(t.StartsAt) != monday),
            "Fixed dates are reserved before distributed repetitions");
        var absences = workers.Select(w => (w.Id, monday.AddDays(1))).ToHashSet();
        var available = await Generate([distributed], Ledger(baseLoad, absences));
        Check(available.All(t => WeekContract.LocalToday(t.StartsAt) != monday.AddDays(1)), "Lowest load never overrides absence");
        var temporal = new TemporalEligibilityContext { Enabled = true, Schedules = workers.Select(w => new PersonalScheduleVersion {
            PersonId = w.Id, Revision = 1, DaysJson = JsonSerializer.Serialize(Enumerable.Range(0, 7).ToDictionary(i => i,
                i => i == 5 ? new[] { new WorkInterval(1080, 1140) } : new[] { new WorkInterval(540, 1140) })) }).ToArray() };
        var scheduledLedger = new CapacityLedger(workers, baseLoad, [], [], [], monday) { Temporal = temporal };
        var scheduleFit = await Generate([distributed], scheduledLedger);
        Check(scheduleFit.All(t => t.StartsAt.DayOfWeek != DayOfWeek.Friday), "A low-load day with no presence at activation is excluded");
        var repeated = await Generate([distributed], Ledger(baseLoad));
        Check(spread.Select(t => (t.PersonId, t.StartsAt)).SequenceEqual(repeated.Select(t => (t.PersonId, t.StartsAt))),
            "Identical inputs produce a stable distribution");
        var noCapacity = new CapacityLedger(workers, [], workers.Select(w => new OperationalCapacity { PersonId = w.Id,
            From = monday, Through = monday.AddDays(6), Percent = 0 }).ToArray(), [], [], monday);
        try { await Generate([distributed], noCapacity); throw new Exception("Capacity bypassed"); }
        catch (DirectionPlanException e) when (e.Code == "PLAN_NO_AVAILABLE_WORKER") { Check(true, "No capacity rejects the complete generation"); }
        var persisted = await Generate([distributed], new CapacityLedger(workers.Take(2).ToArray(), baseLoad.Where(t => t.PersonId != c).ToArray(), [], [], [], monday));
        db.DirectionPreparedPlans.Add(plan); db.OperationalAssignments.AddRange(persisted); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var replay = await OperationalPlanGenerator.GenerateAsync(db, plan, [distributed.TaskCode], now, default, allowReserved: true,
            preparedLedger: Ledger(persisted));
        Check(replay.Length == 0, "Repeating generation never duplicates repetitions moved to low-load days");
        var thursday = OperationalSchedule.Instant(monday.AddDays(3), 12);
        var partial = new DirectionPreparedPlan(Guid.NewGuid(), BranchScope.LorettaId, actor, thursday,
            new("Parcial", "Tienda", "SEMANAL", 2026, 44, [], IsWeeklyOperationsPlan: true));
        var partialTask = await Task("DIST-PARTIAL", 10, 3);
        var tail = await OperationalPlanGenerator.GenerateAsync(db, partial, [partialTask.TaskCode], thursday, default,
            preparedLedger: new CapacityLedger(workers, [], [], [], [], monday.AddDays(3)));
        Check(tail.Length == 1 && tail.All(t => WeekContract.LocalToday(t.StartsAt) >= monday.AddDays(3)),
            "A partial operations week preserves its remaining repetition count and excludes earlier dates");
        Console.WriteLine("Distribution checks passed.");
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS: " + label);
    }
}
