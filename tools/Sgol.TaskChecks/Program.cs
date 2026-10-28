using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.Endpoints;
using Sgol.Configuration.Contracts;
using System.Security.Claims;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;

if (args.Contains("--checklist-rules")) { ChecklistChecks.Rules(); return; }
if (args.Contains("--carryover-rules")) { CarryoverChecks.Rules(); return; }
if (args.Contains("--postponement-rules")) { PostponementChecks.Rules(); return; }
if (args.Contains("--scheduling-rules-only")) { OperationalSchedulingChecks.Rules(); return; }
if (args.Contains("--balance-rules-only")) { DailyBalanceChecks.Run(); return; }
if (args.Contains("--schedule-rules-only")) { PersonalScheduleChecks.Run(); return; }
if (args.Contains("--capacity-rules-only")) { CapacityRuleChecks.Run(); return; }
if (args.Contains("--weekly-flag-rules-only")) { WeeklyOperationsFlagChecks.Run(); return; }
TaskDescriptionChecks.Run();
if (args.Contains("--description-rules-only")) return;
SequenceRuleChecks.Run();
if (args.Contains("--sequence-rules-only")) return;
var connection = Environment.GetEnvironmentVariable("SGOL_TASK_CHECKS_CONNECTION") ?? throw new Exception("Set SGOL_TASK_CHECKS_CONNECTION to an empty local test database.");
var settings = new NpgsqlConnectionStringBuilder(connection);
if (settings.Host != "localhost" || settings.Database is null || !settings.Database.StartsWith("sgol_task_checks_"))
    throw new Exception("Checks only run against a dedicated localhost sgol_task_checks_* database.");
