using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalProcessService
{
    private static OperationalSequenceAttempt? PendingCountProducer(ProcessRunView view)
    {
        var stage=view.Stages.Single(s=>s.Position==view.Run.ActivePosition);
        var current=view.Attempts.SingleOrDefault(a=>a.Id==stage.CurrentAttemptId);
        if(view.Run.Status!="EN_CURSO" || stage.Status is not ("PENDIENTE_ASIGNACION" or "PROGRAMADA" or "FUERA_DE_PLAZO") || current?.Status!="PREPARADO" || current.AssignmentId!=null) return null;
        var id=stage.LoadSourceAttemptId ?? view.Events.SingleOrDefault(e=>e.Id==stage.DependencyEventId)?.AttemptId;
        var producer=view.Attempts.SingleOrDefault(a=>a.Id==id && a.ItemQuantity!=null && (a.Status is "ENTREGADO" or "APROBADO"));
        return producer!=null && view.Stages.Any(s=>s.Id==producer.StageId && s.CurrentAttemptId==producer.Id && s.Position<stage.Position && s.Status=="CONCLUIDA") ? producer : null;
    }

    private async Task<ProcessRunView> PendingCountActions(Guid actor, ProcessRunView view, CancellationToken ct)
    {
        var producer=PendingCountProducer(view); if(producer==null) return view;
        var stage=view.Stages.Single(s=>s.Position==view.Run.ActivePosition);
        var owner=stage.ReservedPersonId==await Person(actor,ct);
        if(!owner) {
            try { await direction.AuthorizeAsync(actor,ct); owner=true; }
            catch(DirectionPlanException e)when(e.Status==403) { }
        }
        return view with { CanReturnPendingCount=owner, PendingCountProducerId=producer.Id };
    }

    private static void ResetReturnedLoad(OperationalSequenceStage stage, HashSet<Guid> replaced)
    {
        if(stage.LoadSourceAttemptId is {} source && replaced.Contains(source)) {
            stage.LoadMinutes=stage.EstimatedLoadMinutes ?? stage.LoadMinutes;
            stage.CalculatedLoadMinutes=null;stage.LoadSourceAttemptId=null;
        }
    }

    internal static void RebaseReturnedAgenda(OperationalSequenceStage stage, DateTimeOffset now)
    {
        if(stage.SchedulingPolicyId==null || stage.OperationalCriticality=="CRITICA" || stage.FixedDueAt!=null) return;
        var nominal=new[]{now,stage.NominalStartsAt??now,stage.MinimumStartsAt??now}.Max();
        stage.NominalStartsAt=nominal;
        stage.PlannedStartsAt=new[]{stage.PlannedStartsAt,nominal}.Max();
        stage.PlannedDueAt=stage.PlannedStartsAt.AddMinutes(OperationalAgenda.Duration(stage.LoadMinutes,stage.ClosureMarginPercent??0));
    }

    public async Task ReturnPendingCountAsync(Guid actor, Guid runId, Guid stageId, long revision, Guid key, string reason, CancellationToken ct)
    {
        if(key==Guid.Empty) throw Error("PROCESS_CONFLICT",409);
        if(string.IsNullOrWhiteSpace(reason) || reason.Trim().Length>4000) throw Error("PROCESS_ITEM_RETURN_REASON_REQUIRED");
        var hash=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{actor,runId,stageId,revision,Reason=reason.Trim()})));
        await using var tx=await db.Database.BeginTransactionAsync(ct);await OperationalTaskService.LockAsync(db,ct);
        await RequireAccess(actor,runId,ct);
        var replay=await db.AuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.ResourceId==runId&&a.Action=="PROCESS_PENDING_COUNT_RETURN"&&a.RequestId==key.ToString("N"),ct);
        if(replay!=null) {
            if(replay.ActorUserId!=actor||replay.AfterData?.RootElement.GetProperty("RequestHash").GetString()!=hash) throw Error("PROCESS_CONFLICT",409);
            await tx.CommitAsync(ct);return;
        }
        var view=await ReadAsync(actor,runId,ct);var pending=view.Stages.Single(s=>s.Position==view.Run.ActivePosition);
        if(pending.Id!=stageId||pending.Revision!=revision||view.PendingCountProducerId==null) throw Error("PROCESS_ITEM_PENDING_RETURN_CONFLICT",409);
        if(!view.CanReturnPendingCount) throw Error("ACCESO_DENEGADO",403);
        var producer=view.Attempts.Single(a=>a.Id==view.PendingCountProducerId);
        var target=view.Stages.Single(s=>s.Id==producer.StageId);
        var run=await db.Set<OperationalSequenceRun>().SingleAsync(r=>r.Id==runId,ct);
        var stage=await db.Set<OperationalSequenceStage>().SingleAsync(s=>s.Id==stageId,ct);
        var pendingAttempt=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==stage.CurrentAttemptId,ct);
        db.Add(Event(run,stage,pendingAttempt,"DEVOLUCION",$"pending-count-return:{key:N}",actor,clock.UtcNow,pendingAttempt.InputDocumentId,
            reason.Trim()));
        run.Round++;run.ActivePosition=target.Position;run.Revision++;
        var stages=await db.Set<OperationalSequenceStage>().Where(s=>s.RunId==runId&&s.Position>=target.Position).OrderBy(s=>s.Position).ToArrayAsync(ct);
        var replaced=stages.Select(s=>s.CurrentAttemptId!.Value).ToHashSet();
        foreach(var downstream in stages) {
            var old=await db.Set<OperationalSequenceAttempt>().SingleAsync(a=>a.Id==downstream.CurrentAttemptId,ct);
            old.Status="SUSTITUIDO";old.Revision++;old.FinishedAt??=clock.UtcNow;
            if(old.AssignmentId is {} assignmentId) {
                var assignment=await db.OperationalAssignments.SingleAsync(a=>a.Id==assignmentId,ct);
                assignment.ReviewStatus="NO_SATISFECHA";assignment.ReviewDecision="NO_SATISFECHA";assignment.ReviewComment=reason.Trim();assignment.ReviewedAt=clock.UtcNow;assignment.ReviewedBy=actor;assignment.Revision++;
            }
            ResetReturnedLoad(downstream,replaced);
            RebaseReturnedAgenda(downstream,clock.UtcNow);
            downstream.Status="EN_ESPERA";downstream.EnabledAt=null;downstream.EffectiveDueAt=null;downstream.EligibleAt=null;downstream.DependencySatisfiedAt=null;downstream.DependencyEventId=null;downstream.OmittedByAttemptId=null;downstream.BlockReason=null;downstream.Revision++;
            var next=NewAttempt(downstream,run,(await db.Set<OperationalSequenceAttempt>().Where(a=>a.StageId==downstream.Id).MaxAsync(a=>a.Number,ct))+1,pendingAttempt.InputDocumentId,clock.UtcNow);
            next.ContinuationAttemptId=old.Id;next.UnresolvedDifferences=pendingAttempt.UnresolvedDifferences;db.Add(next);downstream.CurrentAttemptId=next.Id;
            if(downstream.Id==target.Id) {
                var start=Event(run,downstream,next,"INICIO",$"return-count:{key:N}",actor,clock.UtcNow,pendingAttempt.InputDocumentId,reason.Trim());db.Add(start);
                downstream.DependencySatisfiedAt=clock.UtcNow;downstream.DependencyEventId=start.Id;
            }
        }
        await db.SaveChangesAsync(ct);await EnableActive(run,ct);
        db.Add(new AuditEvent{Id=Guid.CreateVersion7(),ActorUserId=actor,ActorType="USER",OccurredAt=clock.UtcNow,ResourceType="OPERATIONAL_PROCESS",ResourceId=runId,
            BranchId=BranchScope.LorettaId,Action="PROCESS_PENDING_COUNT_RETURN",RequestId=key.ToString("N"),CorrelationId=key,Outcome="SUCCESS",Reason=reason.Trim(),
            AfterData=JsonSerializer.SerializeToDocument(new{RequestHash=hash,ProducerAttemptId=producer.Id,Quantity=producer.ItemQuantity,PendingStageId=stageId,TargetStageId=target.Id})});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
