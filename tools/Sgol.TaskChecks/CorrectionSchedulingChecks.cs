using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class CorrectionSchedulingChecks
{
    internal sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = TemporalEligibilityContext.At(new(2026,10,31), 805); }
    internal static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS correction: " + name); }
    internal static OperationalSupervisionService Service(SgolDbContext db, IClock clock) => new(db, clock,
        new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db)));
    internal static async Task<OperationalAssignment> Source(SgolDbContext db, QaFixtures.Actors fixture, Clock clock, int load = 10)
    {
        var definition = await db.OperationalTasks.SingleAsync(t => t.Id == fixture.Definition);
        var task = new OperationalAssignment { Id=Guid.CreateVersion7(), PlanId=fixture.Plan, TaskId=definition.Id, TaskCode=definition.TaskCode,
            Name="Corrección QA " + Guid.NewGuid().ToString("N")[..6], PersonId=fixture.Person, PersonName="QA responsable", AssignedRole=CanonicalRole.SalesFloor,
            Requirement="COMENTARIO", Comment="Entrega original conservada", LoadMinutes=load, CompletedAt=clock.UtcNow.AddMinutes(-30),
            CompletedBy=fixture.Worker, CompletionKind="ENTREGA", ReviewStatus="PENDIENTE_REVISION" };
        OperationalAgenda.Capture(task, await OperationalSchedulingPolicyService.CurrentAsync(db, default), "FLEXIBLE", clock.UtcNow.AddHours(-2));
        task.DueAt=clock.UtcNow.AddMinutes(-45);
        db.Add(task); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return task;
    }
    public static async Task Run(SgolDbContext db, bool ui)
    {
        Rules();
        var fixture=await QaFixtures.CreateAsync(db); var clock=new Clock(); var service=Service(db,clock);
        var historicalPlan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),Sgol.Organization.Contracts.BranchScope.LorettaId,fixture.Director,clock.UtcNow.AddDays(-7),"Origen histórico de correcciones QA",new(2026,10,1),new(2026,10,30));
        db.Add(historicalPlan);await db.SaveChangesAsync();db.ChangeTracker.Clear();fixture=fixture with {Plan=historicalPlan.Id};
        var source=await Source(db,fixture,clock);
        var evidence=System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 Synthetic historical evidence fixture");
        db.OperationalAttachments.Add(new(){AssignmentId=source.Id,FileName="synthetic-history.pdf",ContentType="application/pdf",Content=evidence,CreatedAt=source.CompletedAt!.Value});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var requests=new[]{new SupervisionReview(source.Id,source.Revision,"MISMA_PERSONA","Corregir evidencia",new("ASAP"))};
        var count=await db.OperationalAssignments.CountAsync(); var auditCount=await db.AuditEvents.CountAsync();
        var preview=await service.PreviewReviewsAsync(fixture.Director,requests,default);
        var item=preview.Items.Single();
        Check(item.Error==null && item.StartsAt==TemporalEligibilityContext.At(new(2026,10,31),900) && item.DueAt-item.StartsAt==TimeSpan.FromMinutes(15),"five minutes left: next complete shift, never truncated");
        Check(count==await db.OperationalAssignments.CountAsync() && auditCount==await db.AuditEvents.CountAsync(),"preview writes neither assignments nor audit");
        await service.ApplyReviewsAsync(fixture.Director,requests,preview.At,preview.Fingerprint,default); db.ChangeTracker.Clear();
        var correction=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.ReassignedFromId==source.Id);
        var original=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==source.Id);
        Check(original.Comment==source.Comment && original.CompletedAt==source.CompletedAt && original.CompletedBy==source.CompletedBy && original.StartsAt==source.StartsAt && original.DueAt==source.DueAt,"original evidence, author and dates preserved");
        Check((await db.OperationalAttachments.AsNoTracking().SingleAsync(a=>a.AssignmentId==source.Id)).Content.SequenceEqual(evidence),"original attachment bytes remain unchanged");
        Check(correction.CorrectionScheduleJson!=null && correction.PlanId==source.PlanId,"metadata persisted on same historical plan");
        Check(WeekContract.LocalToday(correction.StartsAt)>historicalPlan.EndsOn,"correction allowed after original plan end without creating another plan");
        try { await service.PreviewReviewsAsync(fixture.Worker, requests, default); throw new Exception("unauthorized review"); }
        catch (DirectionPlanException e) { Check(e.Status==403,"worker cannot bypass supervision permissions"); }
        var processAssignment=await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a=>a.AssignmentId!=null).Select(a=>a.AssignmentId).FirstOrDefaultAsync();
        if(processAssignment is {} processId)
        {
            var processTask=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==processId);
            var processPreview=await service.PreviewReviewsAsync(fixture.Director,[new(processId,processTask.Revision,"MISMA_PERSONA","No saltar proceso",new())],default);
            Check(processPreview.Items.Single().Error=="PROCESS_USE_ACTION","real process assignment cannot bypass its stage workflow through correction review");
        }
        try { await service.ApplyReviewsAsync(fixture.Director,requests,preview.At,preview.Fingerprint,default); throw new Exception("duplicate accepted"); }
        catch(DirectionPlanException e) { Check(e.Code=="CORRECTION_PREVIEW_STALE","retry rejects duplicate without partial write"); }
        var freeSource=await Source(db,fixture,clock);
        db.Add(new OperationalCapacity {Id=Guid.CreateVersion7(),PersonId=fixture.Person,From=new(2026,10,31),Through=new(2026,11,7),Percent=1,DailyMinutes=1,Reason="QA exhausted"});
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var free=new[]{new SupervisionReview(freeSource.Id,1,"MISMA_PERSONA","Hueco",new("FREE"))};
        var fp=await service.PreviewReviewsAsync(fixture.Director,free,default);
        Check(fp.Items.Single().StartsAt==correction.DueAt,"free slot skips reservation despite exhausted daily capacity");
        var other=await Source(db,fixture,clock);
        var batch=new[]{free[0],new SupervisionReview(other.Id,1,"MISMA_PERSONA","Segundo hueco",new("FREE"))};
        var bp=await service.PreviewReviewsAsync(fixture.Director,batch,default);
        Check(bp.Items[1].StartsAt==bp.Items[0].DueAt,"batch reserves consecutive distinct slots");
        await service.ApplyReviewsAsync(fixture.Director,batch,bp.At,bp.Fingerprint,default); db.ChangeTracker.Clear();
        var invalid=await Source(db,fixture,clock,4800); var valid=await Source(db,fixture,clock);
        var invalidBatch=new[]{new SupervisionReview(valid.Id,1,"MISMA_PERSONA","Valid",new("FREE")),new SupervisionReview(invalid.Id,1,"MISMA_PERSONA","Too long",new("FREE"))};
        var bad=await service.PreviewReviewsAsync(fixture.Director,invalidBatch,default);
        Check(bad.Items[1].Error=="CORRECTION_NO_SLOT","overlong work produces task-specific diagnostic");
        count=await db.OperationalAssignments.CountAsync();auditCount=await db.AuditEvents.CountAsync();
        try { await service.ApplyReviewsAsync(fixture.Director,invalidBatch,bad.At,bad.Fingerprint,default); throw new Exception("partial batch accepted"); }
        catch(DirectionPlanException) { }
        Check(count==await db.OperationalAssignments.CountAsync() && auditCount==await db.AuditEvents.CountAsync() && (await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==valid.Id)).ReviewStatus=="PENDIENTE_REVISION","invalid batch rolls back all assignments and audits");
        var approved=await Source(db,fixture,clock);var closed=await Source(db,fixture,clock);var fixedTask=await Source(db,fixture,clock);
        var mixed=new[]{new SupervisionReview(approved.Id,1,"CONCLUIDA",null),new SupervisionReview(closed.Id,1,"NO_SATISFECHA","Cierre"),new SupervisionReview(fixedTask.Id,1,"MISMA_PERSONA","Corrección",new())};
        var mixedPreview=await service.PreviewReviewsAsync(fixture.Director,mixed,default);
        await service.ApplyReviewsAsync(fixture.Director,mixed,mixedPreview.At,mixedPreview.Fingerprint,default);db.ChangeTracker.Clear();
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==approved.Id)).ReviewStatus=="CONCLUIDA" && (await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==closed.Id)).ReviewStatus=="NO_SATISFECHA" && await db.OperationalAssignments.CountAsync(a=>a.ReassignedFromId==fixedTask.Id)==1,"mixed batch approves, closes and schedules atomically");
        var tomorrow=new DateOnly(2026,11,1);
        var custom=new[]{new SupervisionReview(valid.Id,1,"MISMA_PERSONA","Mañana",new("CUSTOM",tomorrow,tomorrow))};
        var cp=await service.PreviewReviewsAsync(fixture.Director,custom,default);
        Check(cp.Items.Single().StartsAt==TemporalEligibilityContext.At(tomorrow,540),"custom stays in chosen day across month boundary");
        var outOfRange=await service.PreviewReviewsAsync(fixture.Director,[custom[0] with {Schedule=new("CUSTOM",tomorrow.AddDays(7))}],default);
        Check(outOfRange.Items.Single().Error=="CORRECTION_RANGE_INVALID","today plus eight rejected");
        var boundary=await service.PreviewReviewsAsync(fixture.Director,[custom[0] with {Schedule=new("CUSTOM",new(2026,11,7))}],default);
        Check(boundary.Items.Single().StartsAt is {} boundaryStart && WeekContract.LocalToday(boundaryStart)==new DateOnly(2026,11,7),"today plus seven included");
        var schedule=new PersonalScheduleService(db,new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db)),clock);
        var change=await schedule.ReadExceptionAsync(fixture.Director,fixture.Worker,new(2026,10,31),default);
        change.Reason="QA absence";change.Hours=new(){Mode="REST"};
        var impact=await schedule.PreviewExceptionAsync(fixture.Director,change,default);change.Fingerprint=impact.Fingerprint;
        Check(impact.Conflicts.Any(c=>c.AssignmentId==correction.Id),"schedule change preview identifies correction");
        await schedule.SaveExceptionAsync(fixture.Director,change,default);db.ChangeTracker.Clear();
        Check((await service.CorrectionConflictsAsync(fixture.Director,default)).ContainsKey(correction.Id),"persistent derived conflict after absence/exception");
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==correction.Id)).StartsAt==correction.StartsAt,"schedule change never moves correction automatically");
        try { await service.ApplyReviewsAsync(fixture.Director,custom,cp.At,cp.Fingerprint,default);throw new Exception("stale accepted"); }
        catch(DirectionPlanException e) {Check(e.Code=="CORRECTION_PREVIEW_STALE","schedule edit invalidates preview");}
        var partial=await db.OperationalAssignments.SingleAsync(a=>a.Id==correction.Id);
        partial.GoalUnits=5;partial.ProgressUnits=2;partial.ChecklistDraftJson=JsonSerializer.Serialize(new ChecklistDraft(fixture.Worker,clock.UtcNow,[Guid.NewGuid()]));partial.Revision++;
        db.OperationalProgressEntries.Add(new(){Id=Guid.CreateVersion7(),AssignmentId=partial.Id,ActorId=fixture.Worker,OccurredAt=clock.UtcNow,Units=2,Note="Avance que debe conservarse"});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();correction=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==correction.Id);
        var reschedule=new[]{new SupervisionReview(correction.Id,correction.Revision,"REPROGRAMAR","Ausencia registrada",new("CUSTOM",tomorrow))};
        var rp=await service.PreviewReviewsAsync(fixture.Director,reschedule,default);
        await service.ApplyReviewsAsync(fixture.Director,reschedule,rp.At,rp.Fingerprint,default);db.ChangeTracker.Clear();
        var moved=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==correction.Id);
        Check(moved.Id==correction.Id && moved.ReassignedFromId==source.Id && moved.ReassignmentComment==correction.ReassignmentComment && moved.ChecklistDraftJson==correction.ChecklistDraftJson && moved.ProgressUnits==correction.ProgressUnits && moved.StartsAt==TemporalEligibilityContext.At(tomorrow,540),"explicit reprogramming retains identity, origin, comment, draft and progress");
        Check(await db.OperationalProgressEntries.AnyAsync(p=>p.AssignmentId==correction.Id && p.Units==2 && p.ActorId==fixture.Worker),"reprogramming preserves recorded progress attribution");
        Check(!(await service.CorrectionConflictsAsync(fixture.Director,default)).ContainsKey(correction.Id),"conflict clears only after valid explicit reprogramming");
        var concurrent=await Source(db,fixture,clock);
        var raceRequest=new[]{new SupervisionReview(concurrent.Id,1,"MISMA_PERSONA","Concurrente",new("FREE"))};
        var racePreview=await service.PreviewReviewsAsync(fixture.Director,raceRequest,default);
        async Task<bool> Race() { await using var context=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options);try {await Service(context,clock).ApplyReviewsAsync(fixture.Director,raceRequest,racePreview.At,racePreview.Fingerprint,default);return true;}catch(DirectionPlanException e) when(e.Code=="CORRECTION_PREVIEW_STALE"){return false;} }
        var results=await Task.WhenAll(Race(),Race());
        Check(results.Count(x=>x)==1 && await db.OperationalAssignments.CountAsync(a=>a.ReassignedFromId==concurrent.Id)==1,"two simultaneous supervisors have one winner and one correction");
        var roleSource=await Source(db,fixture,clock);
        var rolePreview=await service.PreviewReviewsAsync(fixture.Director,[new(roleSource.Id,1,"ROL","Más temprano",new())],default);
        Check(rolePreview.Items.Single().StartsAt is {} roleStart && WeekContract.LocalToday(roleStart)==new DateOnly(2026,10,31) && rolePreview.Items.Single().PersonId!=fixture.Person,"role selection prioritizes earliest eligible start over balancing");
        clock.UtcNow=TemporalEligibilityContext.At(tomorrow,600);
        var immediateSource=await Source(db,fixture,clock);
        var immediateRequest=new[]{new SupervisionReview(immediateSource.Id,1,"MISMA_PERSONA","Ahora",new())};
        var immediatePreview=await service.PreviewReviewsAsync(fixture.Director,immediateRequest,default);clock.UtcNow=clock.UtcNow.AddSeconds(30);
        await service.ApplyReviewsAsync(fixture.Director,immediateRequest,immediatePreview.At,immediatePreview.Fingerprint,default);db.ChangeTracker.Clear();
        var immediateSaved=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.ReassignedFromId==immediateSource.Id);
        Check(immediateSaved.StartsAt==clock.UtcNow && immediateSaved.DueAt-immediateSaved.StartsAt==TimeSpan.FromMinutes(15),"immediate confirmation recalculates instant without shortening duration");
        clock.UtcNow=TemporalEligibilityContext.At(tomorrow,795);
        var edgeSource=await Source(db,fixture,clock);var edgeRequest=new[]{new SupervisionReview(edgeSource.Id,1,"MISMA_PERSONA","Fin de turno",new())};
        var edgePreview=await service.PreviewReviewsAsync(fixture.Director,edgeRequest,default);clock.UtcNow=clock.UtcNow.AddSeconds(30);
        try{await service.ApplyReviewsAsync(fixture.Director,edgeRequest,edgePreview.At,edgePreview.Fingerprint,default);throw new Exception("silently moved to next shift");}
        catch(DirectionPlanException e){Check(e.Code=="CORRECTION_PREVIEW_STALE" && !await db.OperationalAssignments.AnyAsync(a=>a.ReassignedFromId==edgeSource.Id),"losing complete immediate slot requires new explicit confirmation");}
        clock.UtcNow=TemporalEligibilityContext.At(new(2026,10,31),805);
        var httpSource=await Source(db,fixture,clock);
        await CorrectionHttpChecks.Run(db,fixture,clock,httpSource,ui);
    }

    private static void Rules()
    {
        var day=new DateOnly(2026,10,31);var now=TemporalEligibilityContext.At(day,540);var person=Guid.NewGuid();
        var worker=new CapacityWorker(person,"QA","QA",CanonicalRole.SalesFloor,null,null);
        var task=new OperationalAssignment {Id=Guid.NewGuid(),TaskId=Guid.NewGuid(),PersonId=person,AssignedRole=CanonicalRole.SalesFloor,LoadMinutes=10,ClosureMarginPercent=50,StartsAt=now,DueAt=now.AddMinutes(15)};
        var block=new OperationalAssignment {Id=Guid.NewGuid(),PersonId=person,StartsAt=now,DueAt=now.AddMinutes(30),LoadMinutes=20};
        var temporal=new TemporalEligibilityContext {Enabled=true,Schedules=[new(){Id=Guid.NewGuid(),PersonId=person,DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,600),new WorkInterval(660,720)}))}]};
        CapacityLedger Ledger(OperationalAssignment[] blocks, HashSet<(Guid,DateOnly)>? absent=null, CapacityWorker? candidate=null, Dictionary<Guid,long>? legacy=null) =>
            new([candidate??worker],blocks,[new(){PersonId=person,From=day,Through=day.AddDays(7),Percent=0}],absent??[],legacy??[],day){Temporal=temporal};
        var ledger=Ledger([block]);var before=JsonSerializer.Serialize(block);
        Check(CorrectionScheduling.Find(ledger,person,task,now,new()).StartsAt==now,"ASAP is default and allows overlap with zero daily capacity");
        Check(CorrectionScheduling.Find(ledger,person,task,now,new("FREE")).StartsAt==block.DueAt,"FREE reserves actual gap and ignores only daily load cap");
        Check(JsonSerializer.Serialize(block)==before,"search leaves neighboring work unchanged");
        Check(CorrectionScheduling.Find(Ledger([]),person,task,now.AddMinutes(55),new()).StartsAt==TemporalEligibilityContext.At(day,660),"break cannot split correction duration");
        Check(WeekContract.LocalToday(CorrectionScheduling.Find(Ledger([],[(person,day)]),person,task,now,new()).StartsAt)==day.AddDays(1),"absence never bypassed by ASAP");
        Check(!CorrectionScheduling.Find(Ledger([],[(person,day)]),person,task,now,new("CUSTOM",day,day)).Eligible,"custom absent day fails without escaping range");
        Check(!CorrectionScheduling.Find(Ledger([],candidate:worker with {EmploymentEnd=now.AddMinutes(10)}),person,task,now,new()).Eligible,"future employment limit must contain full duration");
        task.LoadMinutes=50;
        Check(!CorrectionScheduling.Find(Ledger([]),person,task,now,new()).Eligible,"no shift long enough rejects rather than splitting");task.LoadMinutes=10;
        block.ScheduleMode="PROGRESO";block.GoalUnits=10;block.DueAt=now.AddDays(7);
        Check(CorrectionScheduling.Find(Ledger([block]),person,task,now,new("FREE")).StartsAt==now,"distributed progress deadline does not reserve an entire week");
        block.ReassignedFromId=Guid.NewGuid();block.DueAt=now.AddMinutes(30);
        Check(CorrectionScheduling.Find(Ledger([block]),person,task,now,new("FREE")).StartsAt==block.DueAt,"progress correction still reserves its explicit execution interval");
        Check(!CorrectionScheduling.Find(Ledger([],legacy:new(){{person,30}}),person,task,now,new("FREE")).Eligible,"unknown legacy reservations have explicit diagnosis");
        task.SpecializationId=Guid.NewGuid();
        Check(!CorrectionScheduling.Find(Ledger([]),person,task,now,new()).Eligible,"ASAP never bypasses specialization eligibility");
        task.SpecializationId=null;task.TaskCode="TAR-0032";block.ReassignedFromId=null;block.ScheduleMode="DISTRIBUIDA";block.GoalUnits=0;block.TaskCode="TAR-0004";
        Check(CorrectionScheduling.Find(Ledger([block]),person,task,now,new()).StartsAt==block.DueAt,"ASAP preserves mandatory cash circuit predecessor");
        task.TaskCode="QA";task.OperationalCriticality="CRITICA";task.NominalStartsAt=now.AddDays(-1);
        Check(CorrectionScheduling.Find(Ledger([]),person,task,now,new()).StartsAt==now,"critical correction does not inherit an expired historical anchor");
    }
}
