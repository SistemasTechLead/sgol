using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class BatchReviewChecks
{
    public static async Task RunAsync(SgolDbContext db, OperationalSupervisionService service, Guid actor,
        OperationalAssignment template, OperationalAssignment forbidden)
    {
        OperationalAssignment Copy() => new() {
            Id = Guid.CreateVersion7(), PlanId = template.PlanId, TaskId = template.TaskId,
            TaskCode = template.TaskCode, Name = "Prueba de validación masiva", Requirement = "NINGUNO",
            PersonId = template.PersonId, PersonName = template.PersonName, AssignedRole = template.AssignedRole,
            StartsAt = template.StartsAt, DueAt = template.DueAt, CompletedAt = template.CompletedAt,
            Conclusion = template.Conclusion, ReviewStatus = "PENDIENTE_REVISION", LoadMinutes = template.LoadMinutes
        };
        var first = Copy(); var second = Copy();
        db.OperationalAssignments.AddRange(first, second);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ids = new[] { first.Id, second.Id };
        async Task<string> Snapshot() => JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking()
            .Where(a => ids.Contains(a.Id)).OrderBy(a => a.Id).ToArrayAsync());
        var before = await Snapshot();
        var auditCount = await db.AuditEvents.CountAsync();
        var approve = new SupervisionReview(first.Id, first.Revision, "CONCLUIDA", null);
        async Task Reject(SupervisionReview[] reviews, string expected) {
            try { await service.ReviewBatchAsync(actor, reviews, default); throw new Exception("Invalid batch accepted"); }
            catch (DirectionPlanException e) when (e.Code == expected) { }
            if (await Snapshot() != before || await db.AuditEvents.CountAsync() != auditCount)
                throw new Exception("Batch rollback changed assignments or audit");
        }
        await Reject([approve, new(second.Id, second.Revision + 1, "CONCLUIDA", null)], "TASK_CONFLICT");
        await Reject([approve, new(second.Id, second.Revision, "NO_SATISFECHA", " ")], "REVIEW_COMMENT_REQUIRED");
        await Reject([approve, new(forbidden.Id, forbidden.Revision, "CONCLUIDA", null)], "ACCESO_DENEGADO");
        await Reject([approve, approve], "REVIEW_BATCH_INVALID");
        await Reject([], "REVIEW_BATCH_INVALID");
        await service.ReviewBatchAsync(actor, [approve, new(second.Id, second.Revision, "NO_SATISFECHA", "Requiere corrección")], default);
        db.ChangeTracker.Clear();
        var saved = await db.OperationalAssignments.AsNoTracking().Where(a => ids.Contains(a.Id)).ToArrayAsync();
        if (saved.Single(a => a.Id == first.Id).ReviewStatus != "CONCLUIDA" ||
            saved.Single(a => a.Id == second.Id).ReviewStatus != "NO_SATISFECHA" ||
            saved.Any(a => a.Revision != 2 || a.ReviewedBy != actor || a.CompletedAt != template.CompletedAt) ||
            await db.AuditEvents.CountAsync() != auditCount + 2)
            throw new Exception("Batch results, revisions, original delivery or audits invalid");
        Console.WriteLine("PASS batch review: atomic rollback on stale revision, missing comment and denied role; duplicate/empty rejection; mixed decisions preserve delivery and audit each task.");
    }
}
