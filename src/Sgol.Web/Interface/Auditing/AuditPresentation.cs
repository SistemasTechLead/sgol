using System.Text.Json;
using Sgol.Auditing.Contracts;
using Sgol.Web.Presentation.ApiClient;

namespace Sgol.Web.Presentation.Auditing;

public static class AuditPresentation
{
    private static readonly HashSet<string> Allowed = new("schemaVersion obligationId generationRequestId taskDefinitionVersionId configurationReleaseId assignmentId previousAssignmentId evidenceItemId evidenceVersionId previousEvidenceVersionId requirementId decisionVersionId previousDecisionVersionId policyVersionId evidenceReviewSnapshotId taskCode branchCode versionNo rowVersion status result outcome executionStatus assignmentType authorityType validatorRole requiredRole effectiveFrom effectiveTo assignedAt concludedAt decidedAt evidenceVersionCount selfValidation".Split(' '), StringComparer.Ordinal);
    public static void Validate(AuditEventDetails data)
    {
        if (data is null || data.Id == Guid.Empty || data.OccurredAt.Offset != TimeSpan.Zero || data.Actor is null || data.Resource is null ||
            data.Scope is null || data.Change is null || data.Reason is null || data.CorrelationId == Guid.Empty || data.Change.OmittedFieldCount < 0) throw new ApiProtocolException();
        foreach (var change in new[] { data.Change.Before, data.Change.After })
            if (change is not null && change.Any(p => !Allowed.Contains(p.Key) || p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Undefined ||
                p.Value.ValueKind == JsonValueKind.String && p.Value.GetString()!.Length > 128)) throw new ApiProtocolException();
    }
    public static string Display(IReadOnlyDictionary<string, JsonElement>? source, string key) => source is null || !source.TryGetValue(key, out var value) ? "No informado" :
        value.ValueKind == JsonValueKind.Null ? "Nulo" : value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
}
