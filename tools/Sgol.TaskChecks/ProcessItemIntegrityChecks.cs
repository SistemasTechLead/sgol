using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
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
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessItemIntegrityChecks
{
    public static async Task RunAsync(SgolDbContext db, Guid actor, Guid person, IClock clock, HttpClient client, Action<DateTimeOffset> setClock)
    {
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var scanner=new Scanner();var validator=new FileTechnicalValidator();
        var catalog=new OperationalProcessCatalogService(db,direction,clock,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Processes:OperationsEnabled"]="true"}).Build());
        var engine=new OperationalProcessService(db,direction,clock,validator,scanner);var planning=new ProcessPlanningService(db,direction,clock,validator,scanner);
        var tasks=new OperationalTaskService(db,direction,clock,validator,scanner);
        var root=await db.Set<OperationalSequence>().AsNoTracking().SingleAsync(r=>r.Name=="Ítems: prueba aislada");
        var editor=await catalog.ReadAsync(actor,root.Id,default);var item=editor.Steps[0].OutputItemId!.Value;
        var historic=JsonSerializer.Serialize(await db.Set<OperationalSequenceStage>().AsNoTracking().OrderBy(s=>s.Id).ToArrayAsync());
        var second=Guid.CreateVersion7();await catalog.SaveItemAsync(actor,second,0,"Etiquetas",7,true,default);db.ChangeTracker.Clear();
        var receiverTask=await db.OperationalTasks.SingleAsync(t=>t.Id==editor.Steps[1].TaskId);receiverTask.Requirement="FOTOGRAFIA";receiverTask.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var third=Guid.CreateVersion7();await catalog.SaveItemAsync(actor,third,0,"Resultados para auditoría",5,true,default);db.ChangeTracker.Clear();
        var steps=editor.Steps.ToList();steps[1].OutputItemId=second;steps[2].OutputItemId=item;steps[2].OutputTargetTaskId=steps[3].TaskId;
        await Reject("PROCESS_ITEM_RETIRED",()=>catalog.SaveAsync(actor,new(){Id=root.Id,Revision=editor.Process.Revision,Name=root.Name,StartKind="OCASIONAL",StartHour=12,Steps=steps},default));db.ChangeTracker.Clear();
        steps[2].OutputItemId=third;
        await catalog.SaveAsync(actor,new(){Id=root.Id,Revision=editor.Process.Revision,Name=root.Name,StartKind="OCASIONAL",StartHour=12,Steps=steps},default);db.ChangeTracker.Clear();editor=await catalog.ReadAsync(actor,root.Id,default);
        await catalog.PublishAsync(actor,root.Id,editor.Process.Revision,default);db.ChangeTracker.Clear();editor=await catalog.ReadAsync(actor,root.Id,default);await catalog.SetEnabledAsync(actor,root.Id,editor.Process.Revision,true,default);db.ChangeTracker.Clear();
        Check(historic==JsonSerializer.Serialize(await db.Set<OperationalSequenceStage>().AsNoTracking().OrderBy(s=>s.Id).ToArrayAsync()),"Publishing new output unit and evidence does not alter started executions");
        var latest=await db.Set<OperationalSequenceRun>().MaxAsync(r=>r.PlannedStartsAt);var day=WeekContract.LocalToday(latest).AddDays(1);
        var temporal=await db.Set<TemporalAssignmentState>().SingleOrDefaultAsync(s=>s.Id==1);if(temporal==null)db.Add(new TemporalAssignmentState{Enabled=true});else temporal.Enabled=true;
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        int serial=0;
        ProcessPlanInput Input()=>new(){Name="Items integrity",ProcessId=root.Id,Mode="OCASION",From=WeekContract.LocalToday(clock.UtcNow),Through=WeekContract.LocalToday(clock.UtcNow),People=Enumerable.Range(1,4).ToDictionary(i=>i,i=>person)};
        async Task<Guid> NewRun() {
            var nextDay=day.AddDays(serial++);if(nextDay<=WeekContract.LocalToday(clock.UtcNow)&&serial>1) nextDay=WeekContract.LocalToday(clock.UtcNow).AddDays(1);
            setClock(OperationalSchedule.Instant(nextDay,12));
            var capacity=await db.OperationalCapacities.SingleAsync(c=>c.PersonId==person);capacity.DailyMinutes=480;await db.SaveChangesAsync();db.ChangeTracker.Clear();
            var input=Input();input.Fingerprint=(await planning.PreviewAsync(actor,input,default)).Fingerprint;var plan=await planning.PrepareAsync(actor,input,default);db.ChangeTracker.Clear();var run=(await planning.ActivateAsync(actor,plan,default)).Single();db.ChangeTracker.Clear();return run;
        }
        async Task<(OperationalSequenceStage Stage,OperationalSequenceAttempt Attempt)> Active(Guid id) {
            var view=await engine.ReadAsync(actor,id,default);var stage=view.Stages.Single(s=>s.Position==view.Run.ActivePosition);
            for(var i=0;stage.Status=="PROGRAMADA"&&i<8;i++){setClock(new[]{clock.UtcNow,stage.PlannedStartsAt}.Max());await engine.TickAsync(default);db.ChangeTracker.Clear();view=await engine.ReadAsync(actor,id,default);stage=view.Stages.Single(s=>s.Position==view.Run.ActivePosition);}
            Check(stage.Status=="DISPONIBLE",$"Integration active stage {stage.Position}: {stage.Status}");return(stage,view.Attempts.Single(a=>a.Id==stage.CurrentAttemptId));
        }
        async Task Decide(Guid id,string decision,int? quantity=null,bool reuse=false) {var a=await Active(id);await engine.DecideAsync(actor,id,a.Attempt.Id,a.Attempt.Revision,Guid.CreateVersion7(),decision,"Items integrity",null,null,true,default,itemQuantity:quantity,usePreservedWork:reuse);db.ChangeTracker.Clear();}
        setClock(OperationalSchedule.Instant(day,12));var stale=Input();stale.Fingerprint=(await planning.PreviewAsync(actor,stale,default)).Fingerprint;
        var current=await db.Set<ProcessItem>().AsNoTracking().SingleAsync(i=>i.Id==item);await catalog.SaveItemAsync(actor,item,current.Revision,"Fotografías",5,false,default);db.ChangeTracker.Clear();
        await Reject("PLAN_CAPACITY_STALE",()=>planning.PrepareAsync(actor,stale,default));db.ChangeTracker.Clear();
        Check(!await db.DirectionPreparedPlans.AnyAsync(p=>p.Id==stale.Key),"Rate race leaves no partial prepared plan");
        var run=await NewRun();
        var sharedRoot=Guid.CreateVersion7();await catalog.CreateAsync(actor,sharedRoot,"Proceso compartido de etiquetas","",default);db.ChangeTracker.Clear();
        var sharedTasks=Enumerable.Range(0,2).Select(i=>new OperationalTask{Id=Guid.CreateVersion7(),TaskCode=$"TAR-{9810+i}",Name=$"Compartida {i+1}",ProcessId=sharedRoot,Requirement="COMENTARIO",AssignedRole=CanonicalRole.Direction,LoadMinutes=1,OperationalCriticality="FLEXIBLE"}).ToArray();db.AddRange(sharedTasks);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var sharedEditor=await catalog.ReadAsync(actor,sharedRoot,default);
        await catalog.SaveAsync(actor,new(){Id=sharedRoot,Revision=sharedEditor.Process.Revision,Name="Proceso compartido de etiquetas",StartKind="OCASIONAL",StartHour=12,Steps=[new(){TaskId=sharedTasks[0].Id,Position=1,Action="ENTREGAR",ReleaseCondition="INICIO",DeadlineHours=1,OutputItemId=second,OutputTargetTaskId=sharedTasks[1].Id},new(){TaskId=sharedTasks[1].Id,Position=2,Action="AUTORIZAR",ReleaseCondition="ENTREGA",DeadlineHours=1,IsFinalAudit=true}]},default);db.ChangeTracker.Clear();sharedEditor=await catalog.ReadAsync(actor,sharedRoot,default);
        await catalog.PublishAsync(actor,sharedRoot,sharedEditor.Process.Revision,default);db.ChangeTracker.Clear();sharedEditor=await catalog.ReadAsync(actor,sharedRoot,default);await catalog.SetEnabledAsync(actor,sharedRoot,sharedEditor.Process.Revision,true,default);db.ChangeTracker.Clear();
        var sharedInput=new ProcessPlanInput{Name="Tarifa compartida capturada",ProcessId=sharedRoot,Mode="OCASION",From=WeekContract.LocalToday(clock.UtcNow),Through=WeekContract.LocalToday(clock.UtcNow),People=new(){{1,person},{2,person}}};sharedInput.Fingerprint=(await planning.PreviewAsync(actor,sharedInput,default)).Fingerprint;
        var sharedPlan=await planning.PrepareAsync(actor,sharedInput,default);db.ChangeTracker.Clear();await catalog.SaveItemAsync(actor,second,1,"Etiquetas",8,true,default);db.ChangeTracker.Clear();
        var sharedFuture=new ProcessPlanInput{Name="Compartida futura",ProcessId=sharedRoot,Mode="OCASION",From=sharedInput.From,Through=sharedInput.Through,People=new(){{1,person},{2,person}}};
        var futureShared=await planning.PreviewAsync(actor,sharedFuture,default);var futureMain=await planning.PreviewAsync(actor,Input(),default);
        Check(futureShared.ItemRates[1].Minutes==8&&futureMain.ItemRates[2].Minutes==8&&(await planning.ReadAsync(actor,sharedPlan,default)).Configurations.Single().Stages[0].OutputItem!.Minutes==7&&(await engine.ReadAsync(actor,run,default)).Stages[1].OutputItemMinutes==7,"Shared rate change reaches two processes without republishing while preserving prepared plan and started run rates");
        await Decide(run,"ENTREGA",2);var receiver=await Active(run);
        Check(receiver.Stage.LoadMinutes==10&&receiver.Stage.OutputItemName=="Etiquetas"&&receiver.Stage.OutputItemMinutes==7,"Receiving photographs and reporting labels use distinct units and frozen rates");
        var capture=await tasks.BeginCaptureAsync(actor,receiver.Attempt.AssignmentId!.Value,default);db.ChangeTracker.Clear();
        using var bitmap=new SkiaSharp.SKBitmap(32,32);using var image=SkiaSharp.SKImage.FromBitmap(bitmap);using var jpg=image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg,90);var bytes=jpg.ToArray();
        var photo=await tasks.UploadPhotographAsync(actor,receiver.Attempt.AssignmentId.Value,capture.Id,Guid.CreateVersion7(),new FormFile(new MemoryStream(bytes),0,bytes.Length,"file","capture.jpg"){Headers=new HeaderDictionary(),ContentType="image/jpeg"},default);db.ChangeTracker.Clear();
        var photoForm=await client.GetStringAsync($"/mi-trabajo?assignedTask={receiver.Attempt.AssignmentId}");
        var photoFields=new Dictionary<string,string>{{"__RequestVerificationToken",WebUtility.HtmlDecode(Regex.Match(photoForm,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value)},{"id",receiver.Attempt.AssignmentId.Value.ToString()},{"AttemptId",receiver.Attempt.Id.ToString()},{"Revision",receiver.Attempt.Revision.ToString()},{"Key",Guid.CreateVersion7().ToString()},{"Decision","ENTREGA"},{"Conclusion","Preview with verified camera material"},{"Declared","true"},{"ItemQuantity","2147483647"},{"captureSessionId",capture.Id.ToString()},{"photographIds",JsonSerializer.Serialize(new[]{photo.Id})}};
        var cameraConflict=await client.PostAsync("/mi-trabajo?handler=CompleteProcess",new FormUrlEncodedContent(photoFields));var cameraConflictHtml=await cameraConflict.Content.ReadAsStringAsync();db.ChangeTracker.Clear();
        Check(cameraConflict.StatusCode==HttpStatusCode.Conflict&&cameraConflictHtml.Contains("value=\""+capture.Id+"\"")&&cameraConflictHtml.Contains(photo.Id.ToString())&&cameraConflictHtml.Contains("fotograf"),"Capacity form retains verified camera session and IDs without uploading bytes again");
        Check((await db.OperationalPhotographs.AsNoTracking().SingleAsync(p=>p.Id==photo.Id)).AttachedAt==null,"Rejected preview leaves staged camera receipt unconsumed");
        setClock(clock.UtcNow.AddDays(1).AddMinutes(31));
        await Sgol.Web.Infrastructure.Evidence.ExpiredCameraCleanupService.CleanupAsync(db,clock.UtcNow,default);db.ChangeTracker.Clear();
        Check(await db.OperationalPhotographs.AnyAsync(p=>p.Id==photo.Id),"Expired staged work survives cleanup before its count is returned");
        await Decide(run,"DEVOLUCION");
        var reopened=await Active(run);Check(WeekContract.LocalToday(reopened.Stage.PlannedStartsAt)==WeekContract.LocalToday(clock.UtcNow),"Next-day return rebases complete flexible task within current shift");
        await Decide(run,"ENTREGA",12);receiver=await Active(run);
        Check(receiver.Stage.LoadMinutes==60&&receiver.Attempt.ContinuationAttemptId!=null,"Corrected 12×5 reserves full load with prior camera work");
        await Sgol.Web.Infrastructure.Evidence.ExpiredCameraCleanupService.CleanupAsync(db,clock.UtcNow.AddDays(2),default);db.ChangeTracker.Clear();
        Check(await db.OperationalPhotographs.AnyAsync(p=>p.Id==photo.Id)&&await db.OperationalCaptureSessions.AnyAsync(s=>s.Id==capture.Id),"Real expired-camera cleanup preserves continuation work and its original session context");
        var preserved=await engine.PreservedPhotoAsync(actor,receiver.Attempt.Id,photo.Id,default);
        Check(preserved.AttachedAt==null&&preserved.AssignmentId!=receiver.Attempt.AssignmentId,"Expired staged photo remains available with original ownership");
        var response=await client.GetAsync($"/mi-trabajo/procesos?handler=PreservedPhoto&currentAttemptId={receiver.Attempt.Id}&photoId={photo.Id}");
        Check(response.StatusCode==HttpStatusCode.OK&&(await response.Content.ReadAsByteArrayAsync()).SequenceEqual(preserved.Content),"Preserved material is usable through authenticated HTTP endpoint");
        if(Environment.GetEnvironmentVariable("SGOL_ITEMS_UI_SEED")=="1") { Console.WriteLine($"ITEMS_UI_CONTINUATION_READY /mi-trabajo?assignedTask={receiver.Attempt.AssignmentId}");return; }
        var capturesBefore=await db.OperationalCaptureSessions.CountAsync();var photosBefore=await db.OperationalPhotographs.CountAsync();
        var failing=new OperationalProcessService(db,direction,clock,validator,new FailedScanner());
        await Reject("TASK_FILE_INVALID",()=>failing.DecideAsync(actor,run,receiver.Attempt.Id,receiver.Attempt.Revision,Guid.CreateVersion7(),"ENTREGA","Fail scanner",null,null,true,default,itemQuantity:3,usePreservedWork:true));db.ChangeTracker.Clear();
        Check(capturesBefore==await db.OperationalCaptureSessions.CountAsync()&&photosBefore==await db.OperationalPhotographs.CountAsync()&&(await engine.ReadAsync(actor,run,default)).Run.ActivePosition==2,"Scanner rejection rolls back receipt, copied photos, count and stage transition");
        await Decide(run,"ENTREGA",3,reuse:true);var next=await Active(run);
        Check(next.Stage.LoadMinutes==21,"Second independent count 3×7 affects only third task");
        var delivered=await db.OperationalPhotographs.AsNoTracking().Where(p=>p.AssignmentId==receiver.Attempt.AssignmentId&&p.AttachedAt!=null).SingleAsync();
        Check(delivered.Id!=photo.Id&&delivered.Content.SequenceEqual(preserved.Content)&&delivered.CaptureSessionId!=capture.Id,"Continuation creates a validated new receipt while retaining original bytes/session");
        Check(await db.Set<OperationalSequenceEvent>().AnyAsync(e=>e.RunId==run&&e.EventKey==$"continue-work:{receiver.Attempt.Id:N}"),"Reused material provenance is recorded");
        var definition=await db.OperationalTasks.SingleAsync(t=>t.Id==next.Stage.TaskId);definition.LoadMinutes=99;definition.Name+=" actualizado";definition.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var reportDate=WeekContract.LocalToday(next.Stage.PlannedStartsAt);
        var reportUrl=$"/reportes?handler=Export&F.Range=custom&F.From={reportDate:yyyy-MM-dd}&F.To={reportDate:yyyy-MM-dd}&F.Plan={(await engine.ReadAsync(actor,run,default)).Run.PlanId}&F.Task={next.Stage.TaskCode}&F.Columns=task,minutes&F.Kind=overview";
        var report=await client.GetAsync(reportUrl);using var export=JsonDocument.Parse(await report.Content.ReadAsStringAsync());
        Check(report.StatusCode==HttpStatusCode.OK&&export.RootElement.GetProperty("rows").EnumerateArray().Any(r=>r[1].GetString()=="21 min"),"Authorized real report export consumes calculated 21 minutes rather than catalog 99");
        var refresh=new AssignmentRefreshService(db,direction,clock);var request=new AssignmentRefreshRequest(definition.Id,Guid.CreateVersion7(),clock.UtcNow);
        var preview=await refresh.PreviewAsync(actor,request,default);
        Check(preview.Differences.All(d=>d.After==null||d.After.ItemLoadSourceAttemptId==null||d.After.LoadMinutes==21),"Assignment refresh preserves calculated load when catalog load changes");
        await Decide(run,"ENTREGA",2);
        var audit=await Active(run);Check(audit.Stage.LoadMinutes==10,"Positive count immediately before audit applies normally");
        await Decide(run,"DEVOLUCION");await Decide(run,"ENTREGA",0);audit=await Active(run);
        Check(audit.Stage.LoadMinutes==1&&audit.Stage.CalculatedLoadMinutes==null,"Correcting positive to zero immediately before audit restores audit own load");
        await Decide(run,"SIN_DIFERENCIAS");var completedRun=run;
        // Database rejects history rewrites even outside application services.
        await RejectSql(db,$"UPDATE process_item_rate SET \"Minutes\"=9 WHERE \"ItemId\"='{item}'");
        var counted=await db.Set<OperationalSequenceAttempt>().AsNoTracking().FirstAsync(a=>a.RunId==run&&a.ItemQuantity!=null);
        await RejectSql(db,$"UPDATE operational_sequence_attempt SET \"ItemQuantity\"=99 WHERE \"Id\"='{counted.Id}'");
        // Actual concurrent HTTP requests use independent request scopes and the shared operational lock.
        run=await NewRun();var source=await Active(run);var html=await client.GetStringAsync($"/mi-trabajo/procesos?id={run}");
        var csrf=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);var key=Guid.CreateVersion7();
        var fields=new Dictionary<string,string>{{"__RequestVerificationToken",csrf},{"RunId",run.ToString()},{"AttemptId",source.Attempt.Id.ToString()},{"Revision",source.Attempt.Revision.ToString()},{"Key",key.ToString()},{"Decision","ENTREGA"},{"Conclusion","Concurrent items"},{"Declared","true"},{"ItemQuantity","2"}};
        var replies=await Task.WhenAll(client.PostAsync("/mi-trabajo/procesos?handler=Decide",new FormUrlEncodedContent(fields)),client.PostAsync("/mi-trabajo/procesos?handler=Decide",new FormUrlEncodedContent(fields)));db.ChangeTracker.Clear();
        Check(replies.All(r=>r.StatusCode==HttpStatusCode.Redirect)&&await db.Set<OperationalSequenceEvent>().CountAsync(e=>e.RunId==run&&e.EventKey==$"decision:{key:N}")==1,"Concurrent identical HTTP submissions yield exactly one transition");
        fields["ItemQuantity"]="3";Check((await client.PostAsync("/mi-trabajo/procesos?handler=Decide",new FormUrlEncodedContent(fields))).StatusCode==HttpStatusCode.Conflict,"Same idempotency key with changed quantity conflicts over HTTP");db.ChangeTracker.Clear();
        // Enforce origin role using persisted stage, including specialist cross-role eligibility.
        run=await NewRun();source=await Active(run);var specialty=Guid.CreateVersion7();
        db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.Specialization{Id=specialty,Name="Items specialist",NormalizedName="ITEMS SPECIALIST"});db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.PersonSpecialization{PersonId=person,SpecializationId=specialty});
        var origin=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.Id==source.Stage.Id);origin.Role=CanonicalRole.SalesFloor;origin.SpecializationId=specialty;
        var cap=await db.OperationalCapacities.SingleAsync(c=>c.PersonId==person);cap.DailyMinutes=4;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        await Reject("ACCESO_DENEGADO",()=>engine.DecideAsync(actor,run,source.Attempt.Id,source.Attempt.Revision,Guid.CreateVersion7(),"ENTREGA","Forbidden force",null,null,true,default,itemQuantity:20,capacityResolution:"FORCE"));db.ChangeTracker.Clear();
        string? fingerprint=null;try{await engine.DecideAsync(actor,run,source.Attempt.Id,source.Attempt.Revision,Guid.CreateVersion7(),"ENTREGA","Pending permitted",null,null,true,default,itemQuantity:20);}catch(DirectionPlanException e)when(e.Code=="PROCESS_ITEM_CAPACITY"){fingerprint=e.Data["CapacityFingerprint"] as string;}db.ChangeTracker.Clear();
        Check(fingerprint!=null,"Future recipient capacity is checked before delivery commits");
        await engine.DecideAsync(actor,run,source.Attempt.Id,source.Attempt.Revision,Guid.CreateVersion7(),"ENTREGA","Pending permitted",null,null,true,default,itemQuantity:20,capacityResolution:"PENDING",capacityFingerprint:fingerprint);db.ChangeTracker.Clear();
        Check((await engine.ReadAsync(actor,run,default)).Stages[1].Status=="PENDIENTE_ASIGNACION","Other origin can save pending while server forbids force");
        // No compatible alternative: search must fail without partially delivering.
        run=await NewRun();source=await Active(run);cap=await db.OperationalCapacities.SingleAsync(c=>c.PersonId==person);cap.DailyMinutes=4;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        async Task Deliver(string? option,string? fp)=>await engine.DecideAsync(actor,run,source.Attempt.Id,source.Attempt.Revision,Guid.CreateVersion7(),"ENTREGA","Find lowest",null,null,true,default,itemQuantity:20,capacityResolution:option,capacityFingerprint:fp);
        try{await Deliver(null,null);}catch(DirectionPlanException e)when(e.Code=="PROCESS_ITEM_CAPACITY"){fingerprint=e.Data["CapacityFingerprint"] as string;}db.ChangeTracker.Clear();
        await Reject("PROCESS_ITEM_CAPACITY",()=>Deliver("LOWEST",fingerprint));db.ChangeTracker.Clear();
        var other=Guid.CreateVersion7();var otherActor=Guid.CreateVersion7();var from=clock.UtcNow.AddYears(-1);
        db.People.Add(new(){Id=other,StableCode="ITEMS-ALTERNATIVE",DisplayName="Compatible lower load",CreatedAt=from});db.AppUsers.Add(new(){Id=otherActor,PersonId=other,Status=AccountStatus.Active,SecurityStamp="items"});db.EmploymentVersions.Add(new(Guid.CreateVersion7(),other,BranchScope.LorettaId,EmploymentStatus.Active,from));db.RoleAssignmentVersions.Add(new(){Id=Guid.CreateVersion7(),UserId=otherActor,BranchId=BranchScope.LorettaId,RoleCode=CanonicalRole.Direction,Status=RoleAssignmentStatus.Active,ValidFrom=from});
        db.Add(new OperationalCapacity{Id=Guid.CreateVersion7(),PersonId=other,From=day,Through=day.AddDays(30),DailyMinutes=480,Reason="Items alternative"});
        db.Add(new PersonalScheduleVersion{Id=Guid.CreateVersion7(),PersonId=other,Revision=1,From=day,ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=Guid.CreateVersion7(),RequestHash=new string('0',64),DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(540,1140)}))});await db.SaveChangesAsync();db.ChangeTracker.Clear();
        await Reject("PROCESS_ITEM_CAPACITY",()=>Deliver("LOWEST",fingerprint));db.ChangeTracker.Clear();
        try{await Deliver(null,null);}catch(DirectionPlanException e)when(e.Code=="PROCESS_ITEM_CAPACITY"){fingerprint=e.Data["CapacityFingerprint"] as string;}db.ChangeTracker.Clear();
        var futureAttempt=(await engine.ReadAsync(actor,run,default)).Attempts.First(a=>a.StageId==source.Stage.Id);
        await Reject("ACCESO_DENEGADO",()=>engine.DecideAsync(otherActor,run,futureAttempt.Id,futureAttempt.Revision,Guid.CreateVersion7(),"ENTREGA","Foreign delivery",null,null,true,default,itemQuantity:1));db.ChangeTracker.Clear();
        await Deliver("LOWEST",fingerprint);db.ChangeTracker.Clear();Check((await engine.ReadAsync(actor,run,default)).Stages[1].ReservedPersonId==other,"Lowest-load search chooses a compatible available person after revalidating changed agenda");
        var deletedPlan=(await engine.ReadAsync(actor,completedRun,default)).Run.PlanId;
        var deletion=await direction.PreviewDeletionAsync(actor,deletedPlan);var deleteKey=Guid.CreateVersion7();
        await direction.DeleteAsync(actor,deletedPlan,deleteKey,deleteKey,deletion.Fingerprint);db.ChangeTracker.Clear();
        await direction.DeleteAsync(actor,deletedPlan,deleteKey,deleteKey,deletion.Fingerprint);db.ChangeTracker.Clear();
        Check(!await db.Set<OperationalSequenceRun>().AnyAsync(r=>r.Id==completedRun)&&!await db.OperationalAssignments.AnyAsync(a=>a.PlanId==deletedPlan)&&await db.Set<OperationalSequenceRun>().AnyAsync(r=>r.Id==run),"Audited item-plan deletion and retry remove complete continuation graph without touching another execution");
        Console.WriteLine("ITEMS_INTEGRITY_PASS");
    }
    private static async Task RejectSql(SgolDbContext db,string sql){await using var tx=await db.Database.BeginTransactionAsync();try{await db.Database.ExecuteSqlRawAsync(sql);throw new Exception("Expected database rejection");}catch(Npgsql.PostgresException e)when(e.SqlState=="23514"){Console.WriteLine("PASS: Database immutable history constraint");}await tx.RollbackAsync();db.ChangeTracker.Clear();}
    private static async Task Reject(string code,Func<Task> run){try{await run();throw new Exception("Expected "+code);}catch(DirectionPlanException e)when(e.Code==code){Console.WriteLine("PASS rejected: "+code);}}
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    private sealed class FailedScanner:IFileMalwareScanner{public Task CheckAvailabilityAsync(CancellationToken t)=>Task.CompletedTask;public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken t)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Infectado));}
    private sealed class Scanner:IFileMalwareScanner{public Task CheckAvailabilityAsync(CancellationToken t)=>Task.CompletedTask;public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken t)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));}
}