var options = new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection).Options;
await using var db = new SgolDbContext(options);
var sequenceBeforeUpgrade = args.Contains("--sequence-schema-only") || args.Contains("--process-schema-only") ? await SequenceSchemaChecks.Snapshot(db) : null;
var carryoverBeforeMigration = args.Contains("--carryover-lifecycle") ? System.Text.Json.JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync()) : null;
await db.Database.MigrateAsync();
if (args.Contains("--postponement-current")) { await PostponementChecks.Run(db); return; }
if (args.Contains("--postponement-inbox-current")) { await PostponementChecks.InboxCurrent(db); return; }
if (args.Contains("--postponement-ui")) { await PostponementChecks.Ui(db); return; }
if (args.Contains("--carryover-lifecycle")) {
    if (carryoverBeforeMigration != System.Text.Json.JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync())) throw new Exception("Carryover migration changed historical assignments");
    await CarryoverChecks.Run(db); return;
}
if (args.Contains("--carryover-ui-seed")) { await CarryoverChecks.SeedUi(db); return; }
if (args.Contains("--reassignment-lifecycle")) { await ReassignmentLifecycleChecks.Run(db); return; }
if (args.Contains("--transfer-lifecycle")) { await TransferChecks.Run(db); return; }
if (args.Contains("--transfer-current-assignments")) { await CurrentAssignmentTransferChecks.Run(db); return; }
if (args.Contains("--weekly-repair-lifecycle")) { await WeeklyRepairLifecycleChecks.Run(db); return; }
if (args.Contains("--weekly-reflow")) { await WeeklyReflowChecks.Run(db); return; }
if (args.Contains("--scheduling-benchmark")) { await OperationalSchedulingChecks.Benchmark(db); return; }
if (args.Contains("--scheduling-integration-only")) { OperationalSchedulingChecks.Rules(); await OperationalSchedulingChecks.Integration(db); return; }
if (args.Contains("--monthly-import-only")) { await MonthlyImportChecks.RunAsync(db); return; }
if (args.Contains("--monthly-distributed-current")) { await DistributedMonthlyChecks.RunAsync(db); return; }
if (args.Contains("--opening-daily-current")) { await DailyOpeningChecks.RunAsync(db); return; }
if (args.Contains("--full-weekly-current")) { await FullWeeklyPlanChecks.RunAsync(db); return; }
if (args.Contains("--weekly-plan-http")) { await WeeklyPlanHttpChecks.RunAsync(db); return; }
if(args.Contains("--balance-snapshot-only")) { await DailyBalanceChecks.SnapshotAsync(db); return; }
if(args.Contains("--process-migrate-only")) { Console.WriteLine("Dedicated database migrated; no fixtures or catalogue changes added."); return; }
if(args.Contains("--process-editor-only")) { await ProcessEditorChecks.RunAsync(db); return; }
if(args.Contains("--process-editor-current")) { await ProcessEditorChecks.CurrentAsync(db); return; }
if (args.Contains("--process-cross-role-only")) { await ProcessRuntimeChecks.RunAsync(db, preparePlan:true, reassignment:true, crossRole:true); return; }
if (args.Contains("--process-runtime-only") || args.Contains("--process-demo-seed") || args.Contains("--process-plan-only") || args.Contains("--process-reassign-only") || args.Contains("--process-deletion-only")) { await ProcessRuntimeChecks.RunAsync(db,args.Contains("--process-demo-seed"),args.Contains("--process-plan-only") || args.Contains("--process-deletion-only"),args.Contains("--process-reassign-only"),args.Contains("--process-deletion-only")); return; }
if (args.Contains("--process-schema-only")) { await ProcessCatalogChecks.SchemaAsync(db, sequenceBeforeUpgrade!); return; }
if (args.Contains("--process-plan-auto-only")) { await ProcessPlanAutoChecks.RunAsync(db); return; }
if (args.Contains("--process-add-defaults-only")) { await ProcessAddDefaultsChecks.RunAsync(db); return; }
if (args.Contains("--process-catalog-only")) { await ProcessCatalogChecks.RunAsync(db); return; }
if (args.Contains("--sequence-schema-only"))
{
    if (sequenceBeforeUpgrade != await SequenceSchemaChecks.Snapshot(db)) throw new Exception("Sequence upgrade modified historical operational data.");
    Directory.CreateDirectory("outputs/secuencias");
    await File.WriteAllTextAsync("outputs/secuencias/historical-before-upgrade.json", sequenceBeforeUpgrade);
    await SequenceSchemaChecks.RunAsync(db); return;
}
if (args.Contains("--lifecycle-only")) { await LifecycleChecks.RunAsync(db); return; }
if (args.Contains("--normalization-only"))
{
    await NormalizationChecks.VerifyUpgradeAsync(db);
    await NormalizationChecks.RunAsync(db); return;
}
if (await db.People.AnyAsync()) throw new Exception("Test database must have no people.");
var clock = new CheckClock();
var ids = new Uuid7Generator(clock);
var catalog = new DirectionPlanningCatalogReader(db);
var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, ids, catalog);
var scanner = new CheckScanner();
var tasks = new OperationalTaskService(db, direction, clock, new FileTechnicalValidator(), scanner);
var supervision = new OperationalSupervisionService(db, clock, direction);
var director = AddPerson("DIR", CanonicalRole.Direction);
var a = AddPerson("A", CanonicalRole.SalesFloor);
var b = AddPerson("B", CanonicalRole.SalesFloor);
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
if (args.Contains("--checklist-only")) { await ChecklistChecks.RunAsync(db,tasks,supervision,direction,clock,director.User,a.User,a.Person,b.User,b.Person); return; }
if (args.Contains("--assignment-retirement-only")) { await AssignmentRetirementChecks.RunAsync(db,tasks,direction,clock,director.User,a.User,a.Person,b.Person); return; }
if (args.Contains("--assignment-refresh-global-only")) { await GlobalAssignmentRefreshChecks.RunAsync(db,tasks,direction,clock,director.User,a.Person); return; }
if (args.Contains("--assignment-refresh-only")) { await AssignmentRefreshChecks.RunAsync(db, tasks, direction, clock, director.User, a.User, a.Person, b.Person); return; }
if (args.Contains("--early-delivery-only")) {
    await EarlyDeliveryChecks.RunAsync(db, tasks, supervision, director.User, a.User, a.Person, b.User, clock.UtcNow);
    return;
}
if (args.Contains("--batch-review-only")) {
    var reviewer = AddPerson("BATCH-SC", CanonicalRole.Subcoordination);
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var day = WeekContract.LocalToday(clock.UtcNow);
    var container = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, director.User,
        clock.UtcNow, "Validación masiva", day, day);
    db.DirectionPreparedPlans.Add(container);
    var definition = await db.OperationalTasks.FirstAsync();
    var template = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = container.Id, TaskId = definition.Id,
        TaskCode = definition.TaskCode, Name = "Entrega de prueba", AssignedRole = CanonicalRole.SalesFloor,
        PersonId = a.Person, PersonName = "A", Requirement = "NINGUNO", LoadMinutes = 10,
        StartsAt = clock.UtcNow.AddHours(-1), DueAt = clock.UtcNow.AddHours(1), CompletedAt = clock.UtcNow };
    var forbidden = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = container.Id, TaskId = definition.Id,
        TaskCode = definition.TaskCode, Name = "Entrega de Dirección", AssignedRole = CanonicalRole.Direction,
        PersonId = director.Person, PersonName = "DIR", Requirement = "NINGUNO", LoadMinutes = 10,
        StartsAt = template.StartsAt, DueAt = template.DueAt, CompletedAt = clock.UtcNow };
    db.OperationalAssignments.AddRange(template, forbidden);
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await BatchReviewChecks.RunAsync(db, supervision, reviewer.User, template, forbidden);
    return;
}
if (args.Contains("--distribution-only")) { await DistributionChecks.RunAsync(db, director.User, a.Person, b.Person); return; }
if (args.Contains("--schedule-integration-only")) { await ScheduleIntegrationChecks.RunAsync(db, tasks, direction, supervision, director.User, clock, a.Person, b.Person); return; }
if (args.Contains("--rotation-only")) { await RotationChecks.RunAsync(db, tasks, direction, supervision, director.User, clock, a.Person, b.Person); return; }
if (args.Contains("--weekly-operations-only"))
{
    var operationsSub = AddPerson("SC-OPS", CanonicalRole.Subcoordination);
    var operationsAdmin = AddPerson("ADM-OPS", CanonicalRole.Administration);
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await WeeklyOperationsChecks.RunAsync(db, direction, tasks, clock, director.User, operationsSub.User, operationsAdmin.User, a.User);
    return;
}
if (args.Contains("--subcoord-occasional-only"))
{
    var subcoord = AddPerson("SC-OCC", CanonicalRole.Subcoordination);
    var administration = AddPerson("ADM-OCC", CanonicalRole.Administration);
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await Rejected(() => tasks.ListAsync(subcoord.User, default), "ACCESO_DENEGADO");
    await Rejected(() => tasks.SaveAsync(subcoord.User, new() { Name = "Forbidden" }, default), "ACCESO_DENEGADO");
    var regular = await db.OperationalTasks.AsNoTracking().FirstAsync(t => t.PeriodType != "OCASIONAL");
    var regularBefore = System.Text.Json.JsonSerializer.Serialize(regular);
    await Rejected(() => tasks.ChangeAsync(subcoord.User, regular.Id, regular.Revision, true, default), "ACCESO_DENEGADO");
    await Rejected(() => tasks.CreateOccasionalTaskAsync(a.User, new("Forbidden", "NINGUNO", 5, 24), default), "ACCESO_DENEGADO");
    var adminTaskId = await tasks.CreateOccasionalTaskAsync(administration.User, new("Ocasional Administración", "NINGUNO", 5, 24), default);
    db.ChangeTracker.Clear();
    Check((await tasks.OccasionalTasksAsync(administration.User, default)).Any(t => t.Id == adminTaskId), "Administration can create and read occasional tasks through scoped screen");
    await Rejected(() => tasks.SaveAsync(administration.User, new() { Name = "Forbidden" }, default), "ACCESO_DENEGADO");
    var taskId = await tasks.CreateOccasionalTaskAsync(subcoord.User, new("Ocasional Subcoordinación", "COMENTARIO", 5, 24, Description: "Descripción desde creación ocasional"), default);
    Check((await tasks.OccasionalTasksAsync(subcoord.User, default)).Single(t => t.Id == taskId).Description == "Descripción desde creación ocasional", "Occasional creation stores optional description");
    db.ChangeTracker.Clear();
    var occasional = (await tasks.OccasionalTasksAsync(subcoord.User, default)).Single(t => t.Id == taskId);
    Check((await tasks.OccasionalTasksAsync(subcoord.User, default)).All(t => t.PeriodType == "OCASIONAL" && t.Active && !t.Deleted && t.ProcessId == OperationalSequence.GeneralId), "Scoped catalog only exposes active independent occasions");
    var recipients = await tasks.OccasionalRecipientsAsync(subcoord.User, taskId, default);
    Check(recipients.Any(w => w.Id == subcoord.Person) && recipients.Any(w => w.Id == administration.Person) && recipients.Any(w => w.Id == a.Person) && recipients.All(w => w.Role != CanonicalRole.Direction), "Recipients include self, Administration and Sales Floor, excluding Direction");
    var occasion = new OccasionInput(taskId, occasional.Revision, "Asignación de prueba", "Realizar y entregar", "", "",
        clock.UtcNow.AddMinutes(-1), clock.UtcNow.AddHours(4), [subcoord.Person, administration.Person, a.Person]);
    var adminRecipients = await tasks.OccasionalRecipientsAsync(administration.User, taskId, default);
    Check(adminRecipients.Any(w => w.Id == administration.Person) && adminRecipients.All(w => w.Role != CanonicalRole.Direction), "Administration scoped recipients include self and exclude Direction");
    await Rejected(() => tasks.AssignOccasionAsync(administration.User, Guid.CreateVersion7(), occasion with { PersonIds = [director.Person] }, [], default), "OCCASION_RECIPIENT_INVALID");
    var adminKey = Guid.CreateVersion7();
    await tasks.AssignOccasionAsync(administration.User, adminKey, occasion with { PersonIds = [administration.Person] }, [], default);
    db.ChangeTracker.Clear();
    Check(await db.OperationalAssignments.AnyAsync(x => x.OccasionId == adminKey && x.PersonId == administration.Person), "Administration can assign an occasional task to self");
    await Rejected(() => tasks.AssignOccasionAsync(subcoord.User, Guid.CreateVersion7(), occasion with { TaskId = regular.Id, TaskRevision = regular.Revision }, [], default), "TASK_NOT_FOUND");
    await Rejected(() => tasks.AssignOccasionAsync(subcoord.User, Guid.CreateVersion7(), occasion with { PersonIds = [director.Person] }, [], default), "OCCASION_RECIPIENT_INVALID");
    await Rejected(() => tasks.AssignOccasionAsync(subcoord.User, Guid.CreateVersion7(), occasion with { PlanId = Guid.NewGuid() }, [], default), "ACCESO_DENEGADO");
    await Rejected(() => tasks.AssignOccasionAsync(subcoord.User, Guid.CreateVersion7(), occasion with { Automatic = true, PersonIds = [] }, [], default), "ACCESO_DENEGADO");
    await Rejected(() => tasks.AssignOccasionAsync(subcoord.User, Guid.CreateVersion7(), occasion with { TaskRevision = 99 }, [], default), "TASK_CONFLICT");
    db.ChangeTracker.Clear();
    var occasionKey = Guid.CreateVersion7();
    var occasionPlan = await tasks.AssignOccasionAsync(subcoord.User, occasionKey, occasion, [], default);
    db.ChangeTracker.Clear();
    Check(await tasks.AssignOccasionAsync(subcoord.User, occasionKey, occasion, [], default) == occasionPlan &&
        await db.OperationalAssignments.CountAsync(x => x.OccasionId == occasionKey) == 3, "Retry creates no duplicate assignments");
    var rows = await db.OperationalAssignments.AsNoTracking().Where(x => x.OccasionId == occasionKey).ToArrayAsync();
    Check(rows.Single(x => x.PersonId == subcoord.Person).AssignedRole == CanonicalRole.Subcoordination &&
        rows.Single(x => x.PersonId == administration.Person).AssignedRole == CanonicalRole.Administration, "Assignment stores each recipient role for correct supervision");
    Check((await tasks.MineAsync(subcoord.User, default)).Any(t => t.Id == rows.Single(x => x.PersonId == subcoord.Person).Id), "Self-assignment appears in Mi trabajo");
    Check((await supervision.ListAsync(subcoord.User, default)).Where(t => t.Assignment.OccasionId == occasionKey).All(t => t.Assignment.PersonId == a.Person), "Assignment access does not expand review permissions");
    await Rejected(() => direction.ReadAsync(subcoord.User, occasionPlan), "ACCESO_DENEGADO");
    var unavailableDay = WeekContract.LocalToday(clock.UtcNow).AddDays(2);
    db.AvailabilityDayVersions.Add(new(Guid.CreateVersion7(), administration.Person, BranchScope.LorettaId, unavailableDay, false, director.User));
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var blockedKey = Guid.CreateVersion7();
    await Rejected(() => tasks.AssignOccasionAsync(subcoord.User, blockedKey, occasion with { StartsAt = OperationalSchedule.Instant(unavailableDay, 10), DueAt = OperationalSchedule.Instant(unavailableDay, 18) }, [], default), "PLAN_NO_AVAILABLE_WORKER");
    db.ChangeTracker.Clear();
    Check(!await db.Set<OperationalOccasion>().AnyAsync(x => x.Id == blockedKey) && !await db.OperationalAssignments.AnyAsync(x => x.OccasionId == blockedKey), "Unavailable recipient rejects whole assignment atomically");
    Check(System.Text.Json.JsonSerializer.Serialize(await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == regular.Id)) == regularBefore, "Periodic task remains exactly unchanged");
    Check(await db.AuditEvents.AnyAsync(e => e.ActorUserId == subcoord.User && e.Action == "OPERATIONAL_OCCASION_ASSIGNED"), "Assignment is audited");
    Console.WriteLine("PASS Subcoordination occasional permission and assignment checks");
    return;
}
Check((await tasks.ListAsync(director.User, default)).Count == 8, "Eight existing examples seeded");
await Rejected(() => tasks.ListAsync(a.User, default), "ACCESO_DENEGADO");
Check((await catalog.ReadPredefinedTasksAsync("MENSUAL")).Single().TaskCode == "TAR-0026", "Predefined tasks match period");
Check(OperationalSchedule.Dates(new(2026, 10, 5), new(2026, 10, 11), "SEMANAL", 2).SequenceEqual([new DateOnly(2026, 10, 5), new(2026, 10, 8)]), "Twice weekly dates");
Check(OperationalSchedule.Dates(new(2026, 10, 1), new(2026, 10, 31), "SEMANAL", 2).Count() == 9, "Weekly cadence across monthly edges");
Check(OperationalSchedule.Dates(new(2024, 1, 1), new(2024, 12, 31), "ANUAL", 2).Count() == 2, "Leap year schedule");
var input = new OperationalTask { MaximumPhotos = 5, Name = "Barrer", Description = "  Retira el polvo.\nLimpia debajo de los muebles.  ", Requirement = "COMENTARIO", PeriodType = "SEMANAL", Repetitions = 2, LoadMinutes = 20, DeadlineHours = 24, Revision = 0 };
await tasks.SaveAsync(director.User, input, default); db.ChangeTracker.Clear();
var sweep = (await tasks.ListAsync(director.User, default)).Single(t => t.Name == "Barrer");
var planInput = new DirectionPlanInput("Semana de prueba", "Tienda", "SEMANAL", 2026, 41, [sweep.TaskCode]);
var key = Guid.CreateVersion7();
var saved = await direction.CreateAsync(director.User, key, Guid.CreateVersion7(), planInput);
var first = saved.Plan.Assignments!;
Check(first.All(x => x.Description == input.Description), "Plan assignments preserve multiline task description");
Check((await tasks.MineAsync(a.User, default)).Single(x => x.Id == first[0].Id).Description == input.Description, "Mi trabajo includes description");
Check(first.Count == 2 && first[0].PersonId == a.Person && first[1].PersonId == b.Person, "Initial workload balanced after every assignment");
Check(first.All(x => x.DueAt - x.StartsAt == TimeSpan.FromHours(24)), "Deadlines captured");
var replay = await direction.CreateAsync(director.User, key, Guid.CreateVersion7(), planInput);
Check(replay.Result == "RECOVERED" && await db.OperationalAssignments.CountAsync() == 2, "Retry does not duplicate assignments");
Check((await direction.ReadAsync(director.User, saved.Plan.PlanId)).Assignments!.Count == 2, "Saved plan details load assignments");
await Rejected(() => direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), planInput), "PLAN_OCCURRENCES_RESERVED");
var second = await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), await AnotherSweepPlan());
Check(second.Plan.Assignments!.All(x => x.PreviousLoadMinutes == 20), "Pending load from previous plan included");
await Rejected(() => tasks.CompleteAsync(a.User, first[0].Id, "", null, default, "Listo"), "TASK_COMMENT_REQUIRED");
await Rejected(() => tasks.CompleteAsync(a.User, first[0].Id, "Piso limpio", null, default, new string('x', 4001)), "TASK_CONCLUSION_TOO_LONG");
await Rejected(() => tasks.CompleteAsync(b.User, first[0].Id, "Listo", null, default), "TASK_NOT_FOUND");
await tasks.CompleteAsync(a.User, first[0].Id, "Piso limpio", null, default, "   "); db.ChangeTracker.Clear();
Check((await tasks.MineAsync(a.User, default)).Single(x => x.Id == first[0].Id).Conclusion is null, "Blank optional conclusion is stored as null");
await tasks.CompleteAsync(a.User, first[0].Id, "Piso limpio", null, default);
Check((await tasks.MineAsync(a.User, default)).Single(x => x.Id == first[0].Id).CompletedAt is not null, "Completion is enforced, persisted and idempotent");
await Rejected(() => tasks.CompleteAsync(b.User, first[1].Id, "", null, default), "TASK_COMMENT_REQUIRED");
var third = await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), await AnotherSweepPlan());
Check(third.Plan.Assignments![0].PersonId == a.Person && third.Plan.Assignments[0].PreviousLoadMinutes == 20, "Completed tasks release load");
var start = new DateOnly(2026, 10, 5);
db.AvailabilityDayVersions.Add(new(Guid.CreateVersion7(), a.Person, BranchScope.LorettaId, start, false, director.User));
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var fourth = await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), await AnotherSweepPlan());
Check(fourth.Plan.Assignments![0].PersonId == b.Person, "Unavailable worker excluded");
db.AvailabilityDayVersions.Add(new(Guid.CreateVersion7(), b.Person, BranchScope.LorettaId, start, false, director.User));
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var unavailablePlan = await AnotherSweepPlan();
// Distributed repetitions may move away from Monday's absence. Pin this rejection fixture to Monday.
var unavailableDefinition = (await tasks.ListAsync(director.User, default)).Single(t => t.TaskCode == unavailablePlan.TaskCodes.Single());
unavailableDefinition.ScheduleMode = "DIAS_FIJOS"; unavailableDefinition.WeekDays = 1 << 1;
await tasks.SaveAsync(director.User, unavailableDefinition, default); db.ChangeTracker.Clear();
var before = await db.DirectionPreparedPlans.CountAsync();
await Rejected(() => direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), unavailablePlan), "PLAN_NO_AVAILABLE_WORKER");
Check(await db.DirectionPreparedPlans.CountAsync() == before, "Failed assignment leaves no partial plan");
sweep.Name = "Barrer actualizado"; sweep.Requirement = "NINGUNO";
sweep.Description = "Instrucciones nuevas";
await tasks.SaveAsync(director.User, sweep, default); db.ChangeTracker.Clear();
Check((await direction.ReadAsync(director.User, saved.Plan.PlanId)).Assignments!.All(x => x.Name == "Barrer" && x.Requirement == "COMENTARIO"), "Edits preserve existing snapshots");
Check((await direction.ReadAsync(director.User, saved.Plan.PlanId)).Assignments!.All(x => x.Description == input.Description), "Editing catalogue description preserves historical instructions");
await Rejected(() => tasks.SaveAsync(director.User, sweep, default), "TASK_CONFLICT"); db.ChangeTracker.Clear();
sweep = (await tasks.ListAsync(director.User, default)).Single(t => t.Id == sweep.Id);
await tasks.ChangeAsync(director.User, sweep.Id, sweep.Revision, false, default); db.ChangeTracker.Clear();
Check((await catalog.ReadTaskOptionsAsync()).All(x => x.TaskCode != sweep.TaskCode), "Inactive task excluded from plans");
await Rejected(() => direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), planInput), "DIRECTION_PLAN_TASK_INVALID");
sweep = (await tasks.ListAsync(director.User, default)).Single(t => t.Id == sweep.Id);
await tasks.ChangeAsync(director.User, sweep.Id, sweep.Revision, true, default); db.ChangeTracker.Clear();
Check((await tasks.ListAsync(director.User, default)).All(x => x.Id != sweep.Id) && await db.OperationalAssignments.AnyAsync(x => x.TaskId == sweep.Id), "Delete hides catalog entry and preserves assigned work");
// Exercise a real upgrade from the preceding schema with an existing single-photo delivery.
var historicalBytes = Jpeg(SkiaSharp.SKColors.Green);
var historical = EvidenceAssignment("FOTOGRAFIA"); historical.MaximumPhotos = 5; historical.CompletedAt = clock.UtcNow;
historical.Attachment = new() { AssignmentId = historical.Id, Content = historicalBytes,
    ContentType = "image/jpeg", FileName = "historical.jpg", CreatedAt = clock.UtcNow };
db.Add(historical); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var previousSchema = db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("_AddCameraPhotographSets")).Last();
await db.GetService<IMigrator>().MigrateAsync(previousSchema);
await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
var historicalPhoto = await db.OperationalPhotographs.SingleAsync(p => p.AssignmentId == historical.Id);
Check(historicalPhoto.Content.SequenceEqual(historicalBytes) && historicalPhoto.AttachedAt == historical.CompletedAt &&
    !await db.OperationalAttachments.AnyAsync(a => a.AssignmentId == historical.Id) && historicalPhoto.FileName == "historical.jpg",
    "Schema upgrade preserves original single-photo bytes, timestamp and historical filename"); db.ChangeTracker.Clear();
