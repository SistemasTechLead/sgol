using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessAddDefaultsChecks
{
    public static async Task RunAsync(SgolDbContext db)
    {
        if (await db.People.AnyAsync()) throw new Exception("Requires an empty dedicated database");
        var clock = new Clock(); var actor = Guid.CreateVersion7(); var person = Guid.CreateVersion7();
        db.People.Add(new() { Id = person, DisplayName = "Default stages", StableCode = "DEFAULT-STAGES", CreatedAt = clock.UtcNow.AddYears(-1) });
        db.AppUsers.Add(new() { Id = actor, PersonId = person, Status = AccountStatus.Active, SecurityStamp = "isolated" });
        db.EmploymentVersions.Add(new(Guid.CreateVersion7(), person, BranchScope.LorettaId, EmploymentStatus.Active, clock.UtcNow.AddYears(-1)));
        db.RoleAssignmentVersions.Add(new() { Id = Guid.CreateVersion7(), UserId = actor, BranchId = BranchScope.LorettaId, RoleCode = CanonicalRole.Direction, Status = RoleAssignmentStatus.Active, ValidFrom = clock.UtcNow.AddYears(-1) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var catalog = new OperationalProcessCatalogService(db, direction, clock, new ConfigurationBuilder().Build());
        var tasks = new OperationalTaskService(db, direction, clock, new FileTechnicalValidator(), new Scanner());
        var id = Guid.CreateVersion7(); await catalog.CreateAsync(actor, id, "Default stages", "", default); db.ChangeTracker.Clear();
        async Task<ProcessEditor> Add(int index)
        {
            await tasks.SaveAsync(actor, new() { Name = "Default stage " + index, Revision = 0, AssignedRole = CanonicalRole.Direction, Requirement = "COMENTARIO", OperationalCriticality = "FLEXIBLE" }, default);
            db.ChangeTracker.Clear();
            var task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Default stage " + index);
            var editor = await catalog.ReadAsync(actor, id, default);
            await catalog.AddTaskAsync(actor, id, editor.Process.Revision, task.Id, task.Revision, default);
            db.ChangeTracker.Clear(); return await catalog.ReadAsync(actor, id, default);
        }
        var editor = await Add(1);
        Check(editor.Steps.Single().ReleaseCondition == "INICIO" && editor.Steps.Single().Action == "AUTORIZAR", "Single task both starts the process and accepts the result");
        editor = await Add(2);
        Check(editor.Steps[0].Action == "ENTREGAR" && editor.Steps[0].ReleaseCondition == "INICIO" && editor.Steps[1].Action == "AUTORIZAR" && editor.Steps[1].ReleaseCondition == "ENTREGA", "Second task takes acceptance and previous final task becomes delivery");
        await catalog.PublishAsync(actor, id, editor.Process.Revision, default); db.ChangeTracker.Clear();
        var published = (await catalog.ReadAsync(actor, id, default)).Process.PublishedVersionId;
        editor = await Add(3);
        Check(editor.Steps.Select(s => s.Action).SequenceEqual(new[] { "ENTREGAR", "ENTREGAR", "AUTORIZAR" }), "Third task moves acceptance to the new final stage");
        var frozen = await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == published).OrderBy(s => s.Position).ToArrayAsync();
        Check(frozen.Select(s => s.Action).SequenceEqual(new[] { "ENTREGAR", "AUTORIZAR" }), "Adding after publication preserves the published version");
        var lastTask = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == editor.Steps.Last().TaskId);
        await catalog.AddTaskAsync(actor, id, editor.Process.Revision, lastTask.Id, lastTask.Revision, default); db.ChangeTracker.Clear();
        var retry = await catalog.ReadAsync(actor, id, default);
        Check(retry.Steps.Length == 3 && retry.Process.Revision == editor.Process.Revision && retry.Steps.Last().Action == "AUTORIZAR", "Repeated add does not duplicate stages or change actions");
        editor.Steps.Last().Action = "REVISAR";
        await catalog.SaveAsync(actor, new() { Id = id, Revision = editor.Process.Revision, Name = editor.Process.Name, Steps = editor.Steps.ToList() }, default); db.ChangeTracker.Clear();
        editor = await Add(4);
        Check(editor.Steps[2].Action == "REVISAR" && editor.Steps[3].Action == "AUTORIZAR" && editor.Steps[3].ReleaseCondition == "APROBACION", "Custom review action is preserved and next entry requires approval");
        Check(editor.Steps[0].ReleaseCondition == "INICIO" && !await db.OperationalAssignments.AnyAsync(), "First entry stays at process start and adding creates no assignments");
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 27, 18, 0, 0, TimeSpan.Zero); }
    private sealed class Scanner : IFileMalwareScanner
    {
        public Task CheckAvailabilityAsync(CancellationToken token) => Task.CompletedTask;
        public Task<EvidenceScanOutcome> ScanAsync(Stream stream, long size, CancellationToken token) => Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));
    }
}
