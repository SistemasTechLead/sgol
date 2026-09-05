using Sgol.BuildingBlocks.Versioning;

namespace Sgol.Configuration.Contracts;

public static class EvidencePolicyAuthorization
{
    public const string Administer = "PER-EVIDENCIA-CONFIG";
}

public static class EvidenceRequirementKinds
{
    public const string DigitalRecord = "REGISTRO_DIGITAL";
    public const string ReferencedDocument = "DOCUMENTO_REFERENCIADO";
    public const string Photograph = "FOTOGRAFIA";
    public const string ReferencedForm = "FORMULARIO_REFERENCIADO";
    public const string StructuredChecklist = "CHECKLIST_ESTRUCTURADO";
    public const string StructuredData = "DATO_ESTRUCTURADO";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        DigitalRecord,
        ReferencedDocument,
        Photograph,
        ReferencedForm,
        StructuredChecklist,
        StructuredData,
    };
}

public static class EvidenceConditionCodes
{
    public const string Always = "SIEMPRE";
    public const string DifferenceOrDamage = "DIFERENCIA_O_DANO";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Always,
        DifferenceOrDamage,
    };
}

public sealed record EvidenceRequirementDefinition(
    string Code,
    string Kind,
    string ConditionCode,
    short Ordinal);

public sealed record EvidenceRequirementInput(
    string Code,
    string Kind,
    string ConditionCode);

public static class EvidencePolicyCatalog
{
    private static EvidenceRequirementDefinition Requirement(
        string code,
        string kind,
        short ordinal,
        string conditionCode = EvidenceConditionCodes.Always) =>
        new(code, kind, conditionCode, ordinal);

    private static readonly Dictionary<string, IReadOnlyList<EvidenceRequirementDefinition>> Definitions =
        new Dictionary<string, IReadOnlyList<EvidenceRequirementDefinition>>(StringComparer.Ordinal)
        {
            ["TAR-0005"] =
            [
                Requirement("CALCULO_AVANCE", EvidenceRequirementKinds.DigitalRecord, 1),
                Requirement("ACCION_O_CONFORMIDAD", EvidenceRequirementKinds.DigitalRecord, 2),
            ],
            ["TAR-0007"] =
            [
                Requirement("LIBERACION", EvidenceRequirementKinds.DigitalRecord, 1),
                Requirement("MERCANCIA", EvidenceRequirementKinds.StructuredData, 2),
                Requirement("FECHA_HORA", EvidenceRequirementKinds.StructuredData, 3),
                Requirement("RETORNO_EXHIBICION", EvidenceRequirementKinds.DigitalRecord, 4),
            ],
            ["TAR-0008"] =
            [
                Requirement("EXPEDIENTE", EvidenceRequirementKinds.ReferencedDocument, 1),
                Requirement("SECUENCIA", EvidenceRequirementKinds.DigitalRecord, 2),
                Requirement("DECISION", EvidenceRequirementKinds.DigitalRecord, 3),
                Requirement("FUNDAMENTO", EvidenceRequirementKinds.StructuredData, 4),
                Requirement("AVISO_INTERNO", EvidenceRequirementKinds.DigitalRecord, 5),
            ],
            ["TAR-0011"] =
            [
                Requirement("EVALUACION", EvidenceRequirementKinds.DigitalRecord, 1),
                Requirement("AUTORIZACION", EvidenceRequirementKinds.ReferencedDocument, 2),
                Requirement("REPARACION_O_CAMBIO", EvidenceRequirementKinds.DigitalRecord, 3),
                Requirement("COMPROBANTES", EvidenceRequirementKinds.ReferencedDocument, 4),
                Requirement("ENTREGA", EvidenceRequirementKinds.DigitalRecord, 5),
            ],
            ["TAR-0018"] =
            [
                Requirement("CHECKLIST_COMPLETO", EvidenceRequirementKinds.StructuredChecklist, 1),
                Requirement("FOTOGRAFIA_FINAL", EvidenceRequirementKinds.Photograph, 2),
                Requirement("PLANOGRAMA_O_LISTA", EvidenceRequirementKinds.ReferencedDocument, 3),
            ],
            ["TAR-0026"] =
            [
                Requirement("FORM_ADM_02", EvidenceRequirementKinds.ReferencedForm, 1),
                Requirement("COMPROBANTE_LOCALIZABLE", EvidenceRequirementKinds.ReferencedDocument, 2),
            ],
            ["TAR-0092"] =
            [
                Requirement("DOCUMENTO_RECEPCION", EvidenceRequirementKinds.ReferencedDocument, 1),
                Requirement("F_ENT_001", EvidenceRequirementKinds.ReferencedForm, 2),
                Requirement(
                    "FOTO_DIFERENCIA_DANO",
                    EvidenceRequirementKinds.Photograph,
                    3,
                    EvidenceConditionCodes.DifferenceOrDamage),
            ],
            ["TAR-0093"] =
            [
                Requirement("FOTOGRAFIA_INCIDENCIA", EvidenceRequirementKinds.Photograph, 1),
                Requirement("ANOTACION_F_ENT_001", EvidenceRequirementKinds.ReferencedForm, 2),
                Requirement("CONSTANCIA_AVISO_INTERNO", EvidenceRequirementKinds.DigitalRecord, 3),
            ],
        };