var photo = EvidenceAssignment("FOTOGRAFIA"); var document = EvidenceAssignment("DOCUMENTO"); var none = EvidenceAssignment("NINGUNO");
var excel = EvidenceAssignment("EXCEL");
await tasks.SaveAsync(director.User, new OperationalTask { Name = "Excel delivery fixture", Requirement = "EXCEL", Revision = 0 }, default);
db.Add(excel); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
IFormFile ExcelFile(byte[] bytes, string name = "entrega.xlsx") => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name) {
    Headers = new HeaderDictionary(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" };
await Rejected(() => tasks.CompleteAsync(a.User, excel.Id, null, null, default), "TASK_FILE_REQUIRED");
await Rejected(() => tasks.CompleteAsync(a.User, excel.Id, null, ExcelFile([1, 2, 3]), default), "TASK_FILE_INVALID");
using (var workbook = new MemoryStream())
{
    using (var zip = new System.IO.Compression.ZipArchive(workbook, System.IO.Compression.ZipArchiveMode.Create, true))
    {
        var parts = new Dictionary<string, string> {
            ["[Content_Types].xml"] = "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/></Types>",
            ["xl/workbook.xml"] = "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Entrega' sheetId='1' r:id='rId1'/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/></Relationships>",
            ["xl/worksheets/sheet1.xml"] = "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData/></worksheet>" };
        foreach (var part in parts) { using var writer = new StreamWriter(zip.CreateEntry(part.Key).Open()); writer.Write(part.Value); }
    }
    var bytes = workbook.ToArray();
    await Rejected(() => tasks.CompleteAsync(b.User, excel.Id, null, ExcelFile(bytes), default), "TASK_NOT_FOUND");
    await Rejected(() => tasks.CompleteAsync(a.User, excel.Id, null, ExcelFile(bytes, "entrega.xls"), default), "TASK_FILE_INVALID");
    scanner.Outcome = EvidenceScanResult.Infectado;
    await Rejected(() => tasks.CompleteAsync(a.User, excel.Id, null, ExcelFile(bytes), default), "TASK_FILE_SCAN_FAILED");
    scanner.Outcome = EvidenceScanResult.Limpio;
    await tasks.CompleteAsync(a.User, excel.Id, null, ExcelFile(bytes), default); db.ChangeTracker.Clear();
    var delivered = await tasks.FileAsync(a.User, excel.Id, default);
    Check(delivered.FileContent!.SequenceEqual(bytes) && delivered.FileName == "entrega.xlsx" &&
        delivered.ContentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Excel delivery preserves file bytes, filename and MIME for download");
    Check((await tasks.MineAsync(a.User, default)).Single(t => t.Id == excel.Id).ReviewStatus == "PENDIENTE_REVISION", "Excel delivery enters normal supervision review");
}
photo.MinimumPhotos = 2;
db.OperationalAssignments.AddRange(photo, document, none); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
await Rejected(() => supervision.ReviewAsync(director.User, photo.Id, photo.Revision, "CONCLUIDA", null, default), "TASK_CAMERA_REQUIRED"); db.ChangeTracker.Clear();
await Rejected(() => tasks.CompleteAsync(a.User, photo.Id, null, null, default, "Listo"), "TASK_CAMERA_REQUIRED");
await tasks.CompleteAsync(a.User, none.Id, null, null, default, "Listo"); db.ChangeTracker.Clear();
Check((await tasks.MineAsync(a.User, default)).Single(x => x.Id == none.Id).CompletedAt is not null, "No-requirement task closes directly");
var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1cAAAAASUVORK5CYII=");
for (var offset = 8; offset < png.Length;) {
    var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
    uint crc = uint.MaxValue;
    foreach (var value in png.AsSpan(offset + 4, length + 4)) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320U : crc >> 1; }
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(offset + length + 8, 4), crc ^ uint.MaxValue);
    offset += length + 12;
}
IFormFile Image() => new FormFile(new MemoryStream(png), 0, png.Length, "file", "photo.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
if (args.Contains("--screenshots-only"))
{
    var screenshotsTask = EvidenceAssignment("CAPTURA_PANTALLA");
    screenshotsTask.MinimumPhotos = 2; screenshotsTask.MaximumPhotos = 3;
    db.Add(screenshotsTask); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await Rejected(() => tasks.CompleteAsync(a.User, screenshotsTask.Id, null, null, default, screenshots: [Image()]), "TASK_PHOTO_LIMIT");
    await Rejected(() => tasks.CompleteAsync(a.User, screenshotsTask.Id, null, null, default, screenshots: [Image(), Image(), Image(), Image()]), "TASK_PHOTO_LIMIT");
    IFormFile LargeImage(long length) => new FormFile(new MemoryStream(png), 0, length, "screenshots", "large.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
    await Rejected(() => tasks.CompleteAsync(a.User, screenshotsTask.Id, null, null, default, screenshots: [LargeImage(15728641), Image()]), "TASK_PHOTO_LIMIT");
    await Rejected(() => tasks.CompleteAsync(a.User, screenshotsTask.Id, null, null, default, screenshots: [LargeImage(11000000), LargeImage(11000000), LargeImage(11000000)]), "TASK_PHOTO_LIMIT");
    IFormFile InvalidImage() => new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "screenshots", "bad.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
    await Rejected(() => tasks.CompleteAsync(a.User, screenshotsTask.Id, null, null, default, screenshots: [Image(), InvalidImage()]), "TASK_FILE_INVALID");
    scanner.Outcome = EvidenceScanResult.Infectado;
    await Rejected(() => tasks.CompleteAsync(a.User, screenshotsTask.Id, null, null, default, screenshots: [Image(), Image()]), "TASK_FILE_SCAN_FAILED");
    scanner.Outcome = EvidenceScanResult.Limpio;
    Check(!await db.OperationalPhotographs.AnyAsync(x => x.AssignmentId == screenshotsTask.Id) &&
        await db.OperationalAssignments.AnyAsync(x => x.Id == screenshotsTask.Id && x.CompletedAt == null), "Invalid screenshot collection leaves no partial evidence or completion");
    await tasks.CompleteAsync(a.User, screenshotsTask.Id, null, null, default, screenshots: [Image(), Image()]); db.ChangeTracker.Clear();
    var delivered = await tasks.PhotographsAsync(director.User, screenshotsTask.Id, default);
    Check(delivered.Count == 2 && delivered.Select(x => x.Position).SequenceEqual([0, 1]), "Multiple screenshots are visible to reviewer in order");
    Check((await tasks.PhotographAsync(a.User, screenshotsTask.Id, delivered[1].Id, default)).Content.SequenceEqual(png), "Second screenshot preserves its original PNG content");
    await tasks.CompleteAsync(a.User, screenshotsTask.Id, null, null, default, screenshots: [Image(), Image()]); db.ChangeTracker.Clear();
    Check(await db.OperationalPhotographs.CountAsync(x => x.AssignmentId == screenshotsTask.Id) == 2, "Screenshot submission retries do not duplicate evidence");
    await supervision.ReviewAsync(director.User, screenshotsTask.Id, screenshotsTask.Revision + 1, "CONCLUIDA", null, default); db.ChangeTracker.Clear();
    var twenty = EvidenceAssignment("CAPTURA_PANTALLA"); twenty.MinimumPhotos = 20;
    db.Add(twenty); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await tasks.CompleteAsync(a.User, twenty.Id, null, null, default, screenshots: Enumerable.Range(0, 20).Select(_ => Image()).ToArray()); db.ChangeTracker.Clear();
    Check((await tasks.PhotographsAsync(director.User, twenty.Id, default)).Count == 20, "Twenty screenshots can be delivered together");
    var legacy = EvidenceAssignment("CAPTURA_PANTALLA"); legacy.CompletedAt = clock.UtcNow;
    db.Add(legacy); db.OperationalAttachments.Add(new() { AssignmentId = legacy.Id, FileName = "legacy.png", ContentType = "image/png", Content = png, CreatedAt = clock.UtcNow });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await supervision.ReviewAsync(director.User, legacy.Id, legacy.Revision, "CONCLUIDA", null, default); db.ChangeTracker.Clear();
    Console.WriteLine("PASS Screenshot limits, atomic delivery, review and legacy compatibility");
    return;
}
await Rejected(() => tasks.CompleteAsync(a.User, document.Id, null, Image(), default, "Listo"), "TASK_FILE_INVALID");
// Generate real JPEG frames; capture endpoints accept camera JPEGs only.
byte[] Jpeg(SkiaSharp.SKColor color) {
    using var bitmap = new SkiaSharp.SKBitmap(64, 32); using var canvas = new SkiaSharp.SKCanvas(bitmap); canvas.Clear(color);
    using var encoded = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90); return encoded.ToArray();
}
var jpeg = Jpeg(SkiaSharp.SKColors.Red); var jpeg2 = Jpeg(SkiaSharp.SKColors.Blue);
IFormFile CameraImage(byte[]? bytes = null) { bytes ??= jpeg; return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "photo", "capture.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" }; }
await Rejected(() => tasks.CompleteAsync(a.User, photo.Id, null, CameraImage(), default, "Listo"), "TASK_CAMERA_REQUIRED");
await Rejected(() => tasks.BeginCaptureAsync(b.User, photo.Id, default), "TASK_NOT_FOUND");
var capture = await tasks.BeginCaptureAsync(a.User, photo.Id, default); db.ChangeTracker.Clear();
await Rejected(() => tasks.UploadPhotographAsync(a.User, photo.Id, capture.Id, Guid.NewGuid(), Image(), default), "TASK_FILE_INVALID");
await Rejected(() => tasks.UploadPhotographAsync(a.User, photo.Id, capture.Id, Guid.NewGuid(), CameraImage([1,2,3,4]), default), "TASK_FILE_INVALID"); db.ChangeTracker.Clear();
scanner.Outcome = EvidenceScanResult.Infectado;
await Rejected(() => tasks.UploadPhotographAsync(a.User, photo.Id, capture.Id, Guid.NewGuid(), CameraImage(), default), "TASK_FILE_SCAN_FAILED"); db.ChangeTracker.Clear();
scanner.Outcome = EvidenceScanResult.ErrorEscaneo;
await Rejected(() => tasks.UploadPhotographAsync(a.User, photo.Id, capture.Id, Guid.NewGuid(), CameraImage(), default), "TASK_FILE_SCAN_FAILED"); db.ChangeTracker.Clear();
scanner.Outcome = EvidenceScanResult.Limpio;
var capturedId = Guid.NewGuid();
var firstPhoto = await tasks.UploadPhotographAsync(a.User, photo.Id, capture.Id, capturedId, CameraImage(), default); db.ChangeTracker.Clear();
var replayPhoto = await tasks.UploadPhotographAsync(a.User, photo.Id, capture.Id, capturedId, CameraImage(), default); db.ChangeTracker.Clear();
Check(firstPhoto == replayPhoto && await db.OperationalPhotographs.CountAsync(p => p.Id == capturedId) == 1, "Upload retry recovers exactly one verified photograph");
Check((await tasks.PhotographStatusAsync(a.User, photo.Id, capture.Id, capturedId, default)).Id == capturedId, "Interrupted upload can recover server status without retransmitting");
await Rejected(() => tasks.CompleteAsync(a.User, photo.Id, null, null, default, "Listo", capture.Id, [capturedId]), "TASK_PHOTO_LIMIT"); db.ChangeTracker.Clear();
await Rejected(() => tasks.UploadPhotographAsync(a.User, photo.Id, capture.Id, capturedId, CameraImage(jpeg2), default), "TASK_CONFLICT"); db.ChangeTracker.Clear();
await Rejected(() => tasks.PhotographAsync(director.User, photo.Id, capturedId, default), "TASK_NOT_FOUND");
Check(!(await tasks.PhotographsAsync(director.User, photo.Id, default)).Any(), "Staged photographs are private until atomic submission");
var secondPhoto = await tasks.UploadPhotographAsync(a.User, photo.Id, capture.Id, Guid.NewGuid(), CameraImage(jpeg2), default); db.ChangeTracker.Clear();
await Rejected(() => tasks.CompleteAsync(a.User, photo.Id, null, null, default, "Listo", capture.Id, [capturedId, Guid.NewGuid()]), "TASK_PHOTO_LIMIT"); db.ChangeTracker.Clear();
Check(await db.OperationalAssignments.AnyAsync(x => x.Id == photo.Id && x.CompletedAt == null) && !await db.OperationalPhotographs.AnyAsync(x => x.AssignmentId == photo.Id && x.AttachedAt != null), "Invalid collection cannot partially attach or complete task");
await Rejected(() => tasks.CompleteAsync(a.User, photo.Id, null, null, default, "Listo", capture.Id, [capturedId, capturedId]), "TASK_PHOTO_LIMIT"); db.ChangeTracker.Clear();
await tasks.CompleteAsync(a.User, photo.Id, null, null, default, "Fotografía entregada", capture.Id, [secondPhoto.Id, capturedId]); db.ChangeTracker.Clear();
var evidence = await tasks.FileAsync(director.User, photo.Id, default);
Check(evidence.ContentType == "image/jpeg" && evidence.FileContent![0] == 255 && (await tasks.PhotographsAsync(director.User, photo.Id, default)).Select(p => p.Id).SequenceEqual([secondPhoto.Id, capturedId]), "Normalized JPEG collection persisted in selected order and readable by Direction");
await tasks.CompleteAsync(a.User, photo.Id, null, null, default, "Fotografía entregada", capture.Id, [secondPhoto.Id, capturedId]); db.ChangeTracker.Clear();
Check(await db.OperationalPhotographs.CountAsync(p => p.AssignmentId == photo.Id && p.AttachedAt != null) == 2, "Repeated completion preserves one immutable collection");
await Rejected(() => tasks.FileAsync(b.User, photo.Id, default), "ACCESO_DENEGADO");
await Rejected(() => tasks.SaveAsync(director.User, new OperationalTask { Name = "Too many photos", Requirement = "FOTOGRAFIA", MaximumPhotos = 21 }, default), "TASK_PARAMETERS_INVALID");
await tasks.SaveAsync(director.User, new OperationalTask { Name = "Catalogue with 20 photos", Revision = 0, Requirement = "FOTOGRAFIA", MinimumPhotos = 20, MaximumPhotos = 20 }, default); db.ChangeTracker.Clear();
Check((await tasks.ListAsync(director.User, default)).Single(x => x.Name == "Catalogue with 20 photos").MaximumPhotos == 20 && (await direction.ReadAsync(director.User, saved.Plan.PlanId)).Assignments!.Where(x => first.Any(original => original.Id == x.Id)).All(x => x.MaximumPhotos == 5), "Catalogue accepts twenty photos without changing saved assignment snapshots");
var twentyPhotoTask = EvidenceAssignment("FOTOGRAFIA");
twentyPhotoTask.MinimumPhotos = 20;
db.Add(twentyPhotoTask); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var twentySession = await tasks.BeginCaptureAsync(a.User, twentyPhotoTask.Id, default); db.ChangeTracker.Clear();
var twentyIds = new List<Guid>();
for (var i = 0; i < 20; i++)
{
    var uploaded = await tasks.UploadPhotographAsync(a.User, twentyPhotoTask.Id, twentySession.Id, Guid.NewGuid(), CameraImage(), default);
    twentyIds.Add(uploaded.Id); db.ChangeTracker.Clear();
}
await Rejected(() => tasks.UploadPhotographAsync(a.User, twentyPhotoTask.Id, twentySession.Id, Guid.NewGuid(), CameraImage(), default), "TASK_PHOTO_LIMIT"); db.ChangeTracker.Clear();
await Rejected(() => tasks.CompleteAsync(a.User, twentyPhotoTask.Id, null, null, default, "Entrega de 20", twentySession.Id, twentyIds.Take(19).ToArray()), "TASK_PHOTO_LIMIT"); db.ChangeTracker.Clear();
await tasks.CompleteAsync(a.User, twentyPhotoTask.Id, null, null, default, "Entrega de 20", twentySession.Id, twentyIds); db.ChangeTracker.Clear();
Check((await tasks.PhotographsAsync(director.User, twentyPhotoTask.Id, default)).Select(x => x.Id).SequenceEqual(twentyIds), "Twenty photographs upload and submit in order; the twenty-first is rejected");
await supervision.ReviewAsync(director.User, twentyPhotoTask.Id, twentyPhotoTask.Revision + 1, "CONCLUIDA", null, default); db.ChangeTracker.Clear();
Check(await db.OperationalAssignments.AnyAsync(x => x.Id == historical.Id && x.MaximumPhotos == 5), "Expanding photograph limits preserves historical assignment limits");
var expirationTask = EvidenceAssignment("FOTOGRAFIA"); db.Add(expirationTask); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var expiring = await tasks.BeginCaptureAsync(a.User, expirationTask.Id, default); db.ChangeTracker.Clear();
clock.UtcNow = clock.UtcNow.AddMinutes(31);
await Rejected(() => tasks.UploadPhotographAsync(a.User, expirationTask.Id, expiring.Id, Guid.NewGuid(), CameraImage(), default), "TASK_CAMERA_EXPIRED"); db.ChangeTracker.Clear();
clock.UtcNow = clock.UtcNow.AddMinutes(-31);
await Rejected(() => tasks.SaveAsync(director.User, new OperationalTask { Name = "Invalid role", AssignedRole = "INVALID", Revision = 0 }, default), "TASK_PARAMETERS_INVALID");
await tasks.SaveAsync(director.User, new OperationalTask { Name = "Role check SUBCOORDINACION", AssignedRole = CanonicalRole.Subcoordination, Revision = 0 }, default);
db.ChangeTracker.Clear();
var missingRoleTask = (await tasks.ListAsync(director.User, default)).Single(t => t.Name == "Role check SUBCOORDINACION");
var missingRolePlan = new DirectionPlanInput("Missing role check", "Tienda", "SEMANAL", 2026, 42, [missingRoleTask.TaskCode]);
await Rejected(() => direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), missingRolePlan), "PLAN_NO_WORKER_FOR_ROLE");
var admin = AddPerson("ADM", CanonicalRole.Administration); var sub = AddPerson("SUB", CanonicalRole.Subcoordination);
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
foreach (var role in OperationalSchedule.Roles) {
    var name = "Role check " + role;
    if (role != CanonicalRole.Subcoordination) {
        await tasks.SaveAsync(director.User, new OperationalTask { Name = name, AssignedRole = role, Revision = 0 }, default);
        db.ChangeTracker.Clear();
    }
    var roleTask = (await tasks.ListAsync(director.User, default)).Single(t => t.Name == name);
    Check(roleTask.AssignedRole == role, "Catalog saves " + role);
    var rolePlan = await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(),
        new("Role plan " + role, "Tienda", "SEMANAL", 2026, 42, [roleTask.TaskCode]));
    var roleAssignment = rolePlan.Plan.Assignments!.Single();
    var expected = role switch { CanonicalRole.Direction => director.Person, CanonicalRole.Administration => admin.Person, CanonicalRole.Subcoordination => sub.Person, _ => roleAssignment.PersonId };
    Check(roleAssignment.AssignedRole == role && roleAssignment.PersonId == expected &&
        (role != CanonicalRole.SalesFloor || roleAssignment.PersonId == a.Person || roleAssignment.PersonId == b.Person), "Assignment stays inside " + role);
    roleTask.AssignedRole = role == CanonicalRole.Direction ? CanonicalRole.SalesFloor : CanonicalRole.Direction;
    await tasks.SaveAsync(director.User, roleTask, default); db.ChangeTracker.Clear();
    Check((await direction.ReadAsync(director.User, rolePlan.Plan.PlanId)).Assignments!.Single().AssignedRole == role, "Role edit preserves historical assignment " + role);
}
await Rejected(() => supervision.ListAsync(a.User, default), "ACCESO_DENEGADO");
var directionTasks = await supervision.ListAsync(director.User, default);
var adminTasks = await supervision.ListAsync(admin.User, default);
var subTasks = await supervision.ListAsync(sub.User, default);
Check(directionTasks.Count == await db.OperationalAssignments.CountAsync(), "Direction sees every assigned task without pagination loss");
Check(adminTasks.All(t => t.Role is CanonicalRole.Subcoordination or CanonicalRole.SalesFloor) && adminTasks.Any(t => t.Role == CanonicalRole.Subcoordination), "Administration sees subordinate roles");
Check(subTasks.All(t => t.Role == CanonicalRole.SalesFloor), "Subcoordination sees sales floor only");
Check(directionTasks.All(t => t.Assignment.FileContent is null), "Dashboard never loads attachment bytes");
Check(directionTasks.Single(t => t.Assignment.Id == photo.Id).Assignment.Conclusion == "Fotografía entregada", "Submission records conclusion");
Check((await tasks.PhotographsAsync(sub.User, photo.Id, default)).Count == 2, "Supervisors can read all photographs in their scope");
var protectedTask = directionTasks.Single(t => t.Role == CanonicalRole.Direction).Assignment;
await Rejected(() => supervision.ReviewAsync(admin.User, protectedTask.Id, protectedTask.Revision, "CONCLUIDA", null, default), "ACCESO_DENEGADO");
var submitted = directionTasks.Single(t => t.Assignment.Id == first[0].Id).Assignment;
await Rejected(() => supervision.ReviewAsync(sub.User, submitted.Id, submitted.Revision, "NO_SATISFECHA", " ", default), "REVIEW_COMMENT_REQUIRED");
await supervision.ReviewAsync(sub.User, submitted.Id, submitted.Revision, "CONCLUIDA", null, default); db.ChangeTracker.Clear();
await Rejected(() => supervision.ReviewAsync(sub.User, submitted.Id, submitted.Revision, "ROL", "Corregir", default), "TASK_CONFLICT"); db.ChangeTracker.Clear();
var unsatisfied = (await supervision.ListAsync(sub.User, default)).Single(t => t.Assignment.Id == none.Id).Assignment;
await supervision.ReviewAsync(sub.User, none.Id, unsatisfied.Revision, "NO_SATISFECHA", "No cumple", default); db.ChangeTracker.Clear();
Check((await tasks.MineAsync(a.User, default)).Single(t => t.Id == none.Id).ReviewStatus == "NO_SATISFECHA", "Unsatisfied closure visible to worker");
var retryPerson = AddPerson("RETRY", CanonicalRole.SalesFloor); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var retry = EvidenceAssignment("COMENTARIO"); retry.PersonId = retryPerson.Person; retry.PersonName = "RETRY";
retry.Description = "Conservar al reasignar";
db.OperationalAssignments.Add(retry); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
await tasks.CompleteAsync(retryPerson.User, retry.Id, "Entregado", null, default, "Listo"); db.ChangeTracker.Clear();
var retryView = (await supervision.ListAsync(sub.User, default)).Single(t => t.Assignment.Id == retry.Id).Assignment;
await supervision.ReviewAsync(sub.User, retry.Id, retryView.Revision, "MISMA_PERSONA", "Repetir limpieza", default); db.ChangeTracker.Clear();
var replacement = await db.OperationalAssignments.AsNoTracking().SingleAsync(t => t.ReassignedFromId == retry.Id);
var retryDefinition = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == retry.TaskId);
Check(retryView.Description == retry.Description && replacement.Description == retryDefinition.Description && replacement.Requirement == retryDefinition.Requirement, "Supervision preserves original description; correction uses current catalogue evidence and instructions");
Check(replacement.PersonId == retryPerson.Person && replacement.CompletedAt == null && replacement.FileContent == null && replacement.StartsAt == clock.UtcNow, "Reassignment creates fresh work for same worker");
Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(t => t.Id == retry.Id)).Comment == "Entregado", "Reassignment preserves original evidence");
var otherPerson = AddPerson("RETRY2", CanonicalRole.SalesFloor); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var occupied = EvidenceAssignment("NINGUNO"); occupied.PersonId = retryPerson.Person; occupied.PersonName = "RETRY";
db.OperationalAssignments.Add(occupied); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
await supervision.ReviewAsync(sub.User, replacement.Id, replacement.Revision, "ROL", "Repartir al rol", default); db.ChangeTracker.Clear();
var redistributed = await db.OperationalAssignments.AsNoTracking().SingleAsync(t => t.ReassignedFromId == replacement.Id);
Check(redistributed.PersonId == otherPerson.Person && redistributed.AssignedRole == CanonicalRole.SalesFloor, "Role reassignment balances available workers");
var draft = new DirectionPreparedPlan(Guid.CreateVersion7(), BranchScope.LorettaId, director.User, clock.UtcNow,
    new("Draft activation", "Tienda", "SEMANAL", 2026, 43, [missingRoleTask.TaskCode]));
