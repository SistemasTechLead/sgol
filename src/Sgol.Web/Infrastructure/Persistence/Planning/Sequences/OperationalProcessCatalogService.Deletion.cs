using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalProcessCatalogService
{
    public async Task DeleteAsync(Guid actor, Guid id, long revision, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        await direction.AuthorizeAsync(actor, token);
        var root = await RequireRoot(id, revision, token);
        await RequireNoPreparedPlans(id, token);
        var members = await db.OperationalTasks.Where(t => t.ProcessId == id).ToArrayAsync(token);
        foreach (var task in members) await ReturnToGeneral(task, token);
        root.Enabled = false;
        root.DeletedAt = clock.UtcNow;
        root.Revision++;
        Audit(actor, "PROCESS_DELETED", id, new { ReturnedTaskIds = members.Select(t => t.Id).ToArray() });
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
    }

    private async Task RequireNoPreparedPlans(Guid id, CancellationToken token)
    {
        // Prepared reservations must be explicitly reviewed or deleted in Planning;
        // silently releasing them would change a saved workload proposal.
        var plan = await (from start in db.Set<PreparedProcessStart>()
            join prepared in db.DirectionPreparedPlans on start.PlanId equals prepared.Id
            where start.ProcessId == id && start.Status == "PREPARADO"
            orderby start.StartsAt
            select new { prepared.Id, prepared.Name }).FirstOrDefaultAsync(token);
        if (plan != null)
        {
            var error = Error("PROCESS_PREPARED_PLANS_EXIST", 409);
            error.Data["PlanName"] = plan.Name;
            error.Data["PlanId"] = plan.Id;
            throw error;
        }
    }

    private async Task ReturnToGeneral(OperationalTask task, CancellationToken token)
    {
        await PreservePendingDefinition(task, token);
        task.ProcessId = OperationalSequence.GeneralId;
        task.Revision++;
    }

    private async Task PreservePendingDefinition(OperationalTask task, CancellationToken token)
    {
        var pending = await db.OperationalAssignments.Where(a => a.TaskId == task.Id && a.DefinitionSnapshotJson == null && a.CompletedAt == null).ToArrayAsync(token);
        foreach (var assignment in pending.Where(AssignmentDefinition.Pending))
        {
            var snapshot = JsonSerializer.SerializeToNode(AssignmentDefinition.Baseline(assignment, task))!;
            snapshot["_CalendarKnown"] = assignment.TaskRevision == task.Revision;
            assignment.DefinitionSnapshotJson = snapshot.ToJsonString();
        }
    }
}