    public static IReadOnlyDictionary<string, IReadOnlyList<EvidenceRequirementDefinition>> All => Definitions;

    public static IReadOnlyList<EvidenceRequirementDefinition> Require(string taskCode)
    {
        _ = TaskDefinitionCatalog.Require(taskCode);
        return Definitions[taskCode];
    }

    public static IReadOnlyList<EvidenceRequirementDefinition> Validate(
        string taskCode,
        IReadOnlyCollection<EvidenceRequirementInput> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        var expected = Require(taskCode);
        if (requirements.Count != expected.Count ||
            requirements.Any(item =>
                string.IsNullOrWhiteSpace(item.Code) ||
                string.IsNullOrWhiteSpace(item.Kind) ||
                string.IsNullOrWhiteSpace(item.ConditionCode)) ||
            requirements.GroupBy(item => item.Code, StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            throw new EvidencePolicyValidationException("La política debe contener exactamente una vez cada requisito aprobado para la TAR.");
        }

        var supplied = requirements.ToDictionary(item => item.Code, StringComparer.Ordinal);
        foreach (var definition in expected)
        {
            if (!supplied.TryGetValue(definition.Code, out var item) ||
                !string.Equals(item.Kind, definition.Kind, StringComparison.Ordinal) ||
                !string.Equals(item.ConditionCode, definition.ConditionCode, StringComparison.Ordinal))
            {
                throw new EvidencePolicyValidationException("El requisito, su clase o su condición no coincide con el catálogo aprobado para la TAR.");
            }
        }

        return expected;
    }
}

public sealed class EvidenceRequirementCatalogEntry
{
    private EvidenceRequirementCatalogEntry()
    {
    }

    public EvidenceRequirementCatalogEntry(
        Guid taskDefinitionId,
        string requirementCode,
        string kind,
        string conditionCode,
        short ordinal)
    {
        TaskDefinitionId = taskDefinitionId;
        RequirementCode = requirementCode;
        Kind = kind;
        ConditionCode = conditionCode;
        Ordinal = ordinal;
    }

    public Guid TaskDefinitionId { get; private init; }
    public string RequirementCode { get; private init; } = null!;
    public string Kind { get; private init; } = null!;
    public string ConditionCode { get; private init; } = null!;
    public short Ordinal { get; private init; }
}

public sealed class EvidencePolicyVersion : IVersionedEntity
{
    private EvidencePolicyVersion()
    {
    }

