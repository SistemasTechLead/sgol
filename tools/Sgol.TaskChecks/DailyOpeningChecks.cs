using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class DailyOpeningChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static async Task RunAsync(SgolDbContext db)
    {
        var opening=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskCode=="TAR-0062");
        Check(opening.ScheduleMode=="DIAS_FIJOS"&&opening.WeekDays==127&&opening.Repetitions==7,"Catalog defines one opening for every date");
        var before=await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync();var beforeJson=JsonSerializer.Serialize(before);
        var actor=await db.RoleAssignmentVersions.Where(r=>r.RoleCode=="DIRECCION"&&r.ValidTo==null).Select(r=>r.UserId).FirstAsync();
        var policy=await OperationalSchedulingPolicyService.CurrentAsync(db,default);
        var monday=new DateOnly(2026,10,26);
        async Task<(OperationalAssignment[] Rows,DirectionPreparedPlan Plan)> Generate(DateOnly day,int hour,int week,bool operations=false,CapacityLedger? ledger=null)
        {
            var at=OperationalSchedule.Instant(day,hour);
            var plan=new DirectionPreparedPlan(Guid.CreateVersion7(),BranchScope.LorettaId,actor,at,new("Aperturas por fecha","Prueba","SEMANAL",2026,week,[opening.TaskCode],IsWeeklyOperationsPlan:operations));plan.CaptureSchedulingPolicy(policy.Id);
            return(await OperationalPlanGenerator.GenerateAsync(db,plan,[opening.TaskCode],at,default,true,ledger),plan);
        }
        var early=await Generate(monday,8,44);
        Check(early.Rows.Length==7&&early.Rows.GroupBy(a=>WeekContract.LocalToday(a.StartsAt)).All(g=>g.Count()==1),"Before opening: seven dates, exactly one assignment per date");
        Check(early.Rows.All(a=>TimeZoneInfo.ConvertTime(a.StartsAt,OperationalSchedule.Zone).Hour==(WeekContract.LocalToday(a.StartsAt).DayOfWeek==DayOfWeek.Sunday?10:9)),"Weekday 09:00 and Sunday 10:00 anchors preserved");
        var late=await Generate(monday,11,44);
        var notices=JsonSerializer.Deserialize<MonthlyPlanPending[]>(late.Plan.MonthlyPendingJson)!;
        Check(late.Rows.Length==6&&late.Rows.All(a=>WeekContract.LocalToday(a.StartsAt)>monday)&&late.Rows.GroupBy(a=>WeekContract.LocalToday(a.StartsAt)).All(g=>g.Count()==1),"After opening: six future dates, no seventh repetition moved onto another day");
        Check(notices.Length==1&&notices[0].Reason=="CRITICAL_ACTIVATION_PASSED"&&notices[0].PreferredDay==26,"Elapsed Monday remains a dated notice, never completed or assigned retroactively");
        var thursday=await Generate(monday.AddDays(3),11,44,true);
        Check(thursday.Rows.Length==4&&thursday.Rows.All(a=>WeekContract.LocalToday(a.StartsAt)>=monday.AddDays(3)),"Midweek operations includes Thursday through Sunday without earlier dates");
        Check(thursday.Rows.Single(a=>WeekContract.LocalToday(a.StartsAt)==monday.AddDays(3)).DueAt<OperationalSchedule.Instant(monday.AddDays(3),11)&&thursday.Plan.MonthlyPendingJson=="[]","Operations retains today's elapsed opening as pending overdue work at its original time");
        var lateOperations=await Generate(monday,11,44,true);
        Check(lateOperations.Rows.Length==7&&lateOperations.Rows.Single(a=>WeekContract.LocalToday(a.StartsAt)==monday).DueAt<OperationalSchedule.Instant(monday,11),"Monday operations includes all seven openings, including the elapsed Monday opening");
        var ledger=await OperationalCapacityService.LedgerAsync(db,OperationalSchedule.Instant(monday,11),default);
        var tuesday=monday.AddDays(1);
        var absent=ledger.Workers.Select(w=>(w.Id,tuesday)).ToHashSet();
        var blocked=new CapacityLedger(ledger.Workers,ledger.Tasks,ledger.Capacities,absent,ledger.Legacy,monday){Temporal=ledger.Temporal};
        try{await Generate(monday,11,44,ledger:blocked);throw new Exception("Real lack of coverage accepted");}
        catch(DirectionPlanException e){Check(e.Data[nameof(PlanningAssignmentConflict)] is PlanningAssignmentConflict c&&c.Candidates.All(x=>x.Day==tuesday),"A real uncovered future date still blocks with that exact date, not a preview self-overlap");}
        // A mistakenly distributed critical task still keeps its nominal calendar dates.
        await using(var tx=await db.Database.BeginTransactionAsync())
        {
            var tracked=await db.OperationalTasks.SingleAsync(t=>t.Id==opening.Id);tracked.ScheduleMode="DISTRIBUIDA";tracked.WeekDays=0;await db.SaveChangesAsync();
            var guarded=await Generate(monday,11,44);
            Check(guarded.Rows.Length==6&&guarded.Rows.Select(a=>WeekContract.LocalToday(a.StartsAt)).Distinct().Count()==6,"Critical weekly tasks cannot redistribute elapsed occurrences even if configured as distributed");
            await tx.RollbackAsync();db.ChangeTracker.Clear();
        }
        Check(beforeJson==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),"Preview and regression cases leave original assignments unchanged");
        var clock=new FixedClock(OperationalSchedule.Instant(monday,11));
        var service=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var input=new DirectionPlanInput("Aperturas por fecha verificadas","Prueba","SEMANAL",2026,44,[opening.TaskCode]);
        var state=await PlanningCapacity.ReadAsync(db,input,clock.UtcNow,default);await PlanningCapacity.DailyLoadAsync(db,input,state.Preview,state.Ledger,default);
        Check(state.Preview.TemporalRows.Length==6&&state.Preview.MonthlyPending.Single().Reason=="CRITICAL_ACTIVATION_PASSED","Preparation includes six assignments and the same elapsed-opening notice");
        var key=Guid.CreateVersion7();input=input with{CapacityFingerprint=state.Preview.AgendaFingerprint};
        var saved=await service.CreateAsync(actor,key,Guid.CreateVersion7(),input);
        var rows=await db.OperationalAssignments.AsNoTracking().Where(a=>a.PlanId==saved.Plan.PlanId).ToArrayAsync();
        Check(rows.Length==6&&rows.Select(a=>WeekContract.LocalToday(a.StartsAt)).Distinct().Count()==6,"Real service persists six future openings only in the isolated database");
        var read=await service.ReadAsync(actor,saved.Plan.PlanId);
        Check(read.MonthlyPending!.Single().Reason=="CRITICAL_ACTIVATION_PASSED","Elapsed-opening notice survives saving and reading the plan");
        Check((await service.CreateAsync(actor,key,Guid.CreateVersion7(),input)).Result=="RECOVERED","Retry recovers the same plan without duplicating openings");
        var ids=before.Select(a=>a.Id).ToArray();Check(beforeJson==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>ids.Contains(a.Id)).OrderBy(a=>a.Id).ToArrayAsync()),"All original IDs, people, timestamps and historical state preserved");
        Console.WriteLine(JsonSerializer.Serialize(rows.Select(a=>new{a.TaskCode,a.PersonName,a.StartsAt,a.DueAt,a.ScheduleKey})));
    }
    private sealed class FixedClock(DateTimeOffset at):IClock{public DateTimeOffset UtcNow=>at;}
}
