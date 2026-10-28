using System.Text.Json;
using System.Diagnostics.Metrics;
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

public static class ProcessPlanAutoChecks
{
    public static async Task RunAsync(SgolDbContext db)
    {
        if (await db.People.AnyAsync()) throw new Exception("Requires an empty dedicated database");
        var clock = new Clock(); var day = WeekContract.LocalToday(clock.UtcNow);
        var actor = Guid.CreateVersion7(); var owner = Guid.CreateVersion7();
        var a = Guid.CreateVersion7(); var b = Guid.CreateVersion7(); var absent = Guid.CreateVersion7(); var wrongRole = Guid.CreateVersion7();
        foreach (var person in new[] { owner, a, b, absent, wrongRole })
        {
            var user = person == owner ? actor : Guid.CreateVersion7();
            var role = person == wrongRole ? CanonicalRole.SalesFloor : CanonicalRole.Direction;
            db.People.Add(new() { Id=person, DisplayName=person==a?"Menor carga":person==b?"Mayor carga":person==owner?"Dirección":person==absent?"Sin turno":"Otro rol", StableCode="AUTO-"+Guid.NewGuid().ToString("N")[..8], CreatedAt=clock.UtcNow.AddYears(-1) });
            db.AppUsers.Add(new() { Id=user, PersonId=person, Status=AccountStatus.Active, SecurityStamp="isolated" });
            db.EmploymentVersions.Add(new(Guid.CreateVersion7(),person,BranchScope.LorettaId,EmploymentStatus.Active,clock.UtcNow.AddYears(-1)));
            db.RoleAssignmentVersions.Add(new() { Id=Guid.CreateVersion7(),UserId=user,BranchId=BranchScope.LorettaId,RoleCode=role,Status=RoleAssignmentStatus.Active,ValidFrom=clock.UtcNow.AddYears(-1) });
        }
        await db.SaveChangesAsync();
        foreach (var person in new[] {owner,a,b,absent,wrongRole})
        {
            db.OperationalCapacities.Add(new() { Id=Guid.CreateVersion7(),PersonId=person,From=day,Through=day.AddDays(366),Percent=person==owner?0:100,DailyMinutes=person==a||person==b?60:480,Reason="Isolated auto selection" });
            db.Add(new PersonalScheduleVersion { Id=Guid.CreateVersion7(),PersonId=person,Revision=1,From=day,ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=Guid.CreateVersion7(),RequestHash=new string('0',64),DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>person==absent?Array.Empty<WorkInterval>():new[]{new WorkInterval(540,1080)})) });
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var scanner = new Scanner(); var validator = new FileTechnicalValidator();
        var tasks = new OperationalTaskService(db,direction,clock,validator,scanner);
        var catalog = new OperationalProcessCatalogService(db,direction,clock,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"]="true" }).Build());
        var plans = new ProcessPlanningService(db,direction,clock,validator,scanner);
        var id=Guid.CreateVersion7(); await catalog.CreateAsync(actor,id,"Flujo de prueba","",default); db.ChangeTracker.Clear();
        for (var i=1;i<=2;i++)
        {
            await tasks.SaveAsync(actor,new() { Name="Tarea "+i,Revision=0,AssignedRole=CanonicalRole.Direction,Requirement="NINGUNO",OperationalCriticality="FLEXIBLE",LoadMinutes=40 },default); db.ChangeTracker.Clear();
            var task=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Name=="Tarea "+i);
            var editor=await catalog.ReadAsync(actor,id,default);
            await catalog.AddTaskAsync(actor,id,editor.Process.Revision,task.Id,task.Revision,default); db.ChangeTracker.Clear();
        }
        var configuration=await catalog.ReadAsync(actor,id,default);
        await catalog.SaveAsync(actor,new() { Id=id,Revision=configuration.Process.Revision,Name=configuration.Process.Name,StartKind="SEMESTRAL",FirstMonth=4,WindowFromDay=1,WindowThroughDay=28,StartHour=9,Steps=configuration.Steps.ToList() },default);db.ChangeTracker.Clear();configuration=await catalog.ReadAsync(actor,id,default);
        await catalog.PublishAsync(actor,id,configuration.Process.Revision,default);db.ChangeTracker.Clear();configuration=await catalog.ReadAsync(actor,id,default);
        await catalog.SetEnabledAsync(actor,id,configuration.Process.Revision,true,default);db.ChangeTracker.Clear();
        var policy=await OperationalSchedulingPolicyService.CurrentAsync(db,default);
        var seedPlan=DirectionPreparedPlan.ForProcess(Guid.CreateVersion7(),BranchScope.LorettaId,actor,clock.UtcNow,"Existing work",day,day); db.Add(seedPlan);
        var definition=await db.OperationalTasks.FirstAsync(t=>t.ProcessId==OperationalSequence.GeneralId);
        foreach (var person in new[] {a,b})
        {
            var assignment=new OperationalAssignment { Id=Guid.CreateVersion7(),PlanId=seedPlan.Id,TaskId=definition.Id,TaskCode="AUTO-EXISTING",Name="Carga previa",PersonId=person,AssignedRole=CanonicalRole.Direction,Requirement="NINGUNO",LoadMinutes=person==a?10:20,StartsAt=OperationalSchedule.Instant(day,9),DueAt=OperationalSchedule.Instant(day,10) };
            OperationalAgenda.Capture(assignment,policy,"FLEXIBLE",assignment.StartsAt);
            assignment.DueAt=assignment.StartsAt.AddMinutes(OperationalAgenda.Duration(assignment.LoadMinutes,policy.MarginPercent));
            db.Add(assignment);
        }
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        ProcessPlanInput Input() => new() { ProcessId=id,Name="Selección por etapa",Mode="OCASION",From=day,Through=day,People=new() { [1]=Guid.Empty,[2]=Guid.Empty } };
        var input=Input();var preview=await plans.PreviewAsync(actor,input,default);
        Check(input.People[1]==a && input.People[2]==b,"Automatic stages prefer lower projected occupancy and distribute individual 40-minute loads across 60-minute capacities");
        Check(preview.Stages.All(s=>s.PersonId!=absent&&s.PersonId!=wrongRole&&s.PersonId!=owner),"Automatic choice excludes rest days, incompatible roles and zero capacity");
        Check(preview.Agenda.Values.Single().All(t=>!string.IsNullOrWhiteSpace(t.PersonName)) && preview.Agenda.Values.Single().Sum(t=>t.LoadMinutes)==80,"Full process remains 80 minutes while each person reserves only their own 40-minute stage");
        var repeated=await plans.PreviewAsync(actor,input,default);
        Check(preview.Fingerprint==repeated.Fingerprint,"Resolved preview remains stable for saving without rerandomizing");
        var pinned=Input();pinned.People[1]=b;await plans.PreviewAsync(actor,pinned,default);
        Check(pinned.People[1]==b&&pinned.People[2]==a,"Manual recipient is preserved while the other stage resolves automatically");
        var invalid=Input();invalid.People[1]=wrongRole;await Reject(()=>plans.PreviewAsync(actor,invalid,default),"OCCASION_RECIPIENT_INVALID");
        var capacity=await db.OperationalCapacities.SingleAsync(c=>c.PersonId==a);capacity.DailyMinutes=70;capacity.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        input.Fingerprint=preview.Fingerprint;await Reject(()=>plans.PrepareAsync(actor,input,default),"PLAN_CAPACITY_STALE");db.ChangeTracker.Clear();
        preview=await plans.PreviewAsync(actor,input,default);input.Fingerprint=preview.Fingerprint;
        var prepared=await plans.PrepareAsync(actor,input,default);db.ChangeTracker.Clear();
        var start=await db.Set<PreparedProcessStart>().SingleAsync(s=>s.PlanId==prepared);
        var saved=JsonSerializer.Deserialize<Dictionary<int,Guid>>(start.Participants)!;
        Check(saved.OrderBy(p=>p.Key).SequenceEqual(input.People.OrderBy(p=>p.Key)),"Preparing persists exactly the recipients reviewed by the user");
        Check(await plans.PrepareAsync(actor,input,default)==prepared,"Preparing the same reviewed plan remains idempotent");db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.CountAsync()==2&&!await db.Set<OperationalSequenceRun>().AnyAsync(),"Preview and preparation create no real work or runs");
        var blocked=Input();await Reject(()=>plans.PreviewAsync(actor,blocked,default),"PLAN_NO_AVAILABLE_WORKER");
        Check(blocked.People.Values.All(p=>p==Guid.Empty),"Failed automatic preview does not partially replace the user's selections");
        var calendar=Input(); calendar.Mode="CALENDARIO";calendar.Through=day.AddDays(366);
        var calendarPreview=await plans.PreviewAsync(actor,calendar,default);
        Check(calendarPreview.Starts.Length==2 && calendarPreview.Agenda.Values.All(rows=>rows.Length==2) && calendar.People.Values.All(p=>p!=Guid.Empty),"Automatic recipients fit every start in the semester calendar");
        var absence=new PersonalScheduleException { Id=Guid.CreateVersion7(),PersonId=a,Day=WeekContract.LocalToday(calendarPreview.Starts.Last()),Revision=1,ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=Guid.CreateVersion7(),RequestHash=new string('0',64),Reason="Isolated absence",IntervalsJson="[]" };
        db.Add(absence);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var unavailableCalendar=Input();unavailableCalendar.Mode="CALENDARIO";unavailableCalendar.Through=calendar.Through;
        await Reject(()=>plans.PreviewAsync(actor,unavailableCalendar,default),"PLAN_NO_AVAILABLE_WORKER");

        // Publish a different immutable version after CPU work and before the save lock.
        var racing=Input();racing.From=day.AddDays(30);racing.Through=racing.From;
        racing.Fingerprint=(await plans.PreviewAsync(actor,racing,default)).Fingerprint;
        var changed=false;
        using(var listener=new MeterListener())
        {
            listener.InstrumentPublished=(instrument,l)=> { if(instrument.Meter.Name=="Sgol.Performance" && instrument.Name=="sgol.work.duration")l.EnableMeasurementEvents(instrument); };
            listener.SetMeasurementEventCallback<double>((instrument,value,tags,state)=> {
                if(changed)return;
                foreach(var tag in tags)if(tag.Key=="stage" && tag.Value?.ToString()=="planning.process-preview")
                {
                    changed=true;
                    using var concurrent=new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options);
                    var concurrentDirection=new EfDirectionPreparedPlanService(concurrent,new AuditTransaction(concurrent),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(concurrent));
                    var concurrentCatalog=new OperationalProcessCatalogService(concurrent,concurrentDirection,clock,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Processes:OperationsEnabled"]="true" }).Build());
                    var editor=concurrentCatalog.ReadAsync(actor,id,default).GetAwaiter().GetResult();
                    var v=editor.Configuration!;
                    concurrentCatalog.SaveAsync(actor,new() { Id=id,Revision=editor.Process.Revision,Name=v.Name,Description=v.Description,StartKind=v.StartKind,FirstMonth=v.FirstMonth,WindowFromDay=v.WindowFromDay,WindowThroughDay=v.WindowThroughDay,PreferredWeekDay=v.PreferredWeekDay,StartHour=v.StartHour+1,Steps=editor.Steps.ToList() },default).GetAwaiter().GetResult();
                    concurrent.ChangeTracker.Clear();editor=concurrentCatalog.ReadAsync(actor,id,default).GetAwaiter().GetResult();
                    concurrentCatalog.PublishAsync(actor,id,editor.Process.Revision,default).GetAwaiter().GetResult();
                    concurrent.ChangeTracker.Clear();editor=concurrentCatalog.ReadAsync(actor,id,default).GetAwaiter().GetResult();
                    concurrentCatalog.SetEnabledAsync(actor,id,editor.Process.Revision,true,default).GetAwaiter().GetResult();
                }
            });
            listener.Start();
            await Reject(()=>plans.PrepareAsync(actor,racing,default),"PLAN_CAPACITY_STALE");
        }
        db.ChangeTracker.Clear();
        Check(changed && !await db.DirectionPreparedPlans.AnyAsync(p=>p.Id==racing.Key) && !await db.Set<PreparedProcessStart>().AnyAsync(p=>p.PlanId==racing.Key),"Concurrent process publication rejects with 409 and no partial plan or starts");
        racing.Fingerprint=(await plans.PreviewAsync(actor,racing,default)).Fingerprint;
        var fresh=await plans.PrepareAsync(actor,racing,default);db.ChangeTracker.Clear();
        var runs=await plans.ActivateAsync(actor,fresh,default);db.ChangeTracker.Clear();
        Check(runs.Length==1 && (await plans.ActivateAsync(actor,fresh,default)).SequenceEqual(runs),"Fresh process preparation activates atomically and activation replay reuses the same run");
        await OperationalSchedulingChecks.ProcessOnly(db,actor);

    }
    private static void Check(bool ok,string text) { if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text); }
    private static async Task Reject(Func<Task> action,string code) { try { await action();throw new Exception("Expected "+code); } catch(DirectionPlanException e) when(e.Code==code) { Console.WriteLine("PASS: Rejected "+code); } }
    private sealed class Clock:IClock { public DateTimeOffset UtcNow=>new(2026,10,28,14,0,0,TimeSpan.Zero); }
    private sealed class Scanner:IFileMalwareScanner { public Task CheckAvailabilityAsync(CancellationToken token)=>Task.CompletedTask;public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken token)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio)); }
}
