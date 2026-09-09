using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
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

        var requirements = await context.EvidenceRequirementVersions
            .Where(item => item.PolicyVersionId == policy.Id)
            .OrderBy(item => item.Ordinal)
            .ToListAsync();
        if (requirements.Count == 0)
        {
            requirements = EvidencePolicyCatalog.Require(taskCode)
                .Select(definition => new EvidenceRequirementVersion(
                    Guid.CreateVersion7(),
                    policy.Id,
                    taskVersion.TaskDefinitionId,
                    definition))
                .ToList();
            context.EvidenceRequirementVersions.AddRange(requirements);
        }

        var evidence = new List<ConclusionEvidence>(requirements.Count);
        foreach (var requirement in requirements)
        {
            var item = new EvidenceItem(
                Guid.CreateVersion7(),
                obligationId,
                policy.Id,
                requirement.Id,
                requirement.RequirementCode,
                requirement.Kind,
                concludedAt.AddMinutes(-1));
            EvidenceVersion version;
            FileObject? file = null;
            if (requirement.RequirementCode == "CHECKLIST_COMPLETO")
            {
                var payload = JsonDocument.Parse(
                    "{\"schemaVersion\":1,\"productCorrect\":true,\"zoneAndFamilyCorrect\":true," +
                    "\"stableFormation\":true,\"labelsVisible\":true,\"alignmentConsistent\":true," +
                    "\"occupancyJustified\":true,\"clean\":true,\"intact\":true,\"signageCorrect\":true," +
                    "\"matchesPlanogramOrList\":true}");
                version = new EvidenceVersion(
                    Guid.CreateVersion7(), item.Id, 1, payload, responsibleUserId, concludedAt.AddMinutes(-1));
            }
            else if (requirement.RequirementCode == "F_ENT_001")
            {
                var payload = JsonDocument.Parse(
                    "{\"schemaVersion\":1,\"formCode\":\"F-ENT-001\",\"formReference\":\"CONCLUSION-FIXTURE\"," +
                    "\"completedAt\":\"2026-09-08T22:20:00Z\",\"hasDifference\":false,\"hasDamage\":false}");
                version = new EvidenceVersion(
                    Guid.CreateVersion7(), item.Id, 1, payload, responsibleUserId, concludedAt.AddMinutes(-1));
            }
            else
            {
                file = CreateCleanFile(responsibleUserId, obligationId, policy.Id, requirement, item.Id, concludedAt);
                version = new EvidenceVersion(
                    Guid.CreateVersion7(), item.Id, 1, file.Id, responsibleUserId, concludedAt.AddMinutes(-1));
                context.FileObjects.Add(file);
            }

            context.EvidenceItems.Add(item);
            context.EvidenceVersions.Add(version);
            evidence.Add(new(requirement, item, version, file));
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        obligation = await context.WorkObligations.SingleAsync(item => item.Id == obligationId);
        var evaluation = EvidenceReviewEvaluator.Evaluate(new EvidenceReviewEvaluationInput(
            obligationId,
            policy.Id,
            taskCode,
            EvidencePolicyCatalog.Require(taskCode)
                .Select(definition => new EvidenceReviewExpectedRequirement(
                    definition.Code, definition.Kind, definition.ConditionCode, definition.Ordinal))
                .ToArray(),
            evidence.Select(entry => new EvidenceReviewRequirementInput(
                entry.Requirement.Id,
                entry.Requirement.RequirementCode,
                entry.Requirement.Kind,
                entry.Requirement.ConditionCode,
                entry.Requirement.Ordinal,
                true,
                entry.Item.Id,
                [new EvidenceReviewVersionInput(
                    entry.Version.Id,
                    EvidenceVersionStatuses.Current,
                    entry.File?.Id,
                    entry.Version.StructuredPayload,
                    entry.File is null
                        ? null
                        : new EvidenceReviewFileInput(
                            entry.File.Id,
                            entry.File.RequirementVersionId,
                            entry.File.LinkedEvidenceItemId,
                            entry.File.ScanStatus,
                            entry.File.BucketClass))]))
                .ToArray()));
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

    private static FileObject CreateCleanFile(
        Guid actorUserId,
        Guid obligationId,
        Guid policyId,
        EvidenceRequirementVersion requirement,
        Guid evidenceItemId,
        DateTimeOffset concludedAt)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Guid.CreateVersion7().ToByteArray()));
        var photograph = requirement.Kind == EvidenceRequirementKinds.Photograph;
        var mediaType = photograph ? "image/png" : "application/pdf";
        var file = new FileObject(
            Guid.CreateVersion7(),
            BranchScope.LorettaId,
            obligationId,
            policyId,
            requirement.Id,
            requirement.RequirementCode,
            requirement.Kind,
            null,
            $"v1/{hash[..2]}/{hash[2..4]}/{hash}",
            photograph ? "evidence.png" : "evidence.pdf",
            mediaType,
            128,
            hash,
            actorUserId,
            concludedAt.AddMinutes(-4));
        file.ConfirmUpload(concludedAt.AddMinutes(-3));
        file.MarkClean(mediaType, "synthetic", concludedAt.AddMinutes(-2));
        file.Link(evidenceItemId, concludedAt.AddMinutes(-1));
        return file;
    }

    private sealed record ConclusionEvidence(
        EvidenceRequirementVersion Requirement,
        EvidenceItem Item,
        EvidenceVersion Version,
        FileObject? File);
}
