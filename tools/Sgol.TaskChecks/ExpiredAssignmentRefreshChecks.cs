using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class ExpiredAssignmentRefreshChecks
{
    private static void Check(bool condition,string message) { if(!condition) throw new Exception(message);Console.WriteLine("PASS expired refresh: "+message); }
    public static async Task RunAsync(SgolDbContext db, OperationalTaskService tasks, IDirectionPreparedPlanService direction, IClock clock,Guid actor,Guid person)
    {
        var service=new AssignmentRefreshService(db,direction,clock);var today=WeekContract.LocalToday(clock.UtcNow);var yesterday=today.AddDays(-1);
        db.Add(new TemporalAssignmentState {Enabled=true});
        db.Add(new PersonalScheduleVersion {Id=Guid.CreateVersion7(),PersonId=person,Revision=1,From=yesterday,ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=Guid.NewGuid(),
            DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(8*60,20*60)}))});
        await tasks.SaveAsync(actor,new OperationalTask {Name="Apertura vencida",Revision=0,Requirement="NINGUNO",LoadMinutes=10,OperationalCriticality="CRITICA",ScheduleMode="DIAS_FIJOS",WeekDays=127,StartHour=9},default);
        await tasks.SaveAsync(actor,new OperationalTask {Name="Vecina estable",Revision=0,Requirement="NINGUNO",LoadMinutes=10,OperationalCriticality="FLEXIBLE",StartHour=10},default);
        var task=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name=="Apertura vencida");var neighbour=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name=="Vecina estable");
        var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,actor,clock.UtcNow,"Vencidas aisladas",yesterday,today.AddDays(6));db.Add(plan);
        var policy=await OperationalSchedulingPolicyService.CurrentAsync(db,default);
        OperationalAssignment Make(OperationalTask definition,DateOnly day,int minute) {
            var a=new OperationalAssignment {Id=Guid.CreateVersion7(),PlanId=plan.Id,TaskId=definition.Id,TaskCode=definition.TaskCode,PersonId=person,PersonName="A",AssignedRole=definition.AssignedRole};
            AssignmentDefinition.Adopt(a,definition,null,false);OperationalAgenda.Capture(a,policy,definition.OperationalCriticality,TemporalEligibilityContext.At(day,minute));return a;
        }
        var expired=Make(task,yesterday,540);var future=Make(task,today.AddDays(1),540);var pastNeighbour=Make(neighbour,yesterday,555);
        var submitted=Make(task,yesterday,600);submitted.CompletedAt=clock.UtcNow.AddDays(-1);submitted.ReviewStatus="PENDIENTE_REVISION";
        db.AddRange(expired,future,pastNeighbour,submitted);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var protectedState=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>a.Id==pastNeighbour.Id || a.Id==submitted.Id).OrderBy(a=>a.Id).ToArrayAsync());
        var loadBefore=(await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default)).Load(person,yesterday,yesterday);
        task.Requirement="CHECKLIST";task.ChecklistJson=JsonSerializer.Serialize(new[]{new ChecklistPoint(Guid.NewGuid(),"Verificar apertura")});task.Description="Instrucción nueva";task.LoadMinutes=12;
        await tasks.SaveAsync(actor,task,default);db.ChangeTracker.Clear();
        var request=new AssignmentRefreshRequest(task.Id,Guid.NewGuid(),clock.UtcNow.AddMinutes(3));
        var preview=await service.PreviewAsync(actor,request,default);
        Check(preview.CanApply,"expired critical task no longer blocks the batch: "+string.Join("; ",preview.Conflicts));
        var historical=preview.Differences.Single(d=>d.Before?.Id==expired.Id);
        Check(historical.PreservesExpiredAgenda && historical.After!.StartsAt==expired.StartsAt && historical.After.DueAt==expired.DueAt && historical.After.PersonId==expired.PersonId,"expired assignment keeps its agenda and responsible");
        Check(historical.After!.LoadMinutes==10 && AssignmentDefinition.ConfiguredLoad(historical.After)==12,"new configured load does not rewrite historical workload");
        Check(Sgol.Web.Pages.Tasks.RefreshModel.Changes(historical).Any(c=>c.Contains("No se recalcula ese día")),"preview explains preserved history and continuing overdue status");
        Check(!preview.Differences.Any(d=>d.Before?.Id==pastNeighbour.Id),"past neighbouring task is not part of the update");
        var next=preview.Differences.Single(d=>d.Before?.Id==future.Id);
        Check(!next.PreservesExpiredAgenda && next.After!.LoadMinutes==12 && next.After.DueAt-next.After.StartsAt==TimeSpan.FromMinutes(18),"future assignment still adopts the new duration normally");
        await service.ApplyAsync(actor,request,preview.Fingerprint,"Actualizar checklist sin recalcular días pasados",default);db.ChangeTracker.Clear();
        var saved=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==expired.Id);
        Check(saved.Requirement=="CHECKLIST" && saved.ChecklistJson==task.ChecklistJson && saved.Description==task.Description && saved.CompletedAt==null,"past task adopts requirements and remains pending");
        Check(saved.TemporalDecisionJson==expired.TemporalDecisionJson && saved.NominalStartsAt==expired.NominalStartsAt && saved.SchedulingPolicyId==expired.SchedulingPolicyId,"historical scheduling snapshot remains intact");
        var unchanged=await db.OperationalAssignments.AsNoTracking().Where(a=>a.Id==pastNeighbour.Id || a.Id==submitted.Id).ToArrayAsync();
        Check(protectedState==JsonSerializer.Serialize(unchanged.OrderBy(a=>a.Id)),"all fields of neighbours and deliveries remain unchanged");
        Check(loadBefore==(await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default)).Load(person,yesterday,yesterday),"total historical day load is unchanged");
        Check(!(await service.CountsAsync(actor,default)).ContainsKey(task.Id),"adopted historical load does not remain falsely outdated");
        await service.ApplyAsync(actor,request,preview.Fingerprint,"Actualizar checklist sin recalcular días pasados",default);db.ChangeTracker.Clear();
        Check(await db.AuditEvents.CountAsync(e=>e.ResourceId==request.Key)==1,"retry is idempotent");
        // Existing evidence prevents silently replacing the historical configuration.
        task=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Id==task.Id);task.LoadMinutes=14;
        await tasks.SaveAsync(actor,task,default);db.ChangeTracker.Clear();
        db.Add(new OperationalAttachment {AssignmentId=expired.Id,FileName="avance.pdf",ContentType="application/pdf",Content=[1,2],CreatedAt=clock.UtcNow});await db.SaveChangesAsync();db.ChangeTracker.Clear();
        preview=await service.PreviewAsync(actor,request with {Key=Guid.NewGuid()},default);
        Check(!preview.Differences.Single(d=>d.Before?.Id==expired.Id).PreservesExpiredAgenda,"existing evidence blocks the exception");
        Check(!preview.CanApply && preview.Conflicts.Any(c=>c.Contains(task.TaskCode) && c.Contains("Conflicto en")),"scheduling error names the blocked task and date");
        db.Remove(await db.OperationalAttachments.SingleAsync(a=>a.AssignmentId==expired.Id));
        var marked=await db.OperationalAssignments.SingleAsync(a=>a.Id==expired.Id);
        marked.ChecklistDraftJson=JsonSerializer.Serialize(new ChecklistDraft(actor,clock.UtcNow,[OperationalChecklist.Points(marked.ChecklistJson)[0].Id]));await db.SaveChangesAsync();db.ChangeTracker.Clear();
        preview=await service.PreviewAsync(actor,request with {Key=Guid.NewGuid()},default);
        Check(!preview.Differences.Single(d=>d.Before?.Id==expired.Id).PreservesExpiredAgenda,"checked checklist points count as advance");
    }

    public static async Task PreviewCurrentAsync(SgolDbContext db)
    {
        var clock=new CurrentClock();var direction=new EfDirectionPreparedPlanService(db,new Sgol.Web.Infrastructure.Persistence.Auditing.AuditTransaction(db),clock,
            new Sgol.BuildingBlocks.Identifiers.Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var actor=await db.RoleAssignmentVersions.Where(r=>r.RoleCode=="DIRECCION" && r.ValidTo==null).Select(r=>r.UserId).FirstAsync();
        var preview=await new AssignmentRefreshService(db,direction,clock).PreviewAsync(actor,new(Guid.Empty,Guid.NewGuid(),clock.UtcNow.AddMinutes(3)),default);
        Console.WriteLine(JsonSerializer.Serialize(new{preview.CanApply,preview.Conflicts,Changes=preview.Differences.Select(d=>new{Code=d.After?.TaskCode,d.PreservesExpiredAgenda,d.Fields,Before=d.Before?.StartsAt,After=d.After?.StartsAt,Historical=d.After?.LoadMinutes,Configured=d.After==null?0:AssignmentDefinition.ConfiguredLoad(d.After)})}));
    }
    private sealed class CurrentClock:IClock {public DateTimeOffset UtcNow=>DateTimeOffset.UtcNow;}
}
