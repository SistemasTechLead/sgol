using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class OperationalSchedulingChecks
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Rules()
    {
        ReflowRules();
        foreach (var (load, duration) in new[] { (5, 8), (15, 23), (20, 30), (30, 45), (60, 90) })
        { Require(OperationalAgenda.Duration(load, 50) == duration, "Per-occurrence rounding"); Require(OperationalAgenda.Duration(load, 0) == load, "Zero margin"); }
        var day = new DateOnly(2026, 11, 2); var person = Guid.NewGuid();
        var worker = new CapacityWorker(person, "Fixture", "CHECK", CanonicalRole.SalesFloor, null, null);
        var context = new TemporalEligibilityContext { Enabled = true, Schedules = [new() { Id = Guid.NewGuid(), PersonId = person, Revision = 1,
            DaysJson = JsonSerializer.Serialize(new Dictionary<int, WorkInterval[]> { [1] = [new(540,780),new(900,1140)] }) }] };
        CapacityLedger Ledger(params OperationalAssignment[] tasks) => new([worker], tasks, [], [], [], day) { Temporal = context };
        var policy = new OperationalSchedulingPolicy { Id = OperationalSchedulingPolicy.InitialId, MarginPercent = 50, Revision = 1 };
        OperationalAssignment Task(string code, int load, string level, int minute = 540)
        {
            var task = new OperationalAssignment { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), TaskCode = code, LoadMinutes = load, PersonId = person, AssignedRole = worker.Role };
            OperationalAgenda.Capture(task, policy, level, TemporalEligibilityContext.At(day, minute)); return task;
        }
        var opening = Task("OPEN",10,"CRITICA"); var noon = Task("NOON",20,"CRITICA",720);
        var important = Task("IMPORTANT",15,"IMPORTANTE");
        var placement = OperationalAgenda.Find(Ledger(opening,noon), person, important, TemporalEligibilityContext.At(day,480));
        Require(placement.Eligible && placement.StartsAt == opening.DueAt && placement.DueAt < noon.StartsAt, "Important fills the morning gap before a later critical anchor");
        var conflicting = Task("COLLISION",20,"CRITICA");
        Require(OperationalAgenda.Find(Ledger(opening),person,conflicting,opening.StartsAt).Reason == "SCHEDULING_CRITICAL_OVERLAP", "Critical overlap must be explicit");
        Require(OperationalAgenda.Find(Ledger(),person,opening,opening.StartsAt.AddMinutes(1)).Reason == "SCHEDULING_CRITICAL_ANCHOR_PASSED", "Past critical anchor cannot move");
        var beforeLunch = Task("REST",20,"FLEXIBLE",770);
        placement = OperationalAgenda.Find(Ledger(),person,beforeLunch,beforeLunch.StartsAt);
        Require(placement.Eligible && placement.StartsAt == TemporalEligibilityContext.At(day,900), "A block cannot straddle a break");
        var endOfShift = Task("END",35,"FLEXIBLE",1110);
        Require(!OperationalAgenda.Find(Ledger(),person,endOfShift,endOfShift.StartsAt).Eligible, "No overnight spill");
        var historical = new OperationalAssignment { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), PersonId = person, LoadMinutes = 10, StartsAt = opening.StartsAt, DueAt = opening.StartsAt.AddHours(8) };
        Require(OperationalAgenda.Find(Ledger(historical),person,important,important.StartsAt).Reason == "SCHEDULING_LEGACY_RESERVATION", "Do not ignore or interpret a legacy deadline as an execution block");
        var progress = Task("GOAL",15,"FLEXIBLE"); progress.ScheduleMode = "PROGRESO"; progress.GoalUnits = 50;
        OperationalAgenda.Capture(progress,policy,"FLEXIBLE",opening.StartsAt);
        Require(progress.DueAt == OperationalSchedule.Instant(day.AddDays(7)), "Weekly progress contract");
        var completed = Task("DONE",10,"FLEXIBLE"); completed.CompletedAt = completed.StartsAt.AddMinutes(3);
        placement = OperationalAgenda.Find(Ledger(completed),person,important,important.StartsAt);
        Require(placement.StartsAt == completed.DueAt, "Early delivery does not release the reserved margin");
        var load195 = new[] { ("OPEN",10,"CRITICA"),("PRICE",15,"IMPORTANTE"),("A",40,"FLEXIBLE"),("B",15,"FLEXIBLE"),("C",20,"FLEXIBLE"),("D",10,"FLEXIBLE"),("E",20,"FLEXIBLE"),("F",10,"FLEXIBLE"),("G",10,"FLEXIBLE"),("H",30,"FLEXIBLE"),("I",15,"FLEXIBLE") };
        var scheduled = new List<OperationalAssignment>();
        foreach(var (code,load,level) in load195) { var task = Task(code,load,level); OperationalAgenda.Apply(task,OperationalAgenda.Find(Ledger(scheduled.ToArray()),person,task,opening.StartsAt)); scheduled.Add(task); }
        Require(scheduled.Sum(t=>t.LoadMinutes)==195 && scheduled.Sum(t=>(t.DueAt-t.StartsAt).TotalMinutes)==294 && scheduled.Max(t=>t.DueAt)==TemporalEligibilityContext.At(day,968), "Dennis 195 minutes: 294 reserved, last closure 16:08");
        Console.WriteLine("PASS scheduling rules: rounding, critical anchors, priorities, breaks, legacy safety, weekly progress and Dennis 195-minute scenario.");
    }

    private static void ReflowRules()
    {
        var day = new DateOnly(2026, 11, 2); var person = Guid.NewGuid();
        var worker = new CapacityWorker(person, "Fixture", "CHECK", CanonicalRole.SalesFloor, null, null);
        var policy = new OperationalSchedulingPolicy { Id = OperationalSchedulingPolicy.ReflowId, Revision = 2, AgendaVersion = 2, MarginPercent = 50 };
        CapacityLedger Ledger(int exit) => new([worker], [], [], [], [], day) { Temporal = new() {
            Enabled = true, BaseSchedulePolicyIds = [policy.Id], BaseSchedulesForCapacity = true,
            Schedules = [new() { Id = Guid.NewGuid(), PersonId = person, Revision = 1, DaysJson = JsonSerializer.Serialize(new Dictionary<int, WorkInterval[]> { [1] = [new(540, exit)] }) }] } };
        OperationalAssignment Task(string code, int minutes, string level, int nominal)
        {
            var task = new OperationalAssignment { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), TaskCode = code, PersonId = person, AssignedRole = worker.Role, LoadMinutes = minutes };
            OperationalAgenda.Capture(task, policy, level, TemporalEligibilityContext.At(day, nominal)); return task;
        }
        var important = Task("A", 20, "IMPORTANTE", 600); var longTask = Task("B", 80, "FLEXIBLE", 540);
        var before = JsonSerializer.Serialize(new[] { important, longTask });
        var solution = OperationalAgendaSearch.SolveFixed(Ledger(720), [important, longTask], TemporalEligibilityContext.At(day, 540), default);
        Require(solution.Single(t => t.Id == important.Id).StartsAt == TemporalEligibilityContext.At(day, 660), "Revisit an important placement to preserve the only two-hour window");
        Require(solution.Single(t => t.Id == longTask.Id).StartsAt == TemporalEligibilityContext.At(day, 540), "Long flexible remains flexible");
        Require(before == JsonSerializer.Serialize(new[] { important, longTask }), "Search does not mutate its inputs");
        var late = Task("LATE", 20, "FLEXIBLE", 710);
        var placement = OperationalAgenda.Find(Ledger(720), person, late, TemporalEligibilityContext.At(day, 540));
        Require(placement.Eligible && placement.StartsAt < late.NominalStartsAt, "Flexible advance fallback");
        OperationalAgenda.Apply(late, placement);
        Require(Ledger(720).CanAssign(person, late), "Earlier effective activation is valid with a separate nominal");
        try { OperationalAgendaSearch.SolveFixed(Ledger(720), [important], TemporalEligibilityContext.At(day, 540), default, 0); throw new Exception("Budget accepted"); }
        catch (DirectionPlanException e) { Require(e.Code == "SCHEDULING_SEARCH_INCONCLUSIVE", "Budget is never reported as infeasible"); }
        policy.MarginPercent = 0;
        for (var a = 1; a <= 5; a++) for (var b = 1; b <= 5; b++)
        {
            var left = Task("A", a, "IMPORTANTE", 542); var right = Task("B", b, "FLEXIBLE", 544);
            var oracle = Enumerable.Range(542, 5).Any(x => Enumerable.Range(540, 7).Any(y => x + a <= 546 && y + b <= 546 && (x + a <= y || y + b <= x)));
            var feasible = true;
            try { OperationalAgendaSearch.SolveFixed(Ledger(546), [left, right], TemporalEligibilityContext.At(day, 540), default); }
            catch (DirectionPlanException e) when (e.Code == "SCHEDULING_INFEASIBLE") { feasible = false; }
            Require(feasible == oracle, $"Exhaustive oracle: {a}/{b}");
        }
        Console.WriteLine("PASS agenda 2: backtracking, flexible advances, immutable inputs, budget distinction and 25 exhaustive oracle cases.");
    }

    public static async Task Integration(SgolDbContext db)
    {
        var clock = new SchedulingClock();
        var actor = await db.RoleAssignmentVersions.Where(r=>r.RoleCode==CanonicalRole.Direction && r.ValidTo==null).Select(r=>r.UserId).FirstAsync();
        var baselineIds = await db.OperationalAssignments.Select(a=>a.Id).ToArrayAsync();
        async Task<string> Baseline() => JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>baselineIds.Contains(a.Id)).OrderBy(a=>a.Id).ToArrayAsync());
        var baseline = await Baseline();
        var direction = new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        var taskService = new OperationalTaskService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var policyService = new OperationalSchedulingPolicyService(db,direction,clock);
        var policy = await policyService.ReadAsync(actor,default);
        Require(policy.MarginPercent==50,"Initial policy");
        var tasks = new[] { ("OPEN",10,"CRITICA",9),("NOON",20,"CRITICA",12),("IMPORTANT",15,"IMPORTANTE",9),("FLEXIBLE",30,"FLEXIBLE",9) }.Select(v=>new OperationalTask {
            Id=Guid.CreateVersion7(),TaskCode="CHK-"+Guid.NewGuid().ToString("N")[..8],Name="Scheduling check "+v.Item1,Requirement="NINGUNO",LoadMinutes=v.Item2,OperationalCriticality=v.Item3,
            ScheduleMode="DIAS_FIJOS",WeekDays=2,Repetitions=1,PeriodType="SEMANAL",StartHour=v.Item4,GoalUnits=100,ProgressUnit="unidades" }).ToArray();
        db.AddRange(tasks);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var input = new DirectionPlanInput("Scheduling checks","Isolated test","SEMANAL",2026,45,tasks.Select(t=>t.TaskCode).ToArray(),SchedulingNotBefore:clock.UtcNow.AddMinutes(1));
        var preview = await PlanningCapacity.ReadAsync(db,input,clock.UtcNow,default);
        await PlanningCapacity.DailyLoadAsync(db,input,preview.Preview,preview.Ledger,default);
        Require(preview.Preview.TemporalRows.Length==4,"Actual chosen rows");
        input=input with {CapacityFingerprint=preview.Preview.AgendaFingerprint};
        var key=Guid.CreateVersion7();
        var saved=await direction.CreateAsync(actor,key,Guid.CreateVersion7(),input);
        Require(saved.Plan.Assignments!.Count==4,"All occurrences saved");
        foreach(var task in saved.Plan.Assignments!) {
            Require(task.SchedulingPolicyId==policy.Id && task.NominalStartsAt<=task.StartsAt,"Captured policy and release");
            var row=preview.Preview.TemporalRows.Single(r=>r.Task.StartsWith(task.TaskCode+" · ",StringComparison.Ordinal));
            Require(row.StartsAt==task.StartsAt && row.DueAt==task.DueAt && row.Person==task.PersonName,"Preview equals save after balancing");
        }
        var replay=await direction.CreateAsync(actor,key,Guid.CreateVersion7(),input);Require(replay.Result=="RECOVERED","Idempotent creation");
        var savedIds=saved.Plan.Assignments.Select(a=>a.Id).ToArray();
        var savedJson=JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>savedIds.Contains(a.Id)).OrderBy(a=>a.Id).ToArrayAsync());
        await policyService.ChangeAsync(actor,75,policy.Revision,default);db.ChangeTracker.Clear();
        Require(savedJson==JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a=>savedIds.Contains(a.Id)).OrderBy(a=>a.Id).ToArrayAsync()),"Global policy change preserved published rows");
        try { await direction.CreateAsync(actor,Guid.CreateVersion7(),Guid.CreateVersion7(),input with {Name="Stale policy"}); throw new Exception("Stale preview accepted"); }
        catch(DirectionPlanException e) {Require(e.Code=="PLAN_CAPACITY_STALE","Reject stale policy");}
        var newPolicy=await policyService.ReadAsync(actor,default);await policyService.ChangeAsync(actor,50,newPolicy.Revision,default);db.ChangeTracker.Clear();
        var occasionTask=new OperationalTask {Id=Guid.CreateVersion7(),TaskCode="CHK-"+Guid.NewGuid().ToString("N")[..8],Name="Occasion scheduling check",PeriodType="OCASIONAL",Requirement="NINGUNO",LoadMinutes=20,OperationalCriticality="FLEXIBLE"};
        occasionTask.Validate(); db.Add(occasionTask); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var worker=(await OperationalCapacityService.WorkersAsync(db,clock.UtcNow,default)).First(w=>w.Role==CanonicalRole.SalesFloor);
        var start=TemporalEligibilityContext.At(new(2026,11,2),960);
        var occasion=new OccasionInput(occasionTask.Id,occasionTask.Revision,"Scheduled occasion","Instructions","","",start,start.AddMinutes(30),[worker.Id]);
        var occasionPreview=await taskService.PreviewOccasionAsync(actor,occasion,default);
        occasion=occasion with {SchedulingFingerprint=occasionPreview.Fingerprint,SchedulingNotBefore=occasionPreview.NotBefore};
        var occasionKey=Guid.CreateVersion7();
        var occasionPlan=await taskService.AssignOccasionAsync(actor,occasionKey,occasion,[],default);
        var assigned=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.PlanId==occasionPlan);
        Require(assigned.DueAt-assigned.StartsAt==TimeSpan.FromMinutes(30) && assigned.StartsAt==occasionPreview.Assignments[0].StartsAt,"Occasion uses confirmed server agenda");
        Require(await taskService.AssignOccasionAsync(actor,occasionKey,occasion,[],default)==occasionPlan,"Occasion replay");
        var supervision=new OperationalSupervisionService(db,clock,direction);var capacity=new OperationalCapacityService(db,clock,supervision);
        var source=saved.Plan.Assignments.First().PersonId;
        var adjustment=await capacity.PreviewAsync(actor,new(source,new(2026,11,2),new(2026,11,2),0,480,"Isolated absence scenario"),default);
        var applied=await capacity.ApplyAsync(actor,adjustment.Change,adjustment.Fingerprint,default);
        foreach(var move in applied.Moves) {
            var row=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==move.TaskId);
            Require(row.StartsAt>=row.NominalStartsAt && row.DueAt-row.StartsAt==TimeSpan.FromMinutes(OperationalAgenda.Duration(row.LoadMinutes,50)),"Exceptional move preserves captured margin and release");
            if(row.OperationalCriticality=="CRITICA")Require(row.StartsAt==row.NominalStartsAt,"Transferred critical anchor unchanged");
        }
        await ActivatedSelection(db, direction, clock, actor, tasks.Last(), await policyService.ReadAsync(actor,default));
        await TargetReflow(db,direction,clock,actor,tasks.Last(),await policyService.ReadAsync(actor,default));
        await ProcessIntegration(db, direction, policyService, clock, actor, worker.Id);
        Require(baseline==await Baseline(),"All preexisting assignments preserved across new plans, policy, occasions and adjustment");
        Console.WriteLine($"PASS scheduling integration: preview/save, policy concurrency, immutable published contracts, occasion preview/replay, absence adjustment; {baselineIds.Length} preexisting rows unchanged.");
    }
    private static async Task<Guid> AddWorker(SgolDbContext db, SchedulingClock clock, Guid actor)
    {
        var person=Guid.CreateVersion7();var user=Guid.CreateVersion7();var from=clock.UtcNow.AddYears(-1);var code="AGENDA-"+Guid.NewGuid().ToString("N")[..8];
        db.People.Add(new Person {Id=person,StableCode=code,DisplayName=code,CreatedAt=from});db.AppUsers.Add(new AppUser {Id=user,PersonId=person,Status=AccountStatus.Active,SecurityStamp="isolated-scheduling-check"});
        db.EmploymentVersions.Add(new(Guid.CreateVersion7(),person,BranchScope.LorettaId,EmploymentStatus.Active,from));db.RoleAssignmentVersions.Add(new RoleAssignmentVersion {Id=Guid.CreateVersion7(),UserId=user,BranchId=BranchScope.LorettaId,RoleCode=CanonicalRole.SalesFloor,Status=RoleAssignmentStatus.Active,ValidFrom=from});
        db.Add(new PersonalScheduleVersion {Id=Guid.CreateVersion7(),PersonId=person,Revision=1,From=new(2026,10,21),ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=Guid.CreateVersion7(),RequestHash=new string('0',64),DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>d==0?Array.Empty<WorkInterval>():new[]{new WorkInterval(540,780),new WorkInterval(900,1140)}))});await db.SaveChangesAsync();db.ChangeTracker.Clear();return person;
    }
    private static async Task ActivatedSelection(SgolDbContext db, IDirectionPreparedPlanService direction, SchedulingClock clock, Guid actor, OperationalTask definition, OperationalSchedulingPolicy policy)
    {
        var previousNow=clock.UtcNow;clock.UtcNow=new(2026,10,21,16,0,0,TimeSpan.Zero);
        var source=await AddWorker(db,clock,actor);await AddWorker(db,clock,actor);
        var day=WeekContract.LocalToday(clock.UtcNow);var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,actor,clock.UtcNow,"Selection fixture",day,day);plan.CaptureSchedulingPolicy(policy.Id);db.Add(plan);
        OperationalAssignment Assignment(int minute,int load) { var row=new OperationalAssignment {Id=Guid.CreateVersion7(),PlanId=plan.Id,TaskId=definition.Id,TaskCode=definition.TaskCode,TaskRevision=definition.Revision,Name="Selection fixture",Requirement="NINGUNO",AssignedRole=CanonicalRole.SalesFloor,PersonId=source,PersonName="Selection source",LoadMinutes=load};OperationalAgenda.Capture(row,policy,"FLEXIBLE",TemporalEligibilityContext.At(day,minute));return row; }
        var active=Assignment(585,20);var expired=Assignment(540,5);db.AddRange(active,expired);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var service=new OperationalCapacityService(db,clock,new OperationalSupervisionService(db,clock,direction));var change=new CapacityChange(source,day,day,0,480,"Exceptional selection fixture");
        var notSelected=await service.PreviewAsync(actor,change,default);Require(notSelected.Moves.Count==0,"Activated assignments require explicit selection");
        var selected=await service.PreviewAsync(actor,change with {ActivatedTaskIds=[active.Id]},default);Require(selected.Moves.Count==1 && selected.Moves[0].TaskId==active.Id,"Only the selected active assignment can move");
        clock.UtcNow=clock.UtcNow.AddMinutes(16);
        try {await service.ApplyAsync(actor,selected.Change,selected.Fingerprint,default);throw new Exception("Expired preview accepted");}catch(DirectionPlanException e){Require(e.Code=="CAPACITY_STALE","Expiry invalidates confirmed preview");}
        clock.UtcNow=clock.UtcNow.AddMinutes(-16);db.ChangeTracker.Clear();selected=await service.PreviewAsync(actor,change with {ActivatedTaskIds=[active.Id]},default);await service.ApplyAsync(actor,selected.Change,selected.Fingerprint,default);db.ChangeTracker.Clear();
        var old=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==expired.Id);Require(old.StartsAt==expired.StartsAt && old.DueAt==expired.DueAt && old.PersonId==source,"Expired assignment is never moved or renewed");
        var history=await new OperationalTaskService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner()).LifecycleHistoryAsync(actor,active.Id,default);Require(history.Any(e=>e.Kind=="REDISTRIBUCION" && e.Comment!.Contains("Exceptional selection fixture")),"Collaborator history explains confirmed redistribution");
        clock.UtcNow=previousNow;Console.WriteLine("PASS active selection, expiry during confirmation, preserved overdue task and visible redistribution history.");
    }
    private static async Task TargetReflow(SgolDbContext db, IDirectionPreparedPlanService direction, SchedulingClock clock, Guid actor, OperationalTask definition, OperationalSchedulingPolicy policy)
    {
        var source=await AddWorker(db,clock,actor);var target=await AddWorker(db,clock,actor);var day=WeekContract.LocalToday(clock.UtcNow);
        var specialty=new Specialization {Id=Guid.CreateVersion7(),Name="Reflow "+Guid.NewGuid().ToString("N")[..6],NormalizedName=Guid.NewGuid().ToString("N"),Revision=1};db.Add(specialty);db.Add(new PersonSpecialization {PersonId=source,SpecializationId=specialty.Id});db.Add(new PersonSpecialization {PersonId=target,SpecializationId=specialty.Id});
        var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,actor,clock.UtcNow,"Reflow fixture",day,day);plan.CaptureSchedulingPolicy(policy.Id);db.Add(plan);
        OperationalAssignment Task(Guid person,string name,int load,string priority) {var t=new OperationalAssignment {Id=Guid.CreateVersion7(),PlanId=plan.Id,TaskId=definition.Id,TaskCode=definition.TaskCode,TaskRevision=definition.Revision,Name=name,Requirement="NINGUNO",PersonId=person,PersonName=name,AssignedRole=CanonicalRole.SalesFloor,LoadMinutes=load};OperationalAgenda.Capture(t,policy,priority,TemporalEligibilityContext.At(day,540));return t;}
        var opening=Task(source,"Opening source",10,"CRITICA");opening.SpecializationId=specialty.Id;var flexible=Task(target,"Target flexible",60,"FLEXIBLE");db.AddRange(opening,flexible);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var capacity=new OperationalCapacityService(db,clock,new OperationalSupervisionService(db,clock,direction));var preview=await capacity.PreviewAsync(actor,new(source,day,day,0,480,"Absence requires target reflow"),default);
        Require(preview.Moves.Count==2 && preview.Moves.Any(m=>m.TaskId==opening.Id && m.ToId==target && m.StartsAt==opening.StartsAt) && preview.Moves.Any(m=>m.TaskId==flexible.Id && m.StartsAt==opening.DueAt),"Preview includes transferred critical and explicitly reviewable target reflow");
        await capacity.ApplyAsync(actor,preview.Change,preview.Fingerprint,default);db.ChangeTracker.Clear();
        var rows=await db.OperationalAssignments.AsNoTracking().Where(a=>a.Id==opening.Id || a.Id==flexible.Id).OrderBy(a=>a.StartsAt).ToArrayAsync();Require(rows.All(a=>a.PersonId==target) && rows[0].DueAt==rows[1].StartsAt && rows[1].DueAt==TemporalEligibilityContext.At(day,645),"Confirmed target reflow preserves anchor, durations and no overlap");
        db.Add(new PersonalScheduleException {Id=Guid.CreateVersion7(),PersonId=target,Day=day,Revision=1,Reason="Shift change fixture",ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=Guid.CreateVersion7(),RequestHash=new string('0',64),IntervalsJson=JsonSerializer.Serialize(new[]{new WorkInterval(600,780),new WorkInterval(900,1140)})});await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var timing=await capacity.PreviewAsync(actor,new(target,day,day,100,480,"Coverage changed without aggregate overload"),default);Require(timing.Conflicts.Any(c=>c.Task==opening.Name),"Time conflicts are diagnosed even without aggregate overload");Require(timing.Moves.Any(m=>m.TaskId==flexible.Id && m.ToId==target && m.StartsAt==TemporalEligibilityContext.At(day,600)),"A future flexible may stay with its owner in a later valid shift interval");
        Console.WriteLine("PASS absence target reflow: critical 09:00-09:15, existing future flexible 09:15-10:45, both changes confirmed together.");
    }
    internal static async Task ProcessOnly(SgolDbContext db, Guid actor)
    {
        var clock=new SchedulingClock { UtcNow=new(2026,10,28,14,0,0,TimeSpan.Zero) };
        var direction=new EfDirectionPreparedPlanService(db,new AuditTransaction(db),clock,new Uuid7Generator(clock),new DirectionPlanningCatalogReader(db));
        await ProcessIntegration(db,direction,new OperationalSchedulingPolicyService(db,direction,clock),clock,actor,Guid.Empty);
    }
    private static async Task ProcessIntegration(SgolDbContext db, IDirectionPreparedPlanService direction, OperationalSchedulingPolicyService policies, SchedulingClock clock, Guid actor, Guid person)
    {
        var oldNow=clock.UtcNow;
        person=Guid.CreateVersion7();var user=Guid.CreateVersion7();var from=clock.UtcNow.AddYears(-1);var code="PROCESS-"+Guid.NewGuid().ToString("N")[..8];
        db.People.Add(new Person {Id=person,StableCode=code,DisplayName=code,CreatedAt=from});
        db.AppUsers.Add(new AppUser {Id=user,PersonId=person,Status=AccountStatus.Active,SecurityStamp="isolated-scheduling-check"});
        db.EmploymentVersions.Add(new(Guid.CreateVersion7(),person,BranchScope.LorettaId,EmploymentStatus.Active,from));
        db.RoleAssignmentVersions.Add(new RoleAssignmentVersion {Id=Guid.CreateVersion7(),UserId=user,BranchId=BranchScope.LorettaId,RoleCode=CanonicalRole.SalesFloor,Status=RoleAssignmentStatus.Active,ValidFrom=from});
        db.Add(new PersonalScheduleVersion {Id=Guid.CreateVersion7(),PersonId=person,Revision=1,From=new(2026,10,21),ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=Guid.CreateVersion7(),RequestHash=new string('0',64),DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>d==0?Array.Empty<WorkInterval>():new[]{new WorkInterval(540,780),new WorkInterval(900,1140)}))});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var startMinute=(await TemporalEligibilityContext.LoadAsync(db,clock.UtcNow,default)).Day(person,new(2026,11,3)).Intervals![0].Entry;
        var process=new OperationalSequence {Id=Guid.CreateVersion7(),Name="Scheduling fixture "+Guid.NewGuid().ToString("N")[..6],Enabled=false};
        db.Add(process);await db.SaveChangesAsync();
        var version=new OperationalSequenceVersion {Id=Guid.CreateVersion7(),SequenceId=process.Id,Number=1,StartKind="OCASIONAL",StartHour=startMinute/60,StartHourConfirmed=true,Name=process.Name,AgendaVersion=1,CreatedAt=clock.UtcNow,CreatedBy=actor};
        db.Add(version);
        var tasks=new[] {("OPEN",10,"CRITICA"),("NEXT",15,"FLEXIBLE")}.Select(v=>new OperationalTask {Id=Guid.CreateVersion7(),TaskCode="PCHK-"+Guid.NewGuid().ToString("N")[..8],ProcessId=process.Id,Name="Process "+v.Item1,Requirement="DOCUMENTO",LoadMinutes=v.Item2,OperationalCriticality=v.Item3}).ToArray();
        db.AddRange(tasks);await db.SaveChangesAsync();
        for(var i=0;i<tasks.Length;i++)db.Add(new OperationalSequenceStep {VersionId=version.Id,Position=i+1,TaskId=tasks[i].Id,DeadlineHours=1,ReleaseCondition=i==0?"INICIO":"ENTREGA",Action=i==0?"ENTREGAR":"AUTORIZAR",TaskSnapshot=JsonSerializer.Serialize(tasks[i])});
        await db.SaveChangesAsync(); version.PublishedAt=clock.UtcNow;version.PublishedBy=actor;process.PublishedVersionId=version.Id;process.Enabled=true;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var service=new ProcessPlanningService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());
        var input=new ProcessPlanInput {Key=Guid.CreateVersion7(),Name="Prepared schedule fixture",ProcessId=process.Id,Mode="OCASION",OccasionTime=new(startMinute/60,0),From=new(2026,11,3),Through=new(2026,11,3),People=new(){[1]=person,[2]=person}};
        var preview=await service.PreviewAsync(actor,input,default);Require(preview.SchedulingPolicyId!=null && preview.Agenda.Values.Single().Length==2,"Process preview captures policy and stage blocks");
        input.Fingerprint=preview.Fingerprint;var plan=await service.PrepareAsync(actor,input,default);
        var capturedPolicy=await policies.ReadAsync(actor,default);await policies.ChangeAsync(actor,75,capturedPolicy.Revision,default);db.ChangeTracker.Clear();
        var runs=await service.ActivateAsync(actor,plan,default);Require(runs.Length==1,"Activate prepared automatic process");
        var stages=await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s=>s.RunId==runs[0]).OrderBy(s=>s.Position).ToArrayAsync();
        Require(stages[0].Status=="PROGRAMADA" && stages[1].Status=="EN_ESPERA" && stages.All(s=>s.ClosureMarginPercent==50),"Process waits for time/dependency and retains captured margin");
        clock.UtcNow=TemporalEligibilityContext.At(new(2026,11,3),startMinute);
        var engine=new OperationalProcessService(db,direction,clock,new FileTechnicalValidator(),new CheckScanner());await engine.TickAsync(default);db.ChangeTracker.Clear();
        var first=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.PlanId==plan);
        Require(first.StartsAt==clock.UtcNow && first.DueAt==clock.UtcNow.AddMinutes(15),"Effective stage closure uses captured load and margin");
        Require(await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==runs[0] && s.Position==2).Select(s=>s.Status).SingleAsync()=="EN_ESPERA","Criticidad never skips dependency");
        clock.UtcNow=clock.UtcNow.AddMinutes(30);await engine.TickAsync(default);db.ChangeTracker.Clear();
        var unchanged=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==first.Id);Require(unchanged.StartsAt==first.StartsAt && unchanged.DueAt==first.DueAt,"Repeated process tick never renews an expired assignment");
        clock.UtcNow=oldNow;var current=await policies.ReadAsync(actor,default);await policies.ChangeAsync(actor,50,current.Revision,default);db.ChangeTracker.Clear();
        Console.WriteLine("PASS automatic process: persisted reservations, captured policy, delayed activation, dependencies and overdue preservation.");
    }
    public static async Task Benchmark(SgolDbContext db)
    {
        var clock=new SchedulingClock();
        var inputTasks=Enumerable.Range(0,200).Select(i=>new OperationalTask {Id=Guid.CreateVersion7(),TaskCode="BEN-"+Guid.NewGuid().ToString("N")[..12],Name="Maximum planner fixture "+i,Requirement="NINGUNO",LoadMinutes=1,OperationalCriticality="FLEXIBLE",PeriodType=i<190?"SEMANAL":"MENSUAL",Repetitions=1,StartHour=9}).ToArray();
        db.AddRange(inputTasks);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var input=new DirectionPlanInput("10k benchmark","Isolated maximum size","ANUAL",2027,null,inputTasks.Select(t=>t.TaskCode).ToArray(),SchedulingNotBefore:clock.UtcNow.AddMinutes(1));
        var before = await db.OperationalAssignments.CountAsync();
        try {
            var state=await PlanningCapacity.ReadAsync(db,input,clock.UtcNow,default);
            await PlanningCapacity.DailyLoadAsync(db,input,state.Preview,state.Ledger,default);
            throw new Exception("Expected real calendar expansion to exceed 10000");
        } catch (DirectionPlanException e) when (e.Code == "PLAN_TOO_MANY_OCCURRENCES") { }
        Require(before == await db.OperationalAssignments.CountAsync(), "Occurrence limit leaves no assignments");
        Console.WriteLine("PASS max-size planner: expanded calendar rejected at 10000; no assignments persisted.");
    }
    private sealed class SchedulingClock : IClock { public DateTimeOffset UtcNow {get;set;}=new(2026,10,21,12,0,0,TimeSpan.Zero); }
}
