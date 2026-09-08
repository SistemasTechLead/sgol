using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sgol.Evidence.Contracts;

public static class EvidenceReviewResults
{
    public const string Complete = "COMPLETA";
    public const string Incomplete = "INCOMPLETA";
}

public static class EvidenceReviewApplicability
{
    public const string Applicable = "APLICABLE";
    public const string NotApplicable = "NO_APLICABLE";
    public const string Unresolved = "NO_RESUELTA";
}

public static class EvidenceReviewMissingReasons
{
    public const string CurrentEvidenceAbsent = "EVIDENCIA_VIGENTE_AUSENTE";
    public const string CurrentEvidenceDoesNotSatisfy = "EVIDENCIA_VIGENTE_NO_SATISFACE";
}

public sealed record EvidenceReviewFileInput(
    Guid FileId,
    Guid RequirementVersionId,
    Guid? LinkedEvidenceItemId,
    string ScanStatus,
    string BucketClass);

public sealed record EvidenceReviewVersionInput(
    Guid EvidenceVersionId,
    string Status,
    Guid? FileObjectId,
    JsonDocument? StructuredPayload,
    EvidenceReviewFileInput? File);

public sealed record EvidenceReviewRequirementInput(
    Guid RequirementVersionId,
    string RequirementCode,
    string Kind,
    string ConditionCode,
    short Ordinal,
    bool IsRequired,
    Guid? EvidenceItemId,
    IReadOnlyList<EvidenceReviewVersionInput> Versions);

public sealed record EvidenceReviewExpectedRequirement(
    string RequirementCode,
    string Kind,
    string ConditionCode,
    short Ordinal);

public sealed record EvidenceReviewEvaluationInput(
    Guid ObligationId,
    Guid EvidencePolicyVersionId,
    string TaskCode,
    IReadOnlyList<EvidenceReviewExpectedRequirement> ExpectedRequirements,
    IReadOnlyList<EvidenceReviewRequirementInput> Requirements);

public sealed record EvidenceReviewRequirementDetails(
    Guid RequirementVersionId,
    string RequirementCode,
    string Kind,
    string ConditionCode,
    short Ordinal,
    string Applicability,
    bool Satisfied,
    Guid? EvidenceVersionId,
    string? MissingReason);

public sealed record EvidenceReviewMissingRequirement(
    Guid RequirementVersionId,
    string RequirementCode,
    string Kind,
    string ConditionCode,
    short Ordinal,
    string MissingReason);

public sealed record EvidenceReviewEvaluation(
    string Result,
    IReadOnlyList<EvidenceReviewRequirementDetails> Requirements,
    IReadOnlyList<EvidenceReviewMissingRequirement> MissingRequirements,
    IReadOnlyList<Guid> EvidenceVersionIds,
    string CanonicalInput,
    string InputFingerprint);

public sealed record EvidenceReviewDetails(
    Guid SnapshotId,
    Guid ObligationId,
    Guid EvidencePolicyVersionId,
    string Result,
    DateTimeOffset EvaluatedAt,
    IReadOnlyList<EvidenceReviewRequirementDetails> Requirements,
    IReadOnlyList<EvidenceReviewMissingRequirement> MissingRequirements);

public sealed record EvidenceReviewQuery(
    Guid ActorUserId,
    Guid CorrelationId,
    Guid ObligationId);

