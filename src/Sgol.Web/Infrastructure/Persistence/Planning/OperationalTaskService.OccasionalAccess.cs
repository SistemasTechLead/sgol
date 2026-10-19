using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record OccasionalTaskForm(string Name, string Requirement, int LoadMinutes, int DeadlineHours,
    int MinimumPhotos = 1, int MaximumPhotos = 20, string? Description = null);

public sealed partial class OperationalTaskService
{
    private async Task<string> AuthorizeOccasionalAsync(Guid actor, CancellationToken token)
    {
        var role = await new OperationalSupervisionService(db, clock, direction).AuthorizeAsync(actor, token);
        if (role is not (CanonicalRole.Direction or CanonicalRole.Administration or CanonicalRole.Subcoordination))
            throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        return role;
    }

    public async Task<List<OperationalTask>> OccasionalTasksAsync(Guid actor, CancellationToken token)
    {
        await AuthorizeOccasionalAsync(actor, token);
        return await db.OperationalTasks.AsNoTracking().Where(t => t.ProcessId == OperationalSequence.GeneralId &&
            t.PeriodType == "OCASIONAL" && t.Active && !t.Deleted).OrderBy(t => t.Name).ToListAsync(token);
    }

    // Only creation is exposed here. No submitted id, process, role or periodicity can update an existing task.
    public async Task<Guid> CreateOccasionalTaskAsync(Guid actor, OccasionalTaskForm input, CancellationToken token)
    {
        await AuthorizeOccasionalAsync(actor, token);
        var task = new OperationalTask { Id = Guid.CreateVersion7(), TaskCode = "T-" + Guid.NewGuid().ToString("N")[..12],
            Name = input.Name ?? "", Description = input.Description, Requirement = input.Requirement, PeriodType = "OCASIONAL",
            LoadMinutes = input.LoadMinutes, DeadlineHours = input.DeadlineHours,
            MinimumPhotos = input.MinimumPhotos, MaximumPhotos = input.MaximumPhotos };
        task.Validate();
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token);
        await AuthorizeOccasionalAsync(actor, token);
        db.OperationalTasks.Add(task);
        Audit(actor, "OPERATIONAL_TASK_SAVED", task.Id, new { task.Id, task.Name, task.Description, task.Requirement, task.PeriodType,
            task.ProcessId, task.AssignedRole, task.LoadMinutes, task.DeadlineHours, task.MinimumPhotos, task.MaximumPhotos });
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        return task.Id;
    }

    public async Task<CapacityWorker[]> OccasionalRecipientsAsync(Guid actor, Guid taskId, CancellationToken token)
    {
        var role = await AuthorizeOccasionalAsync(actor, token);
        var task = (await OccasionalTasksAsync(actor, token)).SingleOrDefault(t => t.Id == taskId)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, token);
        return ledger.Workers.Where(w => EligibleOccasionalRecipient(role, task, w)).OrderBy(w => w.Name).ToArray();
    }

    private static bool EligibleOccasionalRecipient(string actorRole, OperationalTask task, CapacityWorker worker) =>
        (actorRole != CanonicalRole.Direction
            ? worker.Role is CanonicalRole.Administration or CanonicalRole.Subcoordination or CanonicalRole.SalesFloor
            : (task.SpecializationId != null || worker.Role == task.AssignedRole)) && worker.Qualified(task.SpecializationId);
}
