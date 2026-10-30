using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalTaskService
{
    public async Task<string> SaveChecklistDraftAsync(Guid actor, Guid id, string answer, CancellationToken token, string? draftBase = null, Guid? saveId = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token);
        var person = await PersonAsync(actor, token);
        var assignment = await db.OperationalAssignments.SingleOrDefaultAsync(a => a.Id == id && a.PersonId == person, token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        // A context may already track a read made before another editor committed.
        // Refresh under the lock before checking either ownership or the draft base.
        await db.Entry(assignment).ReloadAsync(token);
        if (db.Entry(assignment).State == EntityState.Detached || assignment.PersonId != person)
            throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        if (!AssignmentDefinition.Pending(assignment) || assignment.Requirement != "CHECKLIST")
            throw new DirectionPlanException("TASK_CONFLICT", 409);
        var selected = OperationalChecklist.ValidateAnswer(assignment.ChecklistJson, answer, false);
        var currentBase = OperationalChecklist.DraftBase(assignment.ChecklistJson, assignment.ChecklistDraftJson);
        var previous = JsonSerializer.Deserialize<ChecklistDraft>(assignment.ChecklistDraftJson);
        // A retry after a lost response acknowledges only this exact confirmed request.
        if (saveId is not null && saveId != Guid.Empty && previous?.SaveId == saveId && previous.Actor == actor &&
            previous.Base == draftBase && previous.Checked.ToHashSet().SetEquals(selected))
            return currentBase;
        if (string.IsNullOrEmpty(draftBase) || draftBase != currentBase || saveId is null || saveId == Guid.Empty) {
            var conflict = new DirectionPlanException("TASK_CHECKLIST_DRAFT_STALE", 409);
            conflict.Data["DraftBase"] = currentBase;
            conflict.Data["Checked"] = OperationalChecklist.Checked(assignment.ChecklistDraftJson);
            throw conflict;
        }
        assignment.ChecklistDraftJson = JsonSerializer.Serialize(new ChecklistDraft(actor, clock.UtcNow, selected, draftBase, saveId));
        // Drafts do not advance the task state or invalidate an open process attempt.
        assignment.Revision++;
        Audit(actor, "OPERATIONAL_CHECKLIST_DRAFT", id, new { assignment.ChecklistJson, assignment.ChecklistDraftJson });
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        return OperationalChecklist.DraftBase(assignment.ChecklistJson, assignment.ChecklistDraftJson);
    }
}
