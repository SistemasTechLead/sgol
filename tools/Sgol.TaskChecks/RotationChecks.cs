using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class RotationChecks
{
    internal static async Task RunAsync(SgolDbContext db, OperationalTaskService tasks, EfDirectionPreparedPlanService direction,
        OperationalSupervisionService supervision, Guid director, CheckClock clock, Guid a, Guid b)
    {
        await tasks.SaveAsync(director, new() { Name = "Rotación semanal", Requirement = "NINGUNO", PeriodType = "SEMANAL",
            Repetitions = 7, DifferentPeople = true, LoadMinutes = 10, DeadlineHours = 24, Revision = 0 }, default);
        db.ChangeTracker.Clear();
        var task = (await tasks.ListAsync(director, default)).Single(t => t.Name == "Rotación semanal");
        DirectionPlanInput Input(int week) => new($"Rotación {week}", "Tienda", "SEMANAL", 2026, week, [task.TaskCode]);
        var result = await direction.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(), Input(44));
        var assigned = result.Plan.Assignments!;
        Check(assigned.Count == 7, "All seven repetitions are assigned to two workers");
        var counts = assigned.GroupBy(t => t.PersonId).Select(g => g.Count()).Order().ToArray();
        Check(counts.SequenceEqual([3, 4]), "Repeated assignments stay balanced at three and four");
        Check(assigned.OrderBy(t => t.StartsAt).Take(2).Select(t => t.PersonId).Distinct().Count() == 2,
            "Distinct workers are used before anyone repeats");
        var first = assigned.OrderBy(t => t.StartsAt).First();
        clock.UtcNow = first.StartsAt;
        await supervision.ReviewAsync(director, first.Id, first.Revision, "MISMA_PERSONA", "Repetir", default);
        db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.AnyAsync(t => t.ReassignedFromId == first.Id && t.PersonId == first.PersonId),
            "Supervision can reassign to the same worker despite weekly repetitions");
        var capacity = new OperationalCapacityService(db, clock, supervision);
        var reduction = new CapacityChange(a, new(2026, 10, 26), new(2026, 11, 1), 0, 240, "Ausencia");
        var preview = await capacity.PreviewAsync(director, reduction, default);
        Check(preview.Moves.Count > 0 && preview.Moves.All(m => m.ToId == b) && preview.RemainingExcess == 0,
            "Capacity redistribution can use a coworker who already has the task");
        await capacity.ApplyAsync(director, reduction, preview.Fingerprint, default);
        db.ChangeTracker.Clear();
        db.OperationalCapacities.Add(new() { Id = Guid.CreateVersion7(), PersonId = b, From = new(2026, 11, 2),
            Through = new(2026, 11, 15), DailyMinutes = 240, Percent = 0, Reason = "No disponible" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var solo = await direction.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(), Input(45));
        Check(solo.Plan.Assignments!.Count == 7 && solo.Plan.Assignments.All(t => t.PersonId == a),
            "One available worker receives all repetitions without using the unavailable worker");
        db.OperationalCapacities.Add(new() { Id = Guid.CreateVersion7(), PersonId = a, From = new(2026, 11, 9),
            Through = new(2026, 11, 15), DailyMinutes = 240, Percent = 0, Reason = "No disponible" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var before = await db.OperationalAssignments.CountAsync();
        try { await direction.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(), Input(46)); throw new Exception("Capacity limit was bypassed"); }
        catch (DirectionPlanException e) when (e.Code == "PLAN_NO_AVAILABLE_WORKER") { }
        db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.CountAsync() == before, "No available capacity still blocks without partial assignments");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine($"PASS: {message}");
    }
}
