using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessItemChecks
{
    public static async Task RunAsync(SgolDbContext db, Guid actor, Guid person, IClock sharedClock, HttpClient client)
    {
        var clock = new MutableClock { UtcNow = sharedClock.UtcNow };
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var validator = new FileTechnicalValidator(); var scanner = new Scanner();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"]="true" }).Build();
        var catalog = new OperationalProcessCatalogService(db, direction, clock, config);
        var engine = new OperationalProcessService(db, direction, clock, validator, scanner);
        var planning = new ProcessPlanningService(db, direction, clock, validator, scanner);
        var day = WeekContract.LocalToday(clock.UtcNow);
        db.Add(new OperationalCapacity { Id=Guid.CreateVersion7(), PersonId=person, From=day, Through=day.AddDays(30), DailyMinutes=480, Reason="Items isolated fixture" });
        db.Add(new PersonalScheduleVersion { Id=Guid.CreateVersion7(), PersonId=person, Revision=1, From=day, ChangedBy=actor, ChangedAt=clock.UtcNow, IntentKey=Guid.CreateVersion7(), RequestHash=new string('0',64), DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,780),new WorkInterval(840,1140)})) });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check(ProcessItemRules.Calculate(20,3)==60 && ProcessItemRules.Calculate(12,3)==36 && ProcessItemRules.Calculate(int.MaxValue,int.MaxValue)==4611686014132420609L,"Full integer formula, corrected total and wide demand");
        await Reject("PROCESS_ITEM_QUANTITY_INVALID",()=>Task.Run(()=>ProcessItemRules.Calculate(null,3)));
        await Reject("PROCESS_ITEM_QUANTITY_INVALID",()=>Task.Run(()=>ProcessItemRules.Calculate(-1,3)));
        Check(!ProcessItemRules.CanResolve(CanonicalRole.SalesFloor) && !ProcessItemRules.CanResolve(CanonicalRole.Administration) && ProcessItemRules.CanResolve(CanonicalRole.Subcoordination),"Exception origin role policy");
        var item=Guid.CreateVersion7();
        var html=await client.GetStringAsync("/tareas/procesos/items");
        var created=await client.PostAsync("/tareas/procesos/items?handler=Save", Form(html,new() { ["id"]=item.ToString(),["revision"]="0",["name"]="Fotografías",["minutes"]="3",["active"]="true" }));
        Check(created.StatusCode==HttpStatusCode.Redirect,"Shared catalog real HTTP create"); db.ChangeTracker.Clear();
        Check((await catalog.ItemsAsync(actor,default)).Single(i=>i.Item.Id==item).Rate.Minutes==3,"Shared catalog persists and reopens");
        await Reject("PROCESS_CONFLICT",()=>catalog.SaveItemAsync(actor,item,8,"Fotografías",4,true,default));db.ChangeTracker.Clear();
        var root=Guid.CreateVersion7(); await catalog.CreateAsync(actor,root,"Ítems: prueba aislada","",default);db.ChangeTracker.Clear();
        var version=await db.Set<OperationalSequenceVersion>().SingleAsync(v=>v.SequenceId==root);
        var tasks=new List<OperationalTask>();
        for(var i=0;i<4;i++) {
            var task=new OperationalTask { Id=Guid.CreateVersion7(), TaskCode=$"TAR-{9800+i}", Name=$"Ítems etapa {i+1}", ProcessId=root, Requirement="COMENTARIO", AssignedRole=CanonicalRole.Direction, LoadMinutes=1, OperationalCriticality="FLEXIBLE" };
            tasks.Add(task);db.Add(task);
        }
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var steps=tasks.Select((t,i)=>new OperationalSequenceStep { TaskId=t.Id, Position=i+1, Action=i==3?"AUTORIZAR":"ENTREGAR", ReleaseCondition=i==0?"INICIO":"ENTREGA", DeadlineHours=1, ReturnToPosition=i>0?i:null,
            OutputItemId=i<2?item:null, OutputTargetTaskId=i<2?tasks[i+1].Id:null, IsFinalAudit=i==3 }).ToList();
        var editor=await catalog.ReadAsync(actor,root,default);
        var input=new ProcessDraftInput { Id=root,Revision=editor.Process.Revision,Name=editor.Process.Name,StartKind="OCASIONAL",StartHour=12,Steps=steps };
        steps[3].IsFinalAudit=false;await Reject("PROCESS_ITEM_AUDIT_REQUIRED",()=>catalog.SaveAsync(actor,input,default));steps[3].IsFinalAudit=true;
        steps[0].OutputTargetTaskId=tasks[2].Id;await Reject("PROCESS_ITEM_TARGET_INVALID",()=>catalog.SaveAsync(actor,input,default));steps[0].OutputTargetTaskId=tasks[1].Id;
        await catalog.SaveAsync(actor,input,default);db.ChangeTracker.Clear();editor=await catalog.ReadAsync(actor,root,default);
        html=await client.GetStringAsync($"/tareas/procesos?id={root}");
        Check(html.Contains("OutputItemId") && html.Contains("IsFinalAudit") && html.Contains("Fotograf"),"Real editor renders item selector and explicit audit");
        await catalog.PublishAsync(actor,root,editor.Process.Revision,default);db.ChangeTracker.Clear();editor=await catalog.ReadAsync(actor,root,default);
        await catalog.SetEnabledAsync(actor,root,editor.Process.Revision,true,default);db.ChangeTracker.Clear();
        async Task<Guid> Prepare() {
            var request=new ProcessPlanInput { Name="Items test",ProcessId=root,Mode="OCASION",From=day,Through=day,People=Enumerable.Range(1,4).ToDictionary(i=>i,i=>person) };
            request.Fingerprint=(await planning.PreviewAsync(actor,request,default)).Fingerprint;
            var id=await planning.PrepareAsync(actor,request,default);db.ChangeTracker.Clear();return id;
        }
        var plan=await Prepare();
        var ratesBefore=(await db.Set<PreparedProcessStart>().AsNoTracking().SingleAsync(p=>p.PlanId==plan)).ItemRatesJson;
        await catalog.SaveItemAsync(actor,item,1,"Fotografías nuevas",4,false,default);db.ChangeTracker.Clear();
        var future=await Prepare();
        Check(JsonSerializer.Deserialize<Dictionary<int,ProcessItemSnapshot>>((await db.Set<PreparedProcessStart>().AsNoTracking().SingleAsync(p=>p.PlanId==future)).ItemRatesJson!)![1].Minutes==4,"Retired item remains usable by published process; new preparation adopts current rate without republishing");
        Check((await db.Set<PreparedProcessStart>().AsNoTracking().SingleAsync(p=>p.PlanId==plan)).ItemRatesJson==ratesBefore,"Prepared rate frozen after global change");
        var run=(await planning.ActivateAsync(actor,plan,default)).Single();db.ChangeTracker.Clear();
        Check((await engine.ReadAsync(actor,run,default)).Stages.First().OutputItemMinutes==3,"Activation uses prepared rate");
        async Task<(OperationalSequenceStage Stage,OperationalSequenceAttempt Attempt)> Active() {
            var v=await engine.ReadAsync(actor,run,default);var s=v.Stages.Single(x=>x.Position==v.Run.ActivePosition);
            for (var retry=0;s.Status=="PROGRAMADA" && retry<5;retry++) { clock.UtcNow=new[]{clock.UtcNow,s.PlannedStartsAt}.Max();await engine.TickAsync(default);db.ChangeTracker.Clear();v=await engine.ReadAsync(actor,run,default);s=v.Stages.Single(x=>x.Position==v.Run.ActivePosition); }
            Check(s.Status=="DISPONIBLE",$"Active fixture stage {s.Position}: {s.Status} {s.BlockReason}");
            return (s,v.Attempts.Single(a=>a.Id==s.CurrentAttemptId));
        }
        async Task Decide(string decision,int? quantity=null,string? resolution=null) {
            var x=await Active();await engine.DecideAsync(actor,run,x.Attempt.Id,x.Attempt.Revision,Guid.CreateVersion7(),decision,"Evidencia ítems",null,null,true,default,itemQuantity:quantity,capacityResolution:resolution);db.ChangeTracker.Clear();
        }
        var initial=await Active();var key=Guid.CreateVersion7();
        await engine.DecideAsync(actor,run,initial.Attempt.Id,initial.Attempt.Revision,key,"ENTREGA","Conteo 20",null,null,true,default,itemQuantity:20);db.ChangeTracker.Clear();
        var view=await engine.ReadAsync(actor,run,default);
        Check(view.Stages[1].LoadMinutes==60 && view.Stages[0].LoadMinutes==1 && view.Stages[1].Requirement=="COMENTARIO","20 × 3 replaces next estimate only, preserves evidence requirement");
        await engine.DecideAsync(actor,run,initial.Attempt.Id,initial.Attempt.Revision,key,"ENTREGA","Conteo 20",null,null,true,default,itemQuantity:20);db.ChangeTracker.Clear();
        Check(await db.Set<OperationalSequenceEvent>().CountAsync(e=>e.RunId==run&&e.EventKey==$"decision:{key:N}")==1,"Identical retry does not duplicate delivery");
        await Reject("PROCESS_CONFLICT",()=>engine.DecideAsync(actor,run,initial.Attempt.Id,initial.Attempt.Revision,key,"ENTREGA","Conteo 20",null,null,true,default,itemQuantity:12));db.ChangeTracker.Clear();
        var receiver=await Active();
        var work=await db.OperationalAssignments.SingleAsync(a=>a.Id==receiver.Attempt.AssignmentId);work.Comment="Trabajo parcial conservado";await db.SaveChangesAsync();db.ChangeTracker.Clear();
        await Decide("DEVOLUCION");
        await Decide("ENTREGA",12);
        receiver=await Active();
        Check(receiver.Stage.LoadMinutes==36 && receiver.Attempt.ContinuationAttemptId!=null && (await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==receiver.Attempt.AssignmentId)).Comment=="Trabajo parcial conservado","Return preserves receiver work and reserves full corrected 36 minutes");
        await Decide("ENTREGA",0);
        view=await engine.ReadAsync(actor,run,default);
        Check(view.Run.ActivePosition==4&&view.Stages[2].Status=="OMITIDA"&&view.Stages[3].LoadMinutes==1,"Zero at intermediate stage skips to explicit audit with its own load");
        var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
        Check(!ledger.Tasks.Any(a=>a.Id==view.Stages[2].Id),"Skipped stage releases reservation");
        var skip=view.Attempts.Single(a=>a.Id==view.Stages[1].CurrentAttemptId).OmittedStagesJson;
        Check(skip?.Contains(tasks[2].TaskCode)==true,"Historical omission list persisted on producer attempt");
        await Decide("SIN_DIFERENCIAS");
        Check((await engine.ReadAsync(actor,run,default)).Run.Status=="CONCLUIDA","Audit reviews effective zero producer and closes without fictional delivery");
        Check((await engine.ReadAsync(actor,run,default)).Attempts.Any(a=>a.ItemQuantity==20),"Original erroneous count remains in history");
        Console.WriteLine("ITEMS_PHASE_CORE_PASS");
    }
    public static async Task CapacityAsync(SgolDbContext db, Guid actor, Guid person, IClock clock, HttpClient client, Action<DateTimeOffset> setClock)
    {
        var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var scanner=new Scanner(); var validator=new FileTechnicalValidator();
        var engine=new OperationalProcessService(db,direction,clock,validator,scanner);
        var planning=new ProcessPlanningService(db,direction,clock,validator,scanner);
        var root=await db.Set<OperationalSequence>().AsNoTracking().SingleAsync(r=>r.Name=="Ítems: prueba aislada");
        var latestRun=await db.Set<OperationalSequenceRun>().AsNoTracking().MaxAsync(r=>r.PlannedStartsAt);
        var originalDay=WeekContract.LocalToday(latestRun);
        var temporal=await db.Set<TemporalAssignmentState>().SingleOrDefaultAsync(s=>s.Id==1);
        if(temporal==null) db.Add(new TemporalAssignmentState{Id=1,Enabled=true}); else { temporal.Enabled=true;temporal.Revision++; }
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var serial=0;
        async Task<Guid> NewRun() {
            var day=originalDay.AddDays(++serial);
            setClock(OperationalSchedule.Instant(day,12));
            var cap=await db.OperationalCapacities.SingleAsync(c=>c.PersonId==person); cap.DailyMinutes=480;await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var input=new ProcessPlanInput { Name="Items exception HTTP",ProcessId=root.Id,Mode="OCASION",From=day,Through=day,People=Enumerable.Range(1,4).ToDictionary(i=>i,i=>person) };
            input.Fingerprint=(await planning.PreviewAsync(actor,input,default)).Fingerprint;
            var plan=await planning.PrepareAsync(actor,input,default);db.ChangeTracker.Clear();
            var run=(await planning.ActivateAsync(actor,plan,default)).Single();db.ChangeTracker.Clear();return run;
        }
        async Task<(OperationalSequenceStage Stage,OperationalSequenceAttempt Attempt)> Source(Guid run) {
            var v=await engine.ReadAsync(actor,run,default);var s=v.Stages.Single(x=>x.Position==v.Run.ActivePosition);return(s,v.Attempts.Single(a=>a.Id==s.CurrentAttemptId));
        }
        async Task<HttpResponseMessage> Post(Guid run, string quantity,string? option=null,string? fingerprint=null,Guid? requestKey=null,bool main=false,bool csrf=true) {
            var src=await Source(run);
            var html=await client.GetStringAsync($"/mi-trabajo?assignedTask={src.Attempt.AssignmentId}");
            var fields=new Dictionary<string,string> { ["RunId"]=run.ToString(),["id"]=src.Attempt.AssignmentId!.Value.ToString(),["AttemptId"]=src.Attempt.Id.ToString(),["Revision"]=src.Attempt.Revision.ToString(),["Key"]=(requestKey??Guid.CreateVersion7()).ToString(),["Decision"]="ENTREGA",["Conclusion"]="Entrega HTTP ítems",["Declared"]="true",["ItemQuantity"]=quantity };
            if(option!=null)fields["CapacityResolution"]=option;if(fingerprint!=null)fields["CapacityFingerprint"]=fingerprint;
            var content=Form(html,fields);if(!csrf) { fields.Remove("__RequestVerificationToken");content=new(fields); }
            var result=await client.PostAsync(main?"/mi-trabajo?handler=CompleteProcess":"/mi-trabajo/procesos?handler=Decide",content);db.ChangeTracker.Clear();return result;
        }
        string Hidden(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var run=await NewRun();
        var before=JsonSerializer.Serialize(await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a=>a.RunId==run).OrderBy(a=>a.Id).ToArrayAsync());
        Check((await Post(run,"1.5")).StatusCode==HttpStatusCode.UnprocessableEntity,"HTTP rejects fractional counts");
        Check((await Post(run,"-1")).StatusCode==HttpStatusCode.UnprocessableEntity,"HTTP rejects negative counts");
        Check((await Post(run,"")).StatusCode==HttpStatusCode.UnprocessableEntity,"HTTP distinguishes empty quantity from zero");
        Check((await Post(run,"1",csrf:false)).StatusCode==HttpStatusCode.BadRequest,"Real POST requires CSRF");
        var capacity=await db.OperationalCapacities.SingleAsync(c=>c.PersonId==person);capacity.DailyMinutes=4;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        setClock(clock.UtcNow.AddMinutes(50));
        var conflict=await Post(run,"20",main:true);var conflictHtml=await conflict.Content.ReadAsStringAsync();
        Check(conflict.StatusCode==HttpStatusCode.Conflict && conflictHtml.Contains("CapacityResolution") && conflictHtml.Contains("FORCE") && conflictHtml.Contains("PENDING"),"Main My work handler renders capacity options before commit");
        Check(before==JsonSerializer.Serialize(await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a=>a.RunId==run).OrderBy(a=>a.Id).ToArrayAsync()),"Capacity preview rolls back delivery and count atomically");
        var token=Hidden(conflictHtml,"CapacityFingerprint");Check(token.Length==64,"Capacity proposal revision included in real form");
        var stale=await Post(run,"20","FORCE","stale",main:true);Check(stale.StatusCode==HttpStatusCode.Conflict,"Stale or manipulated proposal cannot force");
        var forced=await Post(run,"20","FORCE",token,main:true);
        if(forced.StatusCode!=HttpStatusCode.Redirect) await File.WriteAllTextAsync("artifacts/process-items/force-response.html",await forced.Content.ReadAsStringAsync());
        Check(forced.StatusCode==HttpStatusCode.Redirect,"Authorized explicit force commits through real My work HTTP form");
        var view=await engine.ReadAsync(actor,run,default);var recipient=view.Stages[1];var recipientAttempt=view.Attempts.Single(a=>a.Id==recipient.CurrentAttemptId);
        Check(recipient.LoadMinutes==80 && recipient.Status=="DISPONIBLE" && recipientAttempt.CapacityExceptionJson?.Contains("Breaks")==true && recipientAttempt.CapacityExceptionJson.Contains("13:00"),"Forced assignment captures current 4 min rate and actual break interval");
        var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
        var ordinary=new OperationalAssignment { Id=Guid.CreateVersion7(),TaskId=recipient.TaskId,AssignedRole=recipient.Role,PersonId=person,StartsAt=clock.UtcNow.AddMinutes(15),DueAt=clock.UtcNow.AddMinutes(20),LoadMinutes=1 };
        Check(!ledger.CheckTime(person,ordinary).Eligible,"Force does not make break available to normal assignments");
        Check((await db.Set<PersonalScheduleVersion>().AsNoTracking().SingleAsync(s=>s.PersonId==person)).DaysJson.Contains("780"),"Force leaves general calendar unchanged");
        var target=Guid.CreateVersion7();var targetActor=Guid.CreateVersion7();var from=clock.UtcNow.AddYears(-1);
        db.People.Add(new(){Id=target,StableCode="ITEMS-TRANSFER",DisplayName="Items compatible transfer",CreatedAt=from});db.AppUsers.Add(new(){Id=targetActor,PersonId=target,Status=AccountStatus.Active,SecurityStamp="items"});db.EmploymentVersions.Add(new(Guid.CreateVersion7(),target,BranchScope.LorettaId,EmploymentStatus.Active,from));db.RoleAssignmentVersions.Add(new(){Id=Guid.CreateVersion7(),UserId=targetActor,BranchId=BranchScope.LorettaId,RoleCode=CanonicalRole.Direction,Status=RoleAssignmentStatus.Active,ValidFrom=from});
        db.Add(new OperationalCapacity{Id=Guid.CreateVersion7(),PersonId=target,From=originalDay,Through=originalDay.AddDays(30),DailyMinutes=480,Reason="Items reassignment"});db.Add(new PersonalScheduleVersion{Id=Guid.CreateVersion7(),PersonId=target,Revision=1,From=originalDay,ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=Guid.CreateVersion7(),RequestHash=new string('0',64),DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,1140)}))});
        var forcedWork=await db.OperationalAssignments.SingleAsync(a=>a.Id==recipientAttempt.AssignmentId);forcedWork.Comment="Trabajo conservado al reasignar";await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var supervision=new OperationalSupervisionService(db,clock,direction);
        var transfer=new TransferRequest(forcedWork.Id,forcedWork.Revision,target,80,forcedWork.DueAt,"Transferencia compatible posterior al forzado");
        var transferPreview=await supervision.PreviewTransferAsync(actor,transfer,default);await supervision.TransferAsync(actor,transfer,transferPreview.Fingerprint,default);db.ChangeTracker.Clear();
        var transferred=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==forcedWork.Id);
        Check(transferred.ItemLoadSourceAttemptId==recipient.LoadSourceAttemptId&&transferred.LoadMinutes==80&&!transferred.TemporalDecisionJson!.Contains("PROCESS_ITEM_FORCE"),"Supervision transfer keeps count provenance and receives normally without inherited break permission");
        view=await engine.ReadAsync(actor,run,default);
        await engine.ReassignAsync(actor,run,view.Run.Revision,Guid.CreateVersion7(),new Dictionary<int,Guid>{{2,target}},"Transferencia normal después de excepción",null,default);db.ChangeTracker.Clear();
        var reassigned=await engine.ReadAsync(actor,run,default);var moved=reassigned.Attempts.Single(a=>a.Id==reassigned.Stages[1].CurrentAttemptId);var movedAssignment=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==moved.AssignmentId);
        Check(moved.ContinuationAttemptId==recipientAttempt.Id&&moved.CapacityExceptionJson==null&&movedAssignment.ItemLoadSourceAttemptId==recipient.LoadSourceAttemptId&&movedAssignment.LoadMinutes==80&&movedAssignment.Comment=="Trabajo conservado al reasignar"&&!movedAssignment.TemporalDecisionJson!.Contains("PROCESS_ITEM_FORCE"),"Normal process reassignment preserves work/load provenance and does not inherit the force exception");
        run=await NewRun();
        var sourceInBreak=await Source(run);var specialty=Guid.CreateVersion7();
        db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.Specialization{Id=specialty,Name="Break specialist",NormalizedName="BREAK SPECIALIST"});db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.PersonSpecialization{PersonId=person,SpecializationId=specialty});
        var originInBreak=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.Id==sourceInBreak.Stage.Id);originInBreak.Role=CanonicalRole.Subcoordination;originInBreak.SpecializationId=specialty;
        capacity=await db.OperationalCapacities.SingleAsync(c=>c.PersonId==person);capacity.DailyMinutes=4;await db.SaveChangesAsync();db.ChangeTracker.Clear();setClock(clock.UtcNow.AddMinutes(70));
        conflict=await Post(run,"20",main:true);conflictHtml=await conflict.Content.ReadAsStringAsync();token=Hidden(conflictHtml,"CapacityFingerprint");
        Check((await Post(run,"20","FORCE",token,main:true)).StatusCode==HttpStatusCode.Redirect,"Subcoordination origin may explicitly force reception starting inside a break over HTTP");
        view=await engine.ReadAsync(actor,run,default);Check(view.Stages[1].EnabledAt==clock.UtcNow,"Force inside break keeps full interval from requested reception");
        run=await NewRun();var criticalSource=await Source(run);
        var criticalStage=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.RunId==run&&s.Position==2);criticalStage.OperationalCriticality="CRITICA";await db.SaveChangesAsync();db.ChangeTracker.Clear();setClock(clock.UtcNow.AddMinutes(20));
        conflict=await Post(run,"20",main:true);conflictHtml=await conflict.Content.ReadAsStringAsync();token=Hidden(conflictHtml,"CapacityFingerprint");
        Check(conflict.StatusCode==HttpStatusCode.Conflict&&!conflictHtml.Contains("value=\"FORCE\"")&&!conflictHtml.Contains("value=\"LOWEST\""),"Critical dependency conflict offers only pertinent pending resolution");
        Check((await Post(run,"20","FORCE",token,main:true)).StatusCode==HttpStatusCode.Conflict,"Manipulated force cannot bypass critical dependency anchor");
        Check((await db.Set<OperationalSequenceAttempt>().AsNoTracking().SingleAsync(a=>a.Id==criticalSource.Attempt.Id)).ItemQuantity==null,"Rejected critical force leaves delivery and count uncommitted");
        Check((await Post(run,"20","PENDING",token,main:true)).StatusCode==HttpStatusCode.Redirect,"Critical conflict may save delivery with next stage pending");
        // Reuse zero return: audit returns to an omitted configured target, then normal delivery resumes.
        run=await NewRun();Check((await Post(run,"0")).StatusCode==HttpStatusCode.Redirect,"Zero from first stage via real HTTP delivery");
        view=await engine.ReadAsync(actor,run,default);Check(view.Run.ActivePosition==4&&view.Stages.Count(s=>s.Status=="OMITIDA")==2,"First-stage zero omits precisely intermediate tasks");
        setClock(view.Stages[3].PlannedStartsAt);await engine.TickAsync(default);db.ChangeTracker.Clear();
        var src=await Source(run);
        await engine.DecideAsync(actor,run,src.Attempt.Id,src.Attempt.Revision,Guid.CreateVersion7(),"DEVOLUCION","Revisar tarea omitida",null,null,true,default);db.ChangeTracker.Clear();
        view=await engine.ReadAsync(actor,run,default);
        Check(view.Run.ActivePosition==3&&view.Attempts.Any(a=>a.ItemQuantity==0&&a.OmittedStagesJson!=null),"Audit return reopens configured omitted destination and preserves zero history");
        // Large demand cannot be squeezed into an assignment or silently truncated.
        run=await NewRun();
        conflict=await Post(run,"2147483647");conflictHtml=await conflict.Content.ReadAsStringAsync();token=Hidden(conflictHtml,"CapacityFingerprint");
        Check(conflict.StatusCode==HttpStatusCode.Conflict,"Excessive demand requires explicit pending decision");
        Check((await Post(run,"2147483647","PENDING",token)).StatusCode==HttpStatusCode.Redirect,"Save pending stores an unprogrammable full demand");
        view=await engine.ReadAsync(actor,run,default);ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
        Check(view.Stages[1].CalculatedLoadMinutes==8589934588L&&view.Stages[1].Status=="PENDIENTE_ASIGNACION"&&ledger.Load(person,WeekContract.LocalToday(clock.UtcNow),WeekContract.LocalToday(clock.UtcNow))>=8589934588L,"Capacity reads full wide demand without pretending previous estimate is effective load");
        await engine.TickAsync(default);db.ChangeTracker.Clear();Check((await engine.ReadAsync(actor,run,default)).Stages[1].Status=="PENDIENTE_ASIGNACION","Timer does not force or split excessive demand");
        var pendingView=await engine.ReadAsync(actor,run,default);var pendingStage=pendingView.Stages[1];
        var pendingSpecialty=Guid.CreateVersion7();db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.Specialization{Id=pendingSpecialty,Name="Pending count specialist",NormalizedName="PENDING COUNT SPECIALIST"});
        var pendingOwner=Guid.CreateVersion7();var ownerActor=Guid.CreateVersion7();var stranger=Guid.CreateVersion7();var strangerActor=Guid.CreateVersion7();
        foreach(var entry in new[]{(Person:pendingOwner,User:ownerActor,Code:"COUNT-OWNER"),(Person:stranger,User:strangerActor,Code:"COUNT-STRANGER")}) {
            db.People.Add(new(){Id=entry.Person,StableCode=entry.Code,DisplayName=entry.Code,CreatedAt=clock.UtcNow.AddYears(-1)});db.AppUsers.Add(new(){Id=entry.User,PersonId=entry.Person,Status=AccountStatus.Active,SecurityStamp="items"});db.EmploymentVersions.Add(new(Guid.CreateVersion7(),entry.Person,BranchScope.LorettaId,EmploymentStatus.Active,clock.UtcNow.AddYears(-1)));db.RoleAssignmentVersions.Add(new(){Id=Guid.CreateVersion7(),UserId=entry.User,BranchId=BranchScope.LorettaId,RoleCode=CanonicalRole.SalesFloor,Status=RoleAssignmentStatus.Active,ValidFrom=clock.UtcNow.AddYears(-1)});
            db.Add(new OperationalCapacity{Id=Guid.CreateVersion7(),PersonId=entry.Person,From=originalDay,Through=originalDay.AddDays(30),DailyMinutes=480,Reason="Pending count permission fixture"});db.Add(new PersonalScheduleVersion{Id=Guid.CreateVersion7(),PersonId=entry.Person,Revision=1,From=originalDay,ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=Guid.CreateVersion7(),RequestHash=new string('0',64),DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,1140)}))});
        }
        db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.PersonSpecialization{PersonId=pendingOwner,SpecializationId=pendingSpecialty});var pendingEntity=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.Id==pendingStage.Id);pendingEntity.SpecializationId=pendingSpecialty;pendingEntity.ReservedPersonId=pendingOwner;pendingEntity.ReservedPersonName="COUNT-OWNER";pendingEntity.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        pendingStage=(await engine.ReadAsync(actor,run,default)).Stages[1];
        await Reject("ACCESO_DENEGADO",()=>engine.ReturnPendingCountAsync(strangerActor,run,pendingStage.Id,pendingStage.Revision,Guid.CreateVersion7(),"Foreign return",default));db.ChangeTracker.Clear();
        await Reject("PROCESS_ITEM_PENDING_RETURN_CONFLICT",()=>engine.ReturnPendingCountAsync(ownerActor,run,pendingStage.Id,pendingStage.Revision-1,Guid.CreateVersion7(),"Stale return",default));db.ChangeTracker.Clear();
        await Reject("PROCESS_ITEM_RETURN_REASON_REQUIRED",()=>engine.ReturnPendingCountAsync(ownerActor,run,pendingStage.Id,pendingStage.Revision,Guid.CreateVersion7(),"",default));db.ChangeTracker.Clear();
        setClock(clock.UtcNow.AddDays(1));
        var returnKey=Guid.CreateVersion7();await engine.ReturnPendingCountAsync(ownerActor,run,pendingStage.Id,pendingStage.Revision,returnKey,"La cantidad era un error de captura",default);db.ChangeTracker.Clear();
        view=await engine.ReadAsync(actor,run,default);var returnedRound=view.Run.Round;
        Check(view.Run.ActivePosition==1&&view.Stages[1].CalculatedLoadMinutes==null&&view.Stages[1].LoadMinutes==1&&view.Attempts.Any(a=>a.ItemQuantity==2147483647),"Reserved non-direction owner returns unassigned count, releases invalid demand and preserves original quantity");
        Check((await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default)).Load(pendingOwner,WeekContract.LocalToday(clock.UtcNow),WeekContract.LocalToday(clock.UtcNow))<1000,"Pending return restores estimate rather than retaining rejected huge reservation");
        await engine.ReturnPendingCountAsync(ownerActor,run,pendingStage.Id,pendingStage.Revision,returnKey,"La cantidad era un error de captura",default);db.ChangeTracker.Clear();Check((await engine.ReadAsync(actor,run,default)).Run.Round==returnedRound,"Pending count return retry is idempotent");
        var corrected=view.Stages[0];var correction=view.Attempts.Single(a=>a.Id==corrected.CurrentAttemptId);await engine.DecideAsync(actor,run,correction.Id,correction.Revision,Guid.CreateVersion7(),"ENTREGA","Conteo corregido",null,null,true,default,itemQuantity:20);db.ChangeTracker.Clear();view=await engine.ReadAsync(actor,run,default);pendingStage=view.Stages[1];
        Check(pendingStage.LoadMinutes==80&&pendingStage.Status=="PROGRAMADA"&&pendingStage.ReservedPersonId==pendingOwner,"Corrected pending count adopts full new total and preserves compatible reserved person");
        var returnHtml=await client.GetStringAsync($"/mi-trabajo/procesos?id={run}");
        var returnFields=new Dictionary<string,string>{["RunId"]=run.ToString(),["stageId"]=pendingStage.Id.ToString(),["stageRevision"]=pendingStage.Revision.ToString(),["Key"]=Guid.CreateVersion7().ToString(),["reason"]="Dirección devuelve antes de que se asigne"};
        Check((await client.PostAsync("/mi-trabajo/procesos?handler=ReturnPendingCount",new FormUrlEncodedContent(returnFields))).StatusCode==HttpStatusCode.BadRequest,"Pending count return HTTP form requires CSRF");
        var beforeReturnRound=view.Run.Round;
        var returnResponses=await Task.WhenAll(client.PostAsync("/mi-trabajo/procesos?handler=ReturnPendingCount",Form(returnHtml,new(returnFields))),client.PostAsync("/mi-trabajo/procesos?handler=ReturnPendingCount",Form(returnHtml,new(returnFields))));db.ChangeTracker.Clear();
        view=await engine.ReadAsync(actor,run,default);
        Check(returnResponses.All(r=>r.StatusCode==HttpStatusCode.Redirect)&&view.Run.ActivePosition==1&&view.Run.Round==beforeReturnRound+1,"Direction returns another person's pending count through concurrent real HTTP requests exactly once");
        Console.WriteLine("ITEMS_CAPACITY_HTTP_PASS");
    }
    private static FormUrlEncodedContent Form(string html,Dictionary<string,string> fields) { fields["__RequestVerificationToken"]=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);return new(fields); }
    private static async Task Reject(string code,Func<Task> run) {try{await run();throw new Exception("Expected "+code);}catch(DirectionPlanException e) when(e.Code==code){Console.WriteLine("PASS rejected: "+code);}}
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    private sealed class MutableClock:IClock{public DateTimeOffset UtcNow{get;set;}}
    private sealed class Scanner:IFileMalwareScanner{public Task CheckAvailabilityAsync(CancellationToken t)=>Task.CompletedTask;public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken t)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));}
}
