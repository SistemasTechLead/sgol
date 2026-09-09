using System.Text.Json;
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
        DateTimeOffset concludedAt,
        Guid? fallbackResponsiblePersonId = null)
    {
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var obligation = await context.WorkObligations.SingleAsync(item => item.Id == obligationId);
        var responsiblePersonId = await context.AssignmentVersions.AsNoTracking()
            .Where(assignment => assignment.ObligationId == obligationId &&
                assignment.Status == AssignmentVersionStatuses.Current)
            .Select(assignment => (Guid?)assignment.PersonId)
            .SingleOrDefaultAsync();
        if (responsiblePersonId is null)
        {
            responsiblePersonId = fallbackResponsiblePersonId ??
                throw new InvalidOperationException("A responsible person is required by the conclusion fixture.");
            context.AssignmentVersions.Add(new AssignmentVersion(
                Guid.CreateVersion7(),
                obligationId,
                responsiblePersonId.Value,
                AssignmentVersionStatuses.Current,
                AssignmentTypes.Automatic,
                JsonDocument.Parse("{}"),
                concludedAt.AddMinutes(-1)));
        }

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
                PersonId = responsiblePersonId.Value,
                Status = AccountStatus.Active,
                MustChangePassword = false,
                MfaEnrolledAt = concludedAt.AddDays(-1),
                SecurityStamp = $"synthetic-conclusion-{responsibleUserId:N}",
            });
        }

        var taskVersion = await context.TaskDefinitionVersions.AsNoTracking()
            .SingleAsync(version => version.Id == obligation.TaskDefinitionVersionId);
        var taskCode = await context.TaskDefinitions.AsNoTracking()
            .Where(task => task.Id == taskVersion.TaskDefinitionId)
            .Select(task => task.TaskCode)
            .SingleAsync();
        var policy = obligation.EvidencePolicyVersionId is Guid frozenPolicyId
            ? await context.EvidencePolicyVersions.SingleAsync(version => version.Id == frozenPolicyId)
            : await context.EvidencePolicyVersions
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

        if (obligation.EvidencePolicyVersionId is null)
        {
            context.Entry(obligation).Property(item => item.EvidencePolicyVersionId).CurrentValue = policy.Id;
        }

        var requirement = await context.EvidenceRequirementVersions
            .SingleOrDefaultAsync(item => item.PolicyVersionId == policy.Id);
        if (requirement is null)
        {
            requirement = new EvidenceRequirementVersion(
                Guid.CreateVersion7(),
                policy.Id,
                taskVersion.TaskDefinitionId,
                new EvidenceRequirementDefinition(
                    "CHECKLIST_COMPLETO",
                    EvidenceRequirementKinds.StructuredChecklist,
                    EvidenceConditionCodes.Always,
                    1));
            context.EvidenceRequirementVersions.Add(requirement);
        }

        var evidenceItem = new EvidenceItem(
            Guid.CreateVersion7(),
            obligationId,
            policy.Id,
            requirement.Id,
            requirement.RequirementCode,
            requirement.Kind,
            concludedAt.AddMinutes(-1));
        var checklist = JsonDocument.Parse(
            "{\"schemaVersion\":1,\"productCorrect\":true,\"zoneAndFamilyCorrect\":true," +
            "\"stableFormation\":true,\"labelsVisible\":true,\"alignmentConsistent\":true," +
            "\"occupancyJustified\":true,\"clean\":true,\"intact\":true,\"signageCorrect\":true," +
            "\"matchesPlanogramOrList\":true}");
        var evidenceVersion = new EvidenceVersion(
            Guid.CreateVersion7(), evidenceItem.Id, 1, checklist, responsibleUserId, concludedAt.AddMinutes(-1));
        context.EvidenceItems.Add(evidenceItem);
        context.EvidenceVersions.Add(evidenceVersion);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        obligation = await context.WorkObligations.SingleAsync(item => item.Id == obligationId);
        var evaluation = EvidenceReviewEvaluator.Evaluate(new EvidenceReviewEvaluationInput(
            obligationId,
            policy.Id,
            taskCode,
            [new EvidenceReviewExpectedRequirement(
                requirement.RequirementCode, requirement.Kind, requirement.ConditionCode, requirement.Ordinal)],
            [new EvidenceReviewRequirementInput(
                requirement.Id,
                requirement.RequirementCode,
                requirement.Kind,
                requirement.ConditionCode,
                requirement.Ordinal,
                true,
                evidenceItem.Id,
                [new EvidenceReviewVersionInput(
                    evidenceVersion.Id,
                    EvidenceVersionStatuses.Current,
                    null,
                    checklist,
                    null)])]));
        var snapshot = new EvidenceReviewSnapshot(
            Guid.CreateVersion7(), obligation.Id, policy.Id, evaluation,
            responsibleUserId, concludedAt, Guid.CreateVersion7());
        var result = new ExecutionResult(
            Guid.CreateVersion7(), obligation.Id, snapshot.Id, responsibleUserId, concludedAt);

        obligation.Conclude(responsibleUserId, concludedAt, obligation.RowVersion);
        context.EvidenceReviewSnapshots.Add(snapshot);
        context.ExecutionResults.Add(result);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}
