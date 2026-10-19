using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class ScheduleIntegrationChecks
{
    public static async Task RunAsync(SgolDbContext db, OperationalTaskService tasks, EfDirectionPreparedPlanService direction,
        OperationalSupervisionService supervision, Guid actor, CheckClock clock, Guid a, Guid b)
    {
        int count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine($"PASS {++count}: {name}"); }
        async Task Reject(Func<Task> action, string code) {
            try { await action(); throw new Exception("Expected " + code); } catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine($"PASS {++count}: rejected {code}"); }
            db.ChangeTracker.Clear();
        }
        var day = WeekContract.LocalToday(clock.UtcNow);
        foreach (var person in new[] { a, b }) db.Add(new PersonalScheduleVersion { Id = Guid.CreateVersion7(), PersonId = person, Revision = 1, From = day.AddDays(30),
            ChangedAt = clock.UtcNow, ChangedBy = actor, IntentKey = Guid.CreateVersion7(), RequestHash = "fixture",
            DaysJson = JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d => d, d => person == a ? new[] { new WorkInterval(540,780) } : [new WorkInterval(900,1140)])) });
        db.Add(new TemporalAssignmentState { Enabled = true }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await tasks.SaveAsync(actor, new() { Revision = 0, Name = "Horario fijo",  Requirement = "NINGUNO", StartHour = 9, StartMinute = 50, DeadlineHours = 8, LoadMinutes = 10, Repetitions = 1, ScheduleMode = "DIAS_FIJOS", WeekDays = 2, IsWeeklyOperationsTask=true }, default); db.ChangeTracker.Clear();
        var task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Horario fijo");
        var input = new DirectionPlanInput("Horarios", "Tienda", "SEMANAL", 2026, 42, [task.TaskCode]);
        var preview = await PlanningCapacity.ReadAsync(db, input, clock.UtcNow, default);
        Check(preview.Preview.TemporalRows.Any(r => r.Person == "A" && r.Eligible) && preview.Preview.TemporalRows.Any(r => r.Person == "B" && !r.Eligible), "Preview shows actual schedules and conflicts");
        var key = Guid.CreateVersion7(); var request = input with { CapacityFingerprint = preview.Preview.Fingerprint };
        var created = await direction.CreateAsync(actor, key, Guid.CreateVersion7(), request); db.ChangeTracker.Clear();
        var assignment = await db.OperationalAssignments.SingleAsync(x => x.PlanId == created.Plan.PlanId);
        Check(assignment.PersonId == a && TimeZoneInfo.ConvertTime(assignment.StartsAt, OperationalSchedule.Zone).Minute == 50 && assignment.TemporalDecisionJson != null, "Normal plan respects future presence, minutes and historical decision");
        await direction.CreateAsync(actor, key, Guid.CreateVersion7(), request); db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.CountAsync(x => x.PlanId == created.Plan.PlanId) == 1, "Idempotent plan does not duplicate occurrence");
        var weeklyInput=input with { PeriodNumber=43,IsWeeklyOperationsPlan=true };
        var weeklyPreview=await PlanningCapacity.ReadAsync(db,weeklyInput,clock.UtcNow,default);
        var weekly=await direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),weeklyInput with { CapacityFingerprint=weeklyPreview.Preview.Fingerprint }); db.ChangeTracker.Clear();
        Check(weekly.Plan.IsWeeklyOperationsPlan && weekly.Plan.Assignments!.All(x=>x.PersonId==a), "Weekly operations master uses the same temporal policy");
        Check(weekly.Plan.Assignments!.Count>0,"Weekly operations plan succeeds before original schedule effective date");
        var scheduleService = new PersonalScheduleService(db, direction, clock);
        var userA = await db.AppUsers.Where(u => u.PersonId == a).Select(u => u.Id).SingleAsync();
        var scheduleInput = await scheduleService.ReadAsync(actor, userA, default);
        var schedulePreview = await scheduleService.PreviewAsync(actor, scheduleInput, default);
        scheduleInput.Fingerprint = schedulePreview.Fingerprint;
        var beforeVersions = await db.Set<PersonalScheduleVersion>().CountAsync();
        await scheduleService.SaveAsync(actor, scheduleInput, default); db.ChangeTracker.Clear();
        await scheduleService.SaveAsync(actor, scheduleInput, default); db.ChangeTracker.Clear();
        Check(await db.Set<PersonalScheduleVersion>().CountAsync() == beforeVersions + 1, "Schedule double submit creates exactly one immutable revision");
        var globalContext=await TemporalEligibilityContext.LoadAsync(db,clock.UtcNow,default);
        Check(globalContext.Day(a,day.AddYears(-1),false).Schedule?.Revision==2 && globalContext.Day(a,day.AddYears(1),false).Schedule?.Revision==2,"Saved global revision applies both before and after configuration");
        var preserved=await db.OperationalAssignments.AsNoTracking().SingleAsync(x=>x.Id==assignment.Id);
        Check(preserved.TemporalDecisionJson==assignment.TemporalDecisionJson && preserved.Revision==assignment.Revision,"Schedule save preserves existing assignment decision and revision");
        await Reject(() => direction.CreateAsync(actor, Guid.CreateVersion7(), Guid.CreateVersion7(), input with { PeriodNumber = 43, CapacityFingerprint = preview.Preview.Fingerprint }), "PLAN_CAPACITY_STALE");
        var exception = await scheduleService.ReadExceptionAsync(actor, userA, day.AddDays(7), default);
        exception.Reason = "Ausencia de prueba aislada";
        exception.Fingerprint=(await scheduleService.PreviewExceptionAsync(actor,exception,default)).Fingerprint;
        await scheduleService.SaveExceptionAsync(actor, exception, default); db.ChangeTracker.Clear();
        await scheduleService.SaveExceptionAsync(actor, exception, default); db.ChangeTracker.Clear();
        Check(await db.Set<PersonalScheduleException>().CountAsync() == 1, "Exception replay is idempotent");
        var closingDay=day.AddDays(2);
        var closingRows=await scheduleService.ReadClosingAsync(actor,closingDay,default);
        Check(closingRows[0].PersonId==null && closingRows[0].Candidates.Any(c=>c.PersonId==b),"Shared planning starts without Wednesday coverage and offers the 19:00 shift");
        var sharedPreview=await PlanningCapacity.ReadAsync(db,input with { PeriodNumber=44 },clock.UtcNow,default);
        var coverage=new ClosingSelectionInput { Day=closingDay,PersonId=b,Revision=closingRows[0].Revision };
        await scheduleService.SaveClosingAsync(actor,coverage,default); db.ChangeTracker.Clear();
        await scheduleService.SaveClosingAsync(actor,coverage,default); db.ChangeTracker.Clear();
        Check(await db.Set<ClosingCoverageVersion>().CountAsync()==1,"Closing selection replay creates one shared version");
        var effective=await TemporalEligibilityContext.LoadAsync(db,clock.UtcNow,default);
        Check(effective.Day(b,closingDay).Intervals!.Max(i=>i.Exit)==1200 && effective.Day(b,closingDay.AddDays(7)).Intervals!.Max(i=>i.Exit)==1140,"Coverage extends one hour on its date and preserves the next week");
        await Reject(()=>scheduleService.SaveClosingAsync(actor,new() { Day=closingDay,PersonId=b,Revision=0 },default),"SCHEDULE_STALE");
        await Reject(()=>direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),input with { PeriodNumber=44,CapacityFingerprint=sharedPreview.Preview.Fingerprint }),"PLAN_CAPACITY_STALE");
        await tasks.SaveAsync(actor,new() { Revision=0,Name="Solo domingo",ScheduleMode="DIAS_FIJOS",WeekDays=1,StartHour=16,SundayStartMinute=600,DeadlineHours=1,LoadMinutes=15 },default); db.ChangeTracker.Clear();
        var sundayTask=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name=="Solo domingo");
        var sundayInput=input with { Name="Domingo sin lista especial",TaskCodes=[sundayTask.TaskCode] };
        var sundayPreview=await PlanningCapacity.ReadAsync(db,sundayInput,clock.UtcNow,default);
        var sundayPlan=await direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),sundayInput with { CapacityFingerprint=sundayPreview.Preview.Fingerprint }); db.ChangeTracker.Clear();
        Check(sundayPlan.Plan.Assignments!.Single().PersonId==a && TimeZoneInfo.ConvertTime(sundayPlan.Plan.Assignments!.Single().StartsAt,OperationalSchedule.Zone).Hour==10,"Sunday override uses the normal plan path with no whitelist or duplicate task");
        clock.UtcNow = TemporalEligibilityContext.At(day, 840);
        await tasks.SaveAsync(actor, new() { Revision=0, Name="Ocasional fija", PeriodType="OCASIONAL",  Requirement="NINGUNO", LoadMinutes=10 }, default); db.ChangeTracker.Clear();
        var occasional = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Ocasional fija");
        var occasion = new OccasionInput(occasional.Id, occasional.Revision, "Fuera de turno", "Prueba aislada", "", "", clock.UtcNow, clock.UtcNow.AddHours(4), [a]);
        await Reject(() => tasks.AssignOccasionAsync(actor, Guid.CreateVersion7(), occasion, [], default), "PLAN_NO_AVAILABLE_WORKER");
        occasional=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Id==occasional.Id);
        await Reject(() => tasks.AssignOccasionAsync(actor, Guid.CreateVersion7(), occasion with { TaskRevision=occasional.Revision,PersonIds=[b] }, [], default), "PLAN_NO_AVAILABLE_WORKER");
        await Reject(() => tasks.AssignOccasionAsync(actor, Guid.CreateVersion7(), occasion with { TaskRevision=occasional.Revision,PersonIds=[],Automatic=true }, [], default), "PLAN_NO_AVAILABLE_WORKER");
        var inShift = occasion with { TaskRevision=occasional.Revision,StartsAt=clock.UtcNow.AddHours(1),PersonIds=[b] };
        var inShiftPlan=await tasks.AssignOccasionAsync(actor, Guid.CreateVersion7(), inShift, [], default); db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.AnyAsync(x=>x.PlanId==inShiftPlan && x.PersonId==b && x.TemporalDecisionJson!=null), "Manual occasion assigns only at an activation inside the shift");
        var automaticPlan=await tasks.AssignOccasionAsync(actor,Guid.CreateVersion7(),inShift with { PersonIds=[],Automatic=true },[],default); db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.AnyAsync(x=>x.PlanId==automaticPlan && x.PersonId==b),"Automatic occasion selects a worker present at activation");
        var lateInput=input with { PeriodNumber=41,Name="Activación tardía del plan" };
        var latePreview=await PlanningCapacity.ReadAsync(db,lateInput,clock.UtcNow,default);
        await Reject(()=>direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),lateInput with { CapacityFingerprint=latePreview.Preview.Fingerprint }),"PLAN_NO_AVAILABLE_WORKER");
        clock.UtcNow=TemporalEligibilityContext.At(day.AddDays(3),1140);
        await tasks.SaveAsync(actor,new() { Revision=0,Name="Operación iniciada el jueves",Requirement="NINGUNO",StartHour=9,DeadlineHours=8,LoadMinutes=10,Repetitions=1,ScheduleMode="DIAS_FIJOS",WeekDays=18,IsWeeklyOperationsTask=true },default); db.ChangeTracker.Clear();
        var currentTask=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name=="Operación iniciada el jueves");
        var currentWeek=lateInput with { Name="Semana en curso",TaskCodes=[task.TaskCode,currentTask.TaskCode],IsWeeklyOperationsPlan=true };
        var currentPreview=await PlanningCapacity.ReadAsync(db,currentWeek,clock.UtcNow,default);
        Check(currentPreview.Preview.TemporalRows.All(r=>r.Day>=day.AddDays(3)) && currentPreview.Preview.From==day.AddDays(3),"Midweek operations preview excludes earlier dates");
        Check(currentPreview.Preview.TemporalRows.Any(r=>r.Day==day.AddDays(3) && r.Person=="A" && r.Eligible),"09:00 worker remains eligible for initial-day task even after its deadline");
        var daily=await PlanningCapacity.DailyLoadAsync(db,currentWeek,currentPreview.Preview,currentPreview.Ledger,default);
        Check(daily.All(d=>d.Day>=day.AddDays(3)) && daily.Single(d=>d.Day==day.AddDays(3)).PlanMinutes==10,"Weekly load includes expired initial-day work and excludes earlier dates");
        var partial=await direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),currentWeek with { CapacityFingerprint=currentPreview.Preview.Fingerprint }); db.ChangeTracker.Clear();
        Check(partial.Plan.StartsOn==day.AddDays(3) && partial.Plan.Assignments!.Count==1 && partial.Plan.Assignments.Single().DueAt<clock.UtcNow,"Midweek master persists its trimmed range and expired initial-day assignment");
        var deletion=await direction.PreviewDeletionAsync(actor,partial.Plan.PlanId);
        await direction.DeleteAsync(actor,partial.Plan.PlanId,Guid.CreateVersion7(),Guid.CreateVersion7(),deletion.Fingerprint); db.ChangeTracker.Clear();
        var recreatePreview=await PlanningCapacity.ReadAsync(db,currentWeek,clock.UtcNow,default);
        var recreated=await direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),currentWeek with { CapacityFingerprint=recreatePreview.Preview.Fingerprint }); db.ChangeTracker.Clear();
        Check(recreated.Plan.PlanId!=partial.Plan.PlanId && recreated.Plan.Assignments!.Count==1,"Deleted operations master can be recreated on the same day with expired hours");
        clock.UtcNow=TemporalEligibilityContext.At(day.AddDays(21),1140);
        var mondayWeek=currentWeek with { PeriodNumber=44 };
        var mondayPreview=await PlanningCapacity.ReadAsync(db,mondayWeek,clock.UtcNow,default);
        var mondayPlan=await direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),mondayWeek with { CapacityFingerprint=mondayPreview.Preview.Fingerprint }); db.ChangeTracker.Clear();
        Check(mondayPlan.Plan.Assignments!.Count==3 && mondayPlan.Plan.Assignments.Count(a=>WeekContract.LocalToday(a.StartsAt)==day.AddDays(21) && a.DueAt<clock.UtcNow)==2,"Monday evening creation retains Monday tasks with their original expired deadlines");
        clock.UtcNow=TemporalEligibilityContext.At(day.AddDays(26),1140);
        var weekendWeek=currentWeek with { PeriodNumber=45 };
        var weekendPreview=await PlanningCapacity.ReadAsync(db,weekendWeek,clock.UtcNow,default);
        var weekendPlan=await direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),weekendWeek with { CapacityFingerprint=weekendPreview.Preview.Fingerprint }); db.ChangeTracker.Clear();
        Check(weekendPlan.Plan.StartsOn==day.AddDays(28) && weekendPlan.Plan.EndsOn==day.AddDays(34) && weekendPlan.Plan.Assignments!.All(a=>WeekContract.LocalToday(a.StartsAt)>=day.AddDays(28)),"Saturday creation starts next Monday and ends the following Sunday");
        await Reject(()=>direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),currentWeek with { PeriodNumber=42,CapacityFingerprint=null }),"PLAN_ENDED");
        clock.UtcNow = TemporalEligibilityContext.At(WeekContract.LocalToday(assignment.StartsAt),840);
        await Reject(()=>supervision.ReviewAsync(actor,assignment.Id,assignment.Revision,"MISMA_PERSONA","Recepción tardía",default), "PLAN_NO_AVAILABLE_WORKER");
        clock.UtcNow = TemporalEligibilityContext.At(day,840);
        var capacity=new OperationalCapacityService(db,clock,supervision);
        var reduction=await capacity.PreviewAsync(actor,new(a,day.AddDays(7),day.AddDays(7),0,240,"Prueba aislada"),default);
        Check(reduction.Moves.Count==0, "Capacity redistribution cannot move morning work to afternoon-only worker despite a long deadline");
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"]="true" }).Build();
        var catalog=new OperationalProcessCatalogService(db,direction,clock,config);
        var process=Guid.CreateVersion7(); await catalog.CreateAsync(actor,process,"Proceso con horario","Prueba aislada",default); db.ChangeTracker.Clear();
        var processTasks=new List<Guid>();
        for(var i=0;i<2;i++) {
            await tasks.SaveAsync(actor,new() { Revision=0,Name="Etapa horario "+i,ProcessId=process,Requirement="DOCUMENTO",PeriodType="OCASIONAL", LoadMinutes=10 },default); db.ChangeTracker.Clear();
            processTasks.Add(await db.OperationalTasks.Where(t=>t.Name=="Etapa horario "+i).Select(t=>t.Id).SingleAsync());
        }
        var editor=await catalog.ReadAsync(actor,process,default);
        await catalog.SaveAsync(actor,new() { Id=process,Revision=editor.Process.Revision,Name=editor.Process.Name,Description=editor.Process.Description,StartKind="OCASIONAL",StartHour=15,
            Steps=[new() { TaskId=processTasks[0],Position=1,Action="ENTREGAR",ReleaseCondition="INICIO",DeadlineHours=2 },new() { TaskId=processTasks[1],Position=2,Action="AUTORIZAR",ReleaseCondition="ENTREGA",DeadlineHours=2 }] },default); db.ChangeTracker.Clear();
        editor=await catalog.ReadAsync(actor,process,default); await catalog.PublishAsync(actor,process,editor.Process.Revision,default); db.ChangeTracker.Clear();
        editor=await catalog.ReadAsync(actor,process,default); await catalog.SetEnabledAsync(actor,process,editor.Process.Revision,true,default); db.ChangeTracker.Clear();
        var planning=new ProcessPlanningService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var processInput=new ProcessPlanInput { ProcessId=process,Name="Reserva en turno",Mode="OCASION",From=day,Through=day,People=new() {[1]=b,[2]=b} };
        var processPreview=await planning.PreviewAsync(actor,processInput,default); processInput.Fingerprint=processPreview.Fingerprint;
        var plan=await planning.PrepareAsync(actor,processInput,default); db.ChangeTracker.Clear();
        clock.UtcNow=TemporalEligibilityContext.At(day,900);
        var runs=await planning.ActivateAsync(actor,plan,default); db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.AnyAsync(x=>x.PlanId==plan && x.PersonId==b && x.TemporalDecisionJson!=null), "Process activation respects personal schedules and preserves reserved person");
        var processService=new OperationalProcessService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var run=await processService.ReadAsync(actor,runs.Single(),default);
        await Reject(()=>processService.ReassignAsync(actor,run.Run.Id,run.Run.Revision,Guid.CreateVersion7(),new Dictionary<int,Guid> { [1]=a },"Fuera de turno",null,default),"PLAN_NO_AVAILABLE_WORKER");
        var another=new ProcessPlanInput { ProcessId=process,Name="Cambio después de reservar",Mode="OCASION",From=day,Through=day,People=new() {[1]=b,[2]=b} };
        another.Fingerprint=(await planning.PreviewAsync(actor,another,default)).Fingerprint;
        var pendingPlan=await planning.PrepareAsync(actor,another,default); db.ChangeTracker.Clear();
        var userB=await db.AppUsers.Where(u=>u.PersonId==b).Select(u=>u.Id).SingleAsync();
        var absence=await scheduleService.ReadExceptionAsync(actor,userB,day,default); absence.Reason="Ausencia posterior a reserva";
        absence.Fingerprint=(await scheduleService.PreviewExceptionAsync(actor,absence,default)).Fingerprint;
        await scheduleService.SaveExceptionAsync(actor,absence,default); db.ChangeTracker.Clear();
        var pendingRun=(await planning.ActivateAsync(actor,pendingPlan,default)).Single(); db.ChangeTracker.Clear();
        var pending=await processService.ReadAsync(actor,pendingRun,default);
        Check(pending.Stages.First().Status=="PENDIENTE_ASIGNACION" && pending.Stages.First().ReservedPersonId==b && !await db.OperationalAssignments.AnyAsync(x=>x.PlanId==pendingPlan),"Absence after reservation keeps process pending and does not replace its person");
        Check(pending.Stages.First().BlockReason?.Contains("reserva")==true,"Pending process explains reason and specialized retry");
        Console.WriteLine($"{count} schedule integration checks passed.");
    }
}
