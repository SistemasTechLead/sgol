using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.IntegrationTests;

internal static class ObligationConclusionTestData
{
    public static async Task ConcludeAsync(
        SgolDbContext context,
        Guid obligationId,
        DateTimeOffset concludedAt)
    {
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var obligation = await context.WorkObligations.SingleAsync(item => item.Id == obligationId);
        var responsibleUserId = await (
            from assignment in context.AssignmentVersions.AsNoTracking()
            join user in context.AppUsers.AsNoTracking() on assignment.PersonId equals user.PersonId
            where assignment.ObligationId == obligationId
                && assignment.Status == AssignmentVersionStatuses.Current
                && user.Status == AccountStatus.Active
                && user.MfaEnrolledAt != null
            select user.Id).SingleAsync();

        var evaluation = new EvidenceReviewEvaluation(
            EvidenceReviewResults.Complete,
            [],
            [],
            [],
            "{}",
            new string('0', 64));
        var snapshot = new EvidenceReviewSnapshot(
            Guid.CreateVersion7(),
            obligation.Id,
            obligation.EvidencePolicyVersionId!.Value,
            evaluation,
            responsibleUserId,
            concludedAt,
            Guid.CreateVersion7());
        var result = new ExecutionResult(
            Guid.CreateVersion7(),
            obligation.Id,
            snapshot.Id,
            responsibleUserId,
            concludedAt);

        obligation.Conclude(responsibleUserId, concludedAt, obligation.RowVersion);
        context.EvidenceReviewSnapshots.Add(snapshot);
        context.ExecutionResults.Add(result);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}
