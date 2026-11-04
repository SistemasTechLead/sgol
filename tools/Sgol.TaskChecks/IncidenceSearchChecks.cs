using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    static async Task SearchChecks(SgolDbContext db, Clock clock)
    {
        clock.UtcNow = OperationalSchedule.Instant(new(2026,11,4),10);
        var service = new OperationalIncidenceService(db,clock,CorrectionSchedulingChecks.Service(db,clock));
        foreach(var offset in new[]{0,1})
        {
            var f=await QaFixtures.CreateAsync(db);
            var day=new DateOnly(2026,11,4).AddDays(offset);
            var task=await Row(db,f,clock,OperationalSchedule.Instant(day,11),30);
            var request=new IncidenceRequest(f.Person,day,Guid.NewGuid(),"Reparto automático con sobrecarga revisada",offset==0?clock.UtcNow.AddMinutes(2):OperationalSchedule.Instant(day),[new(task.Id,"TRANSFER",null,true)],true);
            var original=await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync();
            var preview=await service.PreviewAsync(f.Director,request,default);
            Check(preview.CanApply && preview.Changes.Single().After is {} after && after.PersonId!=f.Person && preview.Changes.Single().OverloadAuthorized,
                $"automatic overload identifies actual recipient before confirmation day+{offset}");
            Check(preview.Request.Reflow && preview.Changes.All(c=>c.Action!="MOVE"),"reflow permission preserves neighbours when fixed slots solve the incident");
            if(offset==0)
            {
                // Physical row order is not a business change. Equal timestamps and
                // revisions must not make a reviewed proposal depend on query layout.
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ DECLARE fact_id uuid; BEGIN
                    FOR fact_id IN SELECT "Id" FROM task_bonus_fact ORDER BY "Id" DESC LOOP
                    UPDATE task_bonus_fact SET "Kind"="Kind" WHERE "Id"=fact_id;
                    END LOOP; END $$;
                    """);
                var reordered=await service.PreviewAsync(f.Director,request,default);
                Check(reordered.Fingerprint==preview.Fingerprint,"physical ordering of unchanged bonus facts does not invalidate preview");
            }
            var receipt=await service.ApplyAsync(f.Director,request,preview.Fingerprint,default);db.ChangeTracker.Clear();
            Check((await service.ApplyAsync(f.Director,request,preview.Fingerprint,default)).Id==receipt.Id,"automatic overload with reflow confirms and replays idempotently");
            var neighbours=await db.OperationalAssignments.AsNoTracking().Where(a=>a.Id!=task.Id).OrderBy(a=>a.Id).ToArrayAsync();
            Check(JsonSerializer.Serialize(original.Where(a=>a.Id!=task.Id))==JsonSerializer.Serialize(neighbours),"preserved solution leaves unrelated assignments unchanged");
        }
        var bounded=await QaFixtures.CreateAsync(db);
        var one=await Row(db,bounded,clock,clock.UtcNow.AddHours(1));
        var two=await Row(db,bounded,clock,clock.UtcNow.AddHours(2));
        var impossible=await Row(db,bounded,clock,clock.UtcNow.AddHours(3));
        var directorPerson=await db.AppUsers.Where(u=>u.Id==bounded.Director).Select(u=>u.PersonId).SingleAsync();
        var blockedRequest=new IncidenceRequest(bounded.Person,new(2026,11,4),Guid.NewGuid(),"Dominio posterior imposible no agota búsqueda",clock.UtcNow.AddMinutes(2),
            [new(one.Id,"TRANSFER",bounded.OtherPerson),new(two.Id,"TRANSFER",bounded.OtherPerson),new(impossible.Id,"TRANSFER",directorPerson)]);
        var blocked=await service.PreviewAsync(bounded.Director,blockedRequest,default);
        Check(!blocked.CanApply && blocked.Cases.All(c=>!c.Cause.Contains("20,000")),"empty later domain prunes combinatorial slot search without exhausting budget");
        await Reject(async()=>await service.ApplyAsync(bounded.Director,blockedRequest,blocked.Fingerprint,default),"infeasible automatic search never commits partial changes");

        var reflow=await QaFixtures.CreateAsync(db);
        var critical=await Row(db,reflow,clock,clock.UtcNow.AddHours(1));
        var neighbour=await Row(db,reflow with {Person=reflow.OtherPerson},clock,clock.UtcNow.AddHours(1),20);
        var tracked=await db.OperationalAssignments.SingleAsync(a=>a.Id==critical.Id);
        tracked.OperationalCriticality="CRITICA";tracked.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var reflowRequest=new IncidenceRequest(reflow.Person,new(2026,11,4),Guid.NewGuid(),"Reacomodo requerido para una hora crítica",clock.UtcNow.AddMinutes(2),[new(critical.Id,"TRANSFER",reflow.OtherPerson)]);
        Check(!(await service.PreviewAsync(reflow.Director,reflowRequest,default)).CanApply,"critical transfer detects occupied slot without reflow");
        var moved=await service.PreviewAsync(reflow.Director,reflowRequest with {Reflow=true},default);
        Check(moved.CanApply && moved.Changes.Any(c=>c.Before.Id==neighbour.Id && c.Action=="MOVE"),"reflow still moves a relevant neighbour when preservation cannot solve");
        await service.ApplyAsync(reflow.Director,reflowRequest with {Reflow=true},moved.Fingerprint,default);db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.AnyAsync(a=>a.Id==critical.Id && a.PersonId==reflow.OtherPerson),"required reflow confirms complete joint proposal");
    }
}
