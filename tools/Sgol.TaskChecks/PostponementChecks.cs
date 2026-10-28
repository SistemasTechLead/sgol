using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Organization.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Infrastructure.Persistence.Identity;
using System.Security.Claims;

internal static class PostponementChecks
{
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    public static void Rules()
    {
        var today = new DateOnly(2026, 10, 27); var person = Guid.NewGuid();
        var worker = new CapacityWorker(person, "Fixture", "POST", CanonicalRole.SalesFloor, null, null);
        var temporal = new TemporalEligibilityContext { Enabled = true, Schedules = [new() { Id = Guid.NewGuid(), PersonId = person, Revision = 1,
            DaysJson = JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(i => i, i => new WorkInterval[] {new(540,780),new(900,1080)})) }] };
        var ledger = new CapacityLedger([worker], [], [], [], [], today) { Temporal = temporal };
        var days = CapacityLedger.Days(today.AddDays(1), today.AddDays(7)).Select(d => new PostponementDay(d, 0, d.DayOfWeek == DayOfWeek.Monday ? 210 : 60, 0, 240, d.AddDays(-7))).ToArray();
        OperationalAssignment Task(int minutes) => new() { Id = Guid.NewGuid(), PersonId = person, AssignedRole = worker.Role, TaskId = Guid.NewGuid(),
            TaskCode = "TEST", LoadMinutes = minutes, SchedulingPolicyId = OperationalSchedulingPolicy.InitialId, ClosureMarginPercent = 50,
            OperationalCriticality = "FLEXIBLE", StartsAt = TemporalEligibilityContext.At(today.AddDays(-1),540), DueAt = TemporalEligibilityContext.At(today.AddDays(-1),600) };
        var rows = new[] { Task(60), Task(60), Task(60), Task(60), Task(60), Task(60), Task(60) };
        var result = PostponementAllocator.Calculate(rows, ledger, days);
        Require(result.Conflicts.Length == 0 && result.Items.Length == rows.Length, "All selected overdue assignments receive proposals");
        Require(result.Items.All(i => WeekContract.LocalToday(i.ProposedStart!.Value) > today && WeekContract.LocalToday(i.Proposed) <= today.AddDays(7)), "Tuesday request uses Wednesday through next Tuesday inclusive");
        Require(result.Days.Single(d => d.Day.DayOfWeek == DayOfWeek.Monday).Added == 0, "Historical Monday load prevents treating next unplanned Monday as empty");
        Require(result.Days.All(d => d.Actual + d.Forecast + d.Added <= d.Capacity), "Forecast plus selected demand never exceeds daily capacity");
        Require(result.Items.All(i => TimeZoneInfo.ConvertTime(i.ProposedStart!.Value, OperationalSchedule.Zone).Hour >= 9 && TimeZoneInfo.ConvertTime(i.Proposed, OperationalSchedule.Zone).Hour <= 18), "Proposals stay inside configured shifts");
        Require(JsonSerializer.Serialize(result.Items) == JsonSerializer.Serialize(PostponementAllocator.Calculate(rows.Reverse().ToArray(), ledger, days).Items), "Input selection order does not change distribution");
        var impossible = PostponementAllocator.Calculate([Task(500)], ledger, days);
        Require(impossible.Conflicts.Length == 1 && impossible.Items.Length == 0, "Insufficient capacity returns explicit conflict");
        var critical = Task(10); critical.OperationalCriticality = "CRITICA";
        var criticalResult = PostponementAllocator.Calculate([critical], ledger, days);
        Require(criticalResult.Items.Single().Warning.Contains("Crítica") && TimeZoneInfo.ConvertTime(criticalResult.Items.Single().ProposedStart!.Value, OperationalSchedule.Zone).Hour == 9, "Critical tasks preserve time of day and warn about authorization");
    }
    private static async Task Reject(Func<Task> action, string code) {
        try { await action(); throw new Exception("Expected " + code); } catch (DirectionPlanException e) { Require(e.Code == code, "Rejected with " + code); }
    }
    public static async Task InboxCurrent(SgolDbContext db)
    {
        var clock = new TestClock { UtcNow = DateTimeOffset.UtcNow };
        var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var supervision = new OperationalSupervisionService(db,clock,direction);
        var service = new OperationalCarryoverService(db,clock,supervision);
        var workers = await OperationalCapacityService.WorkersAsync(db,clock.UtcNow,default);
        var users = await db.AppUsers.AsNoTracking().ToArrayAsync();
        var requester = workers.Single(w=>w.Role==CanonicalRole.Subcoordination);
        var request = await db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r=>r.PersonId==requester.Id && r.Kind=="POSPONER" && r.State=="PENDIENTE").OrderByDescending(r=>r.SubmittedAt).FirstAsync();
        var director = users.Single(u=>u.PersonId==workers.Single(w=>w.Role==CanonicalRole.Direction).Id).Id;
        var administrator = users.Single(u=>u.PersonId==workers.Single(w=>w.Role==CanonicalRole.Administration).Id).Id;
        Require((await service.MineAsync(request.RequestedBy,default)).Any(r=>r.Id==request.Id),"The actual subcoordinator request remains in her own history");
        Require((await service.InboxAsync(director,default)).Any(r=>r.Id==request.Id),"Direction sees the actual pending request even with active Administration");
        Require((await service.InboxAsync(administrator,default)).Any(r=>r.Id==request.Id),"The immediate supervisor retains access to the same request");
        Require(!(await service.InboxAsync(request.RequestedBy,default)).Any(r=>r.Id==request.Id),"Requester cannot receive her own request for approval");
        var floor = users.First(u=>workers.Any(w=>w.Id==u.PersonId && w.Role==CanonicalRole.SalesFloor)).Id;
        await Reject(()=>service.InboxAsync(floor,default),"ACCESO_DENEGADO");
        var before = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync());
        await Reject(()=>service.DecideAsync(request.RequestedBy,request.Id,request.Revision,true,"Autoaprobación aislada",default),"ACCESO_DENEGADO");
        // Exercise the actual approval route only in the runner's guarded restored database.
        // A stale capacity context remains a legitimate block; it must never be mistaken for missing authority.
        try {
            await service.DecideAsync(director,request.Id,request.Revision,true,"Aprobación de prueba exclusivamente en copia aislada",default);
            Require((await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==request.Id)).State=="APROBADA","Direction can approve through the actual decision service");
        } catch(DirectionPlanException e) {
            Require(e.Code=="ARRASTRE_CONFLICTO","Any approval block is a stale snapshot, not Direction authorization");
            Require(before==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),"Stale approval preserves all assignments");
            await service.DecideAsync(director,request.Id,request.Revision,false,"Rechazo de prueba exclusivamente en copia aislada",default);
            Require((await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==request.Id)).State=="RECHAZADA","Direction can decide a stale request without changing assignments");
        }
    }
    public static async Task Ui(SgolDbContext db)
    {
        var clock = new TestClock { UtcNow = DateTimeOffset.UtcNow };
        var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var service = new OperationalCarryoverService(db,clock,new OperationalSupervisionService(db,clock,direction));
        var workers = await OperationalCapacityService.WorkersAsync(db,clock.UtcNow,default);
        var users = await db.AppUsers.AsNoTracking().ToArrayAsync();
        var payroll = Environment.GetEnvironmentVariable("SGOL_POSTPONEMENT_UI_PAYROLL") == "true";
        if (payroll) {
            var directorActor=users.Single(u=>u.PersonId==workers.Single(w=>w.Role==CanonicalRole.Direction).Id).Id;
            await service.RestorePostponementAsync(directorActor,Guid.Parse("01a17efe-16a1-710e-8755-281ad35133be"),
                [Guid.Parse("01a1797f-b6bb-7146-b1b3-12448b6f8875"),Guid.Parse("01a1797f-b6bf-7e28-afa8-68ced8fc3b78")],"Prueba visual exclusivamente en copia aislada",default);
        }
        var actor = Guid.Empty; CapacityWorker? worker = null;
        foreach(var u in users) {
            if (payroll && workers.All(w=>w.Id!=u.PersonId || w.Name!="Viri")) continue;
            try { if ((await service.OverdueAsync(u.Id,default)).Count(t=>t.Blocked==null)>=2) { actor=u.Id; worker=workers.Single(w=>w.Id==u.PersonId); break; } } catch(DirectionPlanException) { }
        }
        Require(actor!=Guid.Empty,"UI uses a real worker with multiple current overdue tasks in the restored copy");
        var root = new DirectoryInfo(AppContext.BaseDirectory); while(!File.Exists(Path.Combine(root.FullName,"SistemaSGOL.slnx"))) root=root.Parent!;
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName=typeof(SgolDbContext).Assembly.FullName, WebRootPath=Path.Combine(root.FullName,"src","Sgol.Web","wwwroot"), EnvironmentName="Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:7258"); builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7258";
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddRazorPages(o=>o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(RoleScreenAccessFilter)))).AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddScoped<RoleScreenAccessFilter>(); builder.Services.AddHttpContextAccessor(); builder.Services.AddDataProtection();
        builder.Services.AddAntiforgery(o=> { o.Cookie.Name="SGOL-POSTPONEMENT-TEST-CSRF"; o.Cookie.SecurePolicy=CookieSecurePolicy.None; });
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment); builder.Services.AddScoped<RazorAntiforgeryBridge>(); builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString())); builder.Services.AddSingleton<IClock>(clock); builder.Services.AddSingleton<IUuidGenerator,Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>(); builder.Services.AddScoped<IDirectionPreparedPlanService,EfDirectionPreparedPlanService>(); builder.Services.AddScoped<IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalCarryoverService>(); builder.Services.AddScoped<OperationalSupervisionService>();
        builder.Services.AddScoped<OperationalCarryoverReader>();
        builder.Services.AddScoped<IPersonAvatarReader,PersonAvatarService>();
        builder.Services.AddSingleton<IRazorSessionState>(new UiSession(actor,worker!));
        await using var app=builder.Build();
        app.Use(async(ctx,next)=> { ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,actor.ToString())],"ISOLATED_TEST_ONLY")); await next(); });
        app.UseStaticFiles(); app.MapRazorPages();
        Console.WriteLine("ISOLATED UI http://127.0.0.1:7258/mi-trabajo/arrastre · " + worker!.Name + " · " + db.Database.GetDbConnection().Database);
        await app.RunAsync();
    }
    private sealed class UiSession(Guid actor,CapacityWorker worker):IRazorSessionState
    {
        public bool IsInvalid=>false; public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken token=default)=>Task.FromResult<SessionSnapshot?>(new(actor,worker.Id,"isolated-test",worker.Name,"LOR-001",worker.Role,[],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(2)));
    }
    public static async Task Run(SgolDbContext db)
    {
        Rules();
        Require(!db.Database.HasPendingModelChanges(), "Migration snapshot matches the runtime EF model");
        var clock = new TestClock { UtcNow = TemporalEligibilityContext.At(new(2026,10,27), 12*60) };
        var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var supervision = new OperationalSupervisionService(db,clock,direction); var service = new OperationalCarryoverService(db,clock,supervision);
        var users = await db.AppUsers.AsNoTracking().ToArrayAsync();
        PostponementPreview? proposal = null; Guid actor = default; Guid[] selected = [];
        foreach (var user in users) {
            try {
                var eligible = (await service.OverdueAsync(user.Id,default)).Where(r => r.Blocked == null).Select(r => r.Assignment.Id).Take(2).ToArray();
                if (eligible.Length < 2) continue;
                var p = await service.PreviewPostponementAsync(user.Id,eligible,"FALTA_TIEMPO_OPERATIVO","Prueba aislada",default);
                Console.WriteLine($"CANDIDATE {user.PersonId}: {eligible.Length} selected; {string.Join("; ",p.Conflicts)}");
                if (p.Conflicts.Length == 0) { proposal = p; actor = user.Id; selected = eligible; break; }
            } catch (DirectionPlanException) { }
        }
        Require(proposal != null, "Real current assignments support a multiple-selection postponement");
        async Task<string> Snapshot() => JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync());
        var before = await Snapshot();
        var intent = Guid.NewGuid();
        var id = await service.SubmitPostponementAsync(actor, selected,"FALTA_TIEMPO_OPERATIVO","Prueba aislada",proposal!.Fingerprint,intent,default);
        Require(before == await Snapshot(), "Submitting preserves all current assignment fields");
        Require(id == await service.SubmitPostponementAsync(actor,selected,"FALTA_TIEMPO_OPERATIVO","Prueba aislada",proposal.Fingerprint,intent,default), "Double submission is idempotent");
        await Reject(()=>service.PreviewPostponementAsync(actor,selected,"FALTA_TIEMPO_OPERATIVO","Prueba aislada",default),"ARRASTRE_CONFLICTO");
        await Reject(()=>service.DecideAsync(actor,id,1,true,"Auto",default),"ACCESO_DENEGADO");
        Guid supervisor = default;
        foreach (var user in users.Where(u => u.Id != actor)) {
            try { if ((await service.InboxAsync(user.Id,default)).Any(r=>r.Id==id)) { supervisor = user.Id; break; } } catch(DirectionPlanException) { }
        }
        Require(supervisor != Guid.Empty, "Immediate active supervisor receives the request");
        await service.DecideAsync(supervisor,id,1,false,"Rechazo aislado",default);
        Require(before == await Snapshot(), "Rejecting preserves every assignment field");
        proposal = await service.PreviewPostponementAsync(actor,selected,"FALTA_TIEMPO_OPERATIVO","Prueba aislada",default);
        id = await service.SubmitPostponementAsync(actor,selected,"FALTA_TIEMPO_OPERATIVO","Prueba aislada",proposal.Fingerprint,Guid.NewGuid(),default);
        // Change a competing commitment: approval must be atomic and the original request remains immutable.
        var requestPerson = users.Single(u=>u.Id==actor).PersonId;
        var capacity = await db.Set<OperationalCapacity>().FirstOrDefaultAsync(c=>c.PersonId==requestPerson);
        var newCapacity = capacity == null;
        capacity ??= new() { Id=Guid.NewGuid(), PersonId=requestPerson, From=proposal.From, Through=proposal.Through, DailyMinutes=240, Reason="Prueba aislada" };
        var previousMinutes = capacity.DailyMinutes;
        capacity.DailyMinutes = previousMinutes > 1 ? previousMinutes - 1 : 2;
        if (newCapacity) db.Add(capacity);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var staleBefore = await Snapshot();
        await Reject(()=>service.DecideAsync(supervisor,id,1,true,"Cambio de capacidad",default),"ARRASTRE_CONFLICTO");
        Require(staleBefore == await Snapshot(), "Changed capacity rejects approval without partial writes");
        capacity = await db.Set<OperationalCapacity>().SingleAsync(c=>c.Id==capacity.Id);
        if (newCapacity) db.Remove(capacity); else capacity.DailyMinutes = previousMinutes;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var r = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==id);
        if (r.State == "PENDIENTE") {
            var delivered = await db.OperationalAssignments.SingleAsync(a=>a.Id==selected[0]);
            delivered.CompletedAt = clock.UtcNow; delivered.ReviewStatus = "PENDIENTE_REVISION"; delivered.Revision++;
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var deliveredBefore = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==delivered.Id));
            var othersBefore = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>!selected.Contains(a.Id)).OrderBy(a=>a.Id).ToArrayAsync());
            await service.DecideAsync(supervisor,id,1,true,"Aprobación aislada",default); db.ChangeTracker.Clear();
            Require(deliveredBefore == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==delivered.Id)), "Delivery during approval wait preserves every field");
            Require(othersBefore == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>!selected.Contains(a.Id)).OrderBy(a=>a.Id).ToArrayAsync()), "Approval leaves unrelated assignments unchanged");
            var moved = await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==selected[1]); var item = proposal.Items.Single(i=>i.AssignmentId==moved.Id);
            Require(moved.StartsAt == item.ProposedStart && moved.DueAt == item.Proposed, "Approval applies exactly the reviewed future slot");
            var nextSelection = (await service.OverdueAsync(actor,default)).First(t=>t.Blocked==null).Assignment.Id;
            var followup = await service.PreviewPostponementAsync(actor,[nextSelection],"FALTA_TIEMPO_OPERATIVO","Siguiente lote aislado",default);
            var targetDay = WeekContract.LocalToday(moved.StartsAt);
            var targetLoad = followup.Days.Single(d=>d.Day==targetDay);
            Require(targetLoad.Actual >= moved.RemainingMinutes, "The next request sees the approved backlog as a real capacity reservation");
            if (targetLoad.Reference is {} reference) {
                var originalRows = JsonSerializer.Deserialize<OperationalAssignment[]>(before)!;
                var historic = originalRows.Where(a=>a.PersonId==moved.PersonId && a.PeriodType=="SEMANAL" && a.GoalUnits==0 && a.ReviewStatus!="REASIGNADA" && WeekContract.LocalToday(a.StartsAt)==reference).Sum(a=>(decimal)a.LoadMinutes);
                var currentLedger = await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
                var routine = currentLedger.Tasks.Where(a=>a.PersonId==moved.PersonId && a.PeriodType=="SEMANAL" && a.GoalUnits==0 && a.OccasionId==null && a.Id!=moved.Id && a.Id!=nextSelection)
                    .Sum(a=>currentLedger.Footprint(a).Where(f=>f.Day==targetDay).Sum(f=>f.Minutes));
                Require(targetLoad.Forecast == Math.Max(0,historic-routine), "Approved backlog does not replace ordinary forecast demand in the next request");
            }
            await service.DecideAsync(supervisor,id,1,true,"Aprobación aislada",default);
            Require((await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r=>r.Id==id)).Results().Single(x=>x.AssignmentId==delivered.Id).Kind == "ENTREGADA_SIN_CAMBIOS", "Delivered task receives no retroactive postponement");
        }
    }
}
