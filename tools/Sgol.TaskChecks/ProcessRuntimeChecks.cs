using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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

public static class ProcessRuntimeChecks
{
    public static async Task RunAsync(SgolDbContext db,bool seedOnly=false,bool preparePlan=false,bool reassignment=false,bool deletion=false,bool crossRole=false)
    {
        if(await db.People.AnyAsync()) throw new Exception("Runtime checks require an empty dedicated database.");
        var clock=new Clock(); var scanner=new Scanner(); var validator=new FileTechnicalValidator();
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var tasks=new OperationalTaskService(db,direction,clock,validator,scanner);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"]="true" }).Build();
        var catalog=new OperationalProcessCatalogService(db,direction,clock,config);
        var processes=new OperationalProcessService(db,direction,clock,validator,scanner);
        var password=Environment.GetEnvironmentVariable("SGOL_PROCESS_HTTP_PASSWORD");
        var roles=new[] { CanonicalRole.SalesFloor,CanonicalRole.Subcoordination,CanonicalRole.Administration,CanonicalRole.Direction };
        if(crossRole) roles[0]=CanonicalRole.Administration;
        var specialty=Guid.CreateVersion7();
        if(crossRole) db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.Specialization { Id=specialty,Name="Inventario transversal",NormalizedName="INVENTARIO TRANSVERSAL" });
        var users=new Guid[4]; var people=new Dictionary<int,Guid>();
        for(var i=0;i<4;i++)
        {
            var person=Guid.CreateVersion7(); var actor=Guid.CreateVersion7(); var from=clock.UtcNow.AddYears(-1); users[i]=actor; people[i+1]=person;
            db.People.Add(new Person { Id=person,StableCode=$"INV-DEMO-{i+1}",DisplayName=$"Ejemplo · {roles[i]}",CreatedAt=from });
            var user=new AppUser { Id=actor,PersonId=person,Status=AccountStatus.Active,SecurityStamp=Guid.NewGuid().ToString("N") }; db.Add(user);
            db.EmploymentVersions.Add(new(Guid.CreateVersion7(),person,BranchScope.LorettaId,EmploymentStatus.Active,from));
            db.RoleAssignmentVersions.Add(new() { Id=Guid.CreateVersion7(),UserId=actor,BranchId=BranchScope.LorettaId,RoleCode=roles[i],Status=RoleAssignmentStatus.Active,ValidFrom=from });
            db.OperationalCapacities.Add(new() { Id=Guid.CreateVersion7(),PersonId=person,From=WeekContract.LocalToday(clock.UtcNow).AddDays(-1),Through=WeekContract.LocalToday(clock.UtcNow).AddDays(30),DailyMinutes=480,Reason="Capacidad del ejemplo aislado; no modifica personas reales" });
            if(password!=null) db.IdentityCredentials.Add(new() { UserId=actor,UserName=$"inventario.ejemplo.{i+1}",NormalizedUserName=$"INVENTARIO.EJEMPLO.{i+1}",PasswordHash=new PasswordHasher<AppUser>().HashPassword(user,password) });
        }
        if(crossRole) db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.PersonSpecialization { PersonId=people[1],SpecializationId=specialty });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        foreach(var old in await db.OperationalTasks.AsNoTracking().ToArrayAsync()) { await tasks.ChangeAsync(users[3],old.Id,old.Revision,true,default); db.ChangeTracker.Clear(); }
        var root=Guid.CreateVersion7(); await catalog.CreateAsync(users[3],root,"Ejemplo: Chequeo de inventario","Demostración sin inventario ni participantes reales. Sizes & Colors se revisa manualmente.",default); db.ChangeTracker.Clear();
        var loads=new[] {360,60,60,60}; var taskIds=new Guid[4];
        for(var i=0;i<4;i++)
        {
            var rule=OperationalSequenceRules.InventoryPilot[i];
            await tasks.SaveAsync(users[3],new OperationalTask { Revision=0,Name=rule.Name+" · Ejemplo",ProcessId=root,
                Description="Descripción de etapa "+i,
                SpecializationId=crossRole && i==0 ? specialty : null,
                AssignedRole=rule.Role,Requirement="DOCUMENTO",LoadMinutes=loads[i],DeadlineHours=rule.DeadlineHours,PeriodType="OCASIONAL" },default); db.ChangeTracker.Clear();
            taskIds[i]=await db.OperationalTasks.Where(t=>t.ProcessId==root && t.AssignedRole==rule.Role).Select(t=>t.Id).SingleAsync();
        }
        var editor=await catalog.ReadAsync(users[3],root,default);
        var input=new ProcessDraftInput { Id=root,Revision=editor.Process.Revision,Name=editor.Process.Name,Description=editor.Process.Description,
            StartKind="SEMESTRAL",StartHour=11,Steps=OperationalSequenceRules.InventoryPilot.Select((s,i)=>new OperationalSequenceStep {
                TaskId=taskIds[i],Position=s.Position,Action=s.Action,ReleaseCondition=s.ReleaseCondition,DeadlineHours=s.DeadlineHours,ReturnToPosition=s.ReturnToPosition,
                Instructions=i==0 ? "Registrar todo en PDF; en devolución buscar faltantes y presentar sobrantes." : i==1 ? "Contrastar manualmente con Sizes & Colors y documentar diferencias pendientes." : i==2 ? "Revisar hoja e irregularidades; devolver con información o pasar a Dirección." : "Aceptar la versión exacta con conclusión y diferencias pendientes, o devolver a Administración." }).ToList() };
        await catalog.SaveAsync(users[3],input,default); db.ChangeTracker.Clear(); editor=await catalog.ReadAsync(users[3],root,default);
        await catalog.PublishAsync(users[3],root,editor.Process.Revision,default); db.ChangeTracker.Clear(); editor=await catalog.ReadAsync(users[3],root,default);
        await catalog.SetEnabledAsync(users[3],root,editor.Process.Revision,true,default); db.ChangeTracker.Clear();
        Check(await db.OperationalTasks.CountAsync(t=>!t.Deleted)==4,"Demo catalog contains only its four process tasks");
        Check(!await db.OperationalAssignments.AnyAsync(),"Creating, publishing and activating create no execution by themselves");
        var start=OperationalSchedule.Instant(WeekContract.LocalToday(clock.UtcNow),11); var key=Guid.CreateVersion7();
        Guid run;
        if(preparePlan)
        {
            var planning=new ProcessPlanningService(db,direction,clock,validator,scanner);
            var planInput=new ProcessPlanInput { Key=key,Name="Plan de inventario de ejemplo",ProcessId=root,Mode="OCASION",From=WeekContract.LocalToday(clock.UtcNow),Through=WeekContract.LocalToday(clock.UtcNow),People=people };
            var preview=await planning.PreviewAsync(users[3],planInput,default); planInput.Fingerprint=preview.Fingerprint;
            var plan=await planning.PrepareAsync(users[3],planInput,default); db.ChangeTracker.Clear();
            if(deletion) await ProcessPlanDeletionChecks.PreparedAsync(db, direction, planning, users, planInput);
            Check(!await db.OperationalAssignments.AnyAsync() && !await db.Set<OperationalSequenceRun>().AnyAsync(),"Prepared process plan reserves workload without enabling tasks or creating a run");
            var preparedLedger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
            Check(preparedLedger.Tasks.Length==4 && preparedLedger.Tasks.Sum(t=>t.LoadMinutes)==540,"Prepared plan reserves all four stages exactly once");
            Check(await planning.PrepareAsync(users[3],planInput,default)==plan && await db.Set<PreparedProcessStart>().CountAsync()==1,"Prepared plan retry keeps one process start"); db.ChangeTracker.Clear();
            var activated=await planning.ActivateAsync(users[3],plan,default); db.ChangeTracker.Clear(); run=activated.Single();
            Check((await planning.ActivateAsync(users[3],plan,default)).Single()==run && await db.OperationalAssignments.CountAsync()==1,"Activating a process plan starts only its first stage and is idempotent"); db.ChangeTracker.Clear();
            Check(await db.Set<PreparedProcessStart>().AllAsync(s=>s.Status=="ACTIVADO"),"Activation replaces prepared reservations with run reservations atomically");
        }
        else { run=await processes.StartAsync(users[3],root,key,start,people,default); db.ChangeTracker.Clear(); }
        Check((await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==run).OrderBy(s=>s.Position).Select(s=>s.Description).ToArrayAsync())
            .SequenceEqual(Enumerable.Range(0,4).Select(i=>"Descripción de etapa "+i)),"Process stages preserve task descriptions");
        Check(await db.OperationalAssignments.Where(a=>a.PlanId==db.Set<OperationalSequenceRun>().Where(r=>r.Id==run).Select(r=>r.PlanId).Single()).AllAsync(a=>a.Description!=null),"Process assignments include description");
        Check(await db.OperationalAssignments.CountAsync()==1,"Only first enabled stage receives an effective assignment");
        var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
        Check(ledger.Tasks.Length==4 && ledger.Tasks.Sum(t=>t.LoadMinutes)==540,"Capacity accounts for first assignment and three reservations exactly once");
        if(seedOnly)
        {
            Directory.CreateDirectory("outputs/procesos");
            await File.WriteAllTextAsync("outputs/procesos/demo-identities.json",System.Text.Json.JsonSerializer.Serialize(new { processId=root,runId=run,people,users }));
            return;
        }
        if(reassignment)
        {
            if(crossRole) Check((await db.OperationalAssignments.SingleAsync()).AssignedRole==CanonicalRole.Administration,"Specialized process activation crosses roles and captures recipient role");
            var original=await processes.ReadAsync(users[3],run,default); var oldAttempt=original.Attempts.Single(a=>a.Id==original.Stages[0].CurrentAttemptId); var originalActor=users[0];
            var targets=new Dictionary<int,Guid>(); var actors=new Dictionary<int,Guid>();
            foreach(var position in new[] {1,4})
            {
                var person=Guid.CreateVersion7(); var actor=Guid.CreateVersion7(); var from=clock.UtcNow.AddYears(-1); targets[position]=person; actors[position]=actor;
                db.People.Add(new Person { Id=person,StableCode=$"CONTROLLED-{position}",DisplayName=$"Controlled stage {position}",CreatedAt=from });
                db.AppUsers.Add(new AppUser { Id=actor,PersonId=person,Status=AccountStatus.Active,SecurityStamp="controlled-check" });
                db.EmploymentVersions.Add(new(Guid.CreateVersion7(),person,BranchScope.LorettaId,EmploymentStatus.Active,from));
                db.RoleAssignmentVersions.Add(new() { Id=Guid.CreateVersion7(),UserId=actor,BranchId=BranchScope.LorettaId,RoleCode=crossRole && position==1 ? CanonicalRole.Subcoordination : roles[position-1],Status=RoleAssignmentStatus.Active,ValidFrom=from });
            }
            if(crossRole) db.Add(new Sgol.Web.Infrastructure.Persistence.Organization.PersonSpecialization { PersonId=targets[1],SpecializationId=specialty });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var request=Guid.CreateVersion7(); var day=WeekContract.LocalToday(clock.UtcNow); var capacity=new CapacityChange(targets[1],day,day.AddDays(8),100,360,"Temporary test capacity");
            await Reject(()=>processes.ReassignAsync(users[0],run,original.Run.Revision,request,targets,"Unauthorized",capacity,default),"ACCESO_DENEGADO"); db.ChangeTracker.Clear();
            await Reject(()=>processes.ReassignAsync(users[3],run,original.Run.Revision,request,targets,"No capacity",null,default),"PLAN_NO_AVAILABLE_WORKER"); db.ChangeTracker.Clear();
            Check(await db.OperationalAssignments.CountAsync()==1 && (await db.Set<OperationalSequenceRun>().SingleAsync(r=>r.Id==run)).Revision==original.Run.Revision,"Rejected reassignment rolls back assignments and run changes"); db.ChangeTracker.Clear();
            await processes.ReassignAsync(users[3],run,original.Run.Revision,request,targets,"User-controlled demonstration accounts",capacity,default); db.ChangeTracker.Clear();
            var assigned=await processes.ReadAsync(users[3],run,default); var next=assigned.Attempts.Single(a=>a.Id==assigned.Stages[0].CurrentAttemptId);
            Check(next.Id!=oldAttempt.Id && next.Number==2 && assigned.Attempts.Single(a=>a.Id==oldAttempt.Id).Status=="SUSTITUIDO","Reassignment opens a new attempt and preserves the previous identity");
            Check(assigned.Run.Round==1 && assigned.Run.ActivePosition==1 && assigned.Stages[0].EffectiveDueAt==original.Stages[0].EffectiveDueAt,"Reassignment preserves the original deadline and does not advance the flow");
            Check(assigned.Stages[1].CurrentAttemptId==original.Stages[1].CurrentAttemptId && assigned.Stages[2].CurrentAttemptId==original.Stages[2].CurrentAttemptId,"Subcoordination and Administration remain unchanged");
            var old=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==oldAttempt.AssignmentId); var current=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==next.AssignmentId);
            if(crossRole) Check(current.AssignedRole==CanonicalRole.Subcoordination && old.AssignedRole==CanonicalRole.Administration,"Process reassignment crosses roles and preserves previous role history");
            Check(old.ReviewStatus=="REASIGNADA" && old.CompletionKind=="SUPERVISION" && current.ReassignedFromId==old.Id && current.PersonId==targets[1],"Old assignment retains its owner and supervised replacement history");
            await processes.ReassignAsync(users[3],run,original.Run.Revision,request,targets,"User-controlled demonstration accounts",capacity,default); db.ChangeTracker.Clear();
            Check(await db.OperationalAssignments.CountAsync()==2 && await db.Set<OperationalSequenceEvent>().CountAsync(e=>e.Kind=="REASIGNACION")==2,"Reassignment retry is idempotent");
            await Reject(()=>processes.DecideAsync(originalActor,run,oldAttempt.Id,oldAttempt.Revision,Guid.CreateVersion7(),"ENTREGA","Obsolete",null,null,true,default),"PROCESS_CONFLICT"); db.ChangeTracker.Clear();
            var actualLedger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
            Check(actualLedger.Tasks.Length==4 && actualLedger.Tasks.Sum(t=>t.LoadMinutes)==540,"Replacement and remaining reservations are counted exactly once");
            Check(actualLedger.DayCapacity(targets[1],day)==360 && actualLedger.DayCapacity(targets[1],day.AddDays(9))==240,"Capacity adjustment expires and restores the normal default");
            users[0]=actors[1]; users[3]=actors[4]; people[1]=targets[1]; people[4]=targets[4];
        }
        if(!preparePlan && !reassignment) Check(await processes.StartAsync(users[3],root,key,start,people,default)==run && await db.OperationalAssignments.CountAsync()==1,"Starting retry does not duplicate the execution"); db.ChangeTracker.Clear();
        async Task Decide(int user,string decision,string conclusion,string? differences=null,IFormFile? pdf=null,Guid? replayKey=null,Guid? oldAttempt=null,long? oldRevision=null)
        {
            var view=await processes.ReadAsync(users[user],run,default); var stage=view.Stages.Single(s=>s.Position==view.Run.ActivePosition); var attempt=view.Attempts.Single(a=>a.Id==stage.CurrentAttemptId);
            await processes.DecideAsync(users[user],run,oldAttempt??attempt.Id,oldRevision??attempt.Revision,replayKey??Guid.CreateVersion7(),decision,conclusion,differences,pdf,true,default); db.ChangeTracker.Clear();
        }
        IFormFile Pdf(string version)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "SistemaSGOL.slnx"))) root = root.Parent ?? throw new Exception("Source root not found.");
            var bytes=File.ReadAllBytes(Path.Combine(root.FullName, $"outputs/procesos/inventario-ejemplo-{version}.pdf"));
            return new FormFile(new MemoryStream(bytes),0,bytes.Length,"Document",$"inventario-ejemplo-{version}.pdf") { Headers=new HeaderDictionary(),ContentType="application/pdf" };
        }
        var first=await processes.ReadAsync(users[0],run,default); var floorAttempt=first.Attempts.Single(a=>a.Id==first.Stages[0].CurrentAttemptId);
        await Reject(()=>tasks.CompleteAsync(users[0],floorAttempt.AssignmentId!.Value,null,Pdf("v1"),default,"No saltar flujo"),"PROCESS_USE_ACTION"); db.ChangeTracker.Clear();
        await ProcessMyWorkHttpChecks.RunAsync(db, users[0], run, Pdf("v1")); db.ChangeTracker.Clear();
        if(deletion) await ProcessPlanDeletionChecks.ActiveAsync(db, direction, users, run);
        var afterFloor=await processes.ReadAsync(users[1],run,default); var obsolete=afterFloor.Attempts.Single(a=>a.Id==afterFloor.Stages[1].CurrentAttemptId);
        await Decide(1,"DEVOLUCION","Ejemplo: buscar dos pares faltantes y presentar un sobrante");
        var returned=await processes.ReadAsync(users[0],run,default);
        Check(returned.Run.Round==2 && returned.Run.ActivePosition==1 && returned.Documents.Length==1 && returned.Attempts.Count(a=>a.Status=="SUSTITUIDO")>=3,"Return keeps V1 and creates new round and attempts");
        await Reject(()=>processes.DecideAsync(users[1],run,obsolete.Id,obsolete.Revision,Guid.CreateVersion7(),"APROBACION","Tardía",null,null,true,default),"PROCESS_CONFLICT"); db.ChangeTracker.Clear();
        await Decide(0,"ENTREGA","Ejemplo: búsqueda y sobrantes documentados",pdf:Pdf("v2"));
        await Decide(1,"APROBACION","Ejemplo: contrastado manualmente contra Sizes & Colors","Ejemplo: dos pares faltantes y un sobrante sin aclarar");
        await Decide(2,"APROBACION","Ejemplo: hoja e irregularidades revisadas","Ejemplo: dos pares faltantes y un sobrante sin aclarar");
        var beforeFinal=await processes.ReadAsync(users[3],run,default); var finalAttempt=beforeFinal.Attempts.Single(a=>a.Id==beforeFinal.Stages[3].CurrentAttemptId);
        var decisionKey=Guid.CreateVersion7();
        await Decide(3,"ACEPTADA_CON_DIFERENCIAS","Ejemplo: acepto el resultado de la revisión de V2","Ejemplo: dos pares faltantes y un sobrante pendientes",replayKey:decisionKey);
        var closed=await processes.ReadAsync(users[3],run,default);
        Check(closed.Run.Status=="CONCLUIDA" && closed.Run.ClosureOutcome=="ACEPTADA_CON_DIFERENCIAS" && closed.Documents.Length==2 && closed.Run.AuthorizedDocumentId==closed.Documents[1].Id,"Final acceptance preserves V2, conclusion and pending differences");
        await processes.DecideAsync(users[3],run,finalAttempt.Id,finalAttempt.Revision,decisionKey,"ACEPTADA_CON_DIFERENCIAS","Ejemplo: acepto el resultado de la revisión de V2","Ejemplo: dos pares faltantes y un sobrante pendientes",null,true,default); db.ChangeTracker.Clear();
        Check(await db.Set<OperationalSequenceEvent>().CountAsync(e=>e.RunId==run && e.Kind=="CIERRE")==1,"Decision retry does not close twice");
        await Reject(()=>processes.DecideAsync(users[3],run,finalAttempt.Id,finalAttempt.Revision,Guid.CreateVersion7(),"DEVOLUCION","Tardía",null,null,true,default),"PROCESS_CONFLICT"); db.ChangeTracker.Clear();
        var file=await tasks.FileAsync(users[3],closed.Documents[0].AssignmentId,default);
        Check(file.FileContent?.Length>0,"Contextual access can consult V1 after final acceptance");
        var finalLedger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,default);
        Check(finalLedger.Tasks.Length==0,"Completed process leaves no duplicate reservations or pending assignments");
        await processes.TickAsync(default); db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.CountAsync()==(reassignment?7:6),"Timer restart does not duplicate effective assignments");
        if(deletion) await ProcessPlanDeletionChecks.CompletedAsync(db, direction, users, run);
    }
    private static void Check(bool value,string text) { if(!value) throw new Exception("FAILED: "+text); Console.WriteLine("PASS: "+text); }
    private static async Task Reject(Func<Task> action,string code) { try { await action(); throw new Exception("Expected: "+code); } catch(Sgol.Planning.Contracts.DirectionPlanException e) when(e.Code==code) { Console.WriteLine("PASS: Rejected "+code); } }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow=>new(2026,10,13,18,0,0,TimeSpan.Zero); }
    private sealed class Scanner : IFileMalwareScanner { public Task CheckAvailabilityAsync(CancellationToken token)=>Task.CompletedTask; public Task<EvidenceScanOutcome> ScanAsync(Stream stream,long size,CancellationToken token)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio)); }
}

