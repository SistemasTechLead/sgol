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
    public static async Task<Guid> EnsurePolicyAsync(
        SgolDbContext context,
        Guid taskDefinitionVersionId,
        DateTimeOffset effectiveAt)
    {
        await context.SaveChangesAsync();

        var taskVersion = await context.TaskDefinitionVersions.AsNoTracking()
            .SingleAsync(version => version.Id == taskDefinitionVersionId);
        var taskCode = await context.TaskDefinitions.AsNoTracking()
            .Where(task => task.Id == taskVersion.TaskDefinitionId)
            .Select(task => task.TaskCode)
            .SingleAsync();
        var policy = await context.EvidencePolicyVersions
            .SingleOrDefaultAsync(version => version.TaskDefinitionVersionId == taskVersion.Id);
        if (policy is null)
        {
            var policyId = Guid.CreateVersion7();
            var nextVersion = await context.EvidencePolicyVersions.AsNoTracking()
                .Where(version => version.TaskDefinitionId == taskVersion.TaskDefinitionId)
                .Select(version => (int?)version.VersionNo)
                .MaxAsync() ?? 0;
            policy = new EvidencePolicyVersion(
                policyId,
                taskVersion.TaskDefinitionId,
                taskVersion.Id,
                taskVersion.ReleaseId,
                null,
                nextVersion + 1);
            policy.ApplyPublished(new VersionRecord(
                policyId,
                VersionStatuses.Current,
                effectiveAt,
                null,
                "Synthetic conclusion fixture",
                null,
                2));
            context.EvidencePolicyVersions.Add(policy);
        }

        var hasRequirements = await context.EvidenceRequirementVersions.AsNoTracking()
            .AnyAsync(item => item.PolicyVersionId == policy.Id);
        if (hasRequirements)
        {
            return policy.Id;
        }

        context.EvidenceRequirementVersions.AddRange(EvidencePolicyCatalog.Require(taskCode)
            .Select(definition => new EvidenceRequirementVersion(
                Guid.CreateVersion7(),
                policy.Id,
                taskVersion.TaskDefinitionId,
                definition)));
        await context.SaveChangesAsync();
        return policy.Id;
    }

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
            InternalNoticeTestData.AddAssignmentWithNotice(context, new AssignmentVersion(
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
        var frozenPolicyId = obligation.EvidencePolicyVersionId ??
            throw new InvalidOperationException("The conclusion fixture requires a policy frozen at obligation creation.");
        var policy = await context.EvidencePolicyVersions.SingleAsync(version => version.Id == frozenPolicyId);

        var requirements = await context.EvidenceRequirementVersions
            .Where(item => item.PolicyVersionId == policy.Id)
            .OrderBy(item => item.Ordinal)
            .ToListAsync();
        if (requirements.Count == 0)
        {
            throw new InvalidOperationException("The frozen evidence policy has no requirements.");
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
            if (requirement.Kind is EvidenceRequirementKinds.Photograph or EvidenceRequirementKinds.ReferencedDocument)
            {
                file = CreateCleanFile(responsibleUserId, obligationId, policy.Id, requirement, item.Id, concludedAt);
                version = new EvidenceVersion(
                    Guid.CreateVersion7(), item.Id, 1, file.Id, responsibleUserId, concludedAt.AddMinutes(-1));
                context.FileObjects.Add(file);
            }
            else
            {
                var payload = CreateStructuredPayload(requirement.RequirementCode);
                version = new EvidenceVersion(
                    Guid.CreateVersion7(), item.Id, 1, payload, responsibleUserId, concludedAt.AddMinutes(-1));
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
        var documentSubtype = requirement.RequirementCode == "DOCUMENTO_RECEPCION" ? "NOTA" : null;
        var file = new FileObject(
            Guid.CreateVersion7(),
            BranchScope.LorettaId,
            obligationId,
            policyId,
            requirement.Id,
            requirement.RequirementCode,
            requirement.Kind,
            documentSubtype,
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

    private static JsonDocument CreateStructuredPayload(string requirementCode) => requirementCode switch
    {
        "CALCULO_AVANCE" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"expectedTarget\":100,\"actualSales\":90,\"sourceReference\":\"VEN-01\"}"),
        "ACCION_O_CONFORMIDAD" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"outcome\":\"CONFORMIDAD\",\"actionDescription\":null," +
            "\"responsiblePersonId\":null,\"startsAt\":null}"),
        "LIBERACION" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"releasedAt\":\"2026-09-08T22:20:00Z\",\"releaseReference\":\"LIB-01\"}"),
        "MERCANCIA" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"merchandiseReference\":\"MER-01\"}"),
        "FECHA_HORA" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"occurredAt\":\"2026-09-08T22:20:00Z\"}"),
        "RETORNO_EXHIBICION" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"returnedAt\":\"2026-09-08T22:20:00Z\",\"returnReference\":\"RET-01\"}"),
        "SECUENCIA" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"sequenceSummary\":\"Secuencia sintetica\"}"),
        "DECISION" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"decisionSummary\":\"Decision sintetica\"," +
            "\"decidedAt\":\"2026-09-08T22:20:00Z\"}"),
        "FUNDAMENTO" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"foundationSummary\":\"Fundamento sintetico\"}"),
        "AVISO_INTERNO" or "CONSTANCIA_AVISO_INTERNO" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"noticeReference\":\"AVI-01\",\"notifiedAt\":\"2026-09-08T22:20:00Z\"}"),
        "EVALUACION" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"assessmentSummary\":\"Evaluacion sintetica\"," +
            "\"assessedAt\":\"2026-09-08T22:20:00Z\"}"),
        "REPARACION_O_CAMBIO" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"solutionType\":\"REPARACION\",\"solutionReference\":\"SOL-01\"," +
            "\"completedAt\":\"2026-09-08T22:20:00Z\"}"),
        "ENTREGA" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"deliveryReference\":\"ENT-01\",\"deliveredAt\":\"2026-09-08T22:20:00Z\"}"),
        "CHECKLIST_COMPLETO" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"productCorrect\":true,\"zoneAndFamilyCorrect\":true," +
            "\"stableFormation\":true,\"labelsVisible\":true,\"alignmentConsistent\":true," +
            "\"occupancyJustified\":true,\"clean\":true,\"intact\":true,\"signageCorrect\":true," +
            "\"matchesPlanogramOrList\":true}"),
        "FORM_ADM_02" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"formCode\":\"FORM-ADM-02\",\"formReference\":\"ADM-01\"," +
            "\"completedAt\":\"2026-09-08T22:20:00Z\"}"),
        "F_ENT_001" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"formCode\":\"F-ENT-001\",\"formReference\":\"REC-01\"," +
            "\"completedAt\":\"2026-09-08T22:20:00Z\",\"hasDifference\":false,\"hasDamage\":false}"),
        "ANOTACION_F_ENT_001" => JsonDocument.Parse(
            "{\"schemaVersion\":1,\"formCode\":\"F-ENT-001\",\"formReference\":\"REC-01\"," +
            "\"annotationReference\":\"ANO-01\",\"recordedAt\":\"2026-09-08T22:20:00Z\"}"),
        _ => throw new InvalidOperationException($"No structured payload exists for {requirementCode}."),
    };

    private sealed record ConclusionEvidence(
        EvidenceRequirementVersion Requirement,
        EvidenceItem Item,
        EvidenceVersion Version,
        FileObject? File);
}
