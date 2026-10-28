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

internal static class DistributedMonthlyChecks
{
    private static void Check(bool ok,string message) { if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message); }
    public static async Task RunAsync(SgolDbContext db)
    {
        var before=await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync();
        var beforeJson=JsonSerializer.Serialize(before);
        Check(!before.Any(a=>a.PersonName=="Viri"&&WeekContract.LocalToday(a.StartsAt).DayOfWeek==DayOfWeek.Sunday),"No persisted Sunday assignments for Viri in the source snapshot");
        var actor=await db.RoleAssignmentVersions.Where(r=>r.RoleCode=="DIRECCION"&&r.ValidTo==null).Select(r=>r.UserId).FirstAsync();
        var policy=await OperationalSchedulingPolicyService.CurrentAsync(db,default);
        var codes=new[]{"TAR-0025","TAR-0028","TAR-0379"};
        var monday=new DateOnly(2026,11,2);
        async Task<(OperationalAssignment[] Rows,DirectionPreparedPlan Plan)> Generate(string[] selected,DateOnly today,int week,CapacityLedger? custom=null)
        {
            var at=OperationalSchedule.Instant(today,0);
            var plan=new DirectionPreparedPlan(Guid.CreateVersion7(),BranchScope.LorettaId,actor,at,new("Mensuales sin domingo","Prueba","SEMANAL",2026,week,selected));
            plan.CaptureSchedulingPolicy(policy.Id);
            var rows=await OperationalPlanGenerator.GenerateAsync(db,plan,selected,at,default,true,custom);
            return(rows,plan);
        }
        var endWeek=await Generate(codes,new(2026,10,26),44);
        Check(endWeek.Rows.All(a=>WeekContract.LocalToday(a.StartsAt).DayOfWeek!=DayOfWeek.Sunday),"Actual monthly tasks never become Sunday assignments");
        var pending=JsonSerializer.Deserialize<MonthlyPlanPending[]>(endWeek.Plan.MonthlyPendingJson)!;
        Check(codes.All(c=>pending.Any(p=>p.TaskCode==c&&p.Month==11&&p.Reason=="MONTH_DISTRIBUTED_NO_SLOT")),"November occurrences remain explicit pending work at the Sunday week boundary");
        var next=await Generate(codes,monday,45);
        Check(codes.All(c=>next.Rows.Count(a=>a.TaskCode==c)==1)&&next.Rows.All(a=>a.PersonName=="Viri"&&WeekContract.LocalToday(a.StartsAt).DayOfWeek!=DayOfWeek.Sunday),"Actual three monthly tasks recover next week with Viri on working days");
        var ledger=await OperationalCapacityService.LedgerAsync(db,OperationalSchedule.Instant(monday),default);
        Check(next.Rows.All(a=>ledger.CheckTime(a.PersonId,a).Eligible),"Every recovered task has a compatible actual shift");
        Console.WriteLine(JsonSerializer.Serialize(next.Rows.Select(a=>new{a.TaskCode,a.PersonName,a.StartsAt,a.DueAt,a.ScheduleKey})));
        await using(var tx=await db.Database.BeginTransactionAsync())
        {
            var viri=ledger.Workers.Single(w=>w.Name=="Viri");
            var fixture=new OperationalTask{Id=Guid.CreateVersion7(),TaskCode="TAR-SUN-CHECK",Name="Monthly scheduling regression",AssignedRole=viri.Role,PeriodType="MENSUAL",Repetitions=2,ScheduleMode="DISTRIBUIDA",OperationalCriticality="FLEXIBLE",LoadMinutes=5,StartHour=9};
            fixture.Validate();db.Add(fixture);await db.SaveChangesAsync();
            var at=OperationalSchedule.Instant(new(2026,11,1));
            var monthPlan=new DirectionPreparedPlan(Guid.CreateVersion7(),BranchScope.LorettaId,actor,at,new("Repeticiones mensuales","Prueba","MENSUAL",2026,11,[fixture.TaskCode]));monthPlan.CaptureSchedulingPolicy(policy.Id);
            var monthLedger=await OperationalCapacityService.LedgerAsync(db,at,default);
            var rows=await OperationalPlanGenerator.GenerateAsync(db,monthPlan,[fixture.TaskCode],at,default,true,monthLedger);
            Check(rows.Length==2&&rows.Select(r=>r.ScheduleKey).Distinct().Count()==2&&rows.All(r=>WeekContract.LocalToday(r.StartsAt).DayOfWeek!=DayOfWeek.Sunday),"Two repetitions retain frequency and stable distinct monthly identities");
            db.Add(monthPlan);db.AddRange(rows);await db.SaveChangesAsync();
            var repeated=await Generate([fixture.TaskCode],new(2026,11,9),46);
            Check(repeated.Rows.Length==0,"Neighboring weeks do not duplicate persisted monthly repetitions");
            db.RemoveRange(rows);await db.SaveChangesAsync();
            rows[0].ScheduleKey=$"{fixture.Id:N}:{WeekContract.LocalToday(rows[0].StartsAt):yyyyMMdd}:0";
            db.Add(rows[0]);await db.SaveChangesAsync();
            var legacy=await Generate([fixture.TaskCode],new(2026,11,16),47);
            Check(legacy.Rows.Length==1&&legacy.Rows.Single().ScheduleKey!.EndsWith(":D:1"),"Legacy date-based reservations count toward the same monthly frequency");
            var absent=CapacityLedger.Days(monday,monday.AddDays(5)).Select(d=>(viri.Id,d)).ToHashSet();
            var unavailable=new CapacityLedger(ledger.Workers,ledger.Tasks,ledger.Capacities,absent,ledger.Legacy,monday){Temporal=ledger.Temporal};
            var rejected=await Generate(codes,monday,45,unavailable);
            Check(rejected.Rows.Length==0&&JsonSerializer.Deserialize<MonthlyPlanPending[]>(rejected.Plan.MonthlyPendingJson)!.Length==3,"A rest or absence cannot receive a monthly task; all three remain pending");
            await tx.RollbackAsync();db.ChangeTracker.Clear();
        }
        Check(beforeJson==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()),"Fixtures roll back without rewriting existing assignments");
        var clock=new FixedClock(OperationalSchedule.Instant(monday));
        var service=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var input=new DirectionPlanInput("Prueba mensual Viri sin domingo","Prueba","SEMANAL",2026,45,codes);
        var state=await PlanningCapacity.ReadAsync(db,input,clock.UtcNow,default);
        await PlanningCapacity.DailyLoadAsync(db,input,state.Preview,state.Ledger,default);
        var saved=await service.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),input with{CapacityFingerprint=state.Preview.AgendaFingerprint});
        var savedRows=await db.OperationalAssignments.AsNoTracking().Where(a=>a.PlanId==saved.Plan.PlanId).ToArrayAsync();
        Check(savedRows.Length==3&&savedRows.All(a=>a.PersonName=="Viri"&&WeekContract.LocalToday(a.StartsAt).DayOfWeek!=DayOfWeek.Sunday),"Real service persists three recovered tasks only on working days in isolated DB");
        var persistedAgain=await Generate(codes,monday,45);
        Check(persistedAgain.Rows.Length==0,"Persisted recovery cannot be generated twice");
        var ids=before.Select(a=>a.Id).ToArray();
        Check(beforeJson==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>ids.Contains(a.Id)).OrderBy(a=>a.Id).ToArrayAsync()),"All original assignments retain their IDs and full historical state");
    }
    private sealed class FixedClock(DateTimeOffset at):IClock {public DateTimeOffset UtcNow=>at;}
}
