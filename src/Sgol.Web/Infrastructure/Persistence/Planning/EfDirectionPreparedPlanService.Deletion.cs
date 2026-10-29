using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class EfDirectionPreparedPlanService
{
    public async Task<DirectionPlanDeletionPreview> PreviewDeletionAsync(Guid actor, Guid id, CancellationToken token = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        await CheckAuthorityAsync(actor, clock.UtcNow, true, token);
        var preview = await DeletionStateAsync(id, token);
        await tx.CommitAsync(token);
        return preview;
    }

    public async Task<DirectionPlanDeleted> DeleteAsync(Guid actor, Guid id, Guid key, Guid correlation,
        string fingerprint, CancellationToken token = default)
    {
        if (actor == Guid.Empty || id == Guid.Empty || key == Guid.Empty || correlation == Guid.Empty || fingerprint?.Length != 64)
            throw new DirectionPlanException("DIRECTION_PLAN_REQUEST_INVALID", 400);
        const string operation = "DIRECTION_PLAN_DELETE";
        var scope = IdempotencyProtocol.Scope(actor.ToString("D"), operation, id.ToString("D"));
        var hash = IdempotencyProtocol.HashCanonical(operation, actor.ToString("D"), id.ToString("D"), new { fingerprint });
        DirectionPlanDeleted? result = null;
        try
        {
            await audit.ExecuteAsync(async ct =>
            {
                await OperationalTaskService.LockAsync(db, ct);
                await CheckAuthorityAsync(actor, clock.UtcNow, true, ct);
                var replay = await db.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.Scope == scope && r.Key == key, ct);
                if (replay is not null)
                {
                    if (replay.RequestHash != hash) throw new DirectionPlanException("IDEMPOTENCY_CONFLICT", 409);
                    result = IdempotencyProtocol.ReadPayload<DirectionPlanDeleted>(replay);
                    return Event(actor, key, correlation, id, "DIRECTION_PLAN_DELETE_RECOVERED", "RECOVERED", clock.UtcNow);
                }
                var preview = await DeletionStateAsync(id, ct);
                if (preview.Fingerprint != fingerprint) throw new DirectionPlanException("PLAN_DELETE_STALE", 409);
                await DeleteProcessExecutionAsync(id, actor, key, ct);
                await db.Set<PreparedProcessStart>().Where(s=>s.PlanId==id).ExecuteDeleteAsync(ct);
                var adjustments = await CapacityAdjustmentsAsync(id, ct);
                foreach (var entry in adjustments)
                {
                    var setting = await OwnedCapacityAsync(entry, ct);
                    // Refuse to overwrite capacity changed by another plan or by supervision.
                    if (setting is null) throw new DirectionPlanException("PLAN_DELETE_CAPACITY_CHANGED", 409);
                    db.OperationalCapacities.Remove(setting);
                    var before = entry.PreviousSegments;
                    foreach (var previous in before)
                    {
                        db.OperationalCapacities.Add(new() { Id = ids.NewUuid(), PersonId = entry.PersonId,
                            From = previous.From, Through = previous.Through, Percent = previous.Percent,
                            DailyMinutes = previous.DailyMinutes, Reason = previous.Reason, Revision = previous.Revision + 1 });
                    }
                }
                var assignments = db.OperationalAssignments.IgnoreQueryFilters().Where(a => a.PlanId == id);
                var assignmentIds = assignments.Select(a => a.Id);
                await db.Set<OperationalTransfer>().Where(t => assignmentIds.Contains(t.AssignmentId)).ExecuteDeleteAsync(ct);
                await db.Set<OperationalProgressEntry>().Where(p => assignmentIds.Contains(p.AssignmentId)).ExecuteDeleteAsync(ct);
                // Detach self references before deleting the entire reassignment history.
                await assignments.ExecuteUpdateAsync(s => s.SetProperty(a => a.ReassignedFromId, (Guid?)null), ct);
                // Entity splitting maps an assignment to three tables; deleting its root cascades to both fragments.
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM operational_assignment WHERE \"PlanId\" = {id}", ct);
                await db.DirectionPreparedPlanItems.Where(i => i.PlanId == id).ExecuteDeleteAsync(ct);
                await db.DirectionPreparedPlans.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
                // Creation replays must never return a removed plan and its deleted evidence.
                await db.IdempotencyRecords.Where(r => r.ResourceType == Resource && r.ResourceId == id).ExecuteDeleteAsync(ct);
                result = new(id, "DELETED");
                db.IdempotencyRecords.Add(IdempotencyProtocol.Completed(scope, key, hash, Resource, id, 200, result, clock.UtcNow, DateTimeOffset.MaxValue));
                return new AuditEvent { Id = ids.NewUuid(), OccurredAt = clock.UtcNow, ActorUserId = actor, ActorType = "USER",
                    Action = operation, ResourceType = Resource, ResourceId = id, BranchId = BranchScope.LorettaId,
                    CorrelationId = correlation, RequestId = key.ToString("D"), Outcome = "DELETED",
                    BeforeData = JsonSerializer.SerializeToDocument(preview, JsonSerializerOptions.Web) };
            }, token);
        }
        finally { db.ChangeTracker.Clear(); }
        return result ?? throw new InvalidOperationException("Deletion was not confirmed.");
    }

    private async Task<DirectionPlanDeletionPreview> DeletionStateAsync(Guid id, CancellationToken token)
    {
        var plan = await db.DirectionPreparedPlans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.BranchId == BranchScope.LorettaId, token)
            ?? throw new DirectionPlanException("DIRECTION_PLAN_NOT_FOUND", 404);
        var tasks = await db.DirectionPreparedPlanItems.AsNoTracking().Where(i => i.PlanId == id).OrderBy(i => i.Position).ToArrayAsync(token);
        var processStarts=await db.Set<PreparedProcessStart>().AsNoTracking().Where(s=>s.PlanId==id).OrderBy(s=>s.Id).ToArrayAsync(token);
        var runs = await db.Set<OperationalSequenceRun>().AsNoTracking().Where(r => r.PlanId == id).OrderBy(r => r.Id).ToArrayAsync(token);
        var runIds = runs.Select(r => r.Id).ToArray();
        var stages = await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s => runIds.Contains(s.RunId)).OrderBy(s => s.Id).ToArrayAsync(token);
        var attempts = await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a => runIds.Contains(a.RunId)).OrderBy(a => a.Id).ToArrayAsync(token);
        var documents = await db.Set<OperationalSequenceDocument>().AsNoTracking().Where(d => runIds.Contains(d.RunId)).OrderBy(d => d.Id).ToArrayAsync(token);
        var events = await db.Set<OperationalSequenceEvent>().AsNoTracking().Where(e => runIds.Contains(e.RunId)).OrderBy(e => e.Id).ToArrayAsync(token);
        var assignments = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => a.PlanId == id).OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.Revision, a.CompletedAt, FileName = a.Attachment != null ? a.Attachment.FileName : null, a.ProgressUnits, a.ReviewStatus, a.PersonId, a.ReassignedFromId }).ToArrayAsync(token);
        var ids = assignments.Select(a => a.Id).ToArray();
        var photographs = await db.OperationalPhotographs.AsNoTracking().Where(p => ids.Contains(p.AssignmentId))
            .OrderBy(p => p.Id).Select(p => new { p.Id, p.AssignmentId, p.Sha256, p.AttachedAt }).ToArrayAsync(token);
        if (await db.OperationalAssignments.IgnoreQueryFilters().AnyAsync(a => a.PlanId != id && a.ReassignedFromId != null && ids.Contains(a.ReassignedFromId.Value), token))
            throw new DirectionPlanException("PLAN_DELETE_DEPENDENCIES", 409);
        var progress = await db.Set<OperationalProgressEntry>().AsNoTracking().Where(p => ids.Contains(p.AssignmentId)).OrderBy(p => p.Id).ToArrayAsync(token);
        var transfers = await db.Set<OperationalTransfer>().AsNoTracking().Where(t => ids.Contains(t.AssignmentId)).OrderBy(t => t.Id).ToArrayAsync(token);
        var occasions = await db.Set<OperationalOccasion>().AsNoTracking().Where(o => o.PlanId == id).OrderBy(o => o.Id).ToArrayAsync(token);
        var occasionIds = occasions.Select(o => o.Id).ToArray();
        var material = await db.Set<OperationalReferenceFile>().AsNoTracking().Where(f => occasionIds.Contains(f.OccasionId))
            .OrderBy(f => f.Id).Select(f => new { f.Id, f.FileName, f.OccasionId }).ToArrayAsync(token);
        var adjustments = await CapacityAdjustmentsAsync(id, token);
        var capacities = new List<OperationalCapacity>();
        foreach (var entry in adjustments)
            capacities.Add(await OwnedCapacityAsync(entry, token) ?? throw new DirectionPlanException("PLAN_DELETE_CAPACITY_CHANGED", 409));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { plan, tasks, assignments, progress, transfers, capacities, adjustments, photographs, occasions, material, processStarts, runs, stages, attempts, documents, events }))));
        return new(id, plan.Name, tasks.Length, assignments.Length, assignments.Count(a => a.CompletedAt != null),
            material.Length + photographs.Length + assignments.Count(a => a.FileName != null), progress.Length, adjustments.Length, fingerprint);
    }

    private async Task DeleteProcessExecutionAsync(Guid planId, Guid actor, Guid key, CancellationToken token)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('sgol.plan_delete_id', {planId.ToString("D")}, true), set_config('sgol.plan_delete_key', {key.ToString("D")}, true)", token);
        var runs = db.Set<OperationalSequenceRun>().Where(r => r.PlanId == planId);
        var runIds = runs.Select(r => r.Id);
        // Break cycles while satisfying the existing state constraints; everything is
        // inside the audited transaction, so a failed delete restores the original graph.
        await runs.ExecuteUpdateAsync(s => s.SetProperty(r => r.AuthorizedDocumentId, (Guid?)null)
            .SetProperty(r => r.Status, "CANCELADA").SetProperty(r => r.ClosedAt, (DateTimeOffset?)clock.UtcNow)
            .SetProperty(r => r.ClosedBy, (Guid?)actor).SetProperty(r => r.ClosureOutcome, "CANCELACION")
            .SetProperty(r => r.ClosureComment, "Eliminación confirmada del plan"), token);
        var stages = db.Set<OperationalSequenceStage>().Where(s => runIds.Contains(s.RunId));
        await stages.ExecuteUpdateAsync(s => s.SetProperty(x => x.CurrentAttemptId, (Guid?)null)
            .SetProperty(x => x.LoadSourceAttemptId, (Guid?)null).SetProperty(x => x.CalculatedLoadMinutes, (long?)null)
            .SetProperty(x => x.Status, "CANCELADA").SetProperty(x => x.DependencyEventId, (Guid?)null)
            .SetProperty(x => x.DependencySatisfiedAt, (DateTimeOffset?)null), token);
        var attempts = db.Set<OperationalSequenceAttempt>().Where(a => runIds.Contains(a.RunId));
        await attempts.ExecuteUpdateAsync(s => s.SetProperty(a => a.InputDocumentId, (Guid?)null)
            .SetProperty(a => a.OutputDocumentId, (Guid?)null).SetProperty(a => a.ContinuationAttemptId, (Guid?)null), token);
        await db.Set<OperationalSequenceEvent>().Where(e => runIds.Contains(e.RunId)).ExecuteDeleteAsync(token);
        var documents = db.Set<OperationalSequenceDocument>().Where(d => runIds.Contains(d.RunId));
        await documents.ExecuteDeleteAsync(token);
        await attempts.ExecuteDeleteAsync(token);
        await stages.ExecuteDeleteAsync(token);
        await runs.ExecuteDeleteAsync(token);
    }

    private async Task<StoredPlanCapacityAdjustment[]> CapacityAdjustmentsAsync(Guid id, CancellationToken token) =>
        await db.PlanCapacityAdjustments.AsNoTracking().Where(e => e.PlanId == id)
            .Include(e => e.PreviousSegments).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToArrayAsync(token);

    private async Task<OperationalCapacity?> OwnedCapacityAsync(StoredPlanCapacityAdjustment entry, CancellationToken token)
    {
        var setting = await db.OperationalCapacities.AsNoTracking().SingleOrDefaultAsync(c => c.Id == entry.SourceCapacityId, token);
        if (setting is null || setting.PersonId != entry.PersonId || setting.From != entry.From || setting.Through != entry.Through ||
            setting.Percent != entry.Percent || setting.DailyMinutes != entry.DailyMinutes || setting.Reason != entry.Reason ||
            entry.SourceRevision != null && setting.Revision != entry.SourceRevision) return null;
        // Only old adjustments without a captured revision need the historical audit guard.
        if (entry.SourceRevision is null && await db.AuditEvents.AnyAsync(e => e.ResourceId == entry.SourceCapacityId &&
            e.Id != entry.Id && e.OccurredAt >= entry.CreatedAt, token)) return null;
        return setting;
    }
}
