using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class PerformancePlanningChecks
{
    public static async Task RunAsync(string outputDirectory)
    {
        var connection=Environment.GetEnvironmentVariable("SGOL_TASK_CHECKS_CONNECTION")??throw new Exception("Dedicated connection required");
        var target=new NpgsqlConnectionStringBuilder(connection);
        if(target.Host!="localhost"||target.Port!=5547||target.Database!="sgol_task_checks_performance")throw new Exception("Dedicated performance container required");
        Directory.CreateDirectory(outputDirectory);
        await using var db=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection,n=>n.CommandTimeout(15)).Options);
        var actor=await db.RoleAssignmentVersions.Where(r=>r.RoleCode==CanonicalRole.Direction&&r.ValidTo==null).Select(r=>r.UserId).FirstAsync();
        var worker=await db.RoleAssignmentVersions.Where(r=>r.RoleCode==CanonicalRole.SalesFloor&&r.ValidTo==null).Join(db.AppUsers,r=>r.UserId,u=>u.Id,(r,u)=>u.PersonId).FirstAsync();
        if(!await db.Set<PersonalScheduleVersion>().AnyAsync(s=>s.PersonId==worker)) {
            db.Add(new PersonalScheduleVersion{Id=Guid.CreateVersion7(),PersonId=worker,Revision=1,From=new(2027,1,1),ChangedBy=actor,ChangedAt=DateTimeOffset.UtcNow,IntentKey=Guid.CreateVersion7(),RequestHash=new string('e',64),DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,1020)}))});
            await db.SaveChangesAsync();
        }
        var samples=new List<object>();var before=await db.OperationalAssignments.CountAsync();
        foreach(var (weekly,monthly,expectLimit) in new[]{(5,1,false),(50,3,false),(175,10,false),(190,10,true)}){
            var tasks=Enumerable.Range(0,weekly+monthly).Select(i=>new OperationalTask{Id=Guid.CreateVersion7(),TaskCode="PV-"+Guid.NewGuid().ToString("N")[..12],Name=$"Planner volume {weekly}-{monthly}-{i}",Requirement="NINGUNO",LoadMinutes=1,OperationalCriticality="FLEXIBLE",PeriodType=i<weekly?"SEMANAL":"MENSUAL",Repetitions=1,StartHour=9}).ToArray();
            db.AddRange(tasks);await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var input=new DirectionPlanInput($"Planner volume {weekly}-{monthly}","Synthetic isolated","ANUAL",2027,null,tasks.Select(t=>t.TaskCode).ToArray(),SchedulingNotBefore:new DateTimeOffset(2027,1,1,6,0,0,TimeSpan.Zero));
            var sw=Stopwatch.StartNew();var allocation=GC.GetTotalAllocatedBytes();
            using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try{
                var state=await PlanningCapacity.ReadAsync(db,input,DateTimeOffset.UtcNow,budget.Token);
                await PlanningCapacity.DailyLoadAsync(db,input,state.Preview,state.Ledger,budget.Token);
                samples.Add(new{weekly,monthly,expectLimit,elapsedMs=sw.Elapsed.TotalMilliseconds,allocatedBytes=GC.GetTotalAllocatedBytes()-allocation,assigned=state.Preview.TemporalRows.Length,pending=state.Preview.MonthlyPending.Length,outcome="completed",passed=!expectLimit});
            }catch(DirectionPlanException e){samples.Add(new{weekly,monthly,expectLimit,elapsedMs=sw.Elapsed.TotalMilliseconds,allocatedBytes=GC.GetTotalAllocatedBytes()-allocation,outcome=e.Code,diagnostics=e.Data.Keys.Cast<object>().ToDictionary(k=>k.ToString()!,k=>e.Data[k]),passed=expectLimit&&e.Code=="PLAN_TOO_MANY_OCCURRENCES"});}
            catch(OperationCanceledException){samples.Add(new{weekly,monthly,expectLimit,elapsedMs=sw.Elapsed.TotalMilliseconds,outcome="60s-budget-cancelled",passed=false});}
            await File.WriteAllTextAsync(Path.Combine(outputDirectory,"planning-results.json"),JsonSerializer.Serialize(new{samples,assignmentsBefore=before,assignmentsAfter=await db.OperationalAssignments.CountAsync(),scope="Real annual generator with synthetic worker schedule; no prepared plan or assignments persisted; one measured execution per volume"},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(JsonSerializer.Serialize(samples[^1]));
        }
        if(before!=await db.OperationalAssignments.CountAsync())throw new Exception("Preview persisted assignments unexpectedly");
    }
}
