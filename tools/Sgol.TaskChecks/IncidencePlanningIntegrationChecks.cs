using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Technical;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    public static async Task PlanningIntegrationChecks(SgolDbContext source)
    {
        var clock=new Clock();clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10);
        await using var db=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(source.Database.GetConnectionString()).Options,bonusClock:clock);
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var tasks=new OperationalTaskService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var supervision=CorrectionSchedulingChecks.Service(db,clock);
        var incidents=new OperationalIncidenceService(db,clock,supervision);
        var f=await QaFixtures.CreateAsync(db);
        foreach(var (week,day,weekly) in new[]{(45,new DateOnly(2026,11,5),false),(46,new DateOnly(2026,11,12),true)}) {
            var absence=new IncidenceRequest(f.Person,day,Guid.NewGuid(),"Ausencia anterior a generación semanal",OperationalSchedule.Instant(day),[]);
            var absencePreview=await incidents.PreviewAsync(f.Director,absence,default);
            Check(absencePreview.CanApply,"planning integration future absence before producer "+week);
            await incidents.ApplyAsync(f.Director,absence,absencePreview.Fingerprint,default);db.ChangeTracker.Clear();
            var name="Integración plan "+Guid.NewGuid();
            await tasks.SaveAsync(f.Director,new(){Revision=0,Name=name,Requirement="NINGUNO",OperationalCriticality="FLEXIBLE",StartHour=10,DeadlineHours=2,LoadMinutes=10,ScheduleMode="DIAS_FIJOS",WeekDays=16,IsWeeklyOperationsTask=weekly},default);db.ChangeTracker.Clear();
            var task=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name==name);
            var input=new DirectionPlanInput(name,"Tienda","SEMANAL",2026,week,[task.TaskCode]){IsWeeklyOperationsPlan=weekly};
            input=await QaFixtures.ConfirmAsync(db,input,clock.UtcNow);
            var created=await direction.CreateAsync(f.Director,Guid.NewGuid(),Guid.NewGuid(),input);db.ChangeTracker.Clear();
            var assigned=await db.OperationalAssignments.AsNoTracking().Where(a=>a.PlanId==created.Plan.PlanId).ToArrayAsync();
            Check(assigned.Length>0 && assigned.All(a=>a.PersonId!=f.Person),"planning integration real "+(weekly?"weekly operations":"ordinary weekly")+" creation excludes confirmed absence");
            var definition=await db.OperationalTasks.SingleAsync(t=>t.Id==task.Id);definition.Name+=" actualizada";definition.Revision++;
            await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var refresh=new AssignmentRefreshService(db,direction,clock);
            var request=new AssignmentRefreshRequest(task.Id,Guid.NewGuid(),clock.UtcNow.AddMinutes(2));
            var preview=await refresh.PreviewAsync(f.Director,request,default);
            Check(preview.CanApply,"planning integration refresh after absence offers real catalog change "+week);
            await refresh.ApplyAsync(f.Director,request,preview.Fingerprint,"Actualizar nombre conservando ausencia",default);db.ChangeTracker.Clear();
            Check(await db.OperationalAssignments.AnyAsync(a=>a.PlanId==created.Plan.PlanId && a.Name==definition.Name) &&
                !await db.OperationalAssignments.AnyAsync(a=>a.PlanId==created.Plan.PlanId && a.PersonId==f.Person),"planning integration refresh persists change without assigning absence "+week);
        }
        // A delivery from yesterday may require correction after today's absence was confirmed.
        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,3),10);
        var delivered=await Row(db,f,clock,clock.UtcNow.AddMinutes(-20));
        await tasks.CompleteAsync(f.Worker,delivered.Id,"Entrega de ayer",null,default);
        clock.UtcNow=OperationalSchedule.Instant(new(2026,11,4),10);db.ChangeTracker.Clear();
        var today=new DateOnly(2026,11,4);var req=new IncidenceRequest(f.Person,today,Guid.NewGuid(),"Ausencia con revisión histórica pendiente",clock.UtcNow.AddMinutes(2),[]);
        var p=await incidents.PreviewAsync(f.Director,req,default);Check(p.CanApply,"planning integration yesterday delivery does not prevent today's absence");
        await incidents.ApplyAsync(f.Director,req,p.Fingerprint,default);db.ChangeTracker.Clear();
        var old=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==delivered.Id);
        var reviews=new[]{new SupervisionReview(old.Id,old.Revision,"MISMA_PERSONA","Corrección tras ausencia",new("CUSTOM",today,today))};
        var review=await supervision.PreviewReviewsAsync(f.Director,reviews,default);
        Check(review.Items.Single().Error!=null,"planning integration correction cannot schedule into confirmed absence");
        await Reject(()=>supervision.ApplyReviewsAsync(f.Director,reviews,review.At,review.Fingerprint,default),"planning integration incompatible correction rejected atomically");
        db.ChangeTracker.Clear();
        Check(!await db.OperationalAssignments.AnyAsync(a=>a.ReassignedFromId==old.Id),"planning integration failed correction creates no child assignment");
        var free=new[]{reviews[0] with {Schedule=new("FREE")}};
        var freePreview=await supervision.PreviewReviewsAsync(f.Director,free,default);
        Check(freePreview.Items.Single().Error==null && WeekContract.LocalToday(freePreview.Items.Single().StartsAt!.Value)>today.AddDays(1),"planning integration free correction skips two confirmed absence days");
        await supervision.ApplyReviewsAsync(f.Director,free,freePreview.At,freePreview.Fingerprint,default);db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.AnyAsync(a=>a.ReassignedFromId==old.Id && a.PersonId==f.Person && a.StartsAt>=OperationalSchedule.Instant(today.AddDays(2))),"planning integration correction persists only on eligible date");

        var closing=await QaFixtures.CreateAsync(db);var closingDay=today.AddDays(2);
        var state=await db.Set<TemporalAssignmentState>().SingleAsync();state.ClosingTaskId=closing.Definition;
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var schedules=new PersonalScheduleService(db,direction,clock);
        var coverage=(await schedules.ReadClosingAsync(closing.Director,closingDay,default))[0];
        await schedules.SaveClosingAsync(closing.Director,new(){Day=closingDay,PersonId=closing.Person,Revision=coverage.Revision},default);db.ChangeTracker.Clear();
        var closingTask=await Row(db,closing,clock,OperationalSchedule.Instant(closingDay,19));
        var closingRequest=new IncidenceRequest(closing.Person,closingDay,Guid.NewGuid(),"Ausencia con tarea de cierre",OperationalSchedule.Instant(closingDay),[new(closingTask.Id,"TRANSFER",closing.OtherPerson,true)]);
        var closingPreview=await incidents.PreviewAsync(closing.Director,closingRequest,default);
        Check(!closingPreview.CanApply,"planning integration overload cannot bypass closing coverage");
        await Reject(async()=>await incidents.ApplyAsync(closing.Director,closingRequest,closingPreview.Fingerprint,default),"planning integration wrong closing recipient commits nothing");
        db.ChangeTracker.Clear();coverage=(await schedules.ReadClosingAsync(closing.Director,closingDay,default))[0];
        await schedules.SaveClosingAsync(closing.Director,new(){Day=closingDay,PersonId=closing.OtherPerson,Revision=coverage.Revision},default);db.ChangeTracker.Clear();
        closingPreview=await incidents.PreviewAsync(closing.Director,closingRequest,default);
        Check(closingPreview.CanApply,"planning integration explicit valid coverage resolves incidence conflict");
        await incidents.ApplyAsync(closing.Director,closingRequest,closingPreview.Fingerprint,default);db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.AnyAsync(a=>a.Id==closingTask.Id && a.PersonId==closing.OtherPerson),"planning integration closing task transfers only after coverage change");
    }
}