db.DirectionPreparedPlans.Add(draft); db.DirectionPreparedPlanItems.Add(new(draft.Id, missingRoleTask.TaskCode, missingRoleTask.Name, 0));
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
await supervision.ActivateAsync(director.User, draft.Id, default); db.ChangeTracker.Clear();
await supervision.ActivateAsync(director.User, draft.Id, default); db.ChangeTracker.Clear();
Check(await db.OperationalAssignments.CountAsync(t => t.PlanId == draft.Id) == 1, "Draft activation assigns once and is idempotent");
var urgent = EvidenceAssignment("NINGUNO"); urgent.StartsAt = clock.UtcNow.AddMinutes(-5); urgent.DueAt = clock.UtcNow.AddMinutes(30);
var later = EvidenceAssignment("NINGUNO"); later.StartsAt = clock.UtcNow.AddDays(-2); later.DueAt = clock.UtcNow.AddHours(8);
var ownAdmin = EvidenceAssignment("NINGUNO"); ownAdmin.PersonId = admin.Person; ownAdmin.AssignedRole = CanonicalRole.SalesFloor;
var ownSub = EvidenceAssignment("NINGUNO"); ownSub.PersonId = sub.Person; ownSub.AssignedRole = CanonicalRole.SalesFloor;
var ownDirector = EvidenceAssignment("NINGUNO"); ownDirector.PersonId = director.Person; ownDirector.AssignedRole = CanonicalRole.Direction;
db.OperationalAssignments.AddRange(urgent, later, ownAdmin, ownSub, ownDirector); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var mine = (await tasks.MineAsync(a.User, default)).ToList();
Check(mine.FindIndex(t => t.Id == urgent.Id) < mine.FindIndex(t => t.Id == later.Id), "My Work prioritizes deadline over start date");
Check(mine.TakeWhile(t => t.CompletedAt == null).Count() == mine.Count(t => t.CompletedAt == null), "Pending tasks precede submitted tasks");
Check((await supervision.ListAsync(admin.User, default)).All(t => t.Assignment.Id != ownAdmin.Id), "Administration cannot supervise own historical assignment");
Check((await supervision.ListAsync(sub.User, default)).All(t => t.Assignment.Id != ownSub.Id), "Subcoordination cannot supervise own historical assignment");
await Rejected(() => supervision.ReviewAsync(admin.User, ownAdmin.Id, ownAdmin.Revision, "CONCLUIDA", null, default), "ACCESO_DENEGADO");
await Rejected(() => supervision.ReviewAsync(sub.User, ownSub.Id, ownSub.Revision, "CONCLUIDA", null, default), "ACCESO_DENEGADO");
await tasks.CompleteAsync(director.User, ownDirector.Id, null, null, default, "Verificación terminada"); db.ChangeTracker.Clear();
var directorView = (await supervision.ListAsync(director.User, default)).Single(t => t.Assignment.Id == ownDirector.Id).Assignment;
await supervision.ReviewAsync(director.User, ownDirector.Id, directorView.Revision, "CONCLUIDA", null, default); db.ChangeTracker.Clear();
Check((await db.OperationalAssignments.SingleAsync(t => t.Id == ownDirector.Id)).ReviewedBy == director.User, "Direction can review own submitted task");
var monthlyCodes = new List<string>();
foreach (var label in new[] { "Pending", "Review", "Done" }) {
    await tasks.SaveAsync(director.User, new OperationalTask { Name = "Monthly " + label, AssignedRole = CanonicalRole.Direction, Requirement = "COMENTARIO", PeriodType = "MENSUAL", DeadlineHours = label == "Done" ? 24 : 744, Revision = 0 }, default);
    db.ChangeTracker.Clear();
    monthlyCodes.Add((await tasks.ListAsync(director.User, default)).Single(t => t.Name == "Monthly " + label).TaskCode);
}
var monthlyPlan = await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), new("Monthly test", "Direction", "MENSUAL", 2026, 10, monthlyCodes));
var monthlyAssignments = monthlyPlan.Plan.Assignments!;
foreach (var assignment in monthlyAssignments.Where(a => a.Name != "Monthly Pending")) {
    await tasks.CompleteAsync(director.User, assignment.Id, "Evidence verified", null, default, "Monthly check complete"); db.ChangeTracker.Clear();
    if (assignment.Name == "Monthly Done") {
        var reviewTask = (await supervision.ListAsync(director.User, default)).Single(t => t.Assignment.Id == assignment.Id).Assignment;
        await supervision.ReviewAsync(director.User, assignment.Id, reviewTask.Revision, "CONCLUIDA", "Approved by Direction", default); db.ChangeTracker.Clear();
    }
}
var directorMine = await tasks.MineAsync(director.User, default);
Check(directorMine.All(t => t.PlanId is not null && t.PlanName is not null && t.PlanPeriodType is not null), "My Work includes source plan metadata");
var groupedPlans = OperationalWorkPlan.Group(directorMine.Reverse());
Check(groupedPlans.TakeWhile(p => p.PeriodType == "SEMANAL").Count() == groupedPlans.Count(p => p.PeriodType == "SEMANAL"), "Weekly plans precede monthly plans regardless of input order or deadline");
var monthlyGroup = groupedPlans.Single(p => p.Id == monthlyPlan.Plan.PlanId);
Check(monthlyGroup.Tasks.Select(t => t.Name).SequenceEqual(new[] { "Monthly Pending", "Monthly Review", "Monthly Done" }), "Each plan puts pending first, review next, concluded last despite earlier concluded deadline");
Check(groupedPlans.All(p => p.Tasks.All(t => t.PlanId == p.Id)), "Assignments stay in their own plan section");
Check(OperationalWorkPlan.Group(new[] { directorMine[0] with { PlanId = Guid.NewGuid(), PlanPeriodType = "ANUAL" }, directorMine[0] with { PlanId = Guid.NewGuid(), PlanPeriodType = "SEMESTRAL" }, directorMine[0] with { PlanId = Guid.NewGuid(), PlanPeriodType = "TRIMESTRAL" }, directorMine[0] with { PlanId = Guid.NewGuid(), PlanPeriodType = "MENSUAL" }, directorMine[0] with { PlanId = Guid.NewGuid(), PlanPeriodType = "SEMANAL" } }).Select(p => p.PeriodType).SequenceEqual(DirectionPlanContract.PeriodTypes), "All plan periods follow weekly through annual order");
foreach (var boundary in new[] { (0, "peligro"), (49, "peligro"), (50, "advertencia"), (79, "advertencia"), (80, "exito"), (100, "exito") }) {
    var progress = new Sgol.Web.Presentation.Components.TaskProgressViewModel(boundary.Item1, 100, "Test progress");
    Check(progress.Percent == boundary.Item1 && progress.Color == boundary.Item2, "Progress threshold " + boundary.Item1);
}
Check(new Sgol.Web.Presentation.Components.TaskProgressViewModel(0, 0, "Empty").Percent == 0, "Empty plan progress is zero without division failure");
Check(new Sgol.Web.Presentation.Components.TaskProgressViewModel(159, 200, "Fraction").Color == "advertencia", "79.5 percent does not prematurely turn green");
var sampleProgress = Sgol.Web.Presentation.Components.TaskProgressViewModel.FromAssignments(monthlyGroup.Tasks, "Own tasks");
Check(sampleProgress.Total == 3 && sampleProgress.Concluded == 1 && sampleProgress.Percent == 33, "Progress counts approved tasks and excludes pending review");
var globalDetails = await direction.ReadAsync(director.User, saved.Plan.PlanId);
var globalProgress = Sgol.Web.Presentation.Components.TaskProgressViewModel.FromAssignments(globalDetails.Assignments!, "Whole plan");
var personalProgress = Sgol.Web.Presentation.Components.TaskProgressViewModel.FromAssignments(directorMine.Where(t => t.PlanId == saved.Plan.PlanId), "My tasks");
Check(globalProgress.Total == await db.OperationalAssignments.CountAsync(t => t.PlanId == saved.Plan.PlanId) && globalProgress.Concluded == await db.OperationalAssignments.CountAsync(t => t.PlanId == saved.Plan.PlanId && t.ReviewStatus == "CONCLUIDA"), "Planning API includes current review state for every assigned worker");
Check(personalProgress.Percent == 100 && globalProgress.Total > personalProgress.Total && globalProgress.Percent < personalProgress.Percent, "Own progress excludes other workers while planning progress includes everyone");
// Calendar and capacity regression checks use the same isolated database.
var capacity = new OperationalCapacityService(db, clock, supervision);
foreach (var periodType in DirectionPlanContract.PeriodTypes)
    Check((await catalog.ReadPredefinedTasksAsync(periodType)).All(t => t.PeriodType == periodType), "Predefined tasks only match " + periodType);
