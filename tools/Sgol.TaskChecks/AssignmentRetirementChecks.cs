using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class AssignmentRetirementChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS retirement: " + message); }
    private static async Task Reject(Func<Task> action, string code)
    {
        try { await action(); throw new Exception("Expected " + code); }
        catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS retirement: rejects " + code); }
    }
    public static async Task RunAsync(SgolDbContext db, OperationalTaskService tasks, IDirectionPreparedPlanService direction, IClock clock,
        Guid actor, Guid collaborator, Guid person, Guid otherPerson)
    {
        var service = new AssignmentRefreshService(db,direction,clock);
        var day = WeekContract.LocalToday(clock.UtcNow);
        await tasks.SaveAsync(actor,new OperationalTask { Name="Retiro de meta",Revision=0,ScheduleMode="PROGRESO",GoalUnits=100,ProgressUnit="piezas",LoadMinutes=100,OperationalCriticality="FLEXIBLE",Requirement="NINGUNO" },default);
        var t = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Retiro de meta");
        var plan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,actor,clock.UtcNow,"Historial de retiro",day,day.AddDays(6)); db.Add(plan);
        var row = new OperationalAssignment { Id=Guid.CreateVersion7(),TaskId=t.Id,TaskCode=t.TaskCode,PlanId=plan.Id,PersonId=person,PersonName="A",AssignedRole=t.AssignedRole,
            StartsAt=clock.UtcNow.AddHours(-1),DueAt=clock.UtcNow.AddDays(3),ScheduleKey="retire:"+Guid.NewGuid().ToString("N") };
        AssignmentDefinition.Adopt(row,t,null,false); row.ProgressUnits=20; row.TransferRemainingMinutes=40; row.TransferProgressUnits=10;
        var parent = AssignmentDefinition.Copy(row); parent.Id=Guid.CreateVersion7(); parent.ScheduleKey="parent:"+Guid.NewGuid().ToString("N");
        parent.ReviewStatus="REASIGNADA"; parent.CompletedAt=clock.UtcNow.AddHours(-2); parent.CompletedBy=actor; parent.CompletionKind="SUPERVISION";
        row.ReassignedFromId=parent.Id;
        var delivered=AssignmentDefinition.Copy(row); delivered.Id=Guid.CreateVersion7(); delivered.ScheduleKey="delivered:"+Guid.NewGuid().ToString("N");
        delivered.ReassignedFromId=null; delivered.ProgressUnits=100; delivered.CompletedAt=clock.UtcNow; delivered.CompletedBy=collaborator; delivered.CompletionKind="ENTREGA"; delivered.ReviewStatus="PENDIENTE_REVISION";
        db.AddRange(parent,row,delivered);
        db.Add(new OperationalProgressEntry { Id=Guid.CreateVersion7(),AssignmentId=row.Id,ActorId=collaborator,OccurredAt=clock.UtcNow,Units=20,Note="Autoría conservada" });
        db.Add(new OperationalAttachment { AssignmentId=row.Id,FileName="previo.pdf",ContentType="application/pdf",Content=[1,2,3,4],CreatedAt=clock.UtcNow });
        db.Add(new OperationalTransfer { Id=Guid.CreateVersion7(),AssignmentId=row.Id,TaskName=row.Name,FromPersonId=otherPerson,FromName="B",ToPersonId=person,ToName="A",
            ActorId=actor,At=clock.UtcNow.AddMinutes(-30),PreviousStartsAt=row.StartsAt,PreviousDueAt=row.DueAt,NewDueAt=row.DueAt,ProgressUnits=10,RemainingMinutes=40,Reason="Transferencia anterior",SnapshotJson=JsonSerializer.Serialize(row) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var protectedBefore=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a => a.Id==parent.Id || a.Id==delivered.Id).OrderBy(a=>a.Id).ToArrayAsync());
        await tasks.ChangeAsync(actor,t.Id,t.Revision,false,default); db.ChangeTracker.Clear();
        var request=new AssignmentRefreshRequest(t.Id,Guid.NewGuid(),clock.UtcNow.AddMinutes(3));
        var preview=await service.PreviewAsync(actor,request,default);
        Check(!preview.CanApply && preview.Decisions.Single().Code=="inactive" && preview.DecisionConflicts.Length==1,"inactive work explains the precise checkbox, without requiring calendar or effort options");
        Check((await service.CountsAsync(actor,default))[t.Id]==1,"inactive pending work keeps the global action enabled");
        await Reject(()=>service.ApplyAsync(actor,request,preview.Fingerprint,"Sin autorización",default),"REFRESH_STALE");
        request=request with { RetireInactiveAssignments=true };
        preview=await service.PreviewAsync(actor,request,default);
        Check(preview.CanApply && preview.Differences.Single().Retire && preview.Decisions.Single().Accepted,"explicit retirement accepts advance, evidence, estimate and correction history");
        Check(await db.OperationalAssignments.AnyAsync(a=>a.Id==row.Id),"preview leaves live assignments untouched");
        await service.ApplyAsync(actor,request,preview.Fingerprint,"La tarea ya no se realiza",default); db.ChangeTracker.Clear();
        Check(!await db.OperationalAssignments.AnyAsync(a=>a.Id==row.Id),"retired assignment disappears from operational queries");
        var retired=await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().SingleAsync(a=>a.Id==row.Id);
        Check(retired.RetiredAt==clock.UtcNow && retired.CompletedAt==null && retired.ReviewStatus==null && retired.ProgressUnits==20 && retired.ReassignedFromId==parent.Id && retired.RemainingMinutes==0,
            "retirement does not fake delivery, failure or lost progress and releases all remaining load");
        Check((await db.OperationalAttachments.SingleAsync(a=>a.AssignmentId==row.Id)).Content.SequenceEqual(new byte[]{1,2,3,4}) && await db.OperationalProgressEntries.AnyAsync(a=>a.AssignmentId==row.Id && a.ActorId==collaborator && a.Units==20),"evidence bytes, advance and original author remain intact");
        Check(await db.Set<OperationalTransfer>().CountAsync(a=>a.AssignmentId==row.Id)==1,"immutable transfer receipt survives retirement");
        Check(protectedBefore==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>a.Id==parent.Id||a.Id==delivered.Id).OrderBy(a=>a.Id).ToArrayAsync()),"delivered and replaced historical assignments keep every field");
        Check(!(await tasks.MineAsync(collaborator,default)).Any(a=>a.Id==row.Id) && !(await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default)).Tasks.Any(a=>a.Id==row.Id),"Mis tareas and capacity no longer include the retired obligation");
        Check(!(await new LiveBoardReader(db,clock).ReadAsync(actor,plan.Id)).Tasks.Any(a=>a.Id==row.Id),"Board no longer displays the retired task");
        Check((await tasks.LifecycleHistoryAsync(collaborator,row.Id,default)).Any(e=>e.Kind=="RETIRO") && (await tasks.ContributionsAsync(collaborator,row.Id,default)).Sum(c=>c.Units)==20,
            "authorized historical access still shows retirement and contributions");
        Check((await service.CountsAsync(actor,default)).Count==0,"the global indicator clears after retirement");
        await Reject(()=>tasks.ProgressAsync(collaborator,row.Id,row.Revision,1,"Intento antiguo",default),"TASK_NOT_FOUND");
        var auditCount=await db.AuditEvents.CountAsync();
        await service.ApplyAsync(actor,request,preview.Fingerprint,"La tarea ya no se realiza",default);
        Check(await db.AuditEvents.CountAsync()==auditCount,"repeating retirement is idempotent");
        var archivePreview=await direction.PreviewDeletionAsync(actor,plan.Id,default);
        Check(archivePreview.Assignments==3 && archivePreview.ProgressEntries==1,"plan archive and deletion preview count retired history too");
        var archive=await direction.DownloadArchiveAsync(actor,plan.Id,archivePreview.Fingerprint,default);
        await using(archive.Content) { using var zip=new ZipArchive(archive.Content,ZipArchiveMode.Read,true); Check(zip.Entries.Any(e=>e.Name.Contains("previo.pdf")),"plan backup retains the retired assignment's evidence"); }

        await tasks.SaveAsync(actor,new OperationalTask { Name="Activa fuera del plan",Revision=0,Requirement="NINGUNO",OperationalCriticality="FLEXIBLE" },default);
        var outsideTask=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name=="Activa fuera del plan");
        var weekly=new DirectionPreparedPlan(Guid.CreateVersion7(),BranchScope.LorettaId,actor,clock.UtcNow,new("Semana de prueba","Prueba","SEMANAL",day.Year,ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue)),[outsideTask.TaskCode],IsWeeklyOperationsPlan:true));
        weekly.MarkAssigned(); db.Add(weekly);
        var outside=new OperationalAssignment { Id=Guid.CreateVersion7(),TaskId=outsideTask.Id,TaskCode=outsideTask.TaskCode,PlanId=weekly.Id,PersonId=person,PersonName="A",AssignedRole=outsideTask.AssignedRole,StartsAt=clock.UtcNow,DueAt=clock.UtcNow.AddHours(1),ReassignedFromId=delivered.Id };
        AssignmentDefinition.Adopt(outside,outsideTask,null,false); db.Add(outside); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        request=new(outsideTask.Id,Guid.NewGuid(),clock.UtcNow.AddMinutes(3));
        preview=await service.PreviewAsync(actor,request,default);
        Check(!preview.CanApply && preview.Decisions.Single().Code=="excluded" && preview.Decisions.Single().Title.Contains("salieron del plan"),"active tasks outside the weekly plan are correctly distinguished from inactive tasks");
        Check((await service.CountsAsync(actor,default)).ContainsKey(outsideTask.Id),"plan membership mismatch is detected even with a matching definition snapshot");
        var wrong=await service.PreviewAsync(actor,request with { RetireInactiveAssignments=true },default);
        Check(!wrong.CanApply,"the inactive checkbox cannot silently authorize removal of an active task outside its plan");
        request=request with { RetireExcludedAssignments=true };
        preview=await service.PreviewAsync(actor,request,default);
        Check(preview.CanApply && preview.Differences.Single().Retire,"explicit plan-exit retirement accepts a correction assignment");
        await service.ApplyAsync(actor,request,preview.Fingerprint,"Se retiró del plan semanal",default); db.ChangeTracker.Clear();
        Check(!await db.OperationalAssignments.AnyAsync(a=>a.Id==outside.Id) && (await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Id==outsideTask.Id)).Active,"removing an excluded assignment keeps its catalogue task active");

        t=await db.OperationalTasks.AsNoTracking().SingleAsync(x=>x.Id==t.Id); await tasks.ChangeAsync(actor,t.Id,t.Revision,false,default); db.ChangeTracker.Clear();
        t=await db.OperationalTasks.AsNoTracking().SingleAsync(x=>x.Id==t.Id);
        var next=AssignmentDefinition.Copy(retired); next.Id=Guid.CreateVersion7(); next.RetiredAt=null; next.ProgressUnits=0; next.TransferRemainingMinutes=null; next.TransferProgressUnits=0; next.ReassignedFromId=null;
        AssignmentDefinition.Adopt(next,t,null,false); db.Add(next); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.CountAsync(a=>a.ScheduleKey==row.ScheduleKey)==1 && await db.OperationalAssignments.IgnoreQueryFilters().CountAsync(a=>a.ScheduleKey==row.ScheduleKey)==2,"reactivation can create new work without colliding with the retired occurrence key");
        await ProcessRetirementAsync(db,tasks,service,clock,actor,collaborator,person);
        Console.WriteLine("PASS assignment retirement lifecycle");
    }

    private static async Task ProcessRetirementAsync(SgolDbContext db, OperationalTaskService tasks, AssignmentRefreshService service, IClock clock, Guid actor, Guid collaborator, Guid person)
    {
        var sequence=new OperationalSequence { Id=Guid.CreateVersion7(),Name="Proceso con etapa retirada" }; db.Add(sequence); await db.SaveChangesAsync();
        var version=new OperationalSequenceVersion { Id=Guid.CreateVersion7(),SequenceId=sequence.Id,Number=1,Name=sequence.Name,StartKind="OCASIONAL",CreatedAt=clock.UtcNow,CreatedBy=actor };
        db.Add(version); await db.SaveChangesAsync();
        var defs=new List<OperationalTask>();
        foreach(var name in new[]{"Etapa entregada","Etapa que se desactiva","Etapa posterior"})
        {
            await tasks.SaveAsync(actor,new OperationalTask { Name=name,ProcessId=sequence.Id,Revision=0,Requirement="NINGUNO",OperationalCriticality="FLEXIBLE" },default);
            defs.Add(await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name==name));
        }
        for(var i=0;i<3;i++) db.Add(new OperationalSequenceStep { VersionId=version.Id,Position=i+1,TaskId=defs[i].Id,
            Action=i==0?"ENTREGAR":i==1?"REVISAR":"AUTORIZAR",ReleaseCondition=i==0?"INICIO":"ENTREGA",DeadlineHours=24,TaskSnapshot=AssignmentDefinition.Capture(defs[i]) });
        await db.SaveChangesAsync(); version.PublishedAt=clock.UtcNow; version.PublishedBy=actor; sequence.PublishedVersionId=version.Id; sequence.Enabled=true; await db.SaveChangesAsync();
        var day=WeekContract.LocalToday(clock.UtcNow);
        var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,actor,clock.UtcNow,"Proceso a cancelar",day,day.AddDays(6)); db.Add(plan);
        var run=new OperationalSequenceRun { Id=Guid.CreateVersion7(),VersionId=version.Id,PlanId=plan.Id,Name="Proceso a cancelar",StartKey="retire:"+Guid.NewGuid(),
            CreatedBy=actor,CreatedAt=clock.UtcNow,PlannedStartsAt=clock.UtcNow.AddHours(-2),ActivePosition=2,UsesTaskEvidence=true }; db.Add(run);
        var stages=new List<OperationalSequenceStage>(); var attempts=new List<OperationalSequenceAttempt>(); var assignments=new List<OperationalAssignment>();
        for(var i=0;i<3;i++)
        {
            var start=clock.UtcNow.AddHours(i==0?-2:i==1?0:1);
            var stage=new OperationalSequenceStage { Id=Guid.CreateVersion7(),RunId=run.Id,VersionId=version.Id,Position=i+1,TaskId=defs[i].Id,TaskRevision=defs[i].Revision,
                TaskCode=defs[i].TaskCode,Name=defs[i].Name,Role=defs[i].AssignedRole,Requirement=defs[i].Requirement,LoadMinutes=defs[i].LoadMinutes,
                OperationalCriticality=defs[i].OperationalCriticality,DefinitionSnapshotJson=AssignmentDefinition.Capture(defs[i]),ReservedPersonId=person,ReservedPersonName="A",PlannedStartsAt=start,PlannedDueAt=start.AddHours(1) };
            stages.Add(stage); db.Add(stage);
            var attempt=new OperationalSequenceAttempt { Id=Guid.CreateVersion7(),RunId=run.Id,StageId=stage.Id,PlanId=plan.Id,Number=1,Round=1,CreatedAt=clock.UtcNow };
            attempts.Add(attempt);
            if(i<2)
            {
                var assignment=OperationalProcessService.Reservation(stage,plan.Id); assignment.Id=Guid.CreateVersion7();
                if(i==0) { assignment.CompletedAt=clock.UtcNow.AddHours(-1); assignment.CompletedBy=collaborator; assignment.CompletionKind="ENTREGA"; assignment.ReviewStatus="CONCLUIDA"; assignment.ReviewedAt=clock.UtcNow.AddHours(-1); assignment.ReviewedBy=actor; }
                db.Add(assignment); assignments.Add(assignment); attempt.AssignmentId=assignment.Id; attempt.Status=i==0?"APROBADO":"ACTIVO";
            }
            db.Add(attempt);
        }
        await db.SaveChangesAsync();
        var release=new OperationalSequenceEvent { Id=Guid.CreateVersion7(),RunId=run.Id,StageId=stages[0].Id,AttemptId=attempts[0].Id,Kind="APROBACION",EventKey="fixture-approved",ActorId=actor,OccurredAt=clock.UtcNow.AddHours(-1) }; db.Add(release);
        for(var i=0;i<3;i++)
        {
            stages[i].CurrentAttemptId=attempts[i].Id;
            if(i<2) { stages[i].Status=i==0?"CONCLUIDA":"DISPONIBLE"; stages[i].EnabledAt=stages[i].PlannedStartsAt; stages[i].EligibleAt=stages[i].PlannedStartsAt; stages[i].EffectiveDueAt=stages[i].PlannedDueAt; }
        }
        stages[1].DependencySatisfiedAt=release.OccurredAt; stages[1].DependencyEventId=release.Id;
        await db.SaveChangesAsync();
        // Publish a replacement without the middle task, then deactivate it through the supported catalogue flow.
        var replacement=new OperationalSequenceVersion { Id=Guid.CreateVersion7(),SequenceId=sequence.Id,Number=2,Name=sequence.Name,StartKind="OCASIONAL",CreatedAt=clock.UtcNow,CreatedBy=actor };
        db.Add(replacement); await db.SaveChangesAsync();
        db.Add(new OperationalSequenceStep { VersionId=replacement.Id,Position=1,TaskId=defs[0].Id,Action="ENTREGAR",ReleaseCondition="INICIO",DeadlineHours=24,TaskSnapshot=AssignmentDefinition.Capture(defs[0]) });
        db.Add(new OperationalSequenceStep { VersionId=replacement.Id,Position=2,TaskId=defs[2].Id,Action="AUTORIZAR",ReleaseCondition="ENTREGA",DeadlineHours=24,TaskSnapshot=AssignmentDefinition.Capture(defs[2]) });
        await db.SaveChangesAsync(); replacement.PublishedAt=clock.UtcNow; replacement.PublishedBy=actor; sequence.PublishedVersionId=replacement.Id; sequence.Revision++; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var inactive=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Id==defs[1].Id);
        await tasks.ChangeAsync(actor,inactive.Id,inactive.Revision,false,default); db.ChangeTracker.Clear();
        var submitted=await db.OperationalAssignments.SingleAsync(a=>a.Id==assignments[1].Id);
        submitted.CompletedAt=clock.UtcNow; submitted.CompletedBy=collaborator; submitted.CompletionKind="ENTREGA"; submitted.ReviewStatus="PENDIENTE_REVISION";
        var submittedAttempt=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==attempts[1].Id); submittedAttempt.Status="ENTREGADO";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var submittedPreview=await service.PreviewAsync(actor,new(inactive.Id,Guid.NewGuid(),clock.UtcNow.AddMinutes(3),RetireInactiveAssignments:true),default);
        Check(submittedPreview.Differences.Length==0 && submittedPreview.Notices.Length==0,"deactivating an already delivered stage does not cancel its pending review or downstream process");
        submitted=await db.OperationalAssignments.SingleAsync(a=>a.Id==assignments[1].Id);
        submitted.CompletedAt=null; submitted.CompletedBy=null; submitted.CompletionKind=null; submitted.ReviewStatus=null;
        submittedAttempt=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==attempts[1].Id); submittedAttempt.Status="ACTIVO";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var doneBefore=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==assignments[0].Id));
        var versionsBefore=JsonSerializer.Serialize(await db.Set<OperationalSequenceVersion>().AsNoTracking().Where(v=>v.SequenceId==sequence.Id).OrderBy(v=>v.Number).ToArrayAsync());
        var request=new AssignmentRefreshRequest(inactive.Id,Guid.NewGuid(),clock.UtcNow.AddMinutes(3),RetireInactiveAssignments:true);
        var preview=await service.PreviewAsync(actor,request,default);
        Check(preview.CanApply && preview.Differences.Length==2 && preview.Differences.All(d=>d.Retire) && preview.Notices.Length==1,"process retirement explicitly previews cancellation and every pending downstream stage");
        await service.ApplyAsync(actor,request,preview.Fingerprint,"Etapa retirada de la operación",default); db.ChangeTracker.Clear();
        Check((await db.Set<OperationalSequenceRun>().SingleAsync(r=>r.Id==run.Id)).Status=="CANCELADA" && await db.Set<OperationalSequenceStage>().CountAsync(s=>s.RunId==run.Id&&s.Status=="CANCELADA")==2,
            "retiring a process task cancels its run and remaining reservations coherently");
        Check(!await db.OperationalAssignments.AnyAsync(a=>a.Id==assignments[1].Id) && !await db.OperationalAssignments.IgnoreQueryFilters().AnyAsync(a=>a.TaskId==defs[2].Id),"active process assignment is retired without inventing an assignment for its waiting successor");
        Check(doneBefore==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==assignments[0].Id)) &&
            versionsBefore==JsonSerializer.Serialize(await db.Set<OperationalSequenceVersion>().AsNoTracking().Where(v=>v.SequenceId==sequence.Id).OrderBy(v=>v.Number).ToArrayAsync()),"completed process work and both published versions remain unchanged");
        Check(await db.Set<OperationalSequenceEvent>().CountAsync(e=>e.RunId==run.Id&&e.Kind=="CANCELACION")==2,"process history records the cancellation of each pending attempt");
    }
}
