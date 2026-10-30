using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class QaFixtures
{
    internal static async Task AvailabilityAsync(SgolDbContext db, Guid actor, params Guid[] people)
    {
        foreach(var person in people)
            db.Add(new PersonalScheduleVersion {Id=Guid.CreateVersion7(),PersonId=person,Revision=1,From=new(2025,1,1),ChangedBy=actor,ChangedAt=new(2025,1,1,0,0,0,TimeSpan.Zero),IntentKey=Guid.NewGuid(),RequestHash="fixture",
                DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(0,1439)}))});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
    }
    internal static async Task<DirectionPlanInput> ConfirmAsync(SgolDbContext db, DirectionPlanInput input, DateTimeOffset now)
    {
        var state=await PlanningCapacity.ReadAsync(db,input,now,default);
        await PlanningCapacity.DailyLoadAsync(db,input,state.Preview,state.Ledger,default);
        return input with {CapacityFingerprint=state.Preview.AgendaFingerprint};
    }
    // Synthetic, independently owned actors remain valid for every fixed clock used by these suites.
    internal sealed record Actors(Guid Director, Guid Worker, Guid Person, Guid Other, Guid OtherPerson, Guid Definition, Guid Plan);
    internal static async Task<Actors> CreateAsync(SgolDbContext db, int shiftExit = 1380)
    {
        var now = new DateTimeOffset(2025,1,1,0,0,0,TimeSpan.Zero);
        var people = new List<(Guid User,Guid Person)>();
        foreach(var role in new[]{CanonicalRole.Direction,CanonicalRole.SalesFloor,CanonicalRole.SalesFloor}) {
            var user=Guid.CreateVersion7();var person=Guid.CreateVersion7();var code="QA-"+Guid.NewGuid().ToString("N")[..12];
            db.People.Add(new(){Id=person,StableCode=code,DisplayName=code,CreatedAt=now});
            db.AppUsers.Add(new(){Id=user,PersonId=person,Status=AccountStatus.Active,SecurityStamp="isolated-qa"});
            db.EmploymentVersions.Add(new(Guid.CreateVersion7(),person,BranchScope.LorettaId,EmploymentStatus.Active,now));
            db.RoleAssignmentVersions.Add(new(){Id=Guid.CreateVersion7(),UserId=user,BranchId=BranchScope.LorettaId,RoleCode=role,Status=RoleAssignmentStatus.Active,ValidFrom=now});
            people.Add((user,person));
            db.Add(new PersonalScheduleVersion {Id=Guid.CreateVersion7(),PersonId=person,Revision=1,From=new(2025,1,1),ChangedBy=people[0].User,ChangedAt=now,IntentKey=Guid.NewGuid(),RequestHash="fixture",
                DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,810),new WorkInterval(900,shiftExit)}))});
        }
        var task=new OperationalTask {Id=Guid.CreateVersion7(),TaskCode="QA-"+Guid.NewGuid().ToString("N")[..12],Name="QA lifecycle definition",Requirement="NINGUNO",LoadMinutes=10,OperationalCriticality="FLEXIBLE"};
        db.Add(task);
        var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,people[0].User,now,"QA fixture",new(2026,10,1),new(2026,11,30));db.Add(plan);
        if(!await db.Set<TemporalAssignmentState>().AnyAsync())db.Add(new TemporalAssignmentState {Enabled=true});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        return new(people[0].User,people[1].User,people[1].Person,people[2].User,people[2].Person,task.Id,plan.Id);
    }
}