var fixedInput = new OperationalTask { Name = "Water trees", Requirement = "NINGUNO", ScheduleMode = "DIAS_FIJOS", WeekDays = (1 << 1) | (1 << 4), StartHour = 9, DifferentPeople = true, LoadMinutes = 10, Revision = 0 };
await tasks.SaveAsync(director.User, fixedInput, default); db.ChangeTracker.Clear();
var fixedTask = (await tasks.ListAsync(director.User, default)).Single(t => t.Name == "Water trees");
var waterPlan = await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), new("Calendar check", "Tienda", "SEMANAL", 2026, 44, [fixedTask.TaskCode]));
var waters = waterPlan.Plan.Assignments!;
Check(waters.Count == 2 && waters.Select(t => t.PersonId).Distinct().Count() == 2, "Fixed Monday Thursday uses distinct workers");
Check(waters.Select(t => WeekContract.LocalToday(t.StartsAt).DayOfWeek).SequenceEqual([DayOfWeek.Monday, DayOfWeek.Thursday]), "Fixed days preserved");
var monday = waters.Single(t => WeekContract.LocalToday(t.StartsAt).DayOfWeek == DayOfWeek.Monday);
var thursday = waters.Single(t => WeekContract.LocalToday(t.StartsAt).DayOfWeek == DayOfWeek.Thursday);
var mondayUser = await db.AppUsers.Where(u => u.PersonId == monday.PersonId).Select(u => u.Id).SingleAsync();
var thursdayUser = await db.AppUsers.Where(u => u.PersonId == thursday.PersonId).Select(u => u.Id).SingleAsync();
clock.UtcNow = new(2026, 10, 26, 14, 59, 59, TimeSpan.Zero);
var liveBoard = new LiveBoardReader(db, clock);
foreach (var viewer in new[] { director.User, admin.User, sub.User, a.User })
{
    var board = await liveBoard.ReadAsync(viewer, waterPlan.Plan.PlanId);
    Check(board.Tasks.Count == 0 && board.PlanId == waterPlan.Plan.PlanId,
        "Live board excludes tomorrow's deadline on activation day for every role");
}
await Rejected(() => liveBoard.ReadAsync(Guid.Empty, waterPlan.Plan.PlanId), "ACCESO_DENEGADO");
clock.UtcNow = new(2026, 11, 5, 18, 0, 0, TimeSpan.Zero);
var overdueBoard = await liveBoard.ReadAsync(a.User, waterPlan.Plan.PlanId);
Check(overdueBoard.Plans.All(p => p.Id != waterPlan.Plan.PlanId) && overdueBoard.Tasks.All(t => waters.All(w => w.Id != t.Id)),
    "Old overdue deadlines do not bring an expired plan back into today's board");
clock.UtcNow = new(2026, 10, 26, 14, 59, 59, TimeSpan.Zero);
await tasks.CompleteAsync(mondayUser, monday.Id, null, null, default, "Done"); db.ChangeTracker.Clear();
Check((await tasks.MineAsync(mondayUser, default)).Single(t => t.Id == monday.Id).CompletedAt == clock.UtcNow, "Pre-activation delivery records the actual timestamp");
clock.UtcNow = monday.StartsAt;
await tasks.CompleteAsync(mondayUser, monday.Id, null, null, default, "Done"); db.ChangeTracker.Clear();
var deliveredBoard = await liveBoard.ReadAsync(a.User, waterPlan.Plan.PlanId);
Check(deliveredBoard.Tasks.Count == 0, "Early delivery waits for its deadline day on the board");
clock.UtcNow = monday.DueAt.AddTicks(-1);
deliveredBoard = await liveBoard.ReadAsync(a.User, waterPlan.Plan.PlanId);
Check(deliveredBoard.Percent == 100 && deliveredBoard.Tasks.Single().Id == monday.Id && deliveredBoard.Tasks.Single().Color(clock.UtcNow) == "delivered",
    "On its deadline day the board keeps early delivery and counts pending-review delivery");
clock.UtcNow = monday.StartsAt;
await Rejected(() => supervision.ReviewAsync(director.User, thursday.Id, thursday.Revision, "CONCLUIDA", null, default), "TASK_NOT_STARTED");
await tasks.CompleteAsync(thursdayUser, thursday.Id, null, null, default, "Done"); db.ChangeTracker.Clear();
Check((await tasks.MineAsync(thursdayUser, default)).Single(t => t.Id == thursday.Id).StartsAt == thursday.StartsAt, "Anticipated delivery preserves the scheduled start");
await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), new("Overlapping monthly calendar", "Tienda", "MENSUAL", 2026, 10, [fixedTask.TaskCode]));
Check(await db.OperationalAssignments.CountAsync(t => t.TaskId == fixedTask.Id && t.StartsAt == thursday.StartsAt) == 1, "Overlapping weekly monthly plans never duplicate a repetition");
await Rejected(() => tasks.SaveAsync(director.User, new OperationalTask { Name = "Bad fixed", ScheduleMode = "DIAS_FIJOS", WeekDays = 0, Revision = 0 }, default), "TASK_PARAMETERS_INVALID");
await Rejected(() => tasks.SaveAsync(director.User, new OperationalTask { Name = "Bad passive", ScheduleMode = "PROGRESO", PeriodType = "MENSUAL", Revision = 0 }, default), "TASK_PARAMETERS_INVALID");
var monthDates = OperationalSchedule.Dates(new(2026, 10, 1), new(2026, 10, 31), "MENSUAL", 4).ToArray();
var weeklyMonthDates = Enumerable.Range(0, 5).SelectMany(i => OperationalSchedule.Dates(new DateOnly(2026, 9, 28).AddDays(i * 7), new DateOnly(2026, 10, 4).AddDays(i * 7), "MENSUAL", 4)).Where(d => d.Month == 10).ToArray();
Check(monthDates.Length == 4 && monthDates.SequenceEqual(weeklyMonthDates), "Monthly quota is calendar-based across five weekly plans");
await tasks.SaveAsync(director.User, new OperationalTask { Name = "Passive stock", Requirement = "NINGUNO", ScheduleMode = "PROGRESO", GoalUnits = 100, ProgressUnit = "pares", LoadMinutes = 60, Revision = 0 }, default); db.ChangeTracker.Clear();
var passiveDef = (await tasks.ListAsync(director.User, default)).Single(t => t.Name == "Passive stock");
var passivePlan = await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), new("Progress check", "Tienda", "SEMANAL", 2026, 44, [passiveDef.TaskCode]));
var passive = passivePlan.Plan.Assignments!.Single();
var passiveUser = await db.AppUsers.Where(u => u.PersonId == passive.PersonId).Select(u => u.Id).SingleAsync();
Check(passive.GoalUnits == 100 && WeekContract.LocalToday(passive.DueAt) == new DateOnly(2026, 11, 2), "Passive task stays open through end of week");
await Rejected(() => tasks.CompleteAsync(passiveUser, passive.Id, null, null, default, "Done"), "TASK_GOAL_PENDING");
await Rejected(() => supervision.ReviewAsync(director.User, passive.Id, passive.Revision, "CONCLUIDA", null, default), "TASK_GOAL_PENDING");
await tasks.ProgressAsync(passiveUser, passive.Id, passive.Revision, 40, "Revisados 40 pares", default); db.ChangeTracker.Clear();
var progressBoard = await liveBoard.ReadAsync(a.User, passivePlan.Plan.PlanId);
Check(progressBoard.Tasks.Count == 0, "Weekly progress task waits for its deadline day on the board");
var progressCheckTime = clock.UtcNow;
clock.UtcNow = passive.DueAt;
progressBoard = await liveBoard.ReadAsync(a.User, passivePlan.Plan.PlanId);
clock.UtcNow = progressCheckTime;
Check(progressBoard.Tasks.Single().GoalUnits == 100 && progressBoard.Tasks.Single().ProgressUnits == 40 && progressBoard.Percent == 0,
    "Live board reads unit progress without treating it as a delivery");
