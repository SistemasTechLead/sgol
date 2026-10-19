using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class DailyBalanceChecks
{
    public static async Task SnapshotAsync(SgolDbContext db)
    {
        var now=DateTimeOffset.UtcNow;
        var tomorrow=OperationalSchedule.Instant(Sgol.Planning.Contracts.WeekContract.LocalToday(now).AddDays(1));
        var plan=await db.DirectionPreparedPlans.AsNoTracking().Where(p=>p.IsWeeklyOperationsPlan).OrderByDescending(p=>p.CreatedAt).FirstAsync();
        var tasks=await db.OperationalAssignments.AsNoTracking().Where(t=>t.PlanId==plan.Id && t.StartsAt>=tomorrow && t.CompletedAt==null && t.ReviewStatus!="REASIGNADA").ToArrayAsync();
        var before=tasks.ToDictionary(t=>t.Id,t=>new {t.PersonId,t.PersonName,t.TaskCode,t.StartsAt,t.DueAt,t.LoadMinutes,t.ScheduleKey});
        var ledger=await OperationalCapacityService.LedgerAsync(db,now,default);
        var ids=tasks.Select(t=>t.Id).ToHashSet();
        ledger=new(ledger.Workers,ledger.Tasks.Where(t=>!ids.Contains(t.Id)).Concat(tasks).ToArray(),ledger.Capacities,ledger.Unavailable,ledger.Legacy,ledger.Today)
            {Temporal=ledger.Temporal,PlannedChanges=ledger.PlannedChanges};
        OperationalPlanBalancer.Balance(ledger,tasks,true,now,default);
        foreach(var task in tasks) {
            var original=before[task.Id];
            if(original.StartsAt!=task.StartsAt || original.DueAt!=task.DueAt || original.LoadMinutes!=task.LoadMinutes || original.ScheduleKey!=task.ScheduleKey)
                throw new Exception("Simulation changed dates or workload.");
            if(!ledger.CheckTime(task.PersonId,task).Eligible) throw new Exception("Simulation assigned outside the shift.");
            ledger.CaptureTime(task);
        }
        Console.WriteLine(JsonSerializer.Serialize(new {
            Before=before.Values.GroupBy(t=>new {Day=Sgol.Planning.Contracts.WeekContract.LocalToday(t.StartsAt),t.PersonName})
                .OrderBy(g=>g.Key.Day).ThenBy(g=>g.Key.PersonName).Select(g=>new {g.Key.Day,g.Key.PersonName,Minutes=g.Sum(t=>t.LoadMinutes)}),
            After=tasks.GroupBy(t=>new {Day=Sgol.Planning.Contracts.WeekContract.LocalToday(t.StartsAt),t.PersonName})
                .OrderBy(g=>g.Key.Day).ThenBy(g=>g.Key.PersonName).Select(g=>new {g.Key.Day,g.Key.PersonName,Minutes=g.Sum(t=>t.LoadMinutes)}),
            Changes=tasks.Where(t=>t.PersonId!=before[t.Id].PersonId).Select(t=>new {t.TaskCode,t.Name,
                Day=Sgol.Planning.Contracts.WeekContract.LocalToday(t.StartsAt),From=before[t.Id].PersonName,To=t.PersonName,t.LoadMinutes}),
            Persisted=false
        },new JsonSerializerOptions {WriteIndented=true}));
    }
    public static void Run()
    {
        var saturday = new DateOnly(2026, 10, 17);
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var m = Guid.NewGuid();
        CapacityWorker[] workers = [new(a,"Alejandra","A","PISO_VENTAS",null,null),
            new(b,"Dennis","B","PISO_VENTAS",null,null), new(m,"Mariana","M","PISO_VENTAS",null,null)];
        var temporal = new TemporalEligibilityContext { Enabled=true, Schedules=workers.Select(w=>new PersonalScheduleVersion {
            Id=Guid.NewGuid(),PersonId=w.Id,Revision=1,DaysJson=JsonSerializer.Serialize(new Dictionary<int,WorkInterval[]> {
                [6]=w.Id==m ? [new(660,900),new(960,1260)] : w.Id==a ? [new(540,840),new(960,1140)] : [new(540,780),new(900,1140)],
                [0]=w.Id==m ? [new(600,900)] : [] }) }).ToArray() };
        OperationalAssignment Task(Guid person,string code,int minutes,int hour,DateOnly? day=null) => new() {
            Id=Guid.NewGuid(),TaskId=Guid.NewGuid(),TaskCode=code,PersonId=person,PersonName=workers.Single(w=>w.Id==person).Name,
            AssignedRole="PISO_VENTAS",LoadMinutes=minutes,StartsAt=OperationalSchedule.Instant(day??saturday,hour),
            DueAt=OperationalSchedule.Instant(day??saturday,23),ScheduleKey=code,PeriodType="SEMANAL" };
        OperationalAssignment[] Saturday() => [Task(a,"01-opening",10,9),Task(a,"02-floor",15,9),Task(a,"03-glass",10,9),
            Task(a,"04-straps",10,9),Task(b,"05-sweep",15,9),Task(b,"06-mop",15,9),Task(b,"07-outside",20,12),
            Task(m,"08-curtain",5,14),Task(a,"09-sidewalk",15,18),Task(m,"10-raise",5,18),Task(m,"11-close",5,20)];
        CapacityLedger Ledger(OperationalAssignment[] assignments, OperationalCapacity[]? capacities=null, HashSet<(Guid,DateOnly)>? absent=null) =>
            new(workers,assignments,capacities??[],absent??[],[],saturday) { Temporal=temporal };
        var tasks=Saturday(); var dates=tasks.Select(t=>(t.Id,t.TaskId,t.StartsAt,t.DueAt,t.LoadMinutes,t.ScheduleKey)).ToArray();
        var now=OperationalSchedule.Instant(saturday.AddDays(-1),12);
        OperationalPlanBalancer.Balance(Ledger(tasks),tasks,true,now,default);
        long Total(Guid person)=>tasks.Where(t=>t.PersonId==person).Sum(t=>(long)t.LoadMinutes);
        Console.WriteLine($"Saturday loads: {Total(a)}/{Total(b)}/{Total(m)}");
        Check(new[] {Total(a),Total(b),Total(m)}.Order().SequenceEqual(new long[] {40,40,45}),"Saturday 60/50/15 becomes 40/40/45 without moving activations");
        Check(tasks.All(t=>temporal.Evaluate(t.PersonId,t,[]).Eligible),"Every final recipient is present at activation, including lunch and closing");
        Check(tasks.Where(t=>t.StartsAt==OperationalSchedule.Instant(saturday,9)).All(t=>t.PersonId!=m),"Morning work never moves to Mariana to improve balance");
        Check(dates.SequenceEqual(tasks.Select(t=>(t.Id,t.TaskId,t.StartsAt,t.DueAt,t.LoadMinutes,t.ScheduleKey))),"Identity, dates, workload and repetition keys are unchanged");
        var repeated=Saturday(); OperationalPlanBalancer.Balance(Ledger(repeated),repeated,true,now,default);
        Check(tasks.Select(t=>(t.TaskCode,t.PersonId)).SequenceEqual(repeated.Select(t=>(t.TaskCode,t.PersonId))),"Final balance is deterministic independently of new assignment IDs");
        var sunday=new[] {Task(m,"sun-opening",10,10,saturday.AddDays(1)),Task(m,"sun-floor",15,10,saturday.AddDays(1)),Task(m,"sun-sidewalk",15,10,saturday.AddDays(1))};
        OperationalPlanBalancer.Balance(Ledger(sunday),sunday,true,now,default);
        Check(sunday.All(t=>t.PersonId==m),"The only Sunday worker retains all Sunday work");
        var saved=Task(a,"saved",100,9); var savedJson=JsonSerializer.Serialize(saved);
        var newWork=new[] {Task(a,"new",20,12)};
        OperationalPlanBalancer.Balance(Ledger([saved,..newWork]),newWork,true,now,default);
        Check(JsonSerializer.Serialize(saved)==savedJson && newWork[0].PersonId!=a,"Existing assignments count toward balance and are never changed");
        var zero=workers.Where(w=>w.Id!=a).Select(w=>new OperationalCapacity { PersonId=w.Id,From=saturday,Through=saturday,Percent=0 }).ToArray();
        newWork=[Task(a,"limited",20,12)];
        OperationalPlanBalancer.Balance(Ledger([saved,..newWork],zero),newWork,true,now,default);
        Check(newWork[0].PersonId==a,"Zero capacity cannot receive balanced work");
        newWork=[Task(a,"absent",20,12)];
        OperationalPlanBalancer.Balance(Ledger([saved,..newWork],absent:[(b,saturday),(m,saturday)]),newWork,true,now,default);
        Check(newWork[0].PersonId==a,"An absent person cannot receive balanced work");
        var otherRole=workers.Select(w=>w.Id==m?w with {Role="ADMINISTRACION"}:w).ToArray();
        newWork=[Task(a,"role",20,12)];
        var roleLedger=new CapacityLedger(otherRole,[saved,..newWork],[],[(b,saturday)],[],saturday) { Temporal=temporal };
        OperationalPlanBalancer.Balance(roleLedger,newWork,true,now,default);
        Check(newWork[0].PersonId==a,"Balancing never crosses required roles");
        var specialty=Guid.NewGuid(); newWork=[Task(a,"specialty",20,12)]; newWork[0].SpecializationId=specialty;
        var specialistWorkers=workers.Select(w=>w.Id==a?w with {SpecializationIds=[specialty]}:w).ToArray();
        OperationalPlanBalancer.Balance(new(specialistWorkers,[saved,..newWork],[],[],[],saturday) {Temporal=temporal},newWork,true,now,default);
        Check(newWork[0].PersonId==a,"Balancing never crosses required specializations");
        Console.WriteLine("Daily balance checks passed.");
    }
    private static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS: "+message); }
}
