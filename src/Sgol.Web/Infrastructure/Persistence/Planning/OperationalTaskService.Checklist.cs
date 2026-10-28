using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalTaskService
{
    public async Task SaveChecklistDraftAsync(Guid actor, Guid id, string answer, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token);
        var person = await PersonAsync(actor, token);
        var assignment = await db.OperationalAssignments.SingleOrDefaultAsync(a => a.Id == id && a.PersonId == person, token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        if (!AssignmentDefinition.Pending(assignment) || assignment.Requirement != "CHECKLIST")
            throw new DirectionPlanException("TASK_CONFLICT", 409);
        var selected = OperationalChecklist.ValidateAnswer(assignment.ChecklistJson, answer, false);
        assignment.ChecklistDraftJson = JsonSerializer.Serialize(new ChecklistDraft(actor, clock.UtcNow, selected));
        // Drafts do not advance the task state or invalidate an open process attempt.
        assignment.Revision++;
        Audit(actor, "OPERATIONAL_CHECKLIST_DRAFT", id, new { assignment.ChecklistJson, assignment.ChecklistDraftJson });
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
    }
}