await Rejected(() => tasks.ProgressAsync(passiveUser, passive.Id, passive.Revision, 40, "Duplicate advance", default), "TASK_CONFLICT"); db.ChangeTracker.Clear();
var advanced = (await tasks.MineAsync(passiveUser, default)).Single(t => t.Id == passive.Id);
await Rejected(() => tasks.ProgressAsync(passiveUser, passive.Id, advanced.Revision, 61, "Too many", default), "TASK_PROGRESS_INVALID"); db.ChangeTracker.Clear();
Check((await tasks.ProgressHistoryAsync(director.User, passive.Id, default)).Single().Units == 40, "Supervisor can inspect progress history");
var source = passive.PersonId;
var rangeFrom = new DateOnly(2026, 10, 26); var rangeTo = new DateOnly(2026, 11, 1);
var change = new CapacityChange(source, rangeFrom, rangeTo, 0, 120, "Apoyo en recepción");
await Rejected(() => capacity.PreviewAsync(passiveUser, change, default), "ACCESO_DENEGADO");
var protectedPreview = await capacity.PreviewAsync(director.User, change, default);
Check(protectedPreview.Moves.All(m => m.TaskId != passive.Id) && protectedPreview.RemainingExcess > 0, "Tasks with progress stay protected and excess is reported");
var transfer = change with { TransferStarted = true };
var preview = await capacity.PreviewAsync(director.User, transfer, default);
Check(preview.Moves.Any(m => m.TaskId == passive.Id) && preview.Moves.All(m => m.ToId != source), "Explicit progress transfer selects another worker");
Check(preview.Moves.All(m => m.StartsAt == (db.OperationalAssignments.AsNoTracking().Single(t => t.Id == m.TaskId)).StartsAt), "Capacity transfer retains activation dates");
await tasks.ProgressAsync(passiveUser, passive.Id, advanced.Revision, 10, "Diez más", default); db.ChangeTracker.Clear();
await Rejected(() => capacity.ApplyAsync(director.User, transfer, preview.Fingerprint, default), "CAPACITY_STALE"); db.ChangeTracker.Clear();
Check(!await db.OperationalCapacities.AnyAsync(c => c.PersonId == source), "Stale preview never partially applies capacity");
preview = await capacity.PreviewAsync(director.User, transfer, default);
var applied = await capacity.ApplyAsync(director.User, transfer, preview.Fingerprint, default); db.ChangeTracker.Clear();
var movedPassive = await db.OperationalAssignments.AsNoTracking().SingleAsync(t => t.Id == passive.Id);
Check(movedPassive.PersonId != source && movedPassive.ProgressUnits == 50 && movedPassive.StartsAt == passive.StartsAt && movedPassive.DueAt == passive.DueAt, "Transfer preserves progress and deadline");
await Rejected(() => tasks.ProgressAsync(passiveUser, passive.Id, movedPassive.Revision, 1, "Old owner", default), "TASK_NOT_FOUND");
var newOwner = await db.AppUsers.Where(u => u.PersonId == movedPassive.PersonId).Select(u => u.Id).SingleAsync();
await tasks.ProgressAsync(newOwner, passive.Id, movedPassive.Revision, 50, "Resto revisado", default); db.ChangeTracker.Clear();
await tasks.CompleteAsync(newOwner, passive.Id, null, null, default, "Meta completa"); db.ChangeTracker.Clear();
Check(await db.AuditEvents.AnyAsync(e => e.Action == "CAPACITY_REDISTRIBUTED"), "Capacity and transfers are audited");
var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default);
Check(ledger.DayCapacity(source, rangeFrom) == 0 && ledger.DayCapacity(source, rangeTo.AddDays(1)) == 240, "Capacity expires automatically");
var overrideChange = change with { From = rangeFrom.AddDays(1), Through = rangeFrom.AddDays(2), Percent = 50 };
preview = await capacity.PreviewAsync(director.User, overrideChange, default);
await capacity.ApplyAsync(director.User, overrideChange, preview.Fingerprint, default); db.ChangeTracker.Clear();
ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default);
Check(ledger.DayCapacity(source, rangeFrom) == 0 && ledger.DayCapacity(source, rangeFrom.AddDays(1)) == 60 && ledger.DayCapacity(source, rangeFrom.AddDays(3)) == 0, "A later adjustment replaces only its date range");
var extra = new OperationalTask { Name = "Extra during week", Requirement = "NINGUNO", ScheduleMode = "DIAS_FIJOS", WeekDays = 1 << 5, LoadMinutes = 5, Revision = 0 };
await tasks.SaveAsync(director.User, extra, default); db.ChangeTracker.Clear();
var extraDef = (await tasks.ListAsync(director.User, default)).Single(t => t.Name == extra.Name);
await supervision.AddTasksAsync(director.User, waterPlan.Plan.PlanId, [extraDef.TaskCode], default); db.ChangeTracker.Clear();
await supervision.AddTasksAsync(director.User, waterPlan.Plan.PlanId, [extraDef.TaskCode], default); db.ChangeTracker.Clear();
Check(await db.OperationalAssignments.CountAsync(t => t.PlanId == waterPlan.Plan.PlanId && t.TaskId == extraDef.Id) == 1, "Adding during week is repeat-safe");
await Rejected(() => supervision.AddTasksAsync(sub.User, waterPlan.Plan.PlanId, [extraDef.TaskCode], default), "ACCESO_DENEGADO");
clock.UtcNow = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);

var loadInput = (await AnotherSweepPlan()) with { PeriodNumber = 46 };
var loadState = await PlanningCapacity.ReadAsync(db, loadInput, clock.UtcNow, default);
Check(loadState.Preview.Workers.All(w => w.Role == CanonicalRole.SalesFloor) && loadState.Preview.Workers.Any(w => w.PersonId == a.Person), "Plan load dialog uses task roles and active workers");
Check(loadState.Preview.Workers.Single(w => w.PersonId == a.Person).CurrentLoadMinutes ==
    loadState.Ledger.Tasks.Where(t => t.PersonId == a.Person).Sum(t => (long)t.RemainingMinutes) + loadState.Ledger.Legacy.GetValueOrDefault(a.Person), "Plan dialog includes pending minutes from the shared ledger");
var adjustmentCount = await db.OperationalCapacities.CountAsync();
var blockedInput = loadInput with { CapacityAdjustments = loadState.Preview.Workers.Select(w => new PlanCapacityAdjustment(w.PersonId, 0)).ToArray(), CapacityFingerprint = loadState.Preview.Fingerprint };
await Rejected(() => direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), blockedInput), "PLAN_NO_AVAILABLE_WORKER");
Check(await db.OperationalCapacities.CountAsync() == adjustmentCount && !await db.DirectionPreparedPlans.AnyAsync(p => p.Year == 2026 && p.PeriodNumber == 46), "Failed plan leaves capacity and plans untouched");
var adjustedInput = loadInput with { CapacityAdjustments = [new(a.Person, 0)], CapacityFingerprint = loadState.Preview.Fingerprint };
await Rejected(() => direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), adjustedInput with { CapacityFingerprint = new string('0', 64) }), "PLAN_CAPACITY_STALE");
Check(await db.OperationalCapacities.CountAsync() == adjustmentCount, "Stale plan cannot persist capacity");
var adjustedKey = Guid.CreateVersion7();
var adjustedPlan = await direction.CreateAsync(director.User, adjustedKey, Guid.CreateVersion7(), adjustedInput);
Check(adjustedPlan.Plan.Assignments!.All(t => t.PersonId != a.Person), "Lowered bar excludes worker from new plan assignments");
ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default);
Check(ledger.DayCapacity(a.Person, loadState.Preview.From) == 0 && ledger.DayCapacity(a.Person, loadState.Preview.Through.AddDays(1)) == 240, "Plan adjustment is shared with supervision and expires");
var adjustedReplay = await direction.CreateAsync(director.User, adjustedKey, Guid.CreateVersion7(), adjustedInput);
Check(adjustedReplay.Result == "RECOVERED" && await db.OperationalAssignments.CountAsync(t => t.PlanId == adjustedPlan.Plan.PlanId) == 2, "Adjusted plan retry preserves assignments without duplicate changes");
Check(await db.AuditEvents.AnyAsync(e => e.Action == "PLAN_CAPACITY_ADJUSTED"), "Plan capacity changes are audited");

// Specialized tasks exercise the same generation, review, capacity and profile services.
var specialties = new SpecializationService(db, direction, clock);
Check((await specialties.OptionsAsync(admin.User, default)).Select(s => s.Id).SequenceEqual((await specialties.OptionsAsync(director.User, default)).Select(s => s.Id)), "Administration reads specialization options for the task catalogue");
await Rejected(() => specialties.SaveAsync(a.User, new Specialization { Name = "Unauthorized" }, default), "ACCESO_DENEGADO");
await specialties.SaveAsync(director.User, new Specialization { Name = "Contaduría" }, default); db.ChangeTracker.Clear();
await specialties.SaveAsync(director.User, new Specialization { Name = "Marketing" }, default); db.ChangeTracker.Clear();
var accounting = (await specialties.OptionsAsync(director.User, default)).Single(s => s.Name == "Contaduría");
var marketing = (await specialties.OptionsAsync(director.User, default)).Single(s => s.Name == "Marketing");
await Rejected(() => specialties.SaveAsync(director.User, new Specialization { Name = " contaduría " }, default), "SPECIALIZATION_DUPLICATE");
var userProfiles = new UserProfileService(db, new AuditTransaction(db), clock, new PasswordHasher<AppUser>());
async Task<UserProfile> Specialist(string name, bool trial, params Guid[] skills) {
    var result = await userProfiles.MutateAsync(director.User, new UserProfileInput {
        DisplayName = name, UserName = name, RoleCode = CanonicalRole.SalesFloor, ManageSpecializations = true,
        IsOnTrial = trial, SpecializationIds = skills }, "save", Guid.CreateVersion7(), default);
    return (await userProfiles.ListAsync(director.User, default)).Single(p => p.Id == result.Id);
}
var skilledA = await Specialist("specialist.a", false, accounting.Id, marketing.Id);
var skilledB = await Specialist("specialist.b", false, accounting.Id);
await userProfiles.MutateAsync(director.User, new UserProfileInput { Id = skilledB.Id, Revision = skilledB.Revision,
    DisplayName = skilledB.DisplayName, UserName = skilledB.UserName, RoleCode = CanonicalRole.Administration }, "save", Guid.CreateVersion7(), default);
skilledB = (await userProfiles.ListAsync(director.User, default)).Single(p => p.Id == skilledB.Id);
var beginner = await Specialist("specialist.trial", true, accounting.Id, marketing.Id);
Check(skilledA.SpecializationIds!.Length == 2 && beginner.IsOnTrial, "Multiple specializations and probation persist on profiles");
await tasks.SaveAsync(director.User, new OperationalTask { Name = "Specialized accounting", SpecializationId = accounting.Id, LoadMinutes = 20, Repetitions = 2, Revision = 0 }, default); db.ChangeTracker.Clear();
var specialtyTask = (await tasks.ListAsync(director.User, default)).Single(t => t.Name == "Specialized accounting");
var specialtyInput = new DirectionPlanInput("Specialized balance", "Checks", "SEMANAL", 2027, 1, [specialtyTask.TaskCode]);
var specialtyPlan = await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), specialtyInput); db.ChangeTracker.Clear();
var skilledIds = await db.AppUsers.Where(u => u.Id == skilledA.Id || u.Id == skilledB.Id).Select(u => u.PersonId).ToArrayAsync();
Check(specialtyPlan.Plan.Assignments!.All(t => skilledIds.Contains(t.PersonId)) && specialtyPlan.Plan.Assignments!.Select(t => t.PersonId).Distinct().Count() == 2,
    "Only specialists outside probation are assigned, balanced by load in the task period");
Check(specialtyPlan.Plan.Assignments!.All(t => t.SpecializationId == accounting.Id && t.SpecializationName == "Contaduría"), "Specialization is captured in assignment snapshots");
var specialistRoles = await db.AppUsers.Where(u => u.Id == skilledA.Id || u.Id == skilledB.Id)
    .Select(u => new { u.PersonId, Role = db.RoleAssignmentVersions.Where(r => r.UserId == u.Id && r.ValidTo == null).Select(r => r.RoleCode).Single() }).ToDictionaryAsync(x => x.PersonId, x => x.Role);
