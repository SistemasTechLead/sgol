using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Planning.Contracts;
using Sgol.Organization.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Sgol.Identity.Contracts;
using System.Security.Claims;
using Sgol.Web.Presentation.Endpoints;

var connection = Environment.GetEnvironmentVariable("SGOL_SCHEDULE_CONNECTION") ?? throw new Exception("Set SGOL_SCHEDULE_CONNECTION.");
var builder = new NpgsqlConnectionStringBuilder(connection);
if (builder.Host != "localhost" || builder.Database is not { } database || !(database.StartsWith("sgol_task_checks_") || database == "sgol_local")) throw new Exception("Local databases only.");
await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection).Options);
var clock = new SystemClock(); var now = clock.UtcNow; var today = WeekContract.LocalToday(now);
var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
if (args.Contains("inventory"))
{
    await db.Database.OpenConnectionAsync();
    var names = new List<string>();
    await using (var cmd = db.Database.GetDbConnection().CreateCommand()) {
        cmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename";
        await using var reader = await cmd.ExecuteReaderAsync(); while (await reader.ReadAsync()) names.Add(reader.GetString(0));
    }
    var result = new SortedDictionary<string, object>();
    foreach (var name in names)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        var projection = name == "operational_task" ? "to_jsonb(t) - ARRAY['StartMinute','ExecutionPolicy','DayStartMinutesJson','Splittable','ClosingGraceMinutes','SundayStartMinute']" : name == "operational_assignment" ? "to_jsonb(t) - ARRAY['TemporalPolicyJson','TemporalDecisionJson']" : "to_jsonb(t)";
        cmd.CommandText = $"SELECT count(*), coalesce(string_agg(row::text, E'\\n' ORDER BY row::text),'') FROM (SELECT {projection} AS row FROM \"{name.Replace("\"", "\"\"")}\" t) s";
        await using var reader = await cmd.ExecuteReaderAsync(); await reader.ReadAsync();
        result[name] = new { Count = reader.GetInt64(0), Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reader.GetString(1)))) };
    }
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })); return;
}
if (args.Contains("migrate")) { await db.Database.MigrateAsync(); Console.WriteLine("Migration applied; pending model changes: " + db.Database.HasPendingModelChanges()); return; }
var den = Guid.Parse("01a13c6f-2e3a-7de7-bda5-de5218c9983c"); var ale = Guid.Parse("01a13c6f-2d9e-735f-b73c-0cfca472228b"); var viri = Guid.Parse("01a13c6f-2dd2-715e-b334-db514f4cf726"); var mar = Guid.Parse("01a13c6f-2e06-7456-bf83-2937b68d64aa"); var director = Guid.Parse("01a13c6f-2ccd-7b0f-add9-9d3ff77f5885");
var ids = new[] { den, ale, viri, mar, director };
var accounts = await db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.PersonId)).ToArrayAsync();
if (accounts.Length != 5) throw new Exception("Verified account inventory differs.");
var actor = accounts.Single(u => u.PersonId == director).Id; await direction.AuthorizeAsync(actor, default);
if (args.Contains("set-sunday-opening-10"))
{
    var task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.TaskCode == "TAR-0062" && t.Active && !t.Deleted);
    if (!task.IsWeeklyOperationsTask || task.StartHour != 9 || task.StartMinute != 0 || task.SundayStartMinute is not (null or 600))
        throw new Exception("Opening task differs from the reviewed configuration.");
    if (task.SundayStartMinute == 600) { Console.WriteLine("Sunday opening already configured at 10:00; no changes."); return; }
    Console.WriteLine($"Before: {task.TaskCode}, base=09:00, Sunday=null, revision={task.Revision}");
    task.SundayStartMinute = 600;
    var service = new OperationalTaskService(db, direction, clock, new Sgol.Evidence.Technical.FileTechnicalValidator(), new PreviewScanner());
    await service.SaveAsync(actor, task, default);
    db.ChangeTracker.Clear();
    var saved = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
    if (saved.SundayStartMinute != 600 || saved.Revision != task.Revision + 1) throw new Exception("Unexpected saved opening configuration.");
    Console.WriteLine($"Saved and audited: {saved.TaskCode}, base=09:00, Sunday=10:00, revision={saved.Revision}");
    return;
}
if (args.Contains("diagnose-week") || args.Contains("verify-week"))
{
    var codes=await db.OperationalTasks.AsNoTracking().Where(t=>t.Active && !t.Deleted && t.IsWeeklyOperationsTask).OrderBy(t=>t.TaskCode).Select(t=>t.TaskCode).ToArrayAsync();
    var operationsWeek=DirectionPlanContract.OperationsWeek(today);
    var input=new DirectionPlanInput("Verificación semanal aislada","Tienda","SEMANAL",operationsWeek.Year,operationsWeek.Week,codes,IsWeeklyOperationsPlan:true);
    if (args.Contains("--recreate-isolated")) {
        if (database=="sgol_local" || !args.Contains("verify-week")) throw new Exception("Recreation requires verify-week against an isolated database.");
        var old=await db.DirectionPreparedPlans.AsNoTracking().Where(p=>p.IsWeeklyOperationsPlan).Select(p=>p.Id).ToArrayAsync();
        foreach(var id in old) {
            var preview=await direction.PreviewDeletionAsync(actor,id);
            await direction.DeleteAsync(actor,id,Guid.CreateVersion7(),Guid.CreateVersion7(),preview.Fingerprint);
            db.ChangeTracker.Clear();
        }
    }
    var state=await PlanningCapacity.ReadAsync(db,input,now,default);
    var groups=state.Preview.TemporalRows.GroupBy(r=>new { r.Day,r.Task }).ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new {
        Now=TimeZoneInfo.ConvertTime(now,OperationalSchedule.Zone),
        Expired=groups.Count(g=>g.All(r=>r.Reason=="SCHEDULE_DEADLINE_PASSED")),
        Compatible=groups.Count(g=>g.Any(r=>r.Eligible)),
        Uncovered=groups.Where(g=>!g.Any(r=>r.Eligible) && !g.All(r=>r.Reason=="SCHEDULE_DEADLINE_PASSED")).Select(g=>new { g.Key.Day,g.Key.Task,Reasons=g.Select(r=>new {r.Person,r.Reason,r.Hours}) }),
        Glass=state.Preview.TemporalRows.Where(r=>r.Task.Contains("vidrio de adentro",StringComparison.OrdinalIgnoreCase))
    },new JsonSerializerOptions { WriteIndented=true }));
    if(args.Contains("verify-week")) {
        if(database=="sgol_local") throw new Exception("Plan creation verification requires an isolated database.");
        var saved=await direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),input with {CapacityFingerprint=state.Preview.Fingerprint});
        Console.WriteLine($"Verified isolated weekly plan: {saved.Plan.Assignments!.Count} assignments.");
        Console.WriteLine(JsonSerializer.Serialize(saved.Plan.Assignments.GroupBy(t=>new {Day=WeekContract.LocalToday(t.StartsAt),t.PersonName})
            .OrderBy(g=>g.Key.Day).ThenBy(g=>g.Key.PersonName).Select(g=>new {g.Key.Day,g.Key.PersonName,Minutes=g.Sum(t=>t.LoadMinutes)}),new JsonSerializerOptions {WriteIndented=true}));
    }
    return;
}
if (args.Contains("preview-ui"))
{
    if (database == "sgol_local") throw new Exception("UI test server requires an isolated database.");
    var web = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = Path.GetFullPath("src/Sgol.Web"), ApplicationName = typeof(SgolDbContext).Assembly.FullName });
    web.WebHost.UseUrls("https://localhost:7268"); web.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly);
    web.Services.AddHttpContextAccessor(); web.Services.AddDataProtection(); web.Services.AddAntiforgery();
    web.Configuration["SgolApi:TrustedOrigin"] = "https://localhost:7268";
    web.Services.AddSgolApiClient(web.Configuration, web.Environment);
    web.Services.AddScoped<RazorAntiforgeryBridge>(); web.Services.AddSingleton<AccessNotice>();
    web.Services.AddScoped(_=>new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection).Options));
    web.Services.AddSingleton<IClock>(clock); web.Services.AddScoped<IDirectionPreparedPlanService>(_=>direction);
    web.Services.AddSingleton<Sgol.BuildingBlocks.Identifiers.IUuidGenerator>(new Uuid7Generator(clock));
    web.Services.AddScoped<PersonalScheduleService>(); web.Services.AddScoped<UserProfileService>(); web.Services.AddScoped<SpecializationService>(); web.Services.AddScoped<AuditTransaction>();
    web.Services.AddScoped<IPasswordHasher<AppUser>,PasswordHasher<AppUser>>();
    web.Services.AddScoped<OperationalTaskService>(); web.Services.AddScoped<OperationalProcessCatalogService>();
    web.Services.AddScoped<OperationalProcessService>();
    web.Services.AddScoped<Sgol.Configuration.Contracts.IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();
    web.Services.AddSingleton<Sgol.Evidence.Contracts.IFileTechnicalValidator,Sgol.Evidence.Technical.FileTechnicalValidator>();
    web.Services.AddSingleton<Sgol.Evidence.Contracts.IFileMalwareScanner,PreviewScanner>();
    var workerPreview=args.Contains("--worker"); var previewPerson=workerPreview?mar:director;
    var previewActor=accounts.Single(u=>u.PersonId==previewPerson).Id;
    web.Services.AddSingleton<IRazorSessionState>(new PreviewSession(previewActor,previewPerson,workerPreview?CanonicalRole.SalesFloor:CanonicalRole.Direction));
    var app=web.Build(); app.UseMiddleware<Sgol.Web.Infrastructure.Http.CorrelationIdMiddleware>(); app.UseStaticFiles(); app.UseRouting();
    app.Use(async(ctx,next)=> { ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,previewActor.ToString())],"ISOLATED_TEST")); await next(); });
    app.MapRazorPages(); app.MapDirectionPreparedPlanApi(); Console.WriteLine("Isolated UI preview https://localhost:7268/personas-y-accesos"); await app.RunAsync(); return;
}
if(args.Contains("seed-preview"))
{
    if(database=="sgol_local") throw new Exception("Fixtures require an isolated database.");
    var existing=await db.DirectionPreparedPlans.Where(p=>p.Name=="Vista aislada de horarios").Select(p=>(Guid?)p.Id).SingleOrDefaultAsync();
    if(existing!=null) { Console.WriteLine(await db.OperationalAssignments.Where(a=>a.PlanId==existing).Select(a=>a.Id).SingleAsync()); return; }
    var day=today; while(day.DayOfWeek!=DayOfWeek.Sunday) day=day.AddDays(1);
    var task=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskCode=="TAR-0200");
    var context=await TemporalEligibilityContext.LoadAsync(db,now,default);
    var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,actor,now,"Vista aislada de horarios",day,day);
    var assignment=new OperationalAssignment { Id=Guid.CreateVersion7(),TaskId=task.Id,TaskRevision=task.Revision,TaskCode=task.TaskCode,Name=task.Name+" · ejemplo aislado",PlanId=plan.Id,
        PersonId=mar,PersonName="Mariana",AssignedRole=task.AssignedRole,Requirement=task.Requirement,LoadMinutes=task.LoadMinutes,StartsAt=task.StartOn(day),DueAt=TemporalEligibilityContext.At(day,900),
        };
    assignment.TemporalDecisionJson=JsonSerializer.Serialize(context.Evaluate(mar,assignment,[]));
    db.Add(plan);db.Add(assignment);await db.SaveChangesAsync();Console.WriteLine(assignment.Id);return;
}
if (args.Contains("enable") || args.Contains("disable"))
{
    await using var tx = await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734651001)");
    var enable=args.Contains("enable");
    var state=await db.Set<TemporalAssignmentState>().SingleAsync();
    if(state.Enabled!=enable) {
        state.Enabled=enable; state.Revision++;
        db.Add(new AuditEvent { Id=Guid.CreateVersion7(),ActorUserId=actor,ActorType="USER",OccurredAt=now,BranchId=BranchScope.LorettaId,ResourceId=actor,ResourceType="PERSONAL_SCHEDULE",Action=enable?"TEMPORAL_POLICY_ENABLED":"TEMPORAL_POLICY_DISABLED",Outcome="SUCCESS",CorrelationId=Guid.CreateVersion7(),AfterData=JsonSerializer.SerializeToDocument(new { state.Enabled,state.Revision }) });
        await db.SaveChangesAsync();
    }
    await tx.CommitAsync(); Console.WriteLine($"Temporal policy enabled: {enable}. Existing assignments preserved."); return;
}
if(args.Contains("simplify"))
{
    await using var tx=await db.Database.BeginTransactionAsync(); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734651001)");
    var closing=await db.OperationalTasks.SingleAsync(t=>t.TaskCode=="TAR-0200" && t.Active && !t.Deleted);
    var state=await db.Set<TemporalAssignmentState>().SingleAsync();
    if(state.ClosingTaskId!=closing.Id) {
        state.ClosingTaskId=closing.Id; state.Revision++;
        db.Add(new AuditEvent { Id=Guid.CreateVersion7(),ActorUserId=actor,ActorType="USER",OccurredAt=now,BranchId=BranchScope.LorettaId,ResourceId=closing.Id,ResourceType="CLOSING_PLANNING",Action="SIMPLE_SCHEDULE_CONFIGURATION",Outcome="SUCCESS",CorrelationId=Guid.CreateVersion7(),AfterData=JsonSerializer.SerializeToDocument(new { state.ClosingTaskId,state.Revision }) });
        await db.SaveChangesAsync();
    }
    await tx.CommitAsync(); Console.WriteLine("Shared closing configured. No task policies, Sunday whitelist or automatic rotation."); return;
}var ledger = await OperationalCapacityService.LedgerAsync(db, now, default);
var temporal = new TemporalEligibilityContext { Enabled = true, Now = now, Schedules = ledger.Temporal.Schedules, Exceptions = ledger.Temporal.Exceptions, Rotations = ledger.Temporal.Rotations, Coverages = ledger.Temporal.Coverages, ClosingTaskId=ledger.Temporal.ClosingTaskId };
var active = await db.OperationalTasks.AsNoTracking().Where(t => t.Active && !t.Deleted).OrderBy(t => t.TaskCode).ToArrayAsync();

