using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sgol.BuildingBlocks.Time;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class ChecklistChecks
{
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS checklist: " + label); }
    private static async Task Reject(Func<Task> action, string code, SgolDbContext? db = null)
    {
        try { await action(); throw new Exception("Expected " + code); }
        catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS checklist rejects " + code); }
        finally { db?.ChangeTracker.Clear(); }
    }
    private static string Answer(string definition, params Guid[] selected) => JsonSerializer.Serialize(new ChecklistAnswer(OperationalChecklist.Fingerprint(definition), selected));
    public static void Rules()
    {
        var points = Enumerable.Range(1, 1500).Select(i => new ChecklistPoint(Guid.NewGuid(), "Punto " + i)).ToArray();
        var json = OperationalChecklist.Normalize(JsonSerializer.Serialize(points), true);
        Check(OperationalChecklist.Points(json).Length == 1500, "large lists have no point-count cap");
        Check(OperationalChecklist.Normalize(json, false) == "[]", "other requirements discard checklist parameters");
        var draft = JsonSerializer.Serialize(new ChecklistDraft(Guid.NewGuid(), DateTimeOffset.UtcNow, [points[0].Id, points[1].Id]));
        var changed = JsonSerializer.Serialize(new[] {points[0], points[1] with {Text="Modificado"}, new ChecklistPoint(Guid.NewGuid(), "Nuevo")});
        Check(OperationalChecklist.Checked(OperationalChecklist.Reconcile(json, changed, draft)).SequenceEqual([points[0].Id]), "definition edits keep only unchanged marks");
        var title = new ChecklistPoint(Guid.NewGuid(), "Caja y operación", true);
        var sectioned = OperationalChecklist.Normalize(JsonSerializer.Serialize(new[] {title, points[0], points[1]}), true);
        Check(OperationalChecklist.ValidateAnswer(sectioned, Answer(sectioned, points[0].Id, points[1].Id), true).Length == 2,
            "titles are persisted but excluded from completion requirements");
        Check(OperationalChecklist.Complete(sectioned, draft), "only checkboxes count toward completion");
        var renamed = JsonSerializer.Serialize(new[] {title with {Text="Otro apartado"}, points[0], points[1]});
        Check(OperationalChecklist.Checked(OperationalChecklist.Reconcile(sectioned, renamed, draft)).Length == 2,
            "renaming a title preserves checkbox marks");
        var converted = JsonSerializer.Serialize(new[] {points[0] with {IsTitle=true}, points[1]});
        Check(OperationalChecklist.Checked(OperationalChecklist.Reconcile(json, converted, draft)).SequenceEqual([points[1].Id]),
            "converting a checkbox to a title removes its mark");
        Check(OperationalChecklist.Normalize(JsonSerializer.Serialize(new[] {points[0]}),true) == JsonSerializer.Serialize(new[] {points[0]}),
            "untitled definitions retain their original format");
        try { OperationalChecklist.ValidateAnswer(sectioned,Answer(sectioned,title.Id,points[0].Id,points[1].Id),true); throw new Exception("Accepted checked title"); }
        catch (DirectionPlanException e) when(e.Code=="TASK_CHECKLIST_INVALID") { Check(true,"title IDs cannot be checked"); }
        foreach (var bad in new[] { "[]", "[null]", "{}", JsonSerializer.Serialize(new[] {title}), JsonSerializer.Serialize(new[] {points[0],points[0]}), JsonSerializer.Serialize(new[] {points[0] with {Text=" "}}) }) {
            try { OperationalChecklist.Normalize(bad,true); throw new Exception("Accepted invalid checklist"); }
            catch (DirectionPlanException e) when(e.Code == "TASK_CHECKLIST_INVALID") { Console.WriteLine("PASS checklist: invalid definition rejected"); }
        }
    }
    public static async Task RunAsync(SgolDbContext db, OperationalTaskService tasks, OperationalSupervisionService supervision,
        IDirectionPreparedPlanService direction, IClock clock, Guid director, Guid actor, Guid person, Guid other, Guid otherPerson)
    {
        Rules();
        var points = new[] { new ChecklistPoint(Guid.NewGuid(), "Revisar limpieza"), new ChecklistPoint(Guid.NewGuid(), "Confirmar orden") };
        var json = JsonSerializer.Serialize(points); var day=WeekContract.LocalToday(clock.UtcNow);
        await tasks.SaveAsync(director, new OperationalTask { Revision=0, Name="Checklist aislado", Requirement="CHECKLIST", ChecklistJson=json, OperationalCriticality="FLEXIBLE", LoadMinutes=5 },default);
        db.ChangeTracker.Clear();
        var definition=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name=="Checklist aislado");
        Check(definition.ChecklistJson==json,"catalogue persists points and identities");
        var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,director,clock.UtcNow,"Checklist checks",day,day.AddDays(7)); db.Add(plan);
        OperationalAssignment Make()=>new() { Id=Guid.CreateVersion7(),PlanId=plan.Id,TaskId=definition.Id,TaskCode=definition.TaskCode,TaskRevision=definition.Revision,
            Name=definition.Name,Requirement="CHECKLIST",ChecklistJson=json,DefinitionSnapshotJson=AssignmentDefinition.Capture(definition),PersonId=person,PersonName="A",
            AssignedRole=CanonicalRole.SalesFloor,OperationalCriticality=definition.OperationalCriticality,StartsAt=clock.UtcNow.AddMinutes(-10),DueAt=clock.UtcNow.AddHours(1),LoadMinutes=5 };
        var assignment=Make(); var correction=Make(); var goal=Make(); goal.GoalUnits=2;goal.ScheduleMode="PROGRESO";
        await tasks.SaveAsync(director,new OperationalTask {Revision=0,Name="Meta checklist",Requirement="CHECKLIST",ChecklistJson=json,ScheduleMode="PROGRESO",GoalUnits=2,OperationalCriticality="FLEXIBLE"},default);
        var goalDefinition=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name=="Meta checklist");
        goal.TaskId=goalDefinition.Id;goal.TaskCode=goalDefinition.TaskCode;goal.TaskRevision=goalDefinition.Revision;goal.DefinitionSnapshotJson=AssignmentDefinition.Capture(goalDefinition);
        db.AddRange(assignment,correction,goal); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject(()=>tasks.SaveChecklistDraftAsync(other,assignment.Id,Answer(json,points[0].Id),default),"TASK_NOT_FOUND",db);
        await tasks.SaveChecklistDraftAsync(actor,assignment.Id,Answer(json,points[0].Id),default);db.ChangeTracker.Clear();
        var mine=(await tasks.MineAsync(actor,default)).Single(a=>a.Id==assignment.Id);
        Check(mine.CompletedAt==null && OperationalChecklist.Checked(mine.ChecklistDraftJson).SequenceEqual([points[0].Id]),"draft survives reload without submitting");
        await Reject(()=>tasks.CompleteAsync(actor,assignment.Id,null,null,default,checklistAnswer:Answer(json,points[0].Id)),"TASK_CHECKLIST_PENDING",db);
        await Reject(()=>tasks.CompleteAsync(actor,assignment.Id,null,null,default,checklistAnswer:Answer(json,points[0].Id,Guid.NewGuid())),"TASK_CHECKLIST_INVALID",db);
        await Reject(()=>tasks.CompleteAsync(actor,assignment.Id,null,null,default,checklistAnswer:Answer(json,points[0].Id,points[0].Id)),"TASK_CHECKLIST_INVALID",db);
        await Reject(()=>tasks.CompleteAsync(actor,assignment.Id,null,null,default,checklistAnswer:Answer("[]",points[0].Id)),"TASK_CHECKLIST_CHANGED",db);
        await Reject(()=>supervision.ReviewBatchAsync(director,[new(assignment.Id,mine.Revision,"CONCLUIDA",null)],default),"TASK_CHECKLIST_PENDING",db);
        var all=Answer(json,points.Select(p=>p.Id).ToArray());
        await Reject(()=>tasks.CompleteAsync(actor,goal.Id,null,null,default,checklistAnswer:all),"TASK_GOAL_PENDING",db);
        await tasks.CompleteAsync(actor,assignment.Id,null,null,default,checklistAnswer:all);db.ChangeTracker.Clear();
        var submitted=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==assignment.Id);
        Check(submitted.ReviewStatus=="PENDIENTE_REVISION" && OperationalChecklist.Complete(submitted.ChecklistJson,submitted.ChecklistDraftJson),"complete answer is stored atomically awaiting review");
        await Reject(()=>tasks.SaveChecklistDraftAsync(actor,assignment.Id,all,default),"TASK_CONFLICT",db);
        await supervision.ReviewBatchAsync(director,[new(assignment.Id,submitted.Revision,"CONCLUIDA",null)],default);db.ChangeTracker.Clear();
        Check((await supervision.ListAsync(director,default)).Single(a=>a.Assignment.Id==assignment.Id).Assignment.ChecklistDraftJson==submitted.ChecklistDraftJson,"supervision projection retains checklist evidence");
        var refresh=new AssignmentRefreshService(db,direction,clock);
        definition.ChecklistJson=JsonSerializer.Serialize(new[] {points[0],points[1] with {Text="Orden actualizado"},new ChecklistPoint(Guid.NewGuid(),"Nuevo punto")});
        await tasks.SaveAsync(director,definition,default);db.ChangeTracker.Clear();
        Check((await refresh.CountsAsync(director,default)).GetValueOrDefault(definition.Id)==1,"checklist-only edits flag pending assignments and protect reviewed history");
        await tasks.SaveChecklistDraftAsync(actor,correction.Id,all,default);db.ChangeTracker.Clear();
        var request=new AssignmentRefreshRequest(definition.Id,Guid.NewGuid(),clock.UtcNow.AddMinutes(3));
        var preview=await refresh.PreviewAsync(director,request,default);
        Check(preview.CanApply && preview.Differences.All(d=>d.MetadataOnly),"checklist updates require no schedule rearrangement: "+string.Join("; ",preview.Conflicts.Concat(preview.Differences.SelectMany(d=>d.Fields))));
        await refresh.ApplyAsync(director,request,preview.Fingerprint,"Actualizar puntos de prueba",default);db.ChangeTracker.Clear();
        var adopted=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==correction.Id);
        Check(adopted.StartsAt==correction.StartsAt && OperationalChecklist.Checked(adopted.ChecklistDraftJson).SequenceEqual([points[0].Id]),"pending adoption preserves dates and clears edited/new points");
        var frozen=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==assignment.Id);
        Check(frozen.ChecklistJson==json && frozen.ChecklistDraftJson==submitted.ChecklistDraftJson,"catalogue edits preserve submitted definition and evidence");
        // Fresh correction keeps original evidence and starts with a new unchecked list.
        var current=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Id==definition.Id);
        await Reject(()=>tasks.CompleteAsync(actor,correction.Id,null,null,default,checklistAnswer:all),"TASK_CHECKLIST_CHANGED",db);
        await tasks.CompleteAsync(actor,correction.Id,null,null,default,checklistAnswer:Answer(current.ChecklistJson,OperationalChecklist.Points(current.ChecklistJson).Select(p=>p.Id).ToArray()));db.ChangeTracker.Clear();
        var source=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==correction.Id);
        await supervision.ReviewAsync(director,source.Id,source.Revision,"MISMA_PERSONA","Repetir verificación",default);db.ChangeTracker.Clear();
        var replacement=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.ReassignedFromId==source.Id);
        Check(OperationalChecklist.Points(replacement.ChecklistJson).Length==3 && OperationalChecklist.Checked(replacement.ChecklistDraftJson).Length==0,"rejected delivery creates unchecked correction using current definition");
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==source.Id)).ChecklistDraftJson==source.ChecklistDraftJson,"correction preserves original evidence");
        foreach(var recipient in new[]{person,otherPerson})
            db.Add(new PersonalScheduleVersion {Id=Guid.CreateVersion7(),PersonId=recipient,Revision=1,From=day,ChangedBy=director,ChangedAt=clock.UtcNow,IntentKey=Guid.NewGuid(),
                DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(0,1439)}))});
        var temporal=await db.Set<TemporalAssignmentState>().SingleOrDefaultAsync();
        if(temporal==null) db.Add(new TemporalAssignmentState {Enabled=true});else temporal.Enabled=true;
        var transfer=Make();db.Add(transfer);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        await tasks.SaveChecklistDraftAsync(actor,transfer.Id,Answer(json,points[0].Id),default);db.ChangeTracker.Clear();
        var beforeTransfer=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==transfer.Id);
        var move=new TransferRequest(transfer.Id,beforeTransfer.Revision,otherPerson,5,clock.UtcNow.AddHours(1),"Transferencia de prueba");
        var movePreview=await supervision.PreviewTransferAsync(director,move,default);
        await supervision.TransferAsync(director,move,movePreview.Fingerprint,default);db.ChangeTracker.Clear();
        Check((await tasks.MineAsync(other,default)).Single(a=>a.Id==transfer.Id).ChecklistDraftJson==beforeTransfer.ChecklistDraftJson,"transfer preserves saved marks and author");
        await Reject(()=>tasks.SaveChecklistDraftAsync(actor,transfer.Id,all,default),"TASK_NOT_FOUND",db);
        var occasionalId=await tasks.CreateOccasionalTaskAsync(director,new OccasionalTaskForm("Checklist ocasional","CHECKLIST",1,24,ChecklistJson:json),default);db.ChangeTracker.Clear();
        var occasionTask=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Id==occasionalId);
        var occasion=await tasks.PreviewOccasionAsync(director,new OccasionInput(occasionalId,occasionTask.Revision,"Ocasión checklist","Revisar lista","","",clock.UtcNow,clock.UtcNow.AddHours(1),[person]),default);
        Check(occasion.Assignments.Single().ChecklistJson==json,"occasional assignment preview captures checklist");
        await Process(db,tasks,direction,clock,director,actor,person,json,all);
    }
    private static async Task Process(SgolDbContext db,OperationalTaskService tasks,IDirectionPreparedPlanService direction,IClock clock,Guid director,Guid actor,Guid person,string json,string all)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"]="true" }).Build();
        var catalog=new OperationalProcessCatalogService(db,direction,clock,config);var id=Guid.CreateVersion7();
        await catalog.CreateAsync(director,id,"Proceso checklist","Prueba aislada",default);db.ChangeTracker.Clear();
        var ids=new List<Guid>();
        for(var i=1;i<=2;i++) { await tasks.SaveAsync(director,new OperationalTask {Revision=0,ProcessId=id,Name="Checklist etapa "+i,Requirement="CHECKLIST",ChecklistJson=json,PeriodType="OCASIONAL",LoadMinutes=1,OperationalCriticality="FLEXIBLE"},default);db.ChangeTracker.Clear();ids.Add(await db.OperationalTasks.Where(t=>t.ProcessId==id && t.Name=="Checklist etapa "+i).Select(t=>t.Id).SingleAsync()); }
        var editor=await catalog.ReadAsync(director,id,default);
        await catalog.SaveAsync(director,new ProcessDraftInput {Id=id,Revision=editor.Process.Revision,Name=editor.Process.Name,Description="Prueba",StartHour=12,Steps=ids.Select((task,i)=>new OperationalSequenceStep {TaskId=task,Position=i+1,Action=i==0?"ENTREGAR":"AUTORIZAR",ReleaseCondition=i==0?"INICIO":"ENTREGA",DeadlineHours=1,Instructions="Revisar lista"}).ToList()},default);db.ChangeTracker.Clear();
        editor=await catalog.ReadAsync(director,id,default);await catalog.PublishAsync(director,id,editor.Process.Revision,default);db.ChangeTracker.Clear();
        editor=await catalog.ReadAsync(director,id,default);await catalog.SetEnabledAsync(director,id,editor.Process.Revision,true,default);db.ChangeTracker.Clear();
        var service=new OperationalProcessService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var day=WeekContract.LocalToday(clock.UtcNow);
        var planning=new ProcessPlanningService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var input=new ProcessPlanInput {Key=Guid.NewGuid(),Name="Plan checklist",ProcessId=id,Mode="OCASION",From=day,Through=day,People=new Dictionary<int,Guid>{{1,person},{2,person}}};
        var preview=await planning.PreviewAsync(director,input,default);input.Fingerprint=preview.Fingerprint;
        var prepared=await planning.PrepareAsync(director,input,default);db.ChangeTracker.Clear();
        var run=(await planning.ActivateAsync(director,prepared,default)).Single();db.ChangeTracker.Clear();
        var firstStart=await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==run && s.Position==1).Select(s=>s.PlannedStartsAt).SingleAsync();
        if(clock is CheckClock checkClock && firstStart>clock.UtcNow) checkClock.UtcNow=firstStart;
        await service.TickAsync(default);db.ChangeTracker.Clear();
        var view=await service.ReadAsync(actor,run,default);var stage=view.Stages[0];var attempt=view.Attempts.Single(a=>a.Id==stage.CurrentAttemptId);
        Check(stage.ChecklistJson==json && view.ChecklistEvidence[attempt.AssignmentId!.Value].Definition==json,"process stages and assignments capture checklist");
        await Reject(()=>service.DecideAsync(actor,run,attempt.Id,attempt.Revision,Guid.NewGuid(),"ENTREGA","Lista revisada",null,null,true,default),"TASK_CHECKLIST_CHANGED",db);
        await service.DecideAsync(actor,run,attempt.Id,attempt.Revision,Guid.NewGuid(),"ENTREGA","Lista revisada",null,null,true,default,checklistAnswer:all);db.ChangeTracker.Clear();
        view=await service.ReadAsync(actor,run,default);
        Check(view.Run.ActivePosition==2 && OperationalChecklist.Complete(view.ChecklistEvidence[attempt.AssignmentId!.Value].Definition,view.ChecklistEvidence[attempt.AssignmentId!.Value].Draft),"complete process checklist delivers and releases next stage with preserved evidence");
    }
}