public interface IEvidenceReviewService
{
    Task<EvidenceReviewDetails> ReviewAsync(
        EvidenceReviewQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class EvidenceReviewSnapshot
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private EvidenceReviewSnapshot()
    {
    }

    public EvidenceReviewSnapshot(
        Guid id,
        Guid obligationId,
        Guid evidencePolicyVersionId,
        EvidenceReviewEvaluation evaluation,
        Guid requestedByUserId,
        DateTimeOffset evaluatedAt,
        Guid correlationId)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        if (id == Guid.Empty || obligationId == Guid.Empty || evidencePolicyVersionId == Guid.Empty ||
            requestedByUserId == Guid.Empty || correlationId == Guid.Empty ||
            evaluation.Result is not (EvidenceReviewResults.Complete or EvidenceReviewResults.Incomplete) ||
            evaluation.InputFingerprint.Length != 64)
        {
            throw new ArgumentException("Invalid evidence review snapshot.");
        }

        Id = id;
        ObligationId = obligationId;
        EvidencePolicyVersionId = evidencePolicyVersionId;
        Result = evaluation.Result;
        SchemaVersion = 1;
        InputFingerprint = evaluation.InputFingerprint;
        CanonicalInput = JsonDocument.Parse(evaluation.CanonicalInput);
        RequirementsSnapshot = JsonSerializer.SerializeToDocument(evaluation.Requirements, JsonOptions);
        MissingRequirements = JsonSerializer.SerializeToDocument(evaluation.MissingRequirements, JsonOptions);
        EvidenceVersionIds = [.. evaluation.EvidenceVersionIds];
        EvaluatedBy = "SYSTEM";
        RequestedByUserId = requestedByUserId;
        EvaluatedAt = evaluatedAt;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private init; }
    public Guid ObligationId { get; private init; }
    public Guid EvidencePolicyVersionId { get; private init; }
    public string Result { get; private init; } = null!;
    public short SchemaVersion { get; private init; }
    public string InputFingerprint { get; private init; } = null!;
    public JsonDocument CanonicalInput { get; private init; } = null!;
    public JsonDocument RequirementsSnapshot { get; private init; } = null!;
    public JsonDocument MissingRequirements { get; private init; } = null!;
    public Guid[] EvidenceVersionIds { get; private init; } = null!;
    public string EvaluatedBy { get; private init; } = null!;
    public Guid RequestedByUserId { get; private init; }
    public DateTimeOffset EvaluatedAt { get; private init; }
    public Guid CorrelationId { get; private init; }
}

public static class EvidenceReviewEvaluator
{
    private const string Always = "SIEMPRE";
    private const string DifferenceOrDamage = "DIFERENCIA_O_DANO";