Check(specialtyPlan.Plan.Assignments!.All(t => t.AssignedRole == specialistRoles[t.PersonId]) && specialistRoles.Values.Distinct().Count() == 2,
    "Specialized generation crosses roles and records each recipient role");
var unqualifiedWorker = new CapacityWorker(Guid.NewGuid(), "No skill", "NO", CanonicalRole.Administration, null, null);
Check(!unqualifiedWorker.EligibleFor(CanonicalRole.SalesFloor, accounting.Id), "Different-role worker without required specialization is rejected");
Check(!unqualifiedWorker.EligibleFor(CanonicalRole.SalesFloor, null), "Tasks without specialization retain their role restriction");
var trialWorker = unqualifiedWorker with { IsOnTrial = true, SpecializationIds = [accounting.Id] };
Check(!trialWorker.EligibleFor(CanonicalRole.SalesFloor, accounting.Id), "Probation blocks cross-role specialized work");
var oldRevision = accounting.Revision;
accounting.Name = "Contaduría avanzada";
await specialties.SaveAsync(director.User, accounting, default); db.ChangeTracker.Clear();
Check((await direction.ReadAsync(director.User, specialtyPlan.Plan.PlanId)).Assignments!.All(t => t.SpecializationName == "Contaduría"), "Renaming preserves assignment history");
await Rejected(() => specialties.SaveAsync(director.User, new Specialization { Id = accounting.Id, Revision = oldRevision, Name = "Stale" }, default), "SPECIALIZATION_CONFLICT");
accounting = (await specialties.OptionsAsync(director.User, default)).Single(s => s.Id == accounting.Id); accounting.Active = false;
await Rejected(() => specialties.SaveAsync(director.User, accounting, default), "SPECIALIZATION_IN_USE"); db.ChangeTracker.Clear();
await Rejected(() => tasks.SaveAsync(director.User, new OperationalTask { Name = "Invalid specialty", SpecializationId = Guid.NewGuid(), Revision = 0 }, default), "TASK_SPECIALIZATION_INVALID");
async Task ProfileRejected(Guid actor, UserProfile p, bool trial, Guid[] skills, int expected) {
    try { await userProfiles.MutateAsync(actor, new UserProfileInput { Id = p.Id, Revision = p.Revision,
        DisplayName = p.DisplayName, UserName = p.UserName, RoleCode = p.RoleCode, ManageSpecializations = true,
        IsOnTrial = trial, SpecializationIds = skills }, "save", Guid.CreateVersion7(), default); throw new Exception("Profile rejection expected"); }
    catch (UserProfileException ex) when (ex.Status == expected) { Check(true, "Specialized profile change rejected " + expected); }
}
await ProfileRejected(admin.User, skilledA, false, [marketing.Id], 403);
await ProfileRejected(director.User, skilledA, true, [accounting.Id, marketing.Id], 409);
await ProfileRejected(director.User, skilledA, false, [marketing.Id], 409);
clock.UtcNow = new(2027, 1, 4, 18, 0, 0, TimeSpan.Zero);
var reviewed = specialtyPlan.Plan.Assignments![0];
var sourceCapacity = new OperationalCapacity { Id = Guid.CreateVersion7(), PersonId = reviewed.PersonId,
    From = new(2027, 1, 4), Through = new(2027, 1, 10), Percent = 0, DailyMinutes = 120, Reason = "Force cross-role reassignment in isolated checks" };
db.OperationalCapacities.Add(sourceCapacity); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
await supervision.ReviewAsync(director.User, reviewed.Id, reviewed.Revision, "ROL", "Otra revisión", default); db.ChangeTracker.Clear();
var specialtyRetry = await db.OperationalAssignments.AsNoTracking().SingleAsync(t => t.ReassignedFromId == reviewed.Id);
Check(skilledIds.Contains(specialtyRetry.PersonId) && specialtyRetry.SpecializationId == accounting.Id, "Role reassignment retains specialty and excludes trial and unqualified workers");
Check(specialtyRetry.AssignedRole != reviewed.AssignedRole && specialtyRetry.AssignedRole == specialistRoles[specialtyRetry.PersonId], "Supervised reassignment crosses roles and records the recipient role");
await db.OperationalCapacities.Where(c => c.Id == sourceCapacity.Id).ExecuteDeleteAsync();
var capacityService = new OperationalCapacityService(db, clock, supervision);
var capacityPreview = await capacityService.PreviewAsync(director.User, new CapacityChange(specialtyRetry.PersonId, new(2027, 1, 4), new(2027, 1, 10), 0, 120, "Specialist capacity"), default);
Check(capacityPreview.Moves.Count > 0 && capacityPreview.Moves.All(m => skilledIds.Contains(m.ToId) && m.ToId != specialtyRetry.PersonId), "Capacity redistribution uses qualified specialists only");
Check(capacityPreview.Moves.Any(m => specialistRoles[m.ToId] != specialistRoles[specialtyRetry.PersonId]), "Capacity preview transfers specialized work across roles");
await capacityService.ApplyAsync(director.User, capacityPreview.Change, capacityPreview.Fingerprint, default); db.ChangeTracker.Clear();
var movedIds = capacityPreview.Moves.Select(m => m.TaskId).ToArray();
Check((await db.OperationalAssignments.Where(t => movedIds.Contains(t.Id)).ToArrayAsync()).All(t => t.AssignedRole == specialistRoles[t.PersonId]), "Applied transfers update recipient roles");
var noSpecialty = new Specialization { Name = "No specialists" }; await specialties.SaveAsync(director.User, noSpecialty, default); db.ChangeTracker.Clear();
var unavailableSkill = (await specialties.OptionsAsync(director.User, default)).Single(s => s.Name == "No specialists");
await tasks.SaveAsync(director.User, new OperationalTask { Name = "No specialist task", SpecializationId = unavailableSkill.Id, Revision = 0 }, default); db.ChangeTracker.Clear();
var noSkillTask = (await tasks.ListAsync(director.User, default)).Single(t => t.Name == "No specialist task");
await Rejected(() => direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), specialtyInput with { Name = "No specialist plan", PeriodNumber = 2, TaskCodes = [noSkillTask.TaskCode] }), "PLAN_NO_SPECIALIST");
Check(!await db.DirectionPreparedPlans.AnyAsync(p => p.Name == "No specialist plan"), "No-specialist error rolls back the entire plan");
var optionsPreview = await PlanningCapacity.ReadAsync(db, specialtyInput with { PeriodNumber = 3 }, clock.UtcNow, default);
Check(optionsPreview.Preview.Workers.All(w => skilledIds.Contains(w.PersonId)), "Planning preview lists qualified specialists only");
Check((await specialties.ListAsync(director.User, default)).Single(s => s.Item.Id == accounting.Id).People.Length == 3, "Specialty page lists all assigned people including probation");
Check(await db.AuditEvents.AnyAsync(e => e.Action == "SPECIALIZATION_SAVED"), "Specialty edits are audited");
Check(OperationalSchedule.LoadRange(new(2027, 2, 15), "MENSUAL") == (new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 28)), "Monthly specialty balancing uses the calendar month");
Check(OperationalSchedule.LoadRange(new(2027, 2, 15), "TRIMESTRAL") == (new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 31)), "Quarterly specialty balancing uses the calendar quarter");
beginner = (await userProfiles.ListAsync(director.User, default)).Single(p => p.Id == beginner.Id);
await userProfiles.MutateAsync(director.User, new UserProfileInput { Id = beginner.Id, Revision = beginner.Revision,
    DisplayName = beginner.DisplayName, UserName = beginner.UserName, RoleCode = beginner.RoleCode, ManageSpecializations = true,
    IsOnTrial = true, SpecializationIds = [accounting.Id, marketing.Id, unavailableSkill.Id] }, "save", Guid.CreateVersion7(), default);
await Rejected(() => direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), specialtyInput with { Name = "Trial only plan", PeriodNumber = 2, TaskCodes = [noSkillTask.TaskCode] }), "PLAN_NO_SPECIALIST");
Check(!await db.DirectionPreparedPlans.AnyAsync(p => p.Name == "Trial only plan"), "Trial-only specialists cannot receive specialized work");
// Deletion removes generated data, preserves unrelated plans, and guards stale confirmations.
var deletePreview = await direction.PreviewDeletionAsync(director.User, saved.Plan.PlanId);
Check(deletePreview.Assignments > 0 && deletePreview.EvidenceFiles > 0, "Deletion preview includes assignments and stored evidence");
await Rejected(() => direction.PreviewDeletionAsync(a.User, saved.Plan.PlanId), "ACCESO_DENEGADO");
await Rejected(() => direction.DeleteAsync(a.User, saved.Plan.PlanId, Guid.CreateVersion7(), Guid.CreateVersion7(), deletePreview.Fingerprint), "ACCESO_DENEGADO");
var changedAssignment = await db.OperationalAssignments.FirstAsync(t => t.PlanId == saved.Plan.PlanId);
changedAssignment.Revision++; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
await Rejected(() => direction.DeleteAsync(director.User, saved.Plan.PlanId, Guid.CreateVersion7(), Guid.CreateVersion7(), deletePreview.Fingerprint), "PLAN_DELETE_STALE");
Check(await db.DirectionPreparedPlans.AnyAsync(p => p.Id == saved.Plan.PlanId), "Stale deletion preserves the entire plan");
deletePreview = await direction.PreviewDeletionAsync(director.User, saved.Plan.PlanId);
var deleteKey = Guid.CreateVersion7();
var deleteAssignmentIds = await db.OperationalAssignments.Where(t => t.PlanId == saved.Plan.PlanId).Select(t => t.Id).ToArrayAsync();
var unrelatedAssignments = await db.OperationalAssignments.CountAsync(t => t.PlanId != saved.Plan.PlanId);
await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event ADD CONSTRAINT test_delete_audit_failure CHECK (action <> 'DIRECTION_PLAN_DELETE') NOT VALID");
try
{
    try { await direction.DeleteAsync(director.User, saved.Plan.PlanId, deleteKey, Guid.CreateVersion7(), deletePreview.Fingerprint); throw new Exception("Expected audit failure"); }
    catch (DbUpdateException e) when (e.InnerException is PostgresException { ConstraintName: "test_delete_audit_failure" }) { }
    Check(await db.DirectionPreparedPlans.AnyAsync(p => p.Id == saved.Plan.PlanId) &&
        await db.OperationalAssignments.CountAsync(p => p.PlanId == saved.Plan.PlanId) == deletePreview.Assignments &&
        await db.DirectionPreparedPlanItems.CountAsync(p => p.PlanId == saved.Plan.PlanId) == deletePreview.Tasks,
        "Audit failure rolls back plan, assignments and task items atomically");
}
finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event DROP CONSTRAINT test_delete_audit_failure"); }
await direction.DeleteAsync(director.User, saved.Plan.PlanId, deleteKey, Guid.CreateVersion7(), deletePreview.Fingerprint);
Check(!await db.DirectionPreparedPlans.AnyAsync(p => p.Id == saved.Plan.PlanId) &&
    !await db.DirectionPreparedPlanItems.AnyAsync(p => p.PlanId == saved.Plan.PlanId) &&
    !await db.OperationalAssignments.AnyAsync(p => p.PlanId == saved.Plan.PlanId) &&
    !await db.Set<OperationalProgressEntry>().AnyAsync(p => deleteAssignmentIds.Contains(p.AssignmentId)), "Plan deletion removes generated tasks, evidence and dependent data");
Check(await db.OperationalAssignments.CountAsync() == unrelatedAssignments && await db.OperationalTasks.AnyAsync(t => t.Id == sweep.Id), "Deletion preserves other plans and task catalog");
Check((await direction.DeleteAsync(director.User, saved.Plan.PlanId, deleteKey, Guid.CreateVersion7(), deletePreview.Fingerprint)).Result == "DELETED", "Deletion retry returns the confirmed result");
await Rejected(() => direction.DeleteAsync(director.User, saved.Plan.PlanId, deleteKey, Guid.CreateVersion7(), new string('0', 64)), "IDEMPOTENCY_CONFLICT");
Check(!await db.IdempotencyRecords.AnyAsync(r => r.Key == key && r.ResourceId == saved.Plan.PlanId), "Deleted plan creation response cannot replay obsolete assignments");
Check(await db.AuditEvents.AnyAsync(e => e.Action == "DIRECTION_PLAN_DELETE" && e.ResourceId == saved.Plan.PlanId), "Plan deletion is audited");

var progressPreview = await direction.PreviewDeletionAsync(director.User, passivePlan.Plan.PlanId);
Check(progressPreview.ProgressEntries > 0, "Deletion preview counts progress history");
var progressIds = await db.OperationalAssignments.Where(t => t.PlanId == passivePlan.Plan.PlanId).Select(t => t.Id).ToArrayAsync();
await direction.DeleteAsync(director.User, passivePlan.Plan.PlanId, Guid.CreateVersion7(), Guid.CreateVersion7(), progressPreview.Fingerprint);
Check(!await db.Set<OperationalProgressEntry>().AnyAsync(p => progressIds.Contains(p.AssignmentId)), "Deletion removes recorded progress");

