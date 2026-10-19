using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class LifecycleChecks
{
    public static async Task RunAsync(SgolDbContext db)
    {
        var clock = new CheckClock();
        var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.SalesFloor).Select(r => r.UserId).FirstAsync();
        var director = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction).Select(r => r.UserId).FirstAsync();
        var other = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.SalesFloor && r.UserId != actor).Select(r => r.UserId).FirstAsync();
        var person = await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync();
        var definition = await db.OperationalTasks.FirstAsync();
        var plan = await db.DirectionPreparedPlans.Select(p => p.Id).FirstAsync();
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var tasks = new OperationalTaskService(db, direction, clock, new Sgol.Evidence.Technical.FileTechnicalValidator(), new CheckScanner());
        var supervision = new OperationalSupervisionService(db, clock, direction);
        var id = Guid.CreateVersion7();
        db.Add(new OperationalAssignment { Id = id, PlanId = plan, TaskId = definition.Id, TaskCode = definition.TaskCode,
            PersonId = person, PersonName = "Lifecycle check", Name = "Lifecycle check", Requirement = "NINGUNO", AssignedRole = CanonicalRole.SalesFloor,
            LoadMinutes = 1, StartsAt = clock.UtcNow.AddHours(-1), DueAt = clock.UtcNow.AddDays(1), ReassignmentComment = "Initial reason" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await tasks.CompleteAsync(actor, id, null, null, default, "First conclusion"); db.ChangeTracker.Clear();
        var history = await tasks.LifecycleHistoryAsync(actor, id, default);
        Assert(history.Single().Kind == "ENTREGA" && history.Single().ActorId == actor && history.Single().ActorName is not null, "Delivery stores actor and kind and resolves its author name");
        clock.UtcNow = clock.UtcNow.AddMinutes(20);
        var revision = await db.OperationalAssignments.Where(a => a.Id == id).Select(a => a.Revision).SingleAsync();
        await supervision.ReviewAsync(director, id, revision, "CONCLUIDA", "Reviewed separately", default); db.ChangeTracker.Clear();
        history = await tasks.LifecycleHistoryAsync(actor, id, default);
        Assert(history.Count == 2 && history[1].ActorId == director && history[1].Decision == "CONCLUIDA" && history[0].At < history[1].At,
            "Delivery and review retain separate authors and timestamps");
        await tasks.CompleteAsync(actor, id, null, null, default, "Retry must not overwrite");
        Assert((await tasks.LifecycleHistoryAsync(actor, id, default))[0].Conclusion == "First conclusion", "Delivery retry preserves history");
        try { await tasks.LifecycleHistoryAsync(other, id, default); throw new Exception("History exposed to another worker"); }
        catch (Sgol.Planning.Contracts.DirectionPlanException e) when (e.Code == "ACCESO_DENEGADO") { Console.WriteLine("PASS: History is scoped to owner and supervisors"); }
        foreach (var table in new[] { "operational_submission", "operational_review" })
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try {
                var deleteSql = table == "operational_submission"
                    ? "DELETE FROM operational_submission WHERE \"AssignmentId\" = {0}"
                    : "DELETE FROM operational_review WHERE \"AssignmentId\" = {0}";
                await db.Database.ExecuteSqlRawAsync(deleteSql, id);
                await tx.CommitAsync(); throw new Exception("Required lifecycle fragment deleted");
            } catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.ForeignKeyViolation) {
                Console.WriteLine("PASS: Required " + table + " cannot disappear from a live assignment");
            }
        }
        var before = await Snapshot(db);
        var previous = db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("_SeparateOperationalLifecycle")).Last();
        await db.GetService<IMigrator>().MigrateAsync(previous); await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        var after = await Snapshot(db);
        if (before != after)
        {
            await File.WriteAllTextAsync("outputs/lifecycle/roundtrip-before.json", before);
            await File.WriteAllTextAsync("outputs/lifecycle/roundtrip-after.json", after);
        }
        Assert(before == after, "Lifecycle downgrade and upgrade preserve authors, decisions and both comments");
    }
    private static async Task<string> Snapshot(SgolDbContext db) => System.Text.Json.JsonSerializer.Serialize(
        await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).Select(a => new {
            a.Id, a.CompletedAt, a.CompletedBy, a.CompletionKind, a.Comment, a.Conclusion,
            a.ReviewStatus, a.ReviewComment, a.ReviewedAt, a.ReviewedBy, a.ReviewDecision, a.ReassignmentComment
        }).ToArrayAsync());
    private static void Assert(bool result, string label) { if (!result) throw new Exception(label); Console.WriteLine("PASS: " + label); }
}