    public static EvidenceReviewEvaluation Evaluate(EvidenceReviewEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var ordered = input.Requirements
            .OrderBy(requirement => requirement.Ordinal)
            .ThenBy(requirement => requirement.RequirementVersionId)
            .ToArray();

        ValidatePolicy(input.ExpectedRequirements, ordered);
        var states = ordered.ToDictionary(
            requirement => requirement.RequirementCode,
            requirement => BuildState(input.TaskCode, input.EvidencePolicyVersionId, requirement),
            StringComparer.Ordinal);

        try
        {
            var condition = ResolveDifferenceOrDamage(input.TaskCode, states);
            var details = new List<EvidenceReviewRequirementDetails>(ordered.Length);
            var canonical = new List<CanonicalRequirement>(ordered.Length);
            foreach (var requirement in ordered)
            {
                var state = states[requirement.RequirementCode];
                var applicability = requirement.ConditionCode switch
                {
                    Always => EvidenceReviewApplicability.Applicable,
                    DifferenceOrDamage when condition.State == ConditionState.True => EvidenceReviewApplicability.Applicable,
                    DifferenceOrDamage when condition.State == ConditionState.False => EvidenceReviewApplicability.NotApplicable,
                    DifferenceOrDamage when condition.State == ConditionState.Unresolved => EvidenceReviewApplicability.Unresolved,
                    _ => throw new EvidenceReviewUnavailableException(),
                };

                var satisfied = applicability == EvidenceReviewApplicability.NotApplicable ||
                    applicability == EvidenceReviewApplicability.Applicable && Satisfies(requirement, state, states);
                var usedVersionId = applicability == EvidenceReviewApplicability.Applicable ? state.Current?.EvidenceVersionId : null;
                var missingReason = applicability == EvidenceReviewApplicability.Applicable && !satisfied
                    ? state.Current is null
                        ? EvidenceReviewMissingReasons.CurrentEvidenceAbsent
                        : EvidenceReviewMissingReasons.CurrentEvidenceDoesNotSatisfy
                    : null;

                var detail = new EvidenceReviewRequirementDetails(
                    requirement.RequirementVersionId,
                    requirement.RequirementCode,
                    requirement.Kind,
                    requirement.ConditionCode,
                    requirement.Ordinal,
                    applicability,
                    satisfied,
                    usedVersionId,
                    missingReason);
                details.Add(detail);
                canonical.Add(new CanonicalRequirement(detail, usedVersionId is null ? null : requirement.EvidenceItemId,
                    requirement.ConditionCode == DifferenceOrDamage
                        ? condition.EvidenceVersionId
                        : null));
            }

            var missing = details
                .Where(requirement => requirement.Applicability == EvidenceReviewApplicability.Applicable && !requirement.Satisfied)
                .Select(requirement => new EvidenceReviewMissingRequirement(
                    requirement.RequirementVersionId,
                    requirement.RequirementCode,
                    requirement.Kind,
                    requirement.ConditionCode,
                    requirement.Ordinal,
                    requirement.MissingReason!))
                .ToArray();
            var complete = missing.Length == 0 && details.All(requirement => requirement.Applicability != EvidenceReviewApplicability.Unresolved);
            var result = complete ? EvidenceReviewResults.Complete : EvidenceReviewResults.Incomplete;
            var canonicalInput = WriteCanonicalInput(input, canonical);
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalInput))).ToLowerInvariant();
            var versionIds = details.Where(requirement => requirement.EvidenceVersionId.HasValue)
                .Select(requirement => requirement.EvidenceVersionId!.Value)
                .Distinct()
                .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
                .ToArray();

            return new(result, details, missing, versionIds, canonicalInput, fingerprint);
        }
        finally
        {
            foreach (var state in states.Values)
            {
                state.CanonicalPayload?.Dispose();
            }
        }
    }

    private static void ValidatePolicy(
        IReadOnlyList<EvidenceReviewExpectedRequirement> expected,
        EvidenceReviewRequirementInput[] actual)
    {
        if (actual.Length != expected.Count || actual.Select(item => item.RequirementVersionId).Distinct().Count() != actual.Length)
        {
            throw new EvidenceReviewUnavailableException();
        }

        for (var index = 0; index < expected.Count; index++)
        {
            var definition = expected[index];
            var requirement = actual[index];
            if (!requirement.IsRequired || requirement.Ordinal != definition.Ordinal ||
                !string.Equals(requirement.RequirementCode, definition.RequirementCode, StringComparison.Ordinal) ||
                !string.Equals(requirement.Kind, definition.Kind, StringComparison.Ordinal) ||
                !string.Equals(requirement.ConditionCode, definition.ConditionCode, StringComparison.Ordinal))
            {
                throw new EvidenceReviewUnavailableException();
            }
        }
    }

    private static RequirementState BuildState(
        string taskCode,
        Guid policyVersionId,
        EvidenceReviewRequirementInput requirement)
    {
        if (requirement.EvidenceItemId is null && requirement.Versions.Count != 0)
        {
            throw new EvidenceReviewUnavailableException();
        }

        var current = requirement.Versions.Where(version => version.Status == EvidenceVersionStatuses.Current).ToArray();
        if (current.Length > 1 || requirement.Versions.Any(version => version.Status is not (EvidenceVersionStatuses.Current or EvidenceVersionStatuses.Superseded)))
        {
            throw new EvidenceReviewUnavailableException();
        }

        if (current.Length == 0)
        {
            return new(requirement, null, null);
        }

        var version = current[0];
        if (requirement.EvidenceItemId is null || (version.FileObjectId.HasValue == (version.StructuredPayload is not null)))
        {
            throw new EvidenceReviewUnavailableException();
        }

        JsonDocument? canonicalPayload = null;
        if (version.StructuredPayload is not null)
        {
            try
            {
                canonicalPayload = StructuredEvidencePayloadValidator.ValidateAndCanonicalize(
                    taskCode,
                    requirement.RequirementCode,
                    requirement.Kind,
                    version.StructuredPayload.RootElement);
            }
            catch (EvidenceRequestInvalidException)
            {
                throw new EvidenceReviewUnavailableException();
            }
        }
        else
        {
            var file = version.File ?? throw new EvidenceReviewUnavailableException();
            if (file.FileId != version.FileObjectId || file.RequirementVersionId != requirement.RequirementVersionId ||
                file.LinkedEvidenceItemId != requirement.EvidenceItemId || file.ScanStatus != EvidenceFileStatuses.Clean ||
                file.BucketClass != EvidenceBucketClasses.Clean || policyVersionId == Guid.Empty)
            {
                throw new EvidenceReviewUnavailableException();
            }
        }

        return new(requirement, version, canonicalPayload);
    }

    private static ConditionResolution ResolveDifferenceOrDamage(
        string taskCode,
        IReadOnlyDictionary<string, RequirementState> states)
    {
        if (taskCode != "TAR-0092")
        {
            return new(ConditionState.NotUsed, null);
        }

        if (!states.TryGetValue("F_ENT_001", out var form))
        {
            throw new EvidenceReviewUnavailableException();
        }

        if (form.Current is null)
        {
            return new(ConditionState.Unresolved, null);
        }

        if (form.CanonicalPayload is null ||
            !StructuredEvidencePayloadValidator.TryResolveDifferenceOrDamage(form.CanonicalPayload.RootElement, out var applies))
        {
            throw new EvidenceReviewUnavailableException();
        }

        return new(applies ? ConditionState.True : ConditionState.False, form.Current.EvidenceVersionId);
    }

    private static bool Satisfies(
        EvidenceReviewRequirementInput requirement,
        RequirementState state,
        IReadOnlyDictionary<string, RequirementState> states)
    {
        if (state.Current is null)
        {
            return false;
        }

        if (state.Current.FileObjectId.HasValue)
        {
            return true;
        }

        var payload = state.CanonicalPayload?.RootElement ?? throw new EvidenceReviewUnavailableException();
        if (requirement.RequirementCode == "CHECKLIST_COMPLETO")
        {
            return payload.EnumerateObject()
                .Where(property => property.Name != "schemaVersion")
                .All(property => property.Value.GetBoolean());
        }

        if (requirement.RequirementCode == "ACCION_O_CONFORMIDAD")
        {
            if (!states.TryGetValue("CALCULO_AVANCE", out var calculation) || calculation.CanonicalPayload is null)
            {
                return false;
            }

            var calculationPayload = calculation.CanonicalPayload.RootElement;
            var percentage = calculationPayload.GetProperty("actualSales").GetDecimal() /
                calculationPayload.GetProperty("expectedTarget").GetDecimal() * 100m;
            var expectedOutcome = percentage < 90m ? "ACCION" : "CONFORMIDAD";
            return payload.GetProperty("outcome").GetString() == expectedOutcome;
        }

        return true;
    }

    private static string WriteCanonicalInput(
        EvidenceReviewEvaluationInput input,
        IReadOnlyList<CanonicalRequirement> requirements)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("obligationId", input.ObligationId);
            writer.WriteString("evidencePolicyVersionId", input.EvidencePolicyVersionId);
            writer.WriteString("taskCode", input.TaskCode);
            writer.WriteStartArray("requirements");
            foreach (var requirement in requirements)
            {
                var detail = requirement.Detail;
                writer.WriteStartObject();
                writer.WriteString("requirementVersionId", detail.RequirementVersionId);
                writer.WriteString("requirementCode", detail.RequirementCode);
                writer.WriteString("kind", detail.Kind);
                writer.WriteString("conditionCode", detail.ConditionCode);
                writer.WriteNumber("ordinal", detail.Ordinal);
                writer.WriteString("applicability", detail.Applicability);
                writer.WriteBoolean("satisfied", detail.Satisfied);
                WriteGuidOrNull(writer, "evidenceItemId", requirement.EvidenceItemId);
                WriteGuidOrNull(writer, "evidenceVersionId", detail.EvidenceVersionId);
                WriteGuidOrNull(writer, "conditionEvidenceVersionId", requirement.ConditionEvidenceVersionId);
                if (detail.MissingReason is null) writer.WriteNull("missingReason");
                else writer.WriteString("missingReason", detail.MissingReason);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteGuidOrNull(Utf8JsonWriter writer, string name, Guid? value)
    {
        if (value.HasValue) writer.WriteString(name, value.Value);
        else writer.WriteNull(name);
    }

    private sealed record RequirementState(
        EvidenceReviewRequirementInput Requirement,
        EvidenceReviewVersionInput? Current,
        JsonDocument? CanonicalPayload);

    private sealed record CanonicalRequirement(
        EvidenceReviewRequirementDetails Detail,
        Guid? EvidenceItemId,
        Guid? ConditionEvidenceVersionId);

    private sealed record ConditionResolution(ConditionState State, Guid? EvidenceVersionId);
    private enum ConditionState { NotUsed, True, False, Unresolved }
}

public sealed class EvidenceReviewAccessDeniedException() : Exception;
public sealed class EvidenceReviewObligationNotFoundException() : Exception;
public sealed class EvidenceReviewUnavailableException() : Exception;
public sealed class EvidenceReviewFailedException() : Exception;