Console.WriteLine("Simulated closing and Sunday occurrences (no assignments written):");
foreach (var date in new[] { new DateOnly(2026,10,18), new(2026,10,21), new(2026,10,28), new(2026,11,4), new(2027,1,6) })
foreach (var task in active.Where(t => t.Id == temporal.ClosingTaskId || date.DayOfWeek == DayOfWeek.Sunday && new[] { "TAR-0201", "TAR-0224" }.Contains(t.TaskCode)))
{
    var assignment = new OperationalAssignment { TaskId = task.Id, StartsAt = task.StartOn(date), DueAt = task.StartOn(date).AddHours(task.DeadlineHours), LoadMinutes = task.LoadMinutes };
    foreach (var worker in ledger.Workers.Where(w => w.Role == task.AssignedRole || task.Id==temporal.ClosingTaskId && temporal.ClosingPerson(date)==w.Id)) {
        var decision = temporal.Evaluate(worker.Id, assignment, ledger.Unavailable);
        var roleValid=worker.Role==task.AssignedRole;
        Console.WriteLine($"{date:yyyy-MM-dd} {task.TaskCode} {worker.Name}: {decision.Eligible && roleValid} {(roleValid?decision.Reason:"SCHEDULE_ROLE_CONFLICT")} limit-local={(decision.Limit is {} limit?TimeZoneInfo.ConvertTime(limit,OperationalSchedule.Zone).ToString("yyyy-MM-dd HH:mm"):"—")}");
    }
}
Console.WriteLine("Other Sunday occurrences:");
foreach (var task in active.Where(t => !new[] { "TAR-0200", "TAR-0201", "TAR-0224" }.Contains(t.TaskCode) && OperationalSchedule.Dates(new(2026,10,18),new(2026,11,22),t).Any(d => d.DayOfWeek == DayOfWeek.Sunday))) Console.WriteLine($"{task.TaskCode}: {task.Name}");
Console.WriteLine("Existing future assignment conflicts:");
foreach (var assignment in await db.OperationalAssignments.AsNoTracking().Where(a => a.CompletedAt == null && a.DueAt >= now).OrderBy(a => a.Id).ToArrayAsync()) {
    var decision = temporal.Evaluate(assignment.PersonId, assignment, ledger.Unavailable);
    if (!decision.Eligible) Console.WriteLine($"{assignment.Id} {assignment.TaskCode} {assignment.StartsAt:O}: {decision.Reason}");
}

sealed class PreviewScanner : Sgol.Evidence.Contracts.IFileMalwareScanner
{
    public Task CheckAvailabilityAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<Sgol.Evidence.Contracts.EvidenceScanOutcome> ScanAsync(Stream stream,long size,CancellationToken ct=default) => throw new NotSupportedException("UI-only preview does not accept evidence.");
}
sealed class PreviewSession(Guid actor,Guid person,string role) : IRazorSessionState
{
    public bool IsInvalid => false;
    public void Invalidate() { }
    public Task<SessionSnapshot?> GetAsync(CancellationToken ct=default) => Task.FromResult<SessionSnapshot?>(new(actor,person,"isolated","Vista aislada","LOR-001",role,
        [WeekAuthorization.View,"PER-PERSONA-ADMIN","PER-BANDEJA-PROPIA","PER-SUPERVISION-VER"],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(2)));
}
