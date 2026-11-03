using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Auditing;

internal static class ProcessRecurrenceChecks
{
    internal static async Task Run(SgolDbContext db, bool uiSeed = false)
    {
        var f = await QaFixtures.CreateAsync(db);
        var clock = new Clock(); var validator = new FileTechnicalValidator(); var scanner = new Scanner();
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db), validator, scanner);
        var catalog = new OperationalProcessCatalogService(db, direction, clock, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"] = "true" }).Build());
        var tasks = new OperationalTaskService(db, direction, clock, validator, scanner);
        var id = Guid.CreateVersion7();
        await catalog.CreateAsync(f.Director, id, "Proceso recurrente de prueba", "", default); db.ChangeTracker.Clear();
        for (var i=0; i<2; i++) { await tasks.SaveAsync(f.Director, new OperationalTask { Revision=0, Name="Etapa recurrente " + i, ProcessId=id, Requirement="NINGUNO", LoadMinutes=10, PeriodType="OCASIONAL", OperationalCriticality="FLEXIBLE" }, default); db.ChangeTracker.Clear(); }
        var editor = await catalog.ReadAsync(f.Director,id,default);
        var input = new ProcessDraftInput { Id=id, Revision=editor.Process.Revision, Name=editor.Process.Name, StartKind="SEMANAL",
            Schedule=new() { Mode="DIAS_FIJOS", WeekDays=(1<<1)|(1<<3), Time=new(9,0), WeeklyOperations=true },
            Steps=editor.Tasks.Select((t,i)=>new OperationalSequenceStep {TaskId=t.Id,Position=i+1,ReleaseCondition=i==0 ? "INICIO" : "ENTREGA",Action=i==0 ? "ENTREGAR" : "AUTORIZAR",DeadlineHours=1}).ToList() };
        await catalog.SaveAsync(f.Director,input,default); db.ChangeTracker.Clear();
        editor=await catalog.ReadAsync(f.Director,id,default);
        Check(ProcessStartSchedule.Read(editor.Configuration!.StartScheduleJson).WeekDays==10,"Save/reopen preserves selected days");
        await catalog.PublishAsync(f.Director,id,editor.Process.Revision,default); db.ChangeTracker.Clear();
        editor=await catalog.ReadAsync(f.Director,id,default); await catalog.SetEnabledAsync(f.Director,id,editor.Process.Revision,true,default); db.ChangeTracker.Clear();
        var ordinary=await db.OperationalTasks.SingleAsync(t=>t.Id==f.Definition);ordinary.IsWeeklyOperationsTask=true;ordinary.StartTime=new(9,0);ordinary.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var options=await new DirectionPlanningCatalogReader(db).ReadTaskOptionsAsync();
        var codes=options.Where(o=>o.IsWeeklyOperationsTask).Select(o=>o.TaskCode).ToArray();
        Check(codes.Length==2 && options.Count(o=>o.IsProcess)==1,"Weekly catalog includes ordinary task and process once");
        var request=new DirectionPlanInput("Semana con proceso","Operaciones","SEMANAL",2026,45,codes,IsWeeklyOperationsPlan:true);
        request=await QaFixtures.ConfirmAsync(db,request,clock.UtcNow);
        var key=Guid.CreateVersion7(); var correlation=Guid.CreateVersion7();
        var saved=await direction.CreateAsync(f.Director,key,correlation,request);
        var planId=saved.Plan.PlanId;
        Check(saved.Plan.Processes.Count==2 && saved.Plan.Processes.All(p=>p.Stages.Count==2),"Creation response includes process reservations");
        Check((await direction.ReadAsync(f.Director,planId)).Processes.Count==2,"Saved weekly detail includes process executions");
        try { await QaFixtures.ConfirmAsync(db,request,clock.UtcNow); throw new Exception("Expected duplicate occurrence rejection"); }
        catch(DirectionPlanException e) when(e.Code=="PLAN_OCCURRENCES_RESERVED") { Check(e.Data["ProcessName"] is string && e.Data["ProcessDay"] is DateOnly,"Duplicate process date identifies its process and date"); }
        Check(await db.Set<OperationalSequenceRun>().CountAsync(r=>r.PlanId==planId)==2,"One independent execution for each weekly repetition");
        var runs=await db.Set<OperationalSequenceRun>().Where(r=>r.PlanId==planId).Select(r=>r.Id).ToArrayAsync();
        var stages=await db.Set<OperationalSequenceStage>().Where(s=>runs.Contains(s.RunId)).ToArrayAsync();
        Check(stages.Length==4 && stages.Where(s=>s.Position==2).All(s=>s.Status=="EN_ESPERA"),"Later stages reserve capacity and wait for delivery");
        var before=JsonSerializer.Serialize(stages);
        await direction.CreateAsync(f.Director,key,correlation,request);
        Check(await db.Set<OperationalSequenceRun>().CountAsync(r=>r.PlanId==planId)==2,"Creation replay does not duplicate process executions");
        var next=new DirectionPlanInput("Semana siguiente","Operaciones","SEMANAL",2026,46,codes,IsWeeklyOperationsPlan:true);
        next=await QaFixtures.ConfirmAsync(db,next,clock.UtcNow);
        try { await direction.CreateAsync(f.Worker,Guid.CreateVersion7(),Guid.CreateVersion7(),next); throw new Exception("Expected authorization rejection"); }
        catch(DirectionPlanException e) when(e.Status==403) { Check(true,"Worker cannot create weekly process plan"); }
        var coordinator=Guid.CreateVersion7(); var coordinatorPerson=Guid.CreateVersion7();
        db.People.Add(new(){Id=coordinatorPerson,StableCode="QA-COORD",DisplayName="Coordinación de prueba",CreatedAt=clock.UtcNow.AddYears(-1)});
        db.AppUsers.Add(new(){Id=coordinator,PersonId=coordinatorPerson,Status=Sgol.Identity.Contracts.AccountStatus.Active,SecurityStamp="isolated"});
        db.EmploymentVersions.Add(new(Guid.CreateVersion7(),coordinatorPerson,Sgol.Organization.Contracts.BranchScope.LorettaId,Sgol.Organization.Contracts.EmploymentStatus.Active,clock.UtcNow.AddYears(-1)));
        db.RoleAssignmentVersions.Add(new(){Id=Guid.CreateVersion7(),UserId=coordinator,BranchId=Sgol.Organization.Contracts.BranchScope.LorettaId,RoleCode=Sgol.Identity.Contracts.CanonicalRole.Subcoordination,Status=Sgol.Identity.Contracts.RoleAssignmentStatus.Active,ValidFrom=clock.UtcNow.AddYears(-1)});
        var currentRoot=await db.Set<OperationalSequence>().SingleAsync(p=>p.Id==id);currentRoot.Revision++;
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var countBefore=await db.DirectionPreparedPlans.CountAsync();
        try { await direction.CreateAsync(f.Director,Guid.CreateVersion7(),Guid.CreateVersion7(),next); throw new Exception("Expected stale preview rejection"); }
        catch(DirectionPlanException e) when(e.Code=="PLAN_CAPACITY_STALE") { Check(await db.DirectionPreparedPlans.CountAsync()==countBefore,"Stale preview rejects without a partial plan"); }
        next=await QaFixtures.ConfirmAsync(db,next,clock.UtcNow);
        await direction.CreateAsync(coordinator,Guid.CreateVersion7(),Guid.CreateVersion7(),next);
        Check(true,"Subcoordination can activate processes through the weekly plan");
        Check(await db.Set<OperationalSequenceRun>().CountAsync()==4,"Next week starts while previous runs are still pending");
        Check(before==JsonSerializer.Serialize(await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s=>runs.Contains(s.RunId)).ToArrayAsync()),"Existing stages remain unchanged");
        foreach(var kind in new[]{"MENSUAL","TRIMESTRAL","ANUAL"}) {
            var schedule=new ProcessStartSchedule {Repetitions=2};
            var version=new OperationalSequenceVersion {StartKind=kind,StartScheduleJson=schedule.Capture(kind)};
            Check(OperationalSequenceRules.Dates(new(2027,1,1),new(2027,12,31),version).Count()==(kind=="MENSUAL"?24:kind=="TRIMESTRAL"?8:2),kind+" recurrence uses its full calendar period");
        }
        var month=new ProcessStartSchedule {MonthDays="31"};
        Check(!OperationalSequenceRules.Dates(new(2027,2,1),new(2027,2,28),new(){StartKind="MENSUAL",StartScheduleJson=month.Capture("MENSUAL")}).Any(),"Nonexistent monthly date is not shifted into another month");
        try { new ProcessStartSchedule {WeeklyOperations=true}.Capture("MENSUAL"); throw new Exception("Expected rejection"); } catch(DirectionPlanException) { Check(true,"Nonweekly operations flag rejected by server"); }
        try { new ProcessStartSchedule {Mode="PROGRESO"}.Capture("SEMANAL"); throw new Exception("Expected rejection"); } catch(DirectionPlanException) { Check(true,"Progress scheduling remains excluded"); }
        if (uiSeed) { Console.WriteLine("PROCESS RECURRENCE UI FIXTURE READY"); return; }
        var engine=new OperationalProcessService(db,direction,clock,validator,scanner);
        var firstRun=await db.Set<OperationalSequenceRun>().Where(r=>r.PlanId==planId).OrderBy(r=>r.PlannedStartsAt).FirstAsync();
        clock.UtcNow=await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==firstRun.Id && s.Position==1).Select(s=>s.PlannedStartsAt).SingleAsync();await engine.TickAsync(default);db.ChangeTracker.Clear();
        var firstStage=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.RunId==firstRun.Id && s.Position==1);
        var owner=await db.AppUsers.Where(u=>u.PersonId==firstStage.ReservedPersonId).Select(u=>u.Id).SingleAsync();
        var attempt=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==firstStage.CurrentAttemptId);
        await engine.DecideAsync(owner,firstRun.Id,attempt.Id,attempt.Revision,Guid.CreateVersion7(),"ENTREGA","Entrega de prueba",null,null,true,default);
        db.ChangeTracker.Clear();
        Check((await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.RunId==firstRun.Id && s.Position==2)).DependencySatisfiedAt!=null,"Delivering the first weekly stage releases the next dependency");
        editor=await catalog.ReadAsync(f.Director,id,default);
        input.Revision=editor.Process.Revision;input.StartKind="MENSUAL";input.Schedule=new(){MonthDays="8,22",Time=new(10,35)};
        await catalog.SaveAsync(f.Director,input,default);db.ChangeTracker.Clear();editor=await catalog.ReadAsync(f.Director,id,default);
        await catalog.PublishAsync(f.Director,id,editor.Process.Revision,default);db.ChangeTracker.Clear();editor=await catalog.ReadAsync(f.Director,id,default);
        await catalog.SetEnabledAsync(f.Director,id,editor.Process.Revision,true,default);db.ChangeTracker.Clear();
        var planning=new ProcessPlanningService(db,direction,clock,validator,scanner);
        var monthly=new ProcessPlanInput {Name="Proceso mensual",ProcessId=id,Mode="CALENDARIO",From=new(2026,12,1),Through=new(2026,12,31),People=new(){[1]=f.Person,[2]=f.OtherPerson}};
        var monthlyPreview=await planning.PreviewAsync(f.Director,monthly,default);monthly.Fingerprint=monthlyPreview.Fingerprint;
        Check(monthlyPreview.Starts.Length==2 && monthlyPreview.Starts.All(t=>TimeZoneInfo.ConvertTime(t,OperationalSchedule.Zone).Minute==35),"Monthly plan preserves explicit dates and minute precision");
        var monthlyId=await planning.PrepareAsync(f.Director,monthly,default);db.ChangeTracker.Clear();
        Check((await planning.ActivateAsync(f.Director,monthlyId,default)).Length==2,"Monthly calendar prepares and activates independent executions");
        Console.WriteLine("PROCESS RECURRENCE CHECKS PASSED");
    }
    private static void Check(bool ok,string text) { if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text); }
    private sealed class Clock:IClock {public DateTimeOffset UtcNow {get;set;}=new(2026,11,2,14,0,0,TimeSpan.Zero);}
    private sealed class Scanner:IFileMalwareScanner {public Task CheckAvailabilityAsync(CancellationToken ct)=>Task.CompletedTask;public Task<EvidenceScanOutcome> ScanAsync(Stream stream,long size,CancellationToken ct)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));}
}
