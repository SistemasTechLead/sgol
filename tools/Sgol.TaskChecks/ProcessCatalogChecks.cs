using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessCatalogChecks
{
    public static async Task SchemaAsync(SgolDbContext db, string before)
    {
        Directory.CreateDirectory("outputs/procesos");
        await File.WriteAllTextAsync("outputs/procesos/historical-before.json", before);
        Check(before == await SequenceSchemaChecks.Snapshot(db), "Process migration preserves every old operational field and evidence byte");
        Check(!db.Database.HasPendingModelChanges(), "EF model matches process migration snapshot");
        Check(await db.OperationalTasks.AllAsync(t => t.ProcessId == OperationalSequence.GeneralId), "Every existing task belongs to General");
        Check(await db.Set<OperationalSequence>().CountAsync(p => p.IsGeneral) == 1, "General has one stable identity");
        var previous = db.Database.GetMigrations().Single(x => x.EndsWith("_AddOperationalSequences"));
        await db.GetService<IMigrator>().MigrateAsync(previous);
        Check(before == await SequenceSchemaChecks.Snapshot(db), "Unused process extension can roll back without historical data loss");
        await db.Database.MigrateAsync();
        Check(before == await SequenceSchemaChecks.Snapshot(db), "Process extension reapplies without changing historical data");
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await RejectSql(db, "UPDATE operational_sequence SET \"Name\" = 'Other' WHERE \"IsGeneral\"", "General cannot be renamed");
            await RejectSql(db, "DELETE FROM operational_sequence WHERE \"IsGeneral\"", "General cannot be deleted");
            await RejectSql(db, "UPDATE operational_task SET \"ProcessId\" = '00000000-0000-0000-0000-000000000099'", "Membership rejects missing processes");
            await tx.RollbackAsync();
        }
        await File.WriteAllTextAsync("outputs/procesos/historical-after.json", await SequenceSchemaChecks.Snapshot(db));
        await File.WriteAllTextAsync("outputs/procesos/migracion-revisable.sql", db.GetService<IMigrator>().GenerateScript("20261013013017_RecoverHistoricalSubmissionAuthors", db.Database.GetMigrations().Last()));
    }

    public static async Task RunAsync(SgolDbContext db)
    {
        if (await db.People.AnyAsync()) throw new Exception("Process catalog checks need an empty dedicated database.");
        var clock = new Clock(); var ids = new Uuid7Generator(clock);
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, ids, new DirectionPlanningCatalogReader(db));
        var tasks = new OperationalTaskService(db, direction, clock, new FileTechnicalValidator(), new Scanner());
        var service = new OperationalProcessCatalogService(db, direction, clock, new ConfigurationBuilder().Build());
        Guid Person(string code, string role)
        {
            var person = Guid.CreateVersion7(); var actor = Guid.CreateVersion7(); var from = clock.UtcNow.AddYears(-1);
            db.People.Add(new Person { Id = person, DisplayName = code, StableCode = code, CreatedAt = from });
            db.AppUsers.Add(new AppUser { Id = actor, PersonId = person, Status = AccountStatus.Active, SecurityStamp = "process-check" });
            db.EmploymentVersions.Add(new(Guid.CreateVersion7(), person, BranchScope.LorettaId, EmploymentStatus.Active, from));
            db.RoleAssignmentVersions.Add(new() { Id = Guid.CreateVersion7(), UserId = actor, BranchId = BranchScope.LorettaId, RoleCode = role, Status = RoleAssignmentStatus.Active, ValidFrom = from });
            return actor;
        }
        var actor = Person("PROCESS-DIR", CanonicalRole.Direction); var admin = Person("PROCESS-ADM", CanonicalRole.Administration);
        var floor = Person("PROCESS-PISO", CanonicalRole.SalesFloor); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check((await service.ListAsync(admin, default)).Single().IsGeneral, "Administration reads General");
        await Reject(() => service.ListAsync(floor, default), "ACCESO_DENEGADO"); db.ChangeTracker.Clear();
        var key = Guid.CreateVersion7();
        await Reject(() => service.CreateAsync(admin, key, "Forbidden", "", default), "ACCESO_DENEGADO"); db.ChangeTracker.Clear();
        await service.CreateAsync(actor, key, "Process test", "Description", default); db.ChangeTracker.Clear();
        await service.CreateAsync(actor, key, "Process test", "Description", default); db.ChangeTracker.Clear();
        Check(await db.Set<OperationalSequence>().CountAsync(p => p.Id == key) == 1, "Creation retry preserves one draft identity");
        Check(!await db.OperationalAssignments.AnyAsync(), "Creation creates no assignments");
        await tasks.SaveAsync(actor,new OperationalTask { Name="Process PDF fixture",Requirement="DOCUMENTO",Revision=0 },default); db.ChangeTracker.Clear();
        var task = await db.OperationalTasks.SingleAsync(t=>t.Name=="Process PDF fixture"); var originalCode = task.TaskCode;
        var root = await service.ReadAsync(actor, key, default);
        await service.AddTaskAsync(actor, key, root.Process.Revision, task.Id, task.Revision, default); db.ChangeTracker.Clear();
        Check((await db.OperationalTasks.SingleAsync(t => t.Id == task.Id)).ProcessId == key, "Moving General task persists unique membership"); db.ChangeTracker.Clear();
        Check(!(await new DirectionPlanningCatalogReader(db).ReadTaskOptionsAsync()).Any(t => t.TaskCode == originalCode), "Process tasks disappear from independent planning options");
        var independent = new DirectionPlanInput("Independent rejected", "Store", "SEMANAL", 2026, 41, [originalCode]);
        await Reject(() => direction.CreateAsync(actor, Guid.CreateVersion7(), Guid.CreateVersion7(), independent), "DIRECTION_PLAN_TASK_INVALID"); db.ChangeTracker.Clear();
        root = await service.ReadAsync(actor, key, default);
        await tasks.SaveAsync(actor,new OperationalTask { Name="Process acceptance fixture",Requirement="DOCUMENTO",AssignedRole=CanonicalRole.Direction,Revision=0 },default); db.ChangeTracker.Clear();
        var finalTask=await db.OperationalTasks.SingleAsync(t=>t.Name=="Process acceptance fixture");
        await service.AddTaskAsync(actor,key,root.Process.Revision,finalTask.Id,finalTask.Revision,default); db.ChangeTracker.Clear();
        root=await service.ReadAsync(actor,key,default);
        root.Steps.Single(s=>s.Position==2).Action="AUTORIZAR";
        var draft = new ProcessDraftInput { Id = key, Revision = root.Process.Revision, Name = "Process test", Description = "Description", StartKind = "SEMESTRAL", Steps = root.Steps.ToList() };
        await service.SaveAsync(actor, draft, default); db.ChangeTracker.Clear(); root = await service.ReadAsync(actor, key, default);
        Check(!root.Draft!.StartHourConfirmed, "Unknown start hour is not treated as configured midnight");
        await Reject(() => service.PublishAsync(actor, key, root.Process.Revision, default), "PROCESS_RULES_INVALID"); db.ChangeTracker.Clear();
        draft.StartHour = 9; draft.Revision = root.Process.Revision;
        await service.SaveAsync(actor, draft, default); db.ChangeTracker.Clear(); root = await service.ReadAsync(actor, key, default);
        await service.PublishAsync(actor, key, root.Process.Revision, default); db.ChangeTracker.Clear(); root = await service.ReadAsync(actor, key, default);
        Check(root.Draft == null && root.Process.PublishedVersionId != null && !root.Process.Enabled, "Publishing freezes a version without activating it");
        Check(!await db.OperationalAssignments.AnyAsync(), "Saving and publishing still create no work");
        await Reject(() => service.SetEnabledAsync(actor, key, root.Process.Revision, true, default), "PROCESS_OPERATION_NOT_READY"); db.ChangeTracker.Clear();
        await Reject(async () => await tasks.ChangeAsync(actor, task.Id, (await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id)).Revision, true, default), "PROCESS_TASK_COMMITTED"); db.ChangeTracker.Clear();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await RejectSql(db, $"UPDATE operational_sequence_version SET \"StartHour\" = 11 WHERE \"Id\" = '{root.Process.PublishedVersionId}'", "Published calendar is immutable before any run");
            await RejectSql(db, $"UPDATE operational_sequence_step SET \"DeadlineHours\" = 2 WHERE \"VersionId\" = '{root.Process.PublishedVersionId}'", "Published rules are immutable before any run");
            await tx.RollbackAsync();
        }
        var frozen = await db.Set<OperationalSequenceStep>().AsNoTracking().SingleAsync(s => s.VersionId == root.Process.PublishedVersionId && s.TaskId==task.Id);
        var changed = await db.OperationalTasks.SingleAsync(t => t.Id == task.Id); changed.Name = "Changed task for future version";
        await tasks.SaveAsync(actor, changed, default); db.ChangeTracker.Clear();
        Check((await db.Set<OperationalSequenceStep>().AsNoTracking().SingleAsync(s => s.VersionId == frozen.VersionId && s.TaskId==task.Id)).TaskSnapshot == frozen.TaskSnapshot, "Task edits preserve the published task snapshot");
        var other = Guid.CreateVersion7(); await service.CreateAsync(actor, other, "Other process", "", default); db.ChangeTracker.Clear();
        var otherRoot = await service.ReadAsync(actor, other, default);
        await Reject(async () => await service.AddTaskAsync(actor, other, otherRoot.Process.Revision, task.Id, (await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id)).Revision, default), "PROCESS_TRANSFER_REQUIRES_PUBLICATION"); db.ChangeTracker.Clear();
        root = await service.ReadAsync(actor, key, default); draft.Revision = root.Process.Revision;
        await service.SaveAsync(actor, draft, default); db.ChangeTracker.Clear(); root = await service.ReadAsync(actor, key, default);
        Check(root.Draft != null && root.Draft.Id != root.Process.PublishedVersionId, "Editing published rules creates another draft");
        await Reject(() => service.SaveAsync(actor, draft, default), "PROCESS_CONFLICT"); db.ChangeTracker.Clear();
        var free = await db.OperationalTasks.AsNoTracking().FirstAsync(t => t.ProcessId == OperationalSequence.GeneralId);
        await service.AddTaskAsync(actor, other, otherRoot.Process.Revision, free.Id, free.Revision, default); db.ChangeTracker.Clear();
        otherRoot = await service.ReadAsync(actor, other, default);
        await service.AbandonAsync(actor, other, otherRoot.Process.Revision, default); db.ChangeTracker.Clear();
        Check((await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == free.Id)).ProcessId == OperationalSequence.GeneralId, "Abandon returns uncommitted tasks to General");
        Check(await db.Set<OperationalSequenceVersion>().AnyAsync(v => v.Id == frozen.VersionId && v.PublishedAt != null), "Abandon preserves published history");
        Check(await db.AuditEvents.AnyAsync(e => e.Action == "PROCESS_PUBLISHED" && e.ActorUserId == actor), "Publication records its real author");
        var blocked = false;
        try { await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().Single(x => x.EndsWith("_AddOperationalSequences"))); }
        catch (PostgresException e) when (e.SqlState == "P0001") { blocked = true; }
        Check(blocked && await db.Set<OperationalSequence>().AnyAsync(p => p.Id == key), "Downgrade refuses to discard catalog data");
    }
    private static async Task Reject(Func<Task> action, string code) { try { await action(); throw new Exception("Expected: " + code); } catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS: Rejected " + code); } }
    private static void Check(bool value, string text) { if (!value) throw new Exception("FAILED: " + text); Console.WriteLine("PASS: " + text); }
    private static async Task RejectSql(SgolDbContext db, string sql, string label)
    {
        var tx = db.Database.CurrentTransaction!; await tx.CreateSavepointAsync("process_reject"); bool rejected = false;
        try { await db.Database.ExecuteSqlRawAsync(sql); }
        catch (PostgresException e) when (e.SqlState is "23514" or "23503") { rejected = true; }
        finally { await tx.RollbackToSavepointAsync("process_reject"); }
        Check(rejected, label);
    }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero); }
    private sealed class Scanner : IFileMalwareScanner {
        public Task CheckAvailabilityAsync(CancellationToken token) => Task.CompletedTask;
        public Task<EvidenceScanOutcome> ScanAsync(Stream content, long length, CancellationToken token) => Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));
    }
}
