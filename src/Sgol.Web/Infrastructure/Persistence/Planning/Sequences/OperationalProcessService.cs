using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Evidence.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record ProcessChecklistEvidence(string Definition, string Draft);
public sealed record ProcessRunView(OperationalSequenceRun Run, OperationalSequenceStage[] Stages,
    OperationalSequenceAttempt[] Attempts, OperationalSequenceDocument[] Documents, OperationalSequenceEvent[] Events,OperationalSequenceStep[] Rules)
{
    public Dictionary<Guid, ProcessChecklistEvidence> ChecklistEvidence { get; init; } = [];
}

public sealed partial class OperationalProcessService(SgolDbContext db, IDirectionPreparedPlanService direction, IClock clock,
    IFileTechnicalValidator validator, IFileMalwareScanner scanner)
{
    public async Task<Guid> StartAsync(Guid actor, Guid processId, Guid key, DateTimeOffset starts,
        IReadOnlyDictionary<int, Guid> people, CancellationToken token, Guid? preparedPlanId=null, Guid? capturedVersionId=null, string? capturedStartKey=null)
    {
        await direction.AuthorizeAsync(actor, token);
        if (key == Guid.Empty || starts.Offset != TimeSpan.Zero || people.Count == 0) throw Error("PROCESS_INVALID");
        await using var tx = db.Database.CurrentTransaction==null ? await db.Database.BeginTransactionAsync(token) : null;
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var root = await db.Set<OperationalSequence>().SingleOrDefaultAsync(p => p.Id == processId && !p.IsGeneral && p.Enabled, token) ?? throw Error("PROCESS_OPERATION_NOT_READY",409);
        var startKey = capturedStartKey ?? $"O:{processId:N}:{key:N}";
        var existing = await db.Set<OperationalSequenceRun>().SingleOrDefaultAsync(r => r.StartKey == startKey, token);
        if (existing != null) { if(tx!=null) await tx.CommitAsync(token); return existing.Id; }
        var version = await db.Set<OperationalSequenceVersion>().AsNoTracking().SingleAsync(v => v.SequenceId==processId && v.Id == (capturedVersionId ?? root.PublishedVersionId) && v.PublishedAt != null, token);
        var steps = await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == version.Id).OrderBy(s => s.Position).ToArrayAsync(token);
        if (steps.Length == 0 || steps.Any(s => s.TaskSnapshot == null || !people.ContainsKey(s.Position))) throw Error("PROCESS_RULES_INVALID");
        if (version.AgendaVersion != null && preparedPlanId == null) throw Error("PROCESS_PREPARE_REQUIRED");
        var tasks = steps.Select(s => JsonSerializer.Deserialize<OperationalTask>(s.TaskSnapshot!)!).ToArray();
        if (await db.OperationalTasks.AnyAsync(t => tasks.Select(x => x.Id).Contains(t.Id) && (t.ProcessId != processId || !t.Active || t.Deleted), token)) throw Error("PROCESS_MEMBERSHIP_INVALID");
        var latest = starts.AddHours(steps.Sum(s => s.DeadlineHours));
        var plan = preparedPlanId is { } planId ? await db.DirectionPreparedPlans.SingleAsync(p=>p.Id==planId && p.BranchId==BranchScope.LorettaId,token) :
            DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, actor, clock.UtcNow, version.Name,WeekContract.LocalToday(starts),WeekContract.LocalToday(latest));
        if (preparedPlanId == null && version.AgendaVersion != null) plan.CaptureSchedulingPolicy((await OperationalSchedulingPolicyService.CurrentAsync(db, token)).Id);
        var schedulingPolicy = plan.SchedulingPolicyId is {} pid ? await db.Set<OperationalSchedulingPolicy>().AsNoTracking().SingleAsync(p => p.Id == pid, token) : null;
        var capturedAgenda = preparedPlanId is {} preparedId ? await db.Set<PreparedProcessStart>().AsNoTracking().Where(p => p.PlanId == preparedId && p.StartKey == startKey).Select(p => p.AgendaJson).SingleOrDefaultAsync(token) : null;
        var preparedAssignments = capturedAgenda == null ? null : JsonSerializer.Deserialize<OperationalAssignment[]>(capturedAgenda);
        if(preparedPlanId==null) db.Add(plan);
        var run = new OperationalSequenceRun { Id = Guid.CreateVersion7(), VersionId = version.Id, PlanId = plan.Id,
            UsesTaskEvidence = tasks.Any(t => t.Requirement != "DOCUMENTO"),
            Name = version.Name, StartKey = startKey, PlannedStartsAt = starts, CreatedAt = clock.UtcNow, CreatedBy = actor }; db.Add(run);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, token);
        var planned = starts;
        var links = new List<(OperationalSequenceStage Stage, Guid Attempt, OperationalSequenceEvent? Start)>();
        for (var i=0; i<steps.Length; i++)
        {
            var task = tasks[i]; var rule=steps[i]; task.Validate();
            var stage = new OperationalSequenceStage { Id=Guid.CreateVersion7(), RunId=run.Id, VersionId=version.Id, Position=rule.Position,
                TaskId=task.Id, TaskRevision=task.Revision, DefinitionSnapshotJson=AssignmentDefinition.Capture(task), TaskCode=task.TaskCode, Name=task.Name, Description=task.Description, Role=task.AssignedRole, Requirement=task.Requirement, ChecklistJson = task.ChecklistJson,
                SpecializationId=task.SpecializationId, LoadMinutes=task.LoadMinutes, MinimumPhotos=task.MinimumPhotos, MaximumPhotos=task.MaximumPhotos,
                MaximumPhotoBytes=task.MaximumPhotoBytes, PlannedStartsAt=planned, PlannedDueAt=planned.AddHours(rule.DeadlineHours),
                MinimumStartsAt=rule.MinimumStartOffsetHours is { } offset ? starts.AddHours(offset) : i==0 ? starts : null,
                FixedDueAt=rule.DeadlineKind=="FIJO" ? starts.AddHours(rule.DeadlineHours) : null };
            var person=people[rule.Position]; var worker=ledger.Workers.SingleOrDefault(w=>w.Id==person && w.EligibleFor(stage.Role, stage.SpecializationId));
            if(worker==null && (preparedPlanId==null || !ledger.Temporal.Enabled)) throw Error("OCCASION_RECIPIENT_INVALID");
            stage.ReservedPersonId=person; stage.ReservedPersonName=worker?.Name ?? await db.People.Where(p=>p.Id==person).Select(p=>p.DisplayName).SingleAsync(token);
            if (stage.SpecializationId is { } specialty) stage.SpecializationName=await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().Where(s=>s.Id==specialty).Select(s=>s.Name).SingleAsync(token);
            if (schedulingPolicy != null) {
                var captured = preparedAssignments?.Single(a => a.TaskId == task.Id);
                var anchoredMinimum = rule.MinimumStartOffsetHours is {} hours ? starts.AddHours(hours) : starts;
                var nominal = captured?.NominalStartsAt ?? (task.OperationalCriticality == "CRITICA" ? anchoredMinimum : new[] { planned, anchoredMinimum }.Max());
                if (task.OperationalCriticality == "CRITICA" && planned > nominal) throw Error("SCHEDULING_CRITICAL_OVERLAP");
                stage.SchedulingPolicyId = schedulingPolicy.Id; stage.ClosureMarginPercent = schedulingPolicy.MarginPercent; stage.OperationalCriticality = task.OperationalCriticality; stage.NominalStartsAt = nominal;
                var projected = Reservation(stage, plan.Id); OperationalAgenda.Capture(projected, schedulingPolicy, task.OperationalCriticality, nominal);
                if (captured != null) { projected.StartsAt = captured.StartsAt; projected.DueAt = captured.DueAt; }
                else OperationalAgenda.Apply(projected, OperationalAgenda.Find(ledger, person, projected, clock.UtcNow));
                stage.PlannedStartsAt = projected.StartsAt; stage.PlannedDueAt = projected.DueAt; stage.FixedDueAt = null;
            }
            var reservation=Reservation(stage,plan.Id);
            if((schedulingPolicy != null || preparedPlanId==null || !ledger.Temporal.Enabled) && !ledger.CanAssign(person,reservation)) throw Error("PLAN_NO_AVAILABLE_WORKER");
            ledger = new(ledger.Workers,ledger.Tasks.Append(reservation).ToArray(),ledger.Capacities,ledger.Unavailable,ledger.Legacy,ledger.Today) { Temporal = ledger.Temporal };
            var attempt = NewAttempt(stage,run,1,null,clock.UtcNow);
            db.Add(stage); db.Add(attempt); if(preparedPlanId==null) db.Add(new DirectionPreparedPlanItem(plan.Id,task.TaskCode,task.Name,i));
            planned=stage.PlannedDueAt;
            OperationalSequenceEvent? startEvent=null;
            if(i==0) { startEvent=Event(run,stage,attempt,"INICIO",$"start:{key:N}",actor,clock.UtcNow,null,null); db.Add(startEvent); }
            links.Add((stage,attempt.Id,startEvent));
        }
        await db.SaveChangesAsync(token);
        foreach(var link in links) { link.Stage.CurrentAttemptId=link.Attempt; if(link.Start!=null) { link.Stage.DependencySatisfiedAt=link.Start.OccurredAt; link.Stage.DependencyEventId=link.Start.Id; } }
        await db.SaveChangesAsync(token); await EnableActive(run, token); await db.SaveChangesAsync(token); if(tx!=null) await tx.CommitAsync(token); return run.Id;
    }

    public async Task<ProcessRunView> ReadAsync(Guid actor, Guid runId, CancellationToken token)
    {
        await RequireAccess(actor,runId,token);
        var run=await db.Set<OperationalSequenceRun>().AsNoTracking().SingleAsync(r=>r.Id==runId,token);
        return new(run,await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s=>s.RunId==runId).OrderBy(s=>s.Position).ToArrayAsync(token),
            await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a=>a.RunId==runId).OrderBy(a=>a.CreatedAt).ThenBy(a=>a.Number).ToArrayAsync(token),
            await db.Set<OperationalSequenceDocument>().AsNoTracking().Where(d=>d.RunId==runId).OrderBy(d=>d.Number).ToArrayAsync(token),
            await db.Set<OperationalSequenceEvent>().AsNoTracking().Where(e=>e.RunId==runId).OrderBy(e=>e.OccurredAt).ToArrayAsync(token),
            await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s=>s.VersionId==run.VersionId).OrderBy(s=>s.Position).ToArrayAsync(token)) {
                ChecklistEvidence = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking()
                    .Where(a => a.Requirement == "CHECKLIST" && db.Set<OperationalSequenceAttempt>().Any(t => t.RunId == runId && t.AssignmentId == a.Id))
                    .Select(a => new { a.Id, a.ChecklistJson, a.ChecklistDraftJson })
                    .ToDictionaryAsync(a => a.Id, a => new ProcessChecklistEvidence(a.ChecklistJson, a.ChecklistDraftJson), token)
            };
    }
    public async Task<ProcessRunView?> ReadAssignmentAsync(Guid actor, Guid assignmentId, CancellationToken token)
    {
        var person = await Person(actor, token);
        if (!await db.OperationalAssignments.AnyAsync(a => a.Id == assignmentId && a.PersonId == person, token))
            throw Error("TASK_NOT_FOUND", 404);
        var runId = await db.Set<OperationalSequenceAttempt>().AsNoTracking()
            .Where(a => a.AssignmentId == assignmentId).Select(a => (Guid?)a.RunId).SingleOrDefaultAsync(token);
        return runId is { } id ? await ReadAsync(actor, id, token) : null;
    }

    public async Task<OperationalSequenceRun[]> MineAsync(Guid actor,CancellationToken token)
    {
        var person=await Person(actor,token);
        return await db.Set<OperationalSequenceRun>().AsNoTracking().Where(r=>db.Set<OperationalSequenceStage>().Any(s=>s.RunId==r.Id && s.ReservedPersonId==person)
            || db.Set<OperationalSequenceAttempt>().Any(a=>a.RunId==r.Id && db.OperationalAssignments.Any(x=>x.Id==a.AssignmentId && x.PersonId==person)))
            .OrderByDescending(r=>r.CreatedAt).ToArrayAsync(token);
    }

    public async Task DecideAsync(Guid actor,Guid runId,Guid attemptId,long revision,Guid key,string decision,string conclusion,
        string? differences,IFormFile? file,bool declared,CancellationToken token,
        Guid? captureSessionId = null, IReadOnlyList<Guid>? photographIds = null, IReadOnlyList<IFormFile>? screenshots = null, string? checklistAnswer = null)
    {
        if(key==Guid.Empty || !declared || string.IsNullOrWhiteSpace(conclusion) || conclusion.Trim().Length>4000 || differences?.Length>4000 ||
            decision is not ("ENTREGA" or "APROBACION" or "DEVOLUCION" or "SIN_DIFERENCIAS" or "ACEPTADA_CON_DIFERENCIAS")) throw Error("PROCESS_DECISION_INVALID");
        // Reuse the existing technical validation and malware adapter before acquiring the lock.
        var usesTaskEvidence = await db.Set<OperationalSequenceRun>().Where(r => r.Id == runId).Select(r => r.UsesTaskEvidence).SingleAsync(token);
        if (!usesTaskEvidence && (captureSessionId != null || photographIds?.Count > 0 || screenshots?.Count > 0)) throw Error("TASK_FILE_INVALID");
        var taskService = new OperationalTaskService(db,direction,clock,validator,scanner);
        var prepared=usesTaskEvidence || file==null ? null : await taskService.ProcessPdfAsync(file,token);
        async Task<string> FileHash(IFormFile incoming) { await using var stream = incoming.OpenReadStream(); return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,token)); }
        var evidenceHash = usesTaskEvidence ? JsonSerializer.Serialize(new {
            File = file == null ? null : await FileHash(file), captureSessionId, photographIds, checklistAnswer,
            Screenshots = screenshots == null ? null : await Task.WhenAll(screenshots.Select(FileHash)) }) : null;
        var requestBytes = usesTaskEvidence ? JsonSerializer.SerializeToUtf8Bytes(new { actor,attemptId,decision,
            conclusion=conclusion.Trim(),differences=differences?.Trim(),declared,evidenceHash }) :
            JsonSerializer.SerializeToUtf8Bytes(new { actor,attemptId,decision,
            conclusion=conclusion.Trim(),differences=differences?.Trim(),declared,FileHash=prepared==null ? null : Convert.ToHexStringLower(SHA256.HashData(prepared.Content)) });
        var requestHash=Convert.ToHexStringLower(SHA256.HashData(requestBytes));
        await using var tx=await db.Database.BeginTransactionAsync(token); await OperationalTaskService.LockAsync(db,token);
        var person=await Person(actor,token); await RequireAccess(actor,runId,token);
        var run=await db.Set<OperationalSequenceRun>().SingleAsync(r=>r.Id==runId,token);
        var eventKey=$"decision:{key:N}";
        var replay=await db.Set<OperationalSequenceEvent>().SingleOrDefaultAsync(e=>e.RunId==runId && e.EventKey==eventKey,token);
        if(replay!=null) {
            var audit=await db.AuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.ResourceId==runId && a.Action=="PROCESS_DECISION" && a.RequestId==key.ToString("N"),token);
            if(replay.ActorId!=actor || replay.AttemptId!=attemptId || audit?.AfterData?.RootElement.GetProperty("RequestHash").GetString()!=requestHash) throw Error("PROCESS_CONFLICT",409);
            await tx.CommitAsync(token); return; }
        var stage=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.RunId==runId && s.Position==run.ActivePosition,token);
        var attempt=await db.Set<OperationalSequenceAttempt>().SingleOrDefaultAsync(a=>a.Id==attemptId && a.RunId==runId,token) ?? throw Error("PROCESS_CONFLICT",409);
        if(run.Status!="EN_CURSO" || stage.Status!="DISPONIBLE" || stage.CurrentAttemptId!=attemptId || attempt.Revision!=revision || attempt.Status!="ACTIVO") throw Error("PROCESS_CONFLICT",409);
        var assignment=await db.OperationalAssignments.SingleOrDefaultAsync(a=>a.Id==attempt.AssignmentId && a.PersonId==person,token) ?? throw Error("ACCESO_DENEGADO",403);
        var rules=await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s=>s.VersionId==run.VersionId).OrderBy(s=>s.Position).ToArrayAsync(token);
        var rule=rules.Single(s=>s.Position==stage.Position);
        var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,token);
        if(!ledger.Workers.Any(w=>w.Id==person && w.EligibleFor(stage.Role, stage.SpecializationId)) || stage.EnabledAt>clock.UtcNow) throw Error("ACCESO_DENEGADO",403);
        var inputDocument=attempt.InputDocumentId;
        if(decision=="DEVOLUCION")
        {
            OperationalSequenceRules.RequireReturn(run.Status,run.ActivePosition,stage.Position,rule.ReturnToPosition??0,rule.ReturnToPosition,conclusion,true);
            db.Add(Event(run,stage,attempt,"DEVOLUCION",eventKey,actor,clock.UtcNow,inputDocument,conclusion.Trim()));
            attempt.Status="SUSTITUIDO"; attempt.Comment=conclusion.Trim(); attempt.FinishedAt=clock.UtcNow; attempt.Revision++;
            attempt.UnresolvedDifferences=differences?.Trim();
            assignment.ReviewStatus="NO_SATISFECHA"; assignment.ReviewDecision="NO_SATISFECHA"; assignment.ReviewComment=conclusion.Trim(); assignment.ReviewedBy=actor; assignment.ReviewedAt=clock.UtcNow; assignment.Revision++;
            // Returned work no longer consumes an independent assignment slot; the new attempt replaces it.
            assignment.CompletedAt ??=clock.UtcNow; assignment.CompletionKind ??="SUPERVISION"; assignment.CompletedBy ??=actor;
            run.Round++; run.ActivePosition=rule.ReturnToPosition!.Value; run.Revision++;
            foreach(var downstream in await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==runId && s.Position>=run.ActivePosition).ToArrayAsync(token))
            {
                var old=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==downstream.CurrentAttemptId,token);
                old.Status="SUSTITUIDO"; old.Revision++; old.FinishedAt ??=clock.UtcNow;
                if (run.UsesTaskEvidence && old.AssignmentId is { } previousId && previousId != assignment.Id)
                {
                    var previous = await db.OperationalAssignments.SingleAsync(a => a.Id == previousId, token);
                    previous.ReviewStatus = "NO_SATISFECHA"; previous.ReviewDecision = "NO_SATISFECHA";
                    previous.ReviewedBy = actor; previous.ReviewedAt = clock.UtcNow; previous.ReviewComment = conclusion.Trim(); previous.Revision++;
                }
                downstream.Status="EN_ESPERA"; downstream.EnabledAt=null; downstream.EffectiveDueAt=null; downstream.EligibleAt=null; downstream.DependencySatisfiedAt=null; downstream.DependencyEventId=null; downstream.Revision++;
                var next=NewAttempt(downstream,run,(await db.Set<OperationalSequenceAttempt>().Where(a=>a.StageId==downstream.Id).MaxAsync(a=>a.Number,token))+1,inputDocument,clock.UtcNow);
                next.UnresolvedDifferences=attempt.UnresolvedDifferences;
                downstream.CurrentAttemptId=next.Id; db.Add(next);
                if(downstream.Position==run.ActivePosition)
                { var evt=Event(run,downstream,next,"INICIO",$"return:{key:N}",actor,clock.UtcNow,inputDocument,conclusion.Trim()); db.Add(evt); downstream.DependencySatisfiedAt=clock.UtcNow; downstream.DependencyEventId=evt.Id; }
            }
            await db.SaveChangesAsync(token); await EnableActive(run,token);
        }
        else
        {
            if(rule.Action=="ENTREGAR" ? decision!="ENTREGA" : decision=="ENTREGA") throw Error("PROCESS_DECISION_INVALID");
            var final=stage.Position==rules.Length;
            if(final && decision is not ("SIN_DIFERENCIAS" or "ACEPTADA_CON_DIFERENCIAS") || !final && decision is "SIN_DIFERENCIAS" or "ACEPTADA_CON_DIFERENCIAS" ||
                decision=="ACEPTADA_CON_DIFERENCIAS" && string.IsNullOrWhiteSpace(differences) || decision=="SIN_DIFERENCIAS" && !string.IsNullOrWhiteSpace(differences)) throw Error("PROCESS_DECISION_INVALID");
            Guid? document=inputDocument;
            if (run.UsesTaskEvidence)
                await taskService.CompleteProcessEvidenceAsync(actor, assignment.Id, conclusion, file, token, conclusion,
                    captureSessionId, photographIds, screenshots, checklistAnswer);
            if(prepared!=null)
            {
                db.Add(new OperationalAttachment { AssignmentId=assignment.Id,FileName=prepared.FileName,ContentType="application/pdf",Content=prepared.Content,CreatedAt=clock.UtcNow });
                var doc=new OperationalSequenceDocument { Id=Guid.CreateVersion7(),RunId=runId,ProducerAttemptId=attempt.Id,AssignmentId=assignment.Id,
                    Number=(await db.Set<OperationalSequenceDocument>().Where(d=>d.RunId==runId).MaxAsync(d=>(int?)d.Number,token)??0)+1,
                    PreviousDocumentId=inputDocument,CreatedAt=clock.UtcNow,CreatedBy=actor,Sha256=Convert.ToHexStringLower(SHA256.HashData(prepared.Content)) }; db.Add(doc); document=doc.Id;
            }
            if(!run.UsesTaskEvidence && (stage.Requirement=="DOCUMENTO" && document==null || rule.Action is "ENTREGAR" or "SELLAR" && prepared==null)) throw Error("TASK_FILE_REQUIRED");
            if(!run.UsesTaskEvidence && document==null) throw Error("PROCESS_DECISION_INVALID");
            attempt.OutputDocumentId=document; attempt.Status=decision=="ENTREGA" ? "ENTREGADO" : "APROBADO"; attempt.Comment=conclusion.Trim(); attempt.UnresolvedDifferences=differences?.Trim(); attempt.FinishedAt=clock.UtcNow; attempt.Revision++;
            assignment.CompletedAt=clock.UtcNow; assignment.CompletedBy=actor; assignment.CompletionKind="ENTREGA"; assignment.Conclusion=conclusion.Trim(); assignment.ReviewStatus=final ? "CONCLUIDA" : "PENDIENTE_REVISION"; assignment.Revision++;
            if (run.UsesTaskEvidence && stage.Position > 1)
            {
                var previousStage = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.RunId == runId && s.Position == stage.Position - 1, token);
                var previousAttempt = await db.Set<OperationalSequenceAttempt>().SingleAsync(a => a.Id == previousStage.CurrentAttemptId, token);
                var previousAssignment = await db.OperationalAssignments.SingleAsync(a => a.Id == previousAttempt.AssignmentId, token);
                previousAssignment.ReviewStatus = "CONCLUIDA"; previousAssignment.ReviewDecision = "CONCLUIDA";
                previousAssignment.ReviewedAt = clock.UtcNow; previousAssignment.ReviewedBy = actor; previousAssignment.ReviewComment = conclusion.Trim(); previousAssignment.Revision++;
            }
            // Accept the exact input submission in the same transaction as the reviewer's output.
            if(inputDocument!=null)
            {
                var producer=(await db.Set<OperationalSequenceDocument>().SingleAsync(d=>d.Id==inputDocument,token)).AssignmentId;
                var reviewed=await db.OperationalAssignments.SingleAsync(a=>a.Id==producer,token);
                if(reviewed.ReviewStatus!="CONCLUIDA") { reviewed.ReviewStatus="CONCLUIDA"; reviewed.ReviewDecision="CONCLUIDA"; reviewed.ReviewedAt=clock.UtcNow; reviewed.ReviewedBy=actor; reviewed.ReviewComment=conclusion.Trim(); reviewed.Revision++; }
            }
            stage.Status="CONCLUIDA"; stage.Revision++; run.Revision++;
            var kind=final ? "CIERRE" : decision; var evt=Event(run,stage,attempt,kind,eventKey,actor,clock.UtcNow,document,conclusion.Trim()); db.Add(evt);
            if(final) { run.Status="CONCLUIDA"; run.ClosedAt=clock.UtcNow; run.ClosedBy=actor; run.AuthorizedDocumentId=document; run.ClosureOutcome=decision; run.ClosureComment=conclusion.Trim(); }
            else
            {
                var next=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.RunId==runId && s.Position==stage.Position+1,token);
                if(!OperationalSequenceRules.Satisfies(rules[stage.Position].ReleaseCondition,decision,"ENTREGA",true,true)) throw Error("PROCESS_RULES_INVALID");
                next.DependencySatisfiedAt=clock.UtcNow; next.DependencyEventId=evt.Id;
                var nextAttempt=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==next.CurrentAttemptId,token); nextAttempt.InputDocumentId=document; nextAttempt.UnresolvedDifferences=attempt.UnresolvedDifferences;
                run.ActivePosition=next.Position; await db.SaveChangesAsync(token); await EnableActive(run,token);
            }
        }
        db.AuditEvents.Add(new AuditEvent { Id=Guid.CreateVersion7(),OccurredAt=clock.UtcNow,ActorUserId=actor,ActorType="USER",
            Action="PROCESS_DECISION",ResourceType="OPERATIONAL_PROCESS",ResourceId=runId,BranchId=BranchScope.LorettaId,
            CorrelationId=key,RequestId=key.ToString("N"),Outcome="SUCCESS",AfterData=JsonSerializer.SerializeToDocument(new { RequestHash=requestHash,attemptId,decision }) });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    public async Task TickAsync(CancellationToken token)
    {
        await using var tx=await db.Database.BeginTransactionAsync(token); await OperationalTaskService.LockAsync(db,token);
        foreach(var run in await db.Set<OperationalSequenceRun>().Where(r=>r.Status=="EN_CURSO").ToArrayAsync(token)) await EnableActive(run,token);
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    public async Task ReassignAsync(Guid actor,Guid runId,long revision,Guid key,IReadOnlyDictionary<int,Guid> people,
        string reason,CapacityChange? capacity,CancellationToken token,bool systemAction=false)
    {
        await direction.AuthorizeAsync(actor,token);
        if(key==Guid.Empty || people.Count==0 || string.IsNullOrWhiteSpace(reason) || reason.Length>1000) throw Error("PROCESS_INVALID");
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { actor,runId,People=people.OrderBy(p=>p.Key),reason,capacity,systemAction })));
        await using var tx=await db.Database.BeginTransactionAsync(token); await OperationalTaskService.LockAsync(db,token); await direction.AuthorizeAsync(actor,token);
        var replay=await db.AuditEvents.SingleOrDefaultAsync(a=>a.ResourceId==runId && a.Action=="PROCESS_REASSIGNED" && a.RequestId==key.ToString("N"),token);
        if(replay!=null) { if(replay.AfterData?.RootElement.GetProperty("RequestHash").GetString()!=hash) throw Error("PROCESS_CONFLICT",409); await tx.CommitAsync(token); return; }
        var run=await db.Set<OperationalSequenceRun>().SingleOrDefaultAsync(r=>r.Id==runId,token) ?? throw Error("PROCESS_NOT_FOUND",404);
        if(run.Status!="EN_CURSO" || run.Revision!=revision) throw Error("PROCESS_CONFLICT",409);
        var stages=await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==runId && people.Keys.Contains(s.Position)).OrderBy(s=>s.Position).ToArrayAsync(token);
        if(stages.Length!=people.Count || stages.Any(s=>s.Position<run.ActivePosition || s.Status is "CONCLUIDA" or "CANCELADA" or "FUERA_DE_PLAZO")) throw Error("PROCESS_CONFLICT",409);
        var before=JsonSerializer.SerializeToDocument(stages);
        var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,token);
        if(capacity!=null)
        {
            if(!people.Values.Contains(capacity.PersonId) || capacity.From<WeekContract.LocalToday(clock.UtcNow) || capacity.Through<capacity.From ||
                capacity.Through.DayNumber-capacity.From.DayNumber>90 || capacity.Percent is <0 or >100 || capacity.DailyMinutes is <1 or >480 ||
                string.IsNullOrWhiteSpace(capacity.Reason) || capacity.Reason.Length>1000) throw Error("CAPACITY_INVALID");
            var oldSettings=JsonSerializer.SerializeToDocument(ledger.Capacities.Where(c=>c.PersonId==capacity.PersonId));
            var setting=await OperationalCapacityService.StoreSettingAsync(db,capacity,token);
            db.Add(new AuditEvent { Id=Guid.CreateVersion7(),OccurredAt=clock.UtcNow,ActorUserId=systemAction?null:actor,ActorType=systemAction?"SYSTEM":"USER",
                Action="PROCESS_CAPACITY_ADJUSTED",ResourceType="OPERATIONAL_CAPACITY",ResourceId=setting.Id,BranchId=BranchScope.LorettaId,
                CorrelationId=key,Outcome="SUCCESS",Reason=reason,BeforeData=oldSettings,AfterData=JsonSerializer.SerializeToDocument(capacity) });
            ledger.Override=capacity;
        }
        var attempts=await db.Set<OperationalSequenceAttempt>().Where(a=>stages.Select(s=>s.CurrentAttemptId).Contains(a.Id)).ToArrayAsync(token);
        var removeIds=attempts.Where(a=>a.AssignmentId!=null).Select(a=>a.AssignmentId!.Value).Concat(stages.Select(s=>s.Id)).ToHashSet();
        ledger=new(ledger.Workers,ledger.Tasks.Where(a=>!removeIds.Contains(a.Id)).ToArray(),ledger.Capacities,ledger.Unavailable,ledger.Legacy,ledger.Today) { Temporal = ledger.Temporal, Override=capacity };
        var links=new List<(OperationalSequenceStage Stage,OperationalSequenceAttempt Attempt)>();
        foreach(var stage in stages)
        {
            var target=ledger.Workers.SingleOrDefault(w=>w.Id==people[stage.Position] && w.EligibleFor(stage.Role, stage.SpecializationId)) ?? throw Error("OCCASION_RECIPIENT_INVALID");
            var previous=attempts.Single(a=>a.Id==stage.CurrentAttemptId);
            var projected=Reservation(stage,run.PlanId); projected.PersonId=target.Id; projected.PersonName=target.Name; projected.AssignedRole=target.Role;
            var active=stage.Status=="DISPONIBLE";
            if(active) { projected.StartsAt=stage.SchedulingPolicyId == null ? clock.UtcNow : stage.EnabledAt ?? stage.PlannedStartsAt; projected.DueAt=stage.EffectiveDueAt!.Value; if(projected.DueAt<=clock.UtcNow) throw Error("PROCESS_CONFLICT",409); }
            if(!ledger.CanAssign(target.Id,projected,active?clock.UtcNow:null)) throw Error("PLAN_NO_AVAILABLE_WORKER");
            ledger=new(ledger.Workers,ledger.Tasks.Append(projected).ToArray(),ledger.Capacities,ledger.Unavailable,ledger.Legacy,ledger.Today) { Temporal = ledger.Temporal, Override=capacity };
            previous.Status="SUSTITUIDO"; previous.Comment=reason; previous.FinishedAt=clock.UtcNow; previous.Revision++;
            var next=NewAttempt(stage,run,(await db.Set<OperationalSequenceAttempt>().Where(a=>a.StageId==stage.Id).MaxAsync(a=>a.Number,token))+1,previous.InputDocumentId,clock.UtcNow);
            next.UnresolvedDifferences=previous.UnresolvedDifferences;
            stage.ReservedPersonId=target.Id; stage.ReservedPersonName=target.Name; stage.Revision++;
            if(active)
            {
                var old=await db.OperationalAssignments.SingleAsync(a=>a.Id==previous.AssignmentId,token);
                if(old.CompletedAt!=null) throw Error("PROCESS_CONFLICT",409);
                // Reuse the supervised reassignment lifecycle; this is not an ENTREGA event.
                old.CompletedAt=clock.UtcNow; old.CompletedBy=actor; old.CompletionKind="SUPERVISION"; old.ReviewStatus="REASIGNADA";
                old.ReviewDecision="ROL"; old.ReviewComment=reason; old.ReviewedAt=clock.UtcNow; old.ReviewedBy=actor; old.Revision++;
                ledger.CaptureTime(projected,clock.UtcNow); projected.Id=Guid.CreateVersion7(); projected.ReassignedFromId=old.Id; projected.ReassignmentComment=reason;
                next.AssignmentId=projected.Id; next.Status="ACTIVO"; db.Add(projected);
                stage.EnabledAt=projected.StartsAt; stage.PlannedStartsAt=projected.StartsAt;
            }
            db.Add(next); links.Add((stage,next));
            db.Add(Event(run,stage,next,"REASIGNACION",$"reassign:{key:N}:{stage.Position}",systemAction?null:actor,clock.UtcNow,next.InputDocumentId,$"{target.Name}: {reason}"));
        }
        var prepared=await db.Set<PreparedProcessStart>().SingleOrDefaultAsync(p=>p.StartKey==run.StartKey,token);
        if(prepared!=null) { var selected=JsonSerializer.Deserialize<Dictionary<int,Guid>>(prepared.Participants)!; foreach(var choice in people) selected[choice.Key]=choice.Value; prepared.Participants=JsonSerializer.Serialize(selected); prepared.Revision++; }
        run.Revision++;
        await db.SaveChangesAsync(token); foreach(var link in links) link.Stage.CurrentAttemptId=link.Attempt.Id;
        db.Add(new AuditEvent { Id=Guid.CreateVersion7(),OccurredAt=clock.UtcNow,ActorUserId=systemAction?null:actor,ActorType=systemAction?"SYSTEM":"USER",
            Action="PROCESS_REASSIGNED",ResourceType="OPERATIONAL_PROCESS",ResourceId=runId,BranchId=BranchScope.LorettaId,CorrelationId=key,RequestId=key.ToString("N"),
            Outcome="SUCCESS",Reason=reason,BeforeData=before,AfterData=JsonSerializer.SerializeToDocument(new { RequestHash=hash,People=people,Attempts=links.Select(l=>l.Attempt.Id) }) });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }
    private async Task EnableActive(OperationalSequenceRun run,CancellationToken token)
    {
        var stage=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.RunId==run.Id && s.Position==run.ActivePosition,token);
        if(stage.Status is "DISPONIBLE" or "CONCLUIDA" or "CANCELADA") return;
        var rule=await db.Set<OperationalSequenceStep>().AsNoTracking().SingleAsync(s=>s.VersionId==run.VersionId && s.Position==stage.Position,token);
        var currentAttempt=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==stage.CurrentAttemptId,token);
        var ledger=await OperationalCapacityService.LedgerAsync(db,clock.UtcNow,token);
        ledger=new(ledger.Workers,ledger.Tasks.Where(a=>a.Id!=stage.Id).ToArray(),ledger.Capacities,ledger.Unavailable,ledger.Legacy,ledger.Today) { Temporal = ledger.Temporal };
        if (stage.SchedulingPolicyId != null) { await EnableScheduledStage(run, stage, currentAttempt, ledger, token); return; }
        var ready=OperationalSequenceRules.Enable(stage.DependencySatisfiedAt,stage.MinimumStartsAt,rule.DeadlineKind,rule.DeadlineHours,stage.FixedDueAt,true,clock.UtcNow,
            stage.Status=="PENDIENTE_ASIGNACION" ? clock.UtcNow : currentAttempt.CreatedAt);
        if(ready.Status=="DISPONIBLE")
        {
            var assignment=Reservation(stage,run.PlanId); assignment.Id=Guid.CreateVersion7(); assignment.StartsAt=ready.EnabledAt!.Value; assignment.DueAt=ready.DueAt!.Value;
            var worker=ledger.Workers.Where(w=>w.EligibleFor(stage.Role, stage.SpecializationId) && ledger.CanAssign(w.Id,assignment,clock.UtcNow))
                .Where(w=>stage.ReservedPersonId!=null ? w.Id==stage.ReservedPersonId : !run.Name.StartsWith("Ejemplo:",StringComparison.Ordinal) || w.Code.StartsWith("INV-DEMO-",StringComparison.Ordinal))
                .OrderByDescending(w=>w.Id==stage.ReservedPersonId).ThenBy(w=>ledger.Tasks.Where(a=>a.PersonId==w.Id).Sum(a=>(long)a.RemainingMinutes)).ThenBy(w=>w.Code).FirstOrDefault();
            if(worker==null) {
                ready=OperationalSequenceRules.Enable(stage.DependencySatisfiedAt,stage.MinimumStartsAt,rule.DeadlineKind,rule.DeadlineHours,stage.FixedDueAt,false,clock.UtcNow);
                if(ledger.Temporal.Enabled && stage.ReservedPersonId is {} reserved) {
                    var decision=ledger.CheckTime(reserved,assignment,clock.UtcNow);
                    if(!decision.Eligible) ready=ready with { Reason=TemporalEligibilityContext.Message(decision.Reason)+" Se conserva la reserva; reintenta tras corregir disponibilidad o usa Reasignar en el proceso." };
                }
            }
            else
            {
                assignment.PersonId=worker.Id; assignment.PersonName=worker.Name; assignment.AssignedRole=worker.Role; ledger.CaptureTime(assignment,clock.UtcNow); stage.ReservedPersonId=worker.Id; stage.ReservedPersonName=worker.Name;
                var attempt=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==stage.CurrentAttemptId,token);
                attempt.AssignmentId=assignment.Id; attempt.Status="ACTIVO"; attempt.Revision++; db.Add(assignment);
                db.Add(Event(run,stage,attempt,"HABILITACION",$"enable:{attempt.Id:N}",null,ready.EnabledAt.Value,attempt.InputDocumentId,null));
                stage.PlannedStartsAt=assignment.StartsAt; stage.PlannedDueAt=assignment.DueAt;
                var planned=assignment.DueAt;
                foreach(var downstream in await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==run.Id && s.Position>stage.Position && s.Status=="EN_ESPERA").OrderBy(s=>s.Position).ToArrayAsync(token))
                {
                    var nextRule=await db.Set<OperationalSequenceStep>().AsNoTracking().SingleAsync(s=>s.VersionId==run.VersionId && s.Position==downstream.Position,token);
                    if(downstream.MinimumStartsAt is { } minimum && minimum>planned) planned=minimum;
                    downstream.PlannedStartsAt=planned; downstream.PlannedDueAt=planned.AddHours(nextRule.DeadlineHours); downstream.Revision++; planned=downstream.PlannedDueAt;
                }
            }
        }
        stage.Status=ready.Status; stage.EligibleAt=ready.EligibleAt; stage.EnabledAt=ready.EnabledAt; stage.EffectiveDueAt=ready.DueAt; stage.BlockReason=ready.Reason; stage.Revision++;
    }

    public async Task<bool> CanReadDocumentAsync(Guid actor,Guid assignmentId,CancellationToken token)
    {
        var doc=await db.Set<OperationalSequenceDocument>().AsNoTracking().SingleOrDefaultAsync(d=>d.AssignmentId==assignmentId,token);
        var runId = doc?.RunId ?? await db.Set<OperationalSequenceAttempt>().AsNoTracking()
            .Where(a => a.AssignmentId == assignmentId && db.Set<OperationalSequenceRun>().Any(r => r.Id == a.RunId && r.UsesTaskEvidence))
            .Select(a => (Guid?)a.RunId).SingleOrDefaultAsync(token);
        if(runId==null) return false;
        try { await RequireAccess(actor,runId.Value,token); return true; } catch(DirectionPlanException e) when(e.Status==403) { return false; }
    }
    public async Task RequireAccess(Guid actor,Guid run,CancellationToken token)
    {
        var person=await Person(actor,token);
        if(!await db.Set<OperationalSequenceRun>().AnyAsync(r=>r.Id==run,token)) throw Error("TASK_NOT_FOUND",404);
        if(await db.Set<OperationalSequenceStage>().AnyAsync(s=>s.RunId==run && s.ReservedPersonId==person,token) ||
            await db.Set<OperationalSequenceAttempt>().AnyAsync(a=>a.RunId==run && db.OperationalAssignments.Any(x=>x.Id==a.AssignmentId && x.PersonId==person),token)) return;
        await direction.AuthorizeAsync(actor,token);
    }
    private async Task<Guid> Person(Guid actor,CancellationToken token)
    {
        var workers=await OperationalCapacityService.WorkersAsync(db,clock.UtcNow,token);
        var person=await db.AppUsers.Where(u=>u.Id==actor && u.Status==AccountStatus.Active).Select(u=>(Guid?)u.PersonId).SingleOrDefaultAsync(token);
        if(person==null || !workers.Any(w=>w.Id==person)) throw Error("ACCESO_DENEGADO",403); return person.Value;
    }
    internal static OperationalAssignment Reservation(OperationalSequenceStage s,Guid plan) => new() { Id=s.Id,PlanId=plan,TaskId=s.TaskId,TaskRevision=s.TaskRevision,DefinitionSnapshotJson=s.DefinitionSnapshotJson,TaskCode=s.TaskCode,Name=s.Name,Description=s.Description,
        Requirement=s.Requirement, ChecklistJson = s.ChecklistJson,AssignedRole=s.Role,LoadMinutes=s.LoadMinutes,MinimumPhotos=s.MinimumPhotos,MaximumPhotos=s.MaximumPhotos,MaximumPhotoBytes=s.MaximumPhotoBytes,
        SpecializationId=s.SpecializationId,SpecializationName=s.SpecializationName,PersonId=s.ReservedPersonId??Guid.Empty,PersonName=s.ReservedPersonName??"",
        StartsAt=s.PlannedStartsAt,DueAt=s.PlannedDueAt,PeriodType="OCASIONAL", SchedulingPolicyId=s.SchedulingPolicyId, ClosureMarginPercent=s.ClosureMarginPercent, OperationalCriticality=s.OperationalCriticality, NominalStartsAt=s.NominalStartsAt };
    private static OperationalSequenceAttempt NewAttempt(OperationalSequenceStage stage,OperationalSequenceRun run,int number,Guid? document,DateTimeOffset at) =>
        new() { Id=Guid.CreateVersion7(),RunId=run.Id,StageId=stage.Id,PlanId=run.PlanId,Number=number,Round=run.Round,InputDocumentId=document,CreatedAt=at };
    private static OperationalSequenceEvent Event(OperationalSequenceRun run,OperationalSequenceStage stage,OperationalSequenceAttempt attempt,string kind,string key,Guid? actor,DateTimeOffset at,Guid? doc,string? comment) =>
        new() { Id=Guid.CreateVersion7(),RunId=run.Id,StageId=stage.Id,AttemptId=attempt.Id,Kind=kind,EventKey=key,ActorId=actor,ActorKind=actor==null?"SYSTEM":"USER",OccurredAt=at,DocumentId=doc,Comment=comment };
    private static DirectionPlanException Error(string code,int status=422)=>new(code,status);
}
