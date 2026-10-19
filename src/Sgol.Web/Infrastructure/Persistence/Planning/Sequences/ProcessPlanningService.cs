using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Evidence.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class ProcessPlanInput
{
    public Guid Key { get; set; }=Guid.CreateVersion7();
    public string Name { get; set; }="";
    public Guid ProcessId { get; set; }
    public string Mode { get; set; }="CALENDARIO";
    public DateOnly From { get; set; }
    public DateOnly Through { get; set; }
    public Dictionary<int,Guid> People { get; set; }=[];
    public string? Fingerprint { get; set; }
}
public sealed record ProcessPlanStage(int Position,string Name,string Role,Guid PersonId,string PersonName,int Minutes,int DeadlineHours);
public sealed record ProcessPlanPreview(Guid VersionId,int Version,string ProcessName,DateTimeOffset[] Starts,
    ProcessPlanStage[] Stages,string Fingerprint);
public sealed record ProcessPlanConfiguration(Guid StartId,string Name,int Version,ProcessPlanStage[] Stages);
public sealed record ProcessPlanView(DirectionPreparedPlan Plan,PreparedProcessStart[] Starts,OperationalSequenceRun[] Runs,ProcessPlanConfiguration[] Configurations);

public sealed class ProcessPlanningService(SgolDbContext db,IDirectionPreparedPlanService direction,IClock clock,
    IFileTechnicalValidator validator,IFileMalwareScanner scanner)
{
    public async Task<ProcessPlanPreview> PreviewAsync(Guid actor,ProcessPlanInput input,CancellationToken token)
    {
        await direction.AuthorizeAsync(actor,token);
        if(input.Key==Guid.Empty || string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length>120 || input.Name.Any(char.IsControl) ||
            input.From.Year<1 || input.Through<input.From || input.Through.DayNumber-input.From.DayNumber>366 ||
            input.Mode is not ("CALENDARIO" or "OCASION")) throw Error("PROCESS_INVALID");
        var root=await db.Set<OperationalSequence>().AsNoTracking().SingleOrDefaultAsync(p=>p.Id==input.ProcessId && !p.IsGeneral && p.Enabled,token)
            ?? throw Error("PROCESS_OPERATION_NOT_READY",409);
        var version=await db.Set<OperationalSequenceVersion>().AsNoTracking().SingleAsync(v=>v.Id==root.PublishedVersionId && v.PublishedAt!=null,token);
        if(!version.StartHourConfirmed) throw Error("PROCESS_RULES_INVALID");
        var steps=await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s=>s.VersionId==version.Id).OrderBy(s=>s.Position).ToArrayAsync(token);
        var tasks=steps.Select(s=>JsonSerializer.Deserialize<OperationalTask>(s.TaskSnapshot!)!).ToArray();
        var today=WeekContract.LocalToday(clock.UtcNow);
        if(input.From<today || input.People.Count!=steps.Length || steps.Any(s=>!input.People.ContainsKey(s.Position))) throw Error("PROCESS_PLAN_PEOPLE_REQUIRED");
        var starts=(input.Mode=="OCASION" ? new[] { input.From } : OperationalSequenceRules.Dates(input.From,input.Through,version).ToArray())
            .Select(d=>OperationalSchedule.Instant(d,version.StartHour)).ToArray();
        if(starts.Length==0) throw Error("PROCESS_PLAN_NO_DATES");
        if(await db.OperationalTasks.AnyAsync(t=>tasks.Select(x=>x.Id).Contains(t.Id) && (t.ProcessId!=root.Id || !t.Active || t.Deleted),token)) throw Error("PROCESS_MEMBERSHIP_INVALID");
        var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,token);
        var snapshot=ledger;
        var workers=steps.Select(s=>ledger.Workers.SingleOrDefault(w=>w.Id==input.People[s.Position] && w.EligibleFor(tasks[s.Position-1].AssignedRole, tasks[s.Position-1].SpecializationId))
            ?? throw Error("OCCASION_RECIPIENT_INVALID")).ToArray();
        var rows=steps.Select((s,i)=>new ProcessPlanStage(s.Position,tasks[i].Name,tasks[i].AssignedRole,workers[i].Id,workers[i].Name,tasks[i].LoadMinutes,s.DeadlineHours)).ToArray();
        foreach(var startsAt in starts)
        {
            var startKey=input.Mode=="CALENDARIO" ? $"C:{root.Id:N}:{WeekContract.LocalToday(startsAt):yyyyMMdd}" : $"P:{input.Key:N}:{root.Id:N}";
            if(await db.Set<PreparedProcessStart>().AnyAsync(p=>p.StartKey==startKey && p.PlanId!=input.Key,token) ||
                await db.Set<OperationalSequenceRun>().AnyAsync(r=>r.StartKey==startKey,token)) throw Error("PLAN_OCCURRENCES_RESERVED");
            foreach(var reservation in Reservations(input.Key,version.Id,startsAt,input.People,steps))
            {
                if(!ledger.CanAssign(reservation.PersonId,reservation)) throw Error("PLAN_NO_AVAILABLE_WORKER");
                ledger=new(ledger.Workers,ledger.Tasks.Append(reservation).ToArray(),ledger.Capacities,ledger.Unavailable,ledger.Legacy,ledger.Today) { Temporal = ledger.Temporal };
            }
        }
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { input.Key,Name=input.Name.Trim(),input.ProcessId,input.Mode,input.From,input.Through,
            People=input.People.OrderBy(p=>p.Key),root.Revision,version,steps,starts,snapshot.Workers,snapshot.Tasks,snapshot.Capacities,snapshot.Temporal,
            Unavailable=snapshot.Unavailable.OrderBy(a=>a.Item1).ThenBy(a=>a.Item2).Select(a=>new { a.Item1,a.Item2 }),
            Legacy=snapshot.Legacy.OrderBy(a=>a.Key) })));
        return new(version.Id,version.Number,version.Name,starts,rows,hash);
    }

    public async Task<Guid> PrepareAsync(Guid actor,ProcessPlanInput input,CancellationToken token)
    {
        await direction.AuthorizeAsync(actor,token);
        await using var tx=await db.Database.BeginTransactionAsync(token); await OperationalTaskService.LockAsync(db,token); await direction.AuthorizeAsync(actor,token);
        var previous=await db.DirectionPreparedPlans.SingleOrDefaultAsync(p=>p.Id==input.Key,token);
        if(previous!=null)
        {
            var audit=await db.AuditEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.ResourceId==input.Key && e.Action=="PROCESS_PLAN_PREPARED",token);
            if(previous.CreatedBy!=actor || audit?.AfterData?.RootElement.GetProperty("Fingerprint").GetString()!=input.Fingerprint) throw Error("PROCESS_CONFLICT",409);
            await tx.CommitAsync(token); return previous.Id;
        }
        var preview=await PreviewAsync(actor,input,token);
        if(input.Fingerprint!=preview.Fingerprint) throw Error("PLAN_CAPACITY_STALE",409);
        var end=WeekContract.LocalToday(preview.Starts.Last().AddHours(preview.Stages.Sum(s=>s.DeadlineHours)));
        var plan=DirectionPreparedPlan.ForProcess(input.Key,BranchScope.LorettaId,actor,clock.UtcNow,input.Name.Trim(),input.From,end>input.Through?end:input.Through); db.Add(plan);
        var steps=await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s=>s.VersionId==preview.VersionId).OrderBy(s=>s.Position).ToArrayAsync(token);
        foreach(var step in steps)
        {
            var task=JsonSerializer.Deserialize<OperationalTask>(step.TaskSnapshot!)!;
            db.Add(new DirectionPreparedPlanItem(plan.Id,task.TaskCode,task.Name,step.Position-1));
        }
        foreach(var start in preview.Starts) db.Add(new PreparedProcessStart { Id=Guid.CreateVersion7(),PlanId=plan.Id,ProcessId=input.ProcessId,VersionId=preview.VersionId,StartsAt=start,
            StartKey=input.Mode=="CALENDARIO" ? $"C:{input.ProcessId:N}:{WeekContract.LocalToday(start):yyyyMMdd}" : $"P:{input.Key:N}:{input.ProcessId:N}",Participants=JsonSerializer.Serialize(input.People) });
        Audit(actor,plan.Id,"PROCESS_PLAN_PREPARED",new { preview.Fingerprint,preview.VersionId,input.Mode,input.From,input.Through });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token); return plan.Id;
    }

    public async Task<Guid[]> ActivateAsync(Guid actor,Guid planId,CancellationToken token)
    {
        await direction.AuthorizeAsync(actor,token);
        await using var tx=db.Database.CurrentTransaction==null?await db.Database.BeginTransactionAsync(token):null;
        await OperationalTaskService.LockAsync(db,token); await direction.AuthorizeAsync(actor,token);
        var plan=await db.DirectionPreparedPlans.SingleOrDefaultAsync(p=>p.Id==planId && p.BranchId==BranchScope.LorettaId,token) ?? throw Error("DIRECTION_PLAN_NOT_FOUND",404);
        var starts=await db.Set<PreparedProcessStart>().Where(s=>s.PlanId==planId).OrderBy(s=>s.StartsAt).ToArrayAsync(token);
        if(starts.Length==0) throw Error("PROCESS_NOT_FOUND",404);
        if(plan.Status=="ASIGNADO") { if(tx!=null) await tx.CommitAsync(token); return await db.Set<OperationalSequenceRun>().Where(r=>r.PlanId==planId).Select(r=>r.Id).ToArrayAsync(token); }
        // Remove prepared reservations inside the same transaction, then materialize run reservations/assignments.
        foreach(var start in starts) { start.Status="ACTIVADO"; start.Revision++; }
        await db.SaveChangesAsync(token);
        var result=new List<Guid>();
        var engine=new OperationalProcessService(db,direction,clock,validator,scanner);
        foreach(var start in starts) result.Add(await engine.StartAsync(actor,start.ProcessId,start.Id,start.StartsAt,JsonSerializer.Deserialize<Dictionary<int,Guid>>(start.Participants)!,token,planId,start.VersionId,start.StartKey));
        plan.MarkAssigned(); Audit(actor,planId,"PROCESS_PLAN_ACTIVATED",new { RunIds=result }); await db.SaveChangesAsync(token);
        if(tx!=null) await tx.CommitAsync(token); return result.ToArray();
    }

    public async Task<ProcessPlanView> ReadAsync(Guid actor,Guid planId,CancellationToken token)
    {
        await TaskCatalogAuthorization.RequireReadAsync(db,actor,clock.UtcNow,token);
        var plan=await db.DirectionPreparedPlans.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==planId && p.BranchId==BranchScope.LorettaId,token) ?? throw Error("DIRECTION_PLAN_NOT_FOUND",404);
        var starts=await db.Set<PreparedProcessStart>().AsNoTracking().Where(s=>s.PlanId==planId).OrderBy(s=>s.StartsAt).ToArrayAsync(token);
        var versions=await db.Set<OperationalSequenceVersion>().AsNoTracking().Where(v=>starts.Select(s=>s.VersionId).Contains(v.Id)).ToArrayAsync(token);
        var steps=await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s=>versions.Select(v=>v.Id).Contains(s.VersionId)).OrderBy(s=>s.Position).ToArrayAsync(token);
        var names=await db.People.AsNoTracking().ToDictionaryAsync(p=>p.Id,p=>p.DisplayName,token);
        var configurations=starts.Select(start=> {
            var version=versions.Single(v=>v.Id==start.VersionId);
            var people=JsonSerializer.Deserialize<Dictionary<int,Guid>>(start.Participants)!;
            return new ProcessPlanConfiguration(start.Id,version.Name,version.Number,steps.Where(s=>s.VersionId==version.Id).Select(step=> {
                var task=JsonSerializer.Deserialize<OperationalTask>(step.TaskSnapshot!)!;
                return new ProcessPlanStage(step.Position,task.Name,task.AssignedRole,people[step.Position],names.GetValueOrDefault(people[step.Position],"Responsable registrado"),task.LoadMinutes,step.DeadlineHours);
            }).ToArray());
        }).ToArray();
        return new(plan,starts,await db.Set<OperationalSequenceRun>().AsNoTracking().Where(r=>r.PlanId==planId).ToArrayAsync(token),configurations);
    }

    internal static IEnumerable<OperationalAssignment> Reservations(Guid id,Guid version,DateTimeOffset starts,IReadOnlyDictionary<int,Guid> people,OperationalSequenceStep[] steps)
    {
        var planned=starts;
        foreach(var step in steps)
        {
            var task=JsonSerializer.Deserialize<OperationalTask>(step.TaskSnapshot!)!;
            var digest=SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{id:N}:{starts:O}:{step.Position}"));
            yield return new OperationalAssignment { Id=new Guid(digest.AsSpan(0,16)),TaskId=task.Id,TaskCode=task.TaskCode,TaskRevision=task.Revision,
                Name=task.Name,Description=task.Description,AssignedRole=task.AssignedRole,PersonId=people[step.Position],LoadMinutes=task.LoadMinutes,Requirement=task.Requirement,
                StartsAt=planned,DueAt=planned.AddHours(step.DeadlineHours),SpecializationId=task.SpecializationId,PeriodType="OCASIONAL" };
            planned=planned.AddHours(step.DeadlineHours);
        }
    }
    internal static async Task<OperationalAssignment[]> PreparedReservationsAsync(SgolDbContext db,CancellationToken token)
    {
        var starts=await db.Set<PreparedProcessStart>().AsNoTracking().Where(s=>s.Status=="PREPARADO").OrderBy(s=>s.Id).ToArrayAsync(token);
        var ids=starts.Select(s=>s.VersionId).ToArray();
        var steps=await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s=>ids.Contains(s.VersionId)).OrderBy(s=>s.Position).ToArrayAsync(token);
        return starts.SelectMany(s=>Reservations(s.Id,s.VersionId,s.StartsAt,JsonSerializer.Deserialize<Dictionary<int,Guid>>(s.Participants)!,steps.Where(p=>p.VersionId==s.VersionId).ToArray())
            .Select(r=> { r.PlanId=s.PlanId; return r; })).ToArray();
    }
    private void Audit(Guid actor,Guid plan,string action,object data)=>db.AuditEvents.Add(new AuditEvent { Id=Guid.CreateVersion7(),OccurredAt=clock.UtcNow,ActorUserId=actor,ActorType="USER",
        Action=action,ResourceType="DIRECTION_PREPARED_PLAN",ResourceId=plan,BranchId=BranchScope.LorettaId,CorrelationId=Guid.CreateVersion7(),Outcome="SUCCESS",AfterData=JsonSerializer.SerializeToDocument(data) });
    private static DirectionPlanException Error(string code,int status=422)=>new(code,status);
}
