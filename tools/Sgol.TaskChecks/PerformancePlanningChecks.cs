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
    public sealed record Sample(int weekly,int monthly,string expected,string outcome,bool passed,double elapsedMs,long allocatedBytes,Dictionary<string,object?>? diagnostics=null);
    public static void AssertResults(IReadOnlyList<Sample> samples,int before,int after,bool diagnostic=false)
    {
        if(before!=after)throw new Exception("Preview persisted assignments unexpectedly");
        if(!diagnostic && (samples.Count==0 || samples.Any(s=>!s.passed)))throw new Exception("Planning assertions failed; inspect planning-results.json.");
        if(diagnostic)Console.WriteLine("DIAGNOSTIC ONLY: process success does not assert sample expectations.");
    }
    public static async Task FailureProbeAsync(string path, bool diagnostic)
    {
        Directory.CreateDirectory(path);
        Sample[] samples=[new(0,0,"completed","deliberate-failure",false,0,0)];
        await File.WriteAllTextAsync(Path.Combine(path,"planning-results.json"),JsonSerializer.Serialize(new{samples}));
        AssertResults(samples,0,0,diagnostic);
    }
    public static async Task RunAsync(string outputDirectory, bool diagnostic = false)
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
        var samples=new List<Sample>();var before=await db.OperationalAssignments.CountAsync();
        foreach(var (weekly,monthly,expected) in new[]{(5,1,"completed"),(50,3,"OCCURRENCE_SEARCH_SIZE_LIMIT"),(175,10,"OCCURRENCE_SEARCH_SIZE_LIMIT"),(190,10,"PLAN_TOO_MANY_OCCURRENCES")}){
            var tasks=Enumerable.Range(0,weekly+monthly).Select(i=>new OperationalTask{Id=Guid.CreateVersion7(),TaskCode="PV-"+Guid.NewGuid().ToString("N")[..12],Name=$"Planner volume {weekly}-{monthly}-{i}",Requirement="NINGUNO",LoadMinutes=1,OperationalCriticality="FLEXIBLE",PeriodType=i<weekly?"SEMANAL":"MENSUAL",Repetitions=1,StartHour=9}).ToArray();
            db.AddRange(tasks);await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var input=new DirectionPlanInput($"Planner volume {weekly}-{monthly}","Synthetic isolated","ANUAL",2027,null,tasks.Select(t=>t.TaskCode).ToArray(),SchedulingNotBefore:new DateTimeOffset(2027,1,1,6,0,0,TimeSpan.Zero));
            var sw=Stopwatch.StartNew();var allocation=GC.GetTotalAllocatedBytes();
            using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try{
                var state=await PlanningCapacity.ReadAsync(db,input,DateTimeOffset.UtcNow,budget.Token);
                await PlanningCapacity.DailyLoadAsync(db,input,state.Preview,state.Ledger,budget.Token);
                samples.Add(new(weekly,monthly,expected,"completed",expected=="completed",sw.Elapsed.TotalMilliseconds,GC.GetTotalAllocatedBytes()-allocation));
            }catch(DirectionPlanException e){var reason=e.Data.Keys.Cast<object>().Select(k=>e.Data[k]?.ToString()).Contains("OCCURRENCE_SEARCH_SIZE_LIMIT") ? "OCCURRENCE_SEARCH_SIZE_LIMIT" : e.Code; samples.Add(new(weekly,monthly,expected,e.Code,expected==reason && (expected!="OCCURRENCE_SEARCH_SIZE_LIMIT" || e.Code=="SCHEDULING_SEARCH_INCONCLUSIVE"),sw.Elapsed.TotalMilliseconds,GC.GetTotalAllocatedBytes()-allocation,e.Data.Keys.Cast<object>().ToDictionary(k=>k.ToString()!,k=>e.Data[k])));}
            catch(OperationCanceledException){samples.Add(new(weekly,monthly,expected,"60s-budget-cancelled",false,sw.Elapsed.TotalMilliseconds,GC.GetTotalAllocatedBytes()-allocation));}
            await File.WriteAllTextAsync(Path.Combine(outputDirectory,"planning-results.json"),JsonSerializer.Serialize(new{mode=diagnostic?"diagnostic":"assertion",samples,assignmentsBefore=before,assignmentsAfter=await db.OperationalAssignments.CountAsync(),scope="Real annual generator with synthetic worker schedule; no prepared plan or assignments persisted; one measured execution per volume"},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(JsonSerializer.Serialize(samples[^1]));
        }
        AssertResults(samples, before, await db.OperationalAssignments.CountAsync(), diagnostic);
    }
}
