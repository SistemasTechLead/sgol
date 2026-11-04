using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    static async Task StateAuditChecks(SgolDbContext db, Clock clock)
    {
        var service=new OperationalIncidenceService(db,clock,CorrectionSchedulingChecks.Service(db,clock));
        foreach(var mutation in new[]{"role","capacity","availability","schedule","attachment","progress"})
        {
            clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10);
            var f=await QaFixtures.CreateAsync(db);
            var a=await Row(db,f,clock,clock.UtcNow.AddHours(1));
            var request=new IncidenceRequest(f.Person,new(2026,11,4),Guid.NewGuid(),"Cambio concurrente de "+mutation,clock.UtcNow.AddMinutes(2),[new(a.Id,"TRANSFER",f.OtherPerson)]);
            var preview=await service.PreviewAsync(f.Director,request,default);
            Check(preview.CanApply,$"state audit {mutation}: initial review");
            switch(mutation)
            {
                case "role":
                    var role=await db.RoleAssignmentVersions.SingleAsync(r=>r.UserId==f.Other && r.Status==RoleAssignmentStatus.Active);
                    db.Entry(role).Property(r=>r.RoleCode).CurrentValue=CanonicalRole.Subcoordination; break;
                case "capacity":
                    db.Add(new OperationalCapacity {Id=Guid.CreateVersion7(),PersonId=f.OtherPerson,From=request.Day,Through=request.Day,Percent=0,Reason="Cambio concurrente aislado"}); break;
                case "availability":
                    db.Add(new AvailabilityDayVersion(Guid.CreateVersion7(),f.OtherPerson,BranchScope.LorettaId,request.Day,false,f.Director)); break;
                case "schedule":
                    var schedule=await db.Set<PersonalScheduleVersion>().SingleAsync(s=>s.PersonId==f.OtherPerson);
                    schedule.Superseded=true;
                    db.Add(new PersonalScheduleVersion {Id=Guid.CreateVersion7(),PersonId=f.OtherPerson,From=request.Day,
                        Revision=schedule.Revision+1,DaysJson="{}",ChangedBy=f.Director,ChangedAt=clock.UtcNow,
                        IntentKey=Guid.NewGuid(),RequestHash="isolated-concurrent-schedule"}); break;
                case "attachment":
                    db.Add(new OperationalAttachment {AssignmentId=a.Id,FileName="concurrent.pdf",ContentType="application/pdf",Content=[1,2,3],CreatedAt=clock.UtcNow}); break;
                case "progress":
                    var row=await db.OperationalAssignments.SingleAsync(x=>x.Id==a.Id);
                    row.Comment="Trabajo iniciado durante la revisión"; row.Revision++; break;
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await Reject(async()=>await service.ApplyAsync(f.Director,request,preview.Fingerprint,default),$"state audit {mutation}: stale confirmation rejected");
            Check(!await db.Set<OperationalIncidence>().AnyAsync(i=>i.PersonId==f.Person) &&
                await db.OperationalAssignments.AnyAsync(x=>x.Id==a.Id && x.PersonId==f.Person),$"state audit {mutation}: no partial absence or transfer");
        }

        foreach(var retire in new[]{false,true})
        {
            var f=await QaFixtures.CreateAsync(db);
            var a=await Row(db,f,clock,clock.UtcNow.AddHours(1),40,10,OperationalSchedule.Instant(new(2026,11,5)));
            var row=await db.OperationalAssignments.SingleAsync(x=>x.Id==a.Id);
            var point=Guid.NewGuid();
            row.ProgressUnits=3; row.Comment="Autora original y avance parcial";
            row.ChecklistJson=JsonSerializer.Serialize(new[]{new ChecklistPoint(point,"Verificación original")});
            row.ChecklistDraftJson=JsonSerializer.Serialize(new ChecklistDraft(f.Worker,clock.UtcNow,[point])); row.Revision++;
            db.Add(new OperationalAttachment {AssignmentId=a.Id,FileName="original.pdf",ContentType="application/pdf",Content=[11,22,33],CreatedAt=clock.UtcNow});
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var original=await db.OperationalAssignments.AsNoTracking().SingleAsync(x=>x.Id==a.Id);
            var request=new IncidenceRequest(f.Person,new(2026,11,4),Guid.NewGuid(),"Conservar avance y autoría de evidencia",clock.UtcNow.AddMinutes(2),[]);
            Check(!(await service.PreviewAsync(f.Director,request,default)).CanApply,"activity audit requires explicit decision for progress and evidence");
            request=request with {Decisions=[new(a.Id,retire?"RETIRE":"TRANSFER",retire?null:f.OtherPerson,true)]};
            var preview=await service.PreviewAsync(f.Director,request,default);
            Check(preview.CanApply,$"activity audit reviewed retirement={retire}");
            await service.ApplyAsync(f.Director,request,preview.Fingerprint,default); db.ChangeTracker.Clear();
            var saved=await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().SingleAsync(x=>x.Id==a.Id);
            Check(saved.ProgressUnits==original.ProgressUnits && saved.LoadMinutes==original.LoadMinutes && saved.GoalUnits==original.GoalUnits &&
                saved.Comment==original.Comment && saved.ChecklistDraftJson==original.ChecklistDraftJson && saved.ChecklistJson==original.ChecklistJson &&
                (await db.OperationalAttachments.SingleAsync(x=>x.AssignmentId==a.Id)).Content.SequenceEqual(new byte[]{11,22,33}),
                $"activity audit retirement={retire} preserves progress, effort, checklist authorship and file bytes");
        }

        foreach(var day in new[]{new DateOnly(2026,11,8),new DateOnly(2026,11,30)})
        {
            clock.UtcNow=OperationalSchedule.Instant(day.AddDays(1)).AddTicks(-1000000);
            var f=await QaFixtures.CreateAsync(db);
            var request=new IncidenceRequest(f.Person,day,Guid.NewGuid(),"Límite de medianoche local",OperationalSchedule.Instant(day),[]);
            var p=await service.PreviewAsync(f.Director,request,default);
            clock.UtcNow=OperationalSchedule.Instant(day.AddDays(1));
            await Reject(async()=>await service.ApplyAsync(f.Director,request,p.Fingerprint,default),$"calendar audit {day}: midnight makes reviewed day past");
            Check(!await db.Set<OperationalIncidence>().AnyAsync(i=>i.PersonId==f.Person),"calendar audit midnight rejection is atomic");
            request=request with {Day=day.AddDays(1),Key=Guid.NewGuid(),NotBefore=clock.UtcNow};
            p=await service.PreviewAsync(f.Director,request,default);
            Check(p.CanApply,$"calendar audit {day}: next week/month date accepted");
            await service.ApplyAsync(f.Director,request,p.Fingerprint,default);
        }
        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10);
    }
}
