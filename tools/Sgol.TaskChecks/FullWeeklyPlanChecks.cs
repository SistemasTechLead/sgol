using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

// The shared runner rejects operational DBs. Use a fresh restored copy: this
// intentionally saves the entire real catalogue, rather than a simplified fixture.
internal static class FullWeeklyPlanChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static async Task RunAsync(SgolDbContext db)
    {
        await FallbackAsync(db);
        var at=Environment.GetEnvironmentVariable("SGOL_WEEK_CHECK_AT") is { } value?DateTimeOffset.Parse(value):DateTimeOffset.UtcNow;
        var clock=new FixedClock(at);
        var week=DirectionPlanContract.OperationsWeek(WeekContract.LocalToday(at));
        var codes=await db.OperationalTasks.AsNoTracking().Where(t=>t.Active&&!t.Deleted&&t.IsWeeklyOperationsTask).OrderBy(t=>t.TaskCode).Select(t=>t.TaskCode).ToArrayAsync();
        var original=await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync();var before=JsonSerializer.Serialize(original);
        var actor=await db.RoleAssignmentVersions.Where(r=>r.RoleCode=="DIRECCION"&&r.ValidTo==null).Select(r=>r.UserId).FirstAsync();
        var input=new DirectionPlanInput($"Prueba semanal completa {week.Week}","Plan de Operaciones Semanal","SEMANAL",week.Year,week.Week,codes,IsWeeklyOperationsPlan:true);
        var watch=Stopwatch.StartNew();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var state=await PlanningCapacity.ReadAsync(db,input,at,timeout.Token);
        await PlanningCapacity.DailyLoadAsync(db,input,state.Preview,state.Ledger,timeout.Token);
        Check(state.Preview.TemporalRows.Length>0&&state.Preview.TemporalRows.All(r=>r.Eligible),$"All {codes.Length} actual catalogue tasks considered; complete preview has {state.Preview.TemporalRows.Length} eligible assignments in {watch.Elapsed.TotalSeconds:F2}s");
        var firstFingerprint=state.Preview.AgendaFingerprint;
        var repeated=await PlanningCapacity.ReadAsync(db,input,at,timeout.Token);
        await PlanningCapacity.DailyLoadAsync(db,input,repeated.Preview,repeated.Ledger,timeout.Token);
        Check(firstFingerprint==repeated.Preview.AgendaFingerprint,"Repeated preparation is deterministic despite sequential asynchronous stack boundaries");
        Check(before==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),"Full preparation does not mutate stored assignments");
        var service=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var key=Guid.CreateVersion7();input=input with{CapacityFingerprint=firstFingerprint};
        var saved=await service.CreateAsync(actor,key,Guid.CreateVersion7(),input,timeout.Token);
        var rows=await db.OperationalAssignments.AsNoTracking().Where(a=>a.PlanId==saved.Plan.PlanId).ToArrayAsync();
        Check(rows.Length==state.Preview.TemporalRows.Length,"The real service persists the complete preview without a partial plan");
        Check(rows.Select(a=>a.ScheduleKey).Distinct().Count()==rows.Length,"Persisted occurrences have unique reservation identities");
        var today=WeekContract.LocalToday(at);
        if (today.DayOfWeek==DayOfWeek.Monday && !original.Any(a=>a.TaskCode=="TAR-0062"&&WeekContract.LocalToday(a.StartsAt)==today))
        {
            var opening=rows.Single(a=>a.TaskCode=="TAR-0062"&&WeekContract.LocalToday(a.StartsAt)==today);
            Check(TimeZoneInfo.ConvertTime(opening.StartsAt,OperationalSchedule.Zone).Hour==9&&opening.CompletedAt==null,"Monday opening keeps 09:00 and remains uncompleted even when created after opening");
            Check(!state.Preview.MonthlyPending.Any(p=>p.TaskCode=="TAR-0062"&&p.Reason=="CRITICAL_ACTIVATION_PASSED"),"Weekly operations does not replace today's opening with an omission notice");
        }
        Check(rows.Where(a=>a.DueAt<=at).All(a=>a.CompletedAt==null&&a.ReviewStatus==null),"All elapsed assignments remain genuinely pending without fabricated delivery or review");
        var read=await service.ReadAsync(actor,saved.Plan.PlanId);
        Check(JsonSerializer.Serialize(read.MonthlyPending)==JsonSerializer.Serialize(state.Preview.MonthlyPending),$"All {state.Preview.MonthlyPending.Length} dated notices survive saving and reading");
        Check((await service.CreateAsync(actor,key,Guid.CreateVersion7(),input,timeout.Token)).Result=="RECOVERED","Retry recovers the complete plan without duplicates");
        var ids=original.Select(a=>a.Id).ToArray();
        Check(before==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>ids.Contains(a.Id)).OrderBy(a=>a.Id).ToArrayAsync()),$"All {original.Length} original assignments retain identity and complete historical state");
        Console.WriteLine($"PASS full weekly lifecycle {saved.Plan.PlanId}; {rows.Length} new assignments ONLY in isolated DB; total {watch.Elapsed.TotalSeconds:F2}s");
    }
    private static async Task FallbackAsync(SgolDbContext db)
    {
        await using var tx=await db.Database.BeginTransactionAsync();
        var day=new DateOnly(2026,10,26);var at=OperationalSchedule.Instant(day,8);
        var source=await OperationalCapacityService.LedgerAsync(db,at,default);
        var people=source.Workers.Where(w=>w.Role=="PISO_VENTAS").Take(2).ToArray();
        Check(people.Length==2,"Restored data supplies two real sales-floor identities for the fallback regression");
        var spec=new Sgol.Web.Infrastructure.Persistence.Organization.Specialization{Id=Guid.CreateVersion7(),Name="Search regression",NormalizedName="SEARCH REGRESSION",Active=true,Revision=1};db.Add(spec);
        var first=new OperationalTask{Id=Guid.CreateVersion7(),TaskCode="TAR-SEARCH-A",Name="Constrained important task",AssignedRole="PISO_VENTAS",SpecializationId=spec.Id,ScheduleMode="DIAS_FIJOS",WeekDays=2,StartHour=10,LoadMinutes=30,OperationalCriticality="IMPORTANTE"};
        var second=new OperationalTask{Id=Guid.CreateVersion7(),TaskCode="TAR-SEARCH-B",Name="Long important task",AssignedRole="PISO_VENTAS",ScheduleMode="DIAS_FIJOS",WeekDays=2,StartHour=9,LoadMinutes=60,OperationalCriticality="IMPORTANTE"};
        first.Validate();second.Validate();db.AddRange(first,second);await db.SaveChangesAsync();
        var policy=await OperationalSchedulingPolicyService.CurrentAsync(db,default);
        var context=new TemporalEligibilityContext{Enabled=true,Now=at,BaseSchedulePolicyIds=[policy.Id],Schedules=people.Select((p,i)=>new PersonalScheduleVersion{PersonId=p.Id,Revision=1,DaysJson=JsonSerializer.Serialize(new Dictionary<int,WorkInterval[]>{[1]=i==0?[new(540,720)]:[]})}).ToArray()};
        var staff=people.Select((p,i)=>p with{SpecializationIds=i==0?[spec.Id]:[]}).ToArray();
        var ledger=new CapacityLedger(staff,[],[],[],[],day){Temporal=context};
        var plan=new DirectionPreparedPlan(Guid.CreateVersion7(),Sgol.Organization.Contracts.BranchScope.LorettaId,Guid.CreateVersion7(),at,new("Search fallback","Test","SEMANAL",2026,44,[first.TaskCode,second.TaskCode]));plan.CaptureSchedulingPolicy(policy.Id);
        var rows=await OperationalPlanGenerator.GenerateAsync(db,plan,[first.TaskCode,second.TaskCode],at,default,true,ledger);
        Check(rows.Single(a=>a.TaskId==first.Id).StartsAt==TemporalEligibilityContext.At(day,630)&&rows.Single(a=>a.TaskId==second.Id).StartsAt==TemporalEligibilityContext.At(day,540),"Lazy fallback still backtracks from the greedy choice to preserve the long task's only window");
        await tx.RollbackAsync();db.ChangeTracker.Clear();
    }
    private sealed class FixedClock(DateTimeOffset at):IClock{public DateTimeOffset UtcNow=>at;}
}