    public EvidencePolicyVersion(
        Guid id,
        Guid taskDefinitionId,
        Guid taskDefinitionVersionId,
        Guid releaseId,
        Guid? basedOnId,
        int versionNo)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNo, 1);
        Id = id;
        TaskDefinitionId = taskDefinitionId;
        TaskDefinitionVersionId = taskDefinitionVersionId;
        ReleaseId = releaseId;
        BasedOnId = basedOnId;
        VersionNo = versionNo;
        Status = VersionStatuses.Draft;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }
    public Guid TaskDefinitionId { get; private init; }
    public Guid TaskDefinitionVersionId { get; private init; }
    public Guid ReleaseId { get; private init; }
    public Guid? BasedOnId { get; private init; }
    public int VersionNo { get; private init; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset? EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public string? Reason { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public long RowVersion { get; private set; }

    public VersionRecord ToVersionRecord() => new(Id, Status, EffectiveFrom, EffectiveTo, Reason, SupersedesId, RowVersion);

    public void ApplyPublished(VersionRecord version) => Apply(version, VersionStatuses.Current);
    public void ApplySuperseded(VersionRecord version) => Apply(version, VersionStatuses.Superseded);

    private void Apply(VersionRecord version, string requiredStatus)
    {
        if (version.Id != Id || version.Status != requiredStatus)
        {
            throw new VersioningStateException("The publication plan does not match this evidence policy version.");
        }

        Status = version.Status;
        EffectiveFrom = version.EffectiveFrom;
        EffectiveTo = version.EffectiveTo;
        Reason = version.Reason;
        SupersedesId = version.SupersedesId;
        RowVersion = version.RowVersion;
    }
}

public sealed class EvidenceRequirementVersion
{
    private EvidenceRequirementVersion()
    {
    }

    public EvidenceRequirementVersion(
        Guid id,
        Guid policyVersionId,
        Guid taskDefinitionId,
        EvidenceRequirementDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Id = id;
        PolicyVersionId = policyVersionId;
        TaskDefinitionId = taskDefinitionId;
        RequirementCode = definition.Code;
        Kind = definition.Kind;
        ConditionCode = definition.ConditionCode;
        Ordinal = definition.Ordinal;
        IsRequired = true;
    }

    public Guid Id { get; private init; }
    public Guid PolicyVersionId { get; private init; }
    public Guid TaskDefinitionId { get; private init; }
    public string RequirementCode { get; private init; } = null!;
    public string Kind { get; private init; } = null!;
    public string ConditionCode { get; private init; } = null!;
    public short Ordinal { get; private init; }
    public bool IsRequired { get; private init; }
}

public sealed record EvidenceConditionDetails(string Code);

public sealed record EvidenceRequirementVersionDetails(
    string Code,
    string Kind,
    EvidenceConditionDetails? Condition,
    bool IsRequired,
    short Ordinal);

public sealed record EvidencePolicyVersionDetails(
    Guid PolicyVersionId,
    string TaskCode,
    Guid TaskDefinitionVersionId,
    Guid ConfigurationReleaseId,
    int VersionNo,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    Guid? BasedOnPolicyVersionId,
    Guid? SupersedesPolicyVersionId,
    IReadOnlyList<EvidenceRequirementVersionDetails> Requirements,
    long RowVersion);

public sealed record PutEvidencePolicyCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    string TaskCode,
    Guid ReleaseId,
    IReadOnlyCollection<EvidenceRequirementInput> Requirements,
    long? ExpectedRowVersion);

public interface IEvidencePolicyService
{
    Task<EvidencePolicyVersionDetails> PutAsync(
        PutEvidencePolicyCommand command,
        CancellationToken cancellationToken = default);

    Task RecordRejectionAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class EvidencePolicyAccessDeniedException()
    : Exception($"{EvidencePolicyAuthorization.Administer} is required for LOR-001.");
public sealed class EvidencePolicyReleaseNotDraftException() : Exception("The configuration release must remain BORRADOR for LOR-001.");
public sealed class EvidencePolicyDefinitionPreconditionException() : Exception("The release must target its TAR draft or the applicable TAR version.");
public sealed class EvidencePolicyCoverageException() : Exception("A configuration publication must leave exactly eight complete evidence policies.");
public sealed class EvidencePolicyIfMatchRequiredException() : Exception("If-Match is required to supersede the current evidence policy.");
public sealed class EvidencePolicyIdempotencyConflictException() : Exception("The idempotency key was already used with different content.");
public sealed class EvidencePolicyOverlapException() : Exception("A policy already exists for this TAR in the release.");
public sealed class EvidencePolicyValidationException(string message) : Exception(message);