var capacityDeletion = await direction.PreviewDeletionAsync(director.User, adjustedPlan.Plan.PlanId);
Check(capacityDeletion.CapacityAdjustments > 0, "Deletion identifies plan capacity settings");
var adjustedSetting = await db.OperationalCapacities.SingleAsync(c => c.PersonId == a.Person && c.From == loadState.Preview.From && c.Through == loadState.Preview.Through);
adjustedSetting.Revision++; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
await Rejected(() => direction.DeleteAsync(director.User, adjustedPlan.Plan.PlanId, Guid.CreateVersion7(), Guid.CreateVersion7(), capacityDeletion.Fingerprint), "PLAN_DELETE_CAPACITY_CHANGED");
Check(await db.DirectionPreparedPlans.AnyAsync(p => p.Id == adjustedPlan.Plan.PlanId), "Changed shared capacity blocks deletion without partial changes");
adjustedSetting = await db.OperationalCapacities.SingleAsync(c => c.Id == adjustedSetting.Id);
adjustedSetting.Revision--; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
await direction.DeleteAsync(director.User, adjustedPlan.Plan.PlanId, Guid.CreateVersion7(), Guid.CreateVersion7(), capacityDeletion.Fingerprint);
ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default);
Check(ledger.DayCapacity(a.Person, loadState.Preview.From) == 240, "Deleting a plan removes its capacity override");
var restoreInput = (await AnotherSweepPlan()) with { Year = 2028, PeriodNumber = 2 };
var restoreRange = DirectionPlanContract.Calculate(restoreInput.PeriodType, restoreInput.Year, restoreInput.PeriodNumber);
db.OperationalCapacities.Add(new() { Id = Guid.CreateVersion7(), PersonId = a.Person,
    From = restoreRange.StartsOn.AddDays(-2), Through = restoreRange.EndsOn.AddDays(2), Percent = 75, DailyMinutes = 100, Reason = "Prior shared setting" });
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var restoreState = await PlanningCapacity.ReadAsync(db, restoreInput, clock.UtcNow, default);
var restorePlan = await direction.CreateAsync(director.User, Guid.CreateVersion7(), Guid.CreateVersion7(), restoreInput with {
    CapacityAdjustments = [new(a.Person, 0)], CapacityFingerprint = restoreState.Preview.Fingerprint });
var restorePreview = await direction.PreviewDeletionAsync(director.User, restorePlan.Plan.PlanId);
await direction.DeleteAsync(director.User, restorePlan.Plan.PlanId, Guid.CreateVersion7(), Guid.CreateVersion7(), restorePreview.Fingerprint);
var restored = await db.OperationalCapacities.AsNoTracking().Where(c => c.PersonId == a.Person && c.From <= restoreRange.EndsOn && c.Through >= restoreRange.StartsOn).ToArrayAsync();
Check(restored.Length == 1 && restored[0].Percent == 75 && restored[0].DailyMinutes == 100 && restored[0].Reason == "Prior shared setting", "Deleting a plan restores overwritten capacity parameters");
ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default);
Check(ledger.DayCapacity(a.Person, restoreRange.StartsOn.AddDays(-1)) == 75 && ledger.DayCapacity(a.Person, restoreRange.EndsOn.AddDays(1)) == 75, "Capacity restoration preserves settings outside the plan interval");
// Administration and Subcoordination consult the same prepared plans.
var planningAdmin = AddPerson("PLAN-ADMIN", CanonicalRole.Administration);
var planningSub = AddPerson("PLAN-SUB", CanonicalRole.Subcoordination);
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
var planningRoleCheckTime = clock.UtcNow;
clock.UtcNow = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);
var adminPage = await direction.ListAsync(planningAdmin.User, 1, null);
var directorPage = await direction.ListAsync(director.User, 1, null);
Check(adminPage.Plans.Select(p => p.PlanId).SequenceEqual(directorPage.Plans.Select(p => p.PlanId)), "Administration and Direction consult the same active plans");
if (adminPage.Plans.Count > 0)
{
    var detail = await direction.ReadAsync(planningAdmin.User, adminPage.Plans[0].PlanId);
    Check(detail.Assignments!.Count == adminPage.Plans[0].Assignments!.Count, "Administration can consult full plan details");
}
Check(adminPage.NextCursor is not null, "Planning role regression has enough plans to exercise pagination");
if (adminPage.NextCursor is { } adminCursor)
{
    Check(adminCursor.Role == CanonicalRole.Administration, "Administration pagination binds the current role");
    var next = await direction.ListAsync(planningAdmin.User, 1, adminCursor);
    Check(next.Plans.All(p => adminPage.Plans.All(first => first.PlanId != p.PlanId)), "Administration pagination advances without duplicate plans");
    await Rejected(() => direction.ListAsync(planningAdmin.User, 1, adminCursor with { Role = CanonicalRole.Direction }), "CURSOR_INVALIDO");
}
await Rejected(() => direction.AuthorizeAsync(planningAdmin.User), "ACCESO_DENEGADO");
await Rejected(() => direction.CreateAsync(planningAdmin.User, Guid.CreateVersion7(), Guid.CreateVersion7(), planInput), "ACCESO_DENEGADO");
Check((await direction.ListAsync(planningSub.User, 1, null)).Plans.Select(p => p.PlanId).SequenceEqual(directorPage.Plans.Select(p => p.PlanId)), "Subcoordination consults the same active plans");
await Rejected(() => direction.ListAsync(a.User, 20, null), "ACCESO_DENEGADO");
clock.UtcNow = planningRoleCheckTime;
await OccasionChecks.RunAsync(db, direction, tasks, supervision, clock, scanner, director.User, a.User);
await NormalizationChecks.RunAsync(db);
var beforeWeeklyClock = clock.UtcNow;
clock.UtcNow = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero);
await WeeklyOperationsChecks.RunAsync(db, direction, tasks, clock, director.User);
clock.UtcNow = beforeWeeklyClock;
Console.WriteLine("All operational task, supervision, specialization, plan deletion and planning role checks passed.");

if (args.Contains("--preview") || args.Contains("--preview-camera") || args.Contains("--preview-occasion") || args.Contains("--preview-weekly-operations"))
{
    if (args.Contains("--preview-weekly-operations")) clock.UtcNow = DateTimeOffset.UtcNow;
    var sourceRoot = new DirectoryInfo(AppContext.BaseDirectory);
    while (!File.Exists(Path.Combine(sourceRoot.FullName, "SistemaSGOL.slnx"))) sourceRoot = sourceRoot.Parent ?? throw new Exception("Source root not found.");
    var cameraPreview = args.Contains("--preview-camera");
    var occasionPreview = args.Contains("--preview-occasion");
    var occasionPerson = occasionPreview ? await db.People.SingleAsync(p => p.StableCode == "OCC-B") : null;
    var occasionUser = occasionPerson == null ? Guid.Empty : await db.AppUsers.Where(u => u.PersonId == occasionPerson.Id).Select(u => u.Id).SingleAsync();
    var previewActor = occasionPreview ? (User: occasionUser, Person: occasionPerson!.Id) : cameraPreview ? a : director;
    if (cameraPreview)
    {
        var cameraTask = EvidenceAssignment("FOTOGRAFIA"); cameraTask.Name = "Prueba de cámara · fotografías";
        cameraTask.PlanId = await db.DirectionPreparedPlans.Select(p => p.Id).FirstAsync(); cameraTask.AssignedRole = CanonicalRole.SalesFloor;
        db.Add(cameraTask); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await File.WriteAllTextAsync(Path.Combine(sourceRoot.FullName, ".local", "camera-preview-task.txt"), cameraTask.Id.ToString("D"));
    }
    var webRoot = Path.Combine(sourceRoot.FullName, "src", "Sgol.Web", "wwwroot");
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(SgolDbContext).Assembly.FullName, WebRootPath = webRoot, EnvironmentName = "Development" });
    builder.WebHost.UseUrls("https://localhost:7251", "http://localhost:7252");
    builder.Configuration["SgolApi:TrustedOrigin"] = "https://localhost:7251";
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly);
    builder.Services.AddHttpContextAccessor(); builder.Services.AddDataProtection(); builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "__Host-SGOL-CSRF"; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.Path = "/"; });
    builder.Services.AddSgolApiClient(builder.Configuration, builder.Environment);
    builder.Services.AddScoped<RazorAntiforgeryBridge>(); builder.Services.AddSingleton<AccessNotice>();
    builder.Services.AddDbContext<SgolDbContext>(o => o.UseNpgsql(connection));
    builder.Services.AddSingleton<IClock>(clock); builder.Services.AddSingleton<IUuidGenerator>(ids);
    builder.Services.AddScoped<AuditTransaction>();
    builder.Services.AddScoped<IDirectionPreparedPlanService, EfDirectionPreparedPlanService>();
    builder.Services.AddScoped<IDirectionPlanningCatalogReader, DirectionPlanningCatalogReader>();
    builder.Services.AddScoped<OperationalTaskService>();
    builder.Services.AddScoped<OperationalProcessCatalogService>();
    builder.Services.AddScoped<OperationalProcessService>();
    builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Organization.SpecializationService>();
    builder.Services.AddScoped<OperationalSupervisionService>();
    builder.Services.AddScoped<OperationalCapacityService>();
    builder.Services.AddSingleton<IFileTechnicalValidator, FileTechnicalValidator>(); builder.Services.AddSingleton<IFileMalwareScanner>(scanner);
    builder.Services.AddSingleton<IRazorSessionState>(new CheckSession(previewActor.User, previewActor.Person, cameraPreview || occasionPreview ? CanonicalRole.SalesFloor : CanonicalRole.Direction));
    var app = builder.Build();
    app.UseMiddleware<Sgol.Web.Infrastructure.Http.CorrelationIdMiddleware>();
    app.Use(async (ctx, next) => { foreach (var cookie in ctx.Request.Cookies.Keys.Where(k => k.StartsWith(".AspNetCore.Antiforgery."))) ctx.Response.Cookies.Delete(cookie); await next(); });
    app.Use(async (ctx, next) => { ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, previewActor.User.ToString())], "TEST_ONLY")); await next(); });
    app.UseMiddleware<Sgol.Web.Infrastructure.Authentication.AntiforgeryValidationMiddleware>();
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) }); app.MapRazorPages(); app.MapDirectionPreparedPlanApi();
    Console.WriteLine("Isolated test preview: https://localhost:7251/tareas");
    await app.RunAsync();
}

async Task<DirectionPlanInput> AnotherSweepPlan() {
    var name = "Balance check " + Guid.NewGuid().ToString("N");
    await tasks.SaveAsync(director.User, new OperationalTask { MaximumPhotos = 5, Name = name, Requirement = "COMENTARIO", PeriodType = "SEMANAL", Repetitions = 2, LoadMinutes = 20, DeadlineHours = 24, Revision = 0 }, default);
    db.ChangeTracker.Clear();
    var definition = (await tasks.ListAsync(director.User, default)).Single(t => t.Name == name);
    return planInput with { TaskCodes = [definition.TaskCode] };
}
(Guid User, Guid Person) AddPerson(string code, string role) {
    var person = Guid.CreateVersion7(); var user = Guid.CreateVersion7(); var from = clock.UtcNow.AddYears(-1);
    db.People.Add(new Person { Id = person, StableCode = code, DisplayName = code, CreatedAt = from });
    db.AppUsers.Add(new AppUser { Id = user, PersonId = person, Status = AccountStatus.Active, SecurityStamp = "checks" });
    db.EmploymentVersions.Add(new(Guid.CreateVersion7(), person, BranchScope.LorettaId, EmploymentStatus.Active, from));
    db.RoleAssignmentVersions.Add(new RoleAssignmentVersion { Id = Guid.CreateVersion7(), UserId = user, BranchId = BranchScope.LorettaId, RoleCode = role, Status = RoleAssignmentStatus.Active, ValidFrom = from });
    return (user, person);
}
OperationalAssignment EvidenceAssignment(string requirement) => new() { Id = Guid.CreateVersion7(), PlanId = saved.Plan.PlanId,
    TaskId = Guid.Parse("019d3a10-0018-7000-8000-000000000018"), TaskCode = "TAR-0018", Name = "Evidence check",
    PersonId = a.Person, PersonName = "A", Requirement = requirement, LoadMinutes = 10,
    StartsAt = clock.UtcNow.AddHours(-1), DueAt = clock.UtcNow.AddDays(1) };
static void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS: " + name); }
static async Task Rejected(Func<Task> action, string code) {
    try { await action(); throw new Exception("Expected rejection: " + code); }
    catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS: Rejected " + code); }
}
sealed class CheckClock : IClock { public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 5, 18, 0, 0, TimeSpan.Zero); }
sealed class CheckScanner : IFileMalwareScanner {
    public EvidenceScanResult Outcome { get; set; } = EvidenceScanResult.Limpio;
    public Task CheckAvailabilityAsync(CancellationToken token) => Task.CompletedTask;
    public Task<EvidenceScanOutcome> ScanAsync(Stream content, long length, CancellationToken token) => Task.FromResult(new EvidenceScanOutcome(Outcome));
}
sealed class CheckSession(Guid user, Guid person, string role = CanonicalRole.Direction) : IRazorSessionState {
    public bool IsInvalid => false;
    public void Invalidate() { }
    public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(new(user, person, "check", "Cuenta de prueba", "LOR-001", role,
        [WeekAuthorization.View, "PER-PERSONA-ADMIN", "PER-BANDEJA-PROPIA", "PER-SUPERVISION-VER"], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2)));
}



