using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class NewPlanCapacityChecks
{
    public static async Task Run(SgolDbContext db)
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
        await using var tx=await db.Database.BeginTransactionAsync();
        var fixture=await QaFixtures.CreateAsync(db);
        var task=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Id==fixture.Definition);
        var today=new DateOnly(2026,11,2);var now=OperationalSchedule.Instant(today,12);
        OperationalAssignment Assignment(int load,DateTimeOffset start,DateTimeOffset due)=>new(){Id=Guid.CreateVersion7(),PlanId=fixture.Plan,TaskId=task.Id,TaskCode=task.TaskCode,Name="Capacity boundary fixture",PersonId=fixture.Person,PersonName="Test",AssignedRole=task.AssignedRole,LoadMinutes=load,StartsAt=start,DueAt=due,Requirement="NINGUNO"};
        var old=Assignment(140,now.AddDays(-2),now.AddDays(-1));
        var boundary=Assignment(25,now.AddHours(-1),now);
        var current=Assignment(90,now.AddMinutes(-30),now.AddHours(1));
        db.AddRange(old,boundary,current);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var before=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>a.PersonId==fixture.Person).OrderBy(a=>a.Id).ToArrayAsync());
        var normal=await OperationalCapacityService.LedgerAsync(db,now,default);
        var input=new DirectionPlanInput("Capacity boundary","Tests","SEMANAL",2026,45,[task.TaskCode]);
        var state=await PlanningCapacity.ReadAsync(db,input,now,default);
        Check(normal.DayLoad(fixture.Person,today)==255,"Normal operational ledger still counts overdue obligations");
        Check(state.Ledger.DayLoad(fixture.Person,today)==90,"New plan excludes past days and exact-expiry boundary, but keeps unexpired work");
        Check(state.Preview.ExcludedOverdue.Count(a=>a.Person=="Test")==2,"Preview separately identifies the excluded overdue tasks");
        var newElapsed=Assignment(11,now.AddHours(-2),now.AddMinutes(-30));
        var withNew=state.Ledger.WithAdded(newElapsed);
        Check(withNew.DayLoad(fixture.Person,today)==101,"New elapsed occurrence still consumes capacity; existing exclusions survive ledger clones");
        Check(before==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>a.PersonId==fixture.Person).OrderBy(a=>a.Id).ToArrayAsync()),"Excluding overdue demand never edits or completes those assignments");
        await tx.RollbackAsync();db.ChangeTracker.Clear();
    }
}
