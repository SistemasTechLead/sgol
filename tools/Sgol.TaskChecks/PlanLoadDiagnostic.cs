using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class PlanLoadDiagnostic
{
    public static async Task Run(SgolDbContext db)
    {
        var now=DateTimeOffset.UtcNow; var today=WeekContract.LocalToday(now);var week=DirectionPlanContract.OperationsWeek(today);
        var codes=await db.OperationalTasks.Where(t=>t.Active&&!t.Deleted&&t.IsWeeklyOperationsTask).Select(t=>t.TaskCode).ToArrayAsync();
        var input=new DirectionPlanInput("Diagnóstico","Consulta","SEMANAL",week.Year,week.Week,codes,IsWeeklyOperationsPlan:true);
        var state=await PlanningCapacity.ReadAsync(db,input,now,default);
        var worker=state.Ledger.Workers.Single(w=>w.Name=="Viri");
        var existing=state.Ledger.Tasks.Where(a=>a.PersonId==worker.Id).SelectMany(a=>state.Ledger.Footprint(a).Where(f=>f.Day==today&&f.Minutes>0).Select(f=>new {a.TaskCode,a.Name,f.Minutes,OriginalDay=WeekContract.LocalToday(a.StartsAt),a.ScheduleMode,a.OperationalCriticality}));
        Console.WriteLine("EXISTING "+JsonSerializer.Serialize(existing));
        var definitions=await db.OperationalTasks.Where(t=>codes.Contains(t.TaskCode)).ToArrayAsync();
        Console.WriteLine("TODAY_DEFINITIONS "+JsonSerializer.Serialize(definitions.Where(t=>worker.EligibleFor(t.AssignedRole,t.SpecializationId)&&OperationalSchedule.Dates(today,today,t).Any()).Select(t=>new{t.TaskCode,t.Name,t.LoadMinutes,t.ScheduleMode,t.OperationalCriticality,UniqueCandidate=state.Ledger.Workers.Count(w=>w.EligibleFor(t.AssignedRole,t.SpecializationId))==1})));
        try {await PlanningCapacity.DailyLoadAsync(db,input,state.Preview,state.Ledger,default);Console.WriteLine("ASSIGNED "+JsonSerializer.Serialize(state.Preview.TemporalRows.Where(r=>r.Person==worker.Name&&r.Day==today)));}
        catch(DirectionPlanException e) {Console.WriteLine("CONFLICT "+JsonSerializer.Serialize(e.Data[nameof(PlanningAssignmentConflict)]));}
    }
}
