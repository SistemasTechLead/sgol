using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class WeeklyOperationsChecks
{
    public static async Task RunAsync(SgolDbContext db, IDirectionPreparedPlanService plans,
        OperationalTaskService tasks, IClock clock, Guid director, Guid? subcoordination = null, Guid? administration = null, Guid? salesFloor = null)
    {
        void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS: " + label); }
        async Task Reject(Func<Task> action, string code)
        {
            try { await action(); throw new Exception("Expected " + code); }
            catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS: " + code); }
        }
        Check(DirectionPlanContract.OperationsWeek(new(2026, 10, 11)) == (2026, 42), "Sunday prepares next ISO week");
        Check(DirectionPlanContract.OperationsWeek(new(2026, 10, 12)) == (2026, 42), "Monday uses the prepared week");
        Check(DirectionPlanContract.OperationsWeek(new(2026, 10, 17)) == (2026, 43), "Saturday prepares the following operations week");
        Check(DirectionPlanContract.OperationsWeek(new(2024, 12, 29)) == (2025, 1), "Sunday handles ISO year rollover");
        await Reject(() => tasks.SaveAsync(director, new OperationalTask { Name = "Invalid occasion", Revision = 0,
            PeriodType = "OCASIONAL", IsWeeklyOperationsTask = true }, default), "WEEKLY_OPERATIONS_TASK_INVALID");
        var template = new OperationalTask { Name = "Weekly master check", Revision = 0, AssignedRole = CanonicalRole.Direction,
            IsWeeklyOperationsTask = true, ScheduleMode = "DIAS_FIJOS", WeekDays = 1 << 5, StartHour = 10,
            DeadlineHours = 4, LoadMinutes = 5, Requirement = "COMENTARIO" };
        await tasks.SaveAsync(director, template, default);
        var task = (await tasks.ListAsync(director, default)).Single(t => t.Name == template.Name);
        var input = new DirectionPlanInput("Any name", "Any area", "SEMANAL", 2026, 44, [task.TaskCode], IsWeeklyOperationsPlan: true);
        var key = Guid.CreateVersion7();
        var saved = await plans.CreateAsync(director, key, Guid.CreateVersion7(), input);
        Check(saved.Plan.IsWeeklyOperationsPlan && saved.Plan.Name == "Semana 44" && saved.Plan.Area == "Plan de Operaciones Semanal",
            "Master persists its identity and canonical name");
        Check(saved.Plan.StartsOn.DayOfWeek == DayOfWeek.Monday && saved.Plan.EndsOn.DayOfWeek == DayOfWeek.Sunday &&
            saved.Plan.Assignments!.Count == 1, "Master reuses calendar generation and assignment");
        var replay = await plans.CreateAsync(director, key, Guid.CreateVersion7(), input);
        Check(replay.Result == "RECOVERED" && replay.Plan.PlanId == saved.Plan.PlanId, "Master replay is idempotent");
        await Reject(() => plans.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(), input), "WEEKLY_OPERATIONS_PLAN_EXISTS");
        await Reject(() => plans.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(), input with { PeriodNumber = 45, TaskCodes = [] }),
            "WEEKLY_OPERATIONS_CATALOG_CHANGED");
        var next = await plans.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(), input with { PeriodNumber = 45 });
        Check(next.Plan.PlanId != saved.Plan.PlanId, "Next week has its own master and retains history");
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            try
            {
                var id = Guid.CreateVersion7();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO direction_prepared_plan (id, branch_id, created_by, created_at, name, area, period_type, year, period_number, starts_on, ends_on, status, is_weekly_operations_plan) SELECT {id}, branch_id, created_by, created_at, name, area, period_type, year, period_number, starts_on, ends_on, status, is_weekly_operations_plan FROM direction_prepared_plan WHERE id = {saved.Plan.PlanId}");
                throw new Exception("Database allowed a duplicate master");
            }
            catch (PostgresException e) when (e.SqlState == "23505") { Console.WriteLine("PASS: Database unique index rejects duplicate masters"); }
            await tx.RollbackAsync();
        }
        await tasks.SaveAsync(director, new OperationalTask { Name = "Ordinary weekly alongside master", Revision = 0,
            AssignedRole = CanonicalRole.Direction, LoadMinutes = 5 }, default);
        var normalTask = (await tasks.ListAsync(director, default)).Single(t => t.Name == "Ordinary weekly alongside master");
        var ordinary = input with { Name = "Ordinary", Area = "Tienda", IsWeeklyOperationsPlan = false, TaskCodes = [normalTask.TaskCode] };
        var first = await plans.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(), ordinary);
        await tasks.SaveAsync(director, new OperationalTask { Name = "Another ordinary task", Revision = 0,
            AssignedRole = CanonicalRole.Direction, LoadMinutes = 5 }, default);
        var anotherTask = (await tasks.ListAsync(director, default)).Single(t => t.Name == "Another ordinary task");
        var second = await plans.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(), ordinary with { TaskCodes = [anotherTask.TaskCode], Name = "Ordinary second" });
        Check(!first.Plan.IsWeeklyOperationsPlan && !second.Plan.IsWeeklyOperationsPlan, "Ordinary weekly plans still work beside masters");
        var supervision = new OperationalSupervisionService(db, clock, plans);
        var currentWeek = DirectionPlanContract.OperationsWeek(WeekContract.LocalToday(clock.UtcNow));
        var currentMaster = await plans.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(),
            input with { Year = currentWeek.Year, PeriodNumber = currentWeek.Week });
        await Reject(() => supervision.AddTasksAsync(director, currentMaster.Plan.PlanId, [normalTask.TaskCode], default), "WEEKLY_OPERATIONS_TASK_INVALID");
        Check((await plans.ReadAsync(director, saved.Plan.PlanId)).IsWeeklyOperationsPlan, "Master identity survives detail reads");
        if (subcoordination is { } sub)
        {
            await plans.AuthorizeWeeklyOperationsAsync(sub);
            await Reject(() => plans.AuthorizeAsync(sub), "ACCESO_DENEGADO");
            await Reject(() => tasks.ListAsync(sub, default), "ACCESO_DENEGADO");
            await Reject(() => plans.CreateAsync(sub, Guid.CreateVersion7(), Guid.CreateVersion7(), ordinary with { PeriodNumber = 46 }), "ACCESO_DENEGADO");
            var subInput = input with { PeriodNumber = 46 };
            var state = await PlanningCapacity.ReadAsync(db, subInput, clock.UtcNow, default);
            subInput = subInput with { CapacityFingerprint = state.Preview.Fingerprint,
                CapacityAdjustments = state.Preview.Workers.Select(w => new PlanCapacityAdjustment(w.PersonId, 50)).ToArray() };
            var subKey = Guid.CreateVersion7();
            var subPlan = await plans.CreateAsync(sub, subKey, Guid.CreateVersion7(), subInput);
            Check(subPlan.Plan.IsWeeklyOperationsPlan && subPlan.Plan.Assignments!.Count > 0, "Subcoordination creates and assigns the weekly operations plan with capacity adjustments");
            Check(await db.DirectionPreparedPlans.AnyAsync(p => p.Id == subPlan.Plan.PlanId && p.CreatedBy == sub), "Weekly operations retains the actual Subcoordination author");
            Check((await plans.CreateAsync(sub, subKey, Guid.CreateVersion7(), subInput)).Result == "RECOVERED", "Subcoordination save retry is idempotent");
            Check((await plans.ReadAsync(sub, subPlan.Plan.PlanId)).PlanId == subPlan.Plan.PlanId, "Subcoordination reads assigned plan details");
            var page = await plans.ListAsync(sub, 1, null);
            Check(page.Plans.Count == 1 && page.NextCursor?.Role == CanonicalRole.Subcoordination, "Subcoordination lists plans with role-bound pagination");
            await plans.ListAsync(sub, 1, page.NextCursor);
            await Reject(() => plans.PreviewDeletionAsync(sub, subPlan.Plan.PlanId), "ACCESO_DENEGADO");
            await Reject(() => plans.DeleteAsync(sub, subPlan.Plan.PlanId, Guid.CreateVersion7(), Guid.CreateVersion7(), new string('0', 64)), "ACCESO_DENEGADO");
            await Reject(() => plans.CreateAsync(sub, Guid.CreateVersion7(), Guid.CreateVersion7(), subInput with { PeriodNumber = 47, TaskCodes = [] }), "WEEKLY_OPERATIONS_CATALOG_CHANGED");
            foreach (var deniedActor in new[] { administration, salesFloor }.OfType<Guid>())
            {
                await Reject(() => plans.AuthorizeWeeklyOperationsAsync(deniedActor), "ACCESO_DENEGADO");
                await Reject(() => plans.CreateAsync(deniedActor, Guid.CreateVersion7(), Guid.CreateVersion7(), subInput with { PeriodNumber = 47 }), "ACCESO_DENEGADO");
            }
        }
        Console.WriteLine("Weekly operations checks passed.");
    }
}
