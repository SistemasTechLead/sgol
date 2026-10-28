using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Search;

internal static class PerformanceIntegrityChecks
{
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow; }
    private sealed class Scanner : IFileMalwareScanner {
        public Task CheckAvailabilityAsync(CancellationToken t) => Task.CompletedTask;
        public Task<EvidenceScanOutcome> ScanAsync(Stream s, long n, CancellationToken t) => Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));
    }
    public static async Task SeedBrowserWorkloadAsync(SgolDbContext db)
    {
        var config = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        if(config.Host!="localhost"||config.Port!=5547||config.Database!="sgol_task_checks_performance")throw new Exception("Dedicated fixture required");
        var users = await db.IdentityCredentials.Where(c=>c.UserName=="perf-browser-before"||c.UserName=="perf-browser-after").Join(db.AppUsers,c=>c.UserId,u=>u.Id,(c,u)=>u).ToArrayAsync();
        if(users.Length!=2)throw new Exception("Seed both comparison accounts first");
        var task=await db.OperationalTasks.FirstAsync(); var now=DateTimeOffset.UtcNow;var day=WeekContract.LocalToday(now);
        var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,users[0].Id,now,"Browser volume comparison",day,day.AddDays(1));
        db.Add(plan);
        foreach(var user in users) {
            if(await db.OperationalAssignments.AnyAsync(a=>a.PersonId==user.PersonId))continue;
            for(var i=0;i<80;i++)db.Add(new OperationalAssignment {Id=Guid.CreateVersion7(),PlanId=plan.Id,TaskId=task.Id,TaskCode=task.TaskCode,Name="Asignación de navegador "+i,Description=string.Concat(Enumerable.Repeat("Descripción extensa para verificar teclado, lectura y adaptación. ",50)),PersonId=user.PersonId,PersonName="Synthetic browser performance",AssignedRole=CanonicalRole.Direction,Requirement="NINGUNO",LoadMinutes=1,StartsAt=now.AddMinutes(-30),DueAt=now.AddHours(2),OperationalCriticality="FLEXIBLE",CompletedAt=i<16?now.AddMinutes(-1):null,CompletedBy=i<16?user.Id:null,CompletionKind=i<16?"ENTREGA":null,ReviewStatus=i<8?"CONCLUIDA":i<16?"PENDIENTE_REVISION":null,ReviewedAt=i<8?now:null,ReviewedBy=i<8?users[0].Id:null});
        }
        await db.SaveChangesAsync(); Console.WriteLine("PASS synthetic browser workload: 80 assignments per comparison account, including historical deliveries");
    }
    public static async Task CancellationTransportAsync()
    {
        var config = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SGOL_TASK_CHECKS_CONNECTION"));
        if (config.Host != "localhost" || config.Port != 5547 || config.Database != "sgol_task_checks_performance") throw new Exception("Dedicated fixture required");
        foreach (var host in new[] { "localhost", "127.0.0.1" }) {
            config.Host = host;
            await using var connection = new NpgsqlConnection(config.ConnectionString); await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT pg_sleep(10)", connection);
            using var cancel = new CancellationTokenSource(50);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try { await command.ExecuteNonQueryAsync(cancel.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Console.WriteLine($"Cancellation transport {host}: {watch.Elapsed.TotalMilliseconds:F1}ms"); }
        }
    }
    public static async Task RunAsync(SgolDbContext db)
    {
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (connection.Host != "localhost" || connection.Port != 5547 || connection.Database != "sgol_task_checks_performance") throw new Exception("Dedicated performance fixture required");
        Check(!db.Database.HasPendingModelChanges(), "Migration model matches the current schema metadata");
        var clock = new Clock(); var ids = new Uuid7Generator(clock);
        var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction && r.ValidTo == null).Select(r => r.UserId).FirstAsync();
        var workerUser = await db.IdentityCredentials.Where(c => c.UserName == "perf-worker").Select(c => c.UserId).SingleAsync();
        var worker = await db.AppUsers.Where(u => u.Id == workerUser).Select(u => u.PersonId).SingleAsync();
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, ids, new DirectionPlanningCatalogReader(db));
        var service = new OperationalTaskService(db, direction, clock, new FileTechnicalValidator(), new Scanner());
        var supervision = new OperationalSupervisionService(db, clock, direction);
        var all = await service.ListAsync(actor, default); var paged = new List<Guid>();
        for (var page = 1; ; page++) {
            var result = await service.PageAsync(actor, null, null, page, default); paged.AddRange(result.Items.Select(t => t.Id));
            if (page * 50 >= result.Total) break;
        }
        Check(paged.Count == all.Count && paged.ToHashSet().SetEquals(all.Select(t => t.Id)), "SQL catalogue pagination preserves every visible record exactly once");
        foreach (var text in new[] { "ÁRBOL Ñandú", "ÉXITO", "piñata" }) {
            var normalized = await db.Database.SqlQuery<string>($"SELECT sgol_search({text}) AS \"Value\"").SingleAsync();
            Check(normalized == SearchText.Normalize(text), "SQL search preserves accent and case insensitive semantics");
        }
        var visible = await supervision.ListAsync(actor, default); var supervised = new List<Guid>();
        for (var page = 1; ; page++) {
            var result = await supervision.PageAsync(actor, CanonicalRole.SalesFloor, new Dictionary<string,string>(), page, default); supervised.AddRange(result.Items.Select(t => t.Assignment.Id));
            if (page * 50 >= result.Filtered) break;
        }
        Check(supervised.SequenceEqual(visible.Where(t => t.Role == CanonicalRole.SalesFloor).Select(t => t.Assignment.Id)), "SQL supervision pagination preserves historical assignments and stable order");
        try { await supervision.PageAsync(workerUser, CanonicalRole.SalesFloor, new Dictionary<string,string>(), 1, default); throw new Exception("Worker accessed supervision"); }
        catch (DirectionPlanException e) when (e.Status == 403) { Check(true, "Worker cannot use paged supervision service"); }
        var mine = await service.MineAsync(workerUser, default);
        var minePage = await service.MinePageAsync(workerUser, null, 1, clock.UtcNow, default);
        var orderedMine = OperationalWorkPlan.OrderedTasks(mine, WeekContract.LocalToday(clock.UtcNow), clock.UtcNow);
        Check(minePage.Items.Select(t => t.Id).SequenceEqual(orderedMine.Take(50).Select(t => t.Id)) && minePage.Total == mine.Count, "My Work SQL page preserves global color order");
        var todayTasks = OperationalWorkPlan.TodayTasks(mine, WeekContract.LocalToday(clock.UtcNow), clock.UtcNow);
        var progress = await service.MineProgressAsync(workerUser, WeekContract.LocalToday(clock.UtcNow), default);
        Check(progress.Total == todayTasks.Count && progress.Concluded == todayTasks.Count(t => t.ReviewStatus == "CONCLUIDA"), "My Work progress counts all daily work across pages");
        if (mine.Count > 50) Check((await service.MineFindAsync(workerUser, orderedMine[^1].Id, default)).Id == orderedMine[^1].Id, "Historical detail remains reachable outside visible page");
        var carryover = new OperationalCarryoverService(db, clock, supervision);
        var inbox = await carryover.InboxAsync(actor, default); var requests = new List<Guid>();
        foreach (var history in new[] { false, true }) for (var page = 1; ; page++) {
            var result = await carryover.InboxPageAsync(actor, history, page, default); requests.AddRange(result.Items.Select(r => r.Id));
            if (page * 10 >= result.Total) break;
        }
        Check(requests.Count == inbox.Length && requests.ToHashSet().SetEquals(inbox.Select(r => r.Id)), "SQL request pagination preserves authorized pending requests and history");
        // Historical approvals, unchanged deliveries, restorations and refreshed reference dates.
        // This controlled fixture is rolled back; the original assignment is never changed.
        var referenceRequest = inbox.First(); var item = referenceRequest.Items()[0];
        await using (var transaction = await db.Database.BeginTransactionAsync()) {
            OperationalCarryoverRequest Approved(string kind) {
                var request = JsonSerializer.Deserialize<OperationalCarryoverRequest>(JsonSerializer.Serialize(referenceRequest))!;
                request.Id = Guid.CreateVersion7(); request.IntentKey = Guid.CreateVersion7(); request.State = "APROBADA";
                request.DecidedBy = actor; request.DecidedAt = clock.UtcNow; request.DecisionComment = "Isolated historical regression";
                request.SnapshotJson = JsonSerializer.Serialize(new[] { item });
                request.ResultsJson = JsonSerializer.Serialize(new[] { new CarryoverResult(item.AssignmentId, item.Due, item.Due.AddMinutes(30), item.Due.AddMinutes(30), null, kind) });
                db.Add(request); return request;
            }
            var accepted = Approved("AJUSTADA"); Approved("ENTREGADA_SIN_CAMBIOS"); var restored = Approved("AJUSTADA");
            AuditEvent Event(string action, JsonDocument? before, JsonDocument after) => new() { Id=Guid.CreateVersion7(), ActorUserId=actor, ActorType="USER", OccurredAt=clock.UtcNow, ResourceType="OPERATIONAL_ASSIGNMENT", ResourceId=item.AssignmentId, BranchId=BranchScope.LorettaId, Action=action, Outcome="SUCCESS", CorrelationId=Guid.CreateVersion7(), BeforeData=before, AfterData=after };
            db.Add(Event(OperationalCarryoverService.RestorationAction, null, JsonSerializer.SerializeToDocument(new { RequestId=restored.Id })));
            db.Add(Event("OPERATIONAL_ASSIGNMENT_REFRESHED", JsonSerializer.SerializeToDocument(new { DueAt=item.Due.AddMinutes(-5) }), JsonSerializer.SerializeToDocument(new { Assignment=new { DueAt=item.Due } })));
            await db.SaveChangesAsync();
            var reader = new OperationalCarryoverReader(db); await reader.PrepareAsync(new[] {item.AssignmentId}, default);
            var timing = await reader.ForAsync(item.AssignmentId);
            Check(timing.ApprovedCount==1 && timing.RecognizedDue==item.Due.AddMinutes(30) && timing.ReferenceDue==item.Due.AddMinutes(-5) && timing.RefreshCount==1 && timing.Pending, "Indexed history retains refresh, restoration and unchanged-delivery rules");
            Check(await reader.IsRestoredAsync(restored.Id,item.AssignmentId), "Historical restoration is still accessible");
            db.Add(Event(OperationalCarryoverService.RestorationAction,null,JsonSerializer.SerializeToDocument(new {RequestId=accepted.Id})));await db.SaveChangesAsync();
            await reader.PrepareAsync(new[] {item.AssignmentId},default);
            Check((await reader.ForAsync(item.AssignmentId)).ApprovedCount==0,"Explicit reindex refreshes restoration metadata without stale scoped cache");
            await transaction.RollbackAsync(); db.ChangeTracker.Clear();
        }
        // Small future plan on a real synthetic calendar. Search and save use real product services.
        var task = new OperationalTask { Id = Guid.CreateVersion7(), TaskCode = "PI-" + Guid.NewGuid().ToString("N")[..12], Name = "Integrity planning", Requirement = "NINGUNO", LoadMinutes = 1, OperationalCriticality = "FLEXIBLE", PeriodType = "SEMANAL", Repetitions = 1, StartHour = 9 };
        db.Add(task);
        if (!await db.Set<PersonalScheduleVersion>().AnyAsync(v=>v.PersonId==worker && v.From==new DateOnly(2027,1,1))) db.Add(new PersonalScheduleVersion { Id = Guid.CreateVersion7(), PersonId = worker, Revision = 1, From = new(2027,1,1), ChangedBy = actor, ChangedAt = clock.UtcNow, IntentKey = Guid.CreateVersion7(), RequestHash = new string('a',64), DaysJson = JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d => d, d => new[] { new WorkInterval(540,1020) })) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var input = new DirectionPlanInput("Integrity performance plan", "Isolated", "SEMANAL", 2027, 2, new[] { task.TaskCode }, SchedulingNotBefore: new DateTimeOffset(2027,1,1,6,0,0,TimeSpan.Zero));
        async Task<DirectionPlanInput> Preview(DirectionPlanInput value) {
            var state = await PlanningCapacity.ReadAsync(db, value, clock.UtcNow, default);
            await PlanningCapacity.DailyLoadAsync(db, value, state.Preview, state.Ledger, default);
            return value with { CapacityFingerprint = state.Preview.AgendaFingerprint };
        }
        input = await Preview(input);
        var baseline = await db.OperationalAssignments.CountAsync();
        var changed = false;
        var parallelDelivery = visible.First(t => t.Assignment.PersonId==worker && t.Assignment.CompletedAt==null && t.Assignment.Requirement=="NINGUNO" && t.Assignment.GoalUnits==0).Assignment;
        var deliveryMs = 0d;
        using (var listener = new MeterListener()) {
            listener.InstrumentPublished = (instrument, l) => { if (instrument.Meter.Name == "Sgol.Performance" && instrument.Name == "sgol.work.duration") l.EnableMeasurementEvents(instrument); };
            listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => {
                if (changed) return;
                foreach (var tag in tags) if (tag.Key == "stage" && tag.Value?.ToString() == "planning.prepare") {
                    changed = true;
                    using var concurrent = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection.ConnectionString).Options);
                    concurrent.OperationalTasks.Where(t => t.Id == task.Id).ExecuteUpdate(s => s.SetProperty(t => t.Revision, t => t.Revision + 1));
                    var settings=concurrent.OperationalCapacities.Where(c=>c.PersonId==worker&&c.From<=new DateOnly(2027,1,1)&&c.Through>=new DateOnly(2027,1,1)).ToArray();
                    if(settings.Length==0)concurrent.OperationalCapacities.Add(new() { Id=Guid.CreateVersion7(),PersonId=worker,From=new(2027,1,1),Through=new(2027,1,31),Percent=90,DailyMinutes=480,Reason="Concurrent capacity regression" });
                    else foreach(var setting in settings)setting.Percent=setting.Percent==90?85:90;
                    concurrent.SaveChanges();
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var concurrentDirection = new EfDirectionPreparedPlanService(concurrent,new AuditTransaction(concurrent),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(concurrent));
                    new OperationalTaskService(concurrent,concurrentDirection,clock,new FileTechnicalValidator(),new Scanner()).CompleteAsync(workerUser,parallelDelivery.Id,null,null,default,"Concurrent delivery during plan calculation").GetAwaiter().GetResult();
                    deliveryMs = watch.Elapsed.TotalMilliseconds;
                }
            });
            listener.Start();
            try { await direction.CreateAsync(actor, Guid.CreateVersion7(), Guid.CreateVersion7(), input); throw new Exception("Changed inputs saved"); }
            catch (DirectionPlanException e) when (e.Code == "PLAN_CAPACITY_STALE") { Check(changed && baseline == await db.OperationalAssignments.CountAsync(), "Simultaneous catalogue/capacity change between calculation and lock rejects atomically"); }
        }
        var delivered = await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==parallelDelivery.Id);
        Check(delivered.CompletedAt!=null && delivered.DueAt==parallelDelivery.DueAt && delivered.PersonId==worker, $"Delivery commits while preparation holds no global lock ({deliveryMs:F0}ms), preserving deadline and author");
        input = await Preview(input);
        var key = Guid.CreateVersion7();
        var saved = await direction.CreateAsync(actor, key, Guid.CreateVersion7(), input);
        var count = await db.OperationalAssignments.CountAsync(a => a.PlanId == saved.Plan.PlanId);
        Check(count > 0, "Prepared agenda saves after fresh under-lock snapshot");
        var replay = await direction.CreateAsync(actor, key, Guid.CreateVersion7(), input);
        Check(replay.Result == "RECOVERED" && count == await db.OperationalAssignments.CountAsync(a => a.PlanId == saved.Plan.PlanId), "Plan replay preserves idempotency and assignment count");
        var midnightInput = await Preview(input with { Name="Midnight stale preparation", PeriodNumber=3, CapacityFingerprint=null });
        var beforeMidnight=await db.OperationalAssignments.CountAsync();var originalTime=clock.UtcNow;var advanced=false;
        using(var listener=new MeterListener()){
            listener.InstrumentPublished=(instrument,l)=>{if(instrument.Meter.Name=="Sgol.Performance"&&instrument.Name=="sgol.work.duration")l.EnableMeasurementEvents(instrument);};
            listener.SetMeasurementEventCallback<double>((instrument,value,tags,state)=>{if(advanced)return;foreach(var tag in tags)if(tag.Key=="stage"&&tag.Value?.ToString()=="planning.prepare"){advanced=true;clock.UtcNow=clock.UtcNow.AddDays(1);}});
            listener.Start();
            try{await direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),midnightInput);throw new Exception("Expired effective date accepted");}
            catch(DirectionPlanException e)when(e.Code=="PLAN_CAPACITY_STALE"){Check(advanced&&beforeMidnight==await db.OperationalAssignments.CountAsync(),"Saving revalidates the actual operational date after calculation");}
            finally{clock.UtcNow=originalTime;}
        }
        OperationalSchedulingChecks.Rules();
    }
    private static IEnumerable<OperationalTask> ThenByName(this IEnumerable<OperationalTask> rows) => rows.OrderBy(t => t.Name).ThenBy(t => t.Id);
}
