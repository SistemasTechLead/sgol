using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

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
        var responsiblePersonId = await context.AssignmentVersions.AsNoTracking()
            .Where(assignment => assignment.ObligationId == obligationId &&
                assignment.Status == AssignmentVersionStatuses.Current)
            .Select(assignment => assignment.PersonId)
            .SingleAsync();
        var responsibleUserId = await context.AppUsers.AsNoTracking()
            .Where(user => user.PersonId == responsiblePersonId &&
                user.Status == AccountStatus.Active && user.MfaEnrolledAt != null)
            .Select(user => user.Id)
            .SingleOrDefaultAsync();
        if (responsibleUserId == Guid.Empty)
        {
            responsibleUserId = Guid.CreateVersion7();
            context.AppUsers.Add(new AppUser
            {
                Id = responsibleUserId,
                PersonId = responsiblePersonId,
                Status = AccountStatus.Active,
                MustChangePassword = false,
                MfaEnrolledAt = concludedAt.AddDays(-1),
                SecurityStamp = $"synthetic-conclusion-{responsibleUserId:N}",
            });
        }

        var evidencePolicyVersionId = obligation.EvidencePolicyVersionId;
        if (evidencePolicyVersionId is null)
        {
            var taskVersion = await context.TaskDefinitionVersions.AsNoTracking()
                .SingleAsync(version => version.Id == obligation.TaskDefinitionVersionId);
            var policy = await context.EvidencePolicyVersions
                .SingleOrDefaultAsync(version => version.TaskDefinitionVersionId == taskVersion.Id);
            if (policy is null)
            {
                var policyId = Guid.CreateVersion7();
                policy = new EvidencePolicyVersion(
                    policyId,
                    taskVersion.TaskDefinitionId,
                    taskVersion.Id,
                    taskVersion.ReleaseId,
                    null,
                    1);
                policy.ApplyPublished(new VersionRecord(
                    policyId,
                    VersionStatuses.Current,
                    concludedAt.AddDays(-1),
                    null,
                    "Synthetic conclusion fixture",
                    null,
                    2));
                context.EvidencePolicyVersions.Add(policy);
            }

            evidencePolicyVersionId = policy.Id;
            context.Entry(obligation).Property(item => item.EvidencePolicyVersionId).CurrentValue = policy.Id;
        }

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
            evidencePolicyVersionId.Value,
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
