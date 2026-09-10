using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;

namespace Sgol.Configuration.Contracts;

public static class ValidationPolicyAuthorization
{
    public const string Administer = "PER-VALIDACION-CONFIG";
}

public static class ValidationPolicyValues
{
    public const string ImmediateSuperior = "SUPERIOR_INMEDIATO";
    public const string Fulfilled = "CUMPLIDA";
    public const string Incomplete = "INCOMPLETA";
    public const string NotFulfilled = "NO_CUMPLIDA";

    public static IReadOnlyList<string> AllowedResults { get; } =
        [Fulfilled, Incomplete, NotFulfilled];
}

public sealed record ValidationPolicyDefinition(string ExecutorRole, string ValidatorRole);

public static class ValidationPolicyCatalog
{
    private static readonly Dictionary<string, ValidationPolicyDefinition> Definitions =
        new Dictionary<string, ValidationPolicyDefinition>(StringComparer.Ordinal)
        {
            ["TAR-0005"] = new("SUBCOORDINACION", "ADMINISTRACION"),
            ["TAR-0007"] = new("PISO_VENTAS", "SUBCOORDINACION"),
            ["TAR-0008"] = new("SUBCOORDINACION", "ADMINISTRACION"),
            ["TAR-0011"] = new("SUBCOORDINACION", "ADMINISTRACION"),
            ["TAR-0018"] = new("PISO_VENTAS", "SUBCOORDINACION"),
            ["TAR-0026"] = new("ADMINISTRACION", "DIRECCION"),
            ["TAR-0092"] = new("SUBCOORDINACION", "ADMINISTRACION"),
            ["TAR-0093"] = new("SUBCOORDINACION", "ADMINISTRACION"),
        };

    public static IReadOnlyDictionary<string, ValidationPolicyDefinition> All => Definitions;

    public static ValidationPolicyDefinition Require(string taskCode)
    {
        _ = TaskDefinitionCatalog.Require(taskCode);
        return Definitions[taskCode];
    }

    public static void Validate(
        string taskCode,
        bool isRequired,
        string executorRole,
        string validatorRelation,
        string validatorRole,
        IReadOnlyCollection<string> allowedResults)
    {
        ArgumentNullException.ThrowIfNull(allowedResults);
        var approved = Require(taskCode);
        if (!isRequired ||
            !string.Equals(executorRole, approved.ExecutorRole, StringComparison.Ordinal) ||
            !string.Equals(validatorRelation, ValidationPolicyValues.ImmediateSuperior, StringComparison.Ordinal) ||
            !string.Equals(validatorRole, approved.ValidatorRole, StringComparison.Ordinal))
        {
            throw new ValidationPolicyValidationException(
                "La obligatoriedad, relación y roles deben coincidir con la autoridad canónica aprobada para la TAR.");
        }

        if (allowedResults.Count != ValidationPolicyValues.AllowedResults.Count ||
            allowedResults.Any(string.IsNullOrWhiteSpace) ||
            allowedResults.Distinct(StringComparer.Ordinal).Count() != ValidationPolicyValues.AllowedResults.Count ||
            ValidationPolicyValues.AllowedResults.Any(result => !allowedResults.Contains(result, StringComparer.Ordinal)))
        {
            throw new ValidationPolicyValidationException(
                "La política debe contener exactamente CUMPLIDA, INCOMPLETA y NO_CUMPLIDA.");
        }
    }
}

public sealed class ValidationPolicyVersion : IVersionedEntity
{
    private ValidationPolicyVersion()
    {
    }

    public ValidationPolicyVersion(
        Guid id,
        Guid taskDefinitionId,
        Guid taskDefinitionVersionId,
        Guid releaseId,
        Guid? basedOnId,
        int versionNo,
        bool isRequired,
        string executorRole,
        string validatorRelation,
        string validatorRole,
        IReadOnlyCollection<string> allowedResults)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNo, 1);
        var taskCode = TaskDefinitionCatalog.All.Single(item => item.Id == taskDefinitionId).TaskCode;
        ValidationPolicyCatalog.Validate(
            taskCode,
            isRequired,
            executorRole,
            validatorRelation,
            validatorRole,
            allowedResults);

        Id = id;
        TaskDefinitionId = taskDefinitionId;
        TaskDefinitionVersionId = taskDefinitionVersionId;
        ReleaseId = releaseId;
        BasedOnId = basedOnId;
        VersionNo = versionNo;
        IsRequired = true;
        ExecutorRole = executorRole;
        ValidatorRelation = validatorRelation;
        ValidatorRole = validatorRole;
        AllowedResults = JsonSerializer.SerializeToDocument(ValidationPolicyValues.AllowedResults);
        Status = VersionStatuses.Draft;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }
    public Guid TaskDefinitionId { get; private init; }
    public Guid TaskDefinitionVersionId { get; private init; }
    public Guid ReleaseId { get; private init; }
    public Guid? BasedOnId { get; private init; }
    public int VersionNo { get; private init; }
    public bool IsRequired { get; private init; }
    public string ExecutorRole { get; private init; } = null!;
    public string ValidatorRelation { get; private init; } = null!;
    public string ValidatorRole { get; private init; } = null!;
    public JsonDocument AllowedResults { get; private init; } = null!;
    public string Status { get; private set; } = null!;
    public DateTimeOffset? EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public string? Reason { get; private set; }
    public Guid? SupersedesId { get; private set; }
    public long RowVersion { get; private set; }

    public VersionRecord ToVersionRecord() =>
        new(Id, Status, EffectiveFrom, EffectiveTo, Reason, SupersedesId, RowVersion);

    public void ApplyPublished(VersionRecord version) => Apply(version, VersionStatuses.Current);
    public void ApplySuperseded(VersionRecord version) => Apply(version, VersionStatuses.Superseded);

    private void Apply(VersionRecord version, string requiredStatus)
    {
        if (version.Id != Id || version.Status != requiredStatus)
        {
            throw new VersioningStateException("The publication plan does not match this validation policy version.");
        }

        Status = version.Status;
        EffectiveFrom = version.EffectiveFrom;
        EffectiveTo = version.EffectiveTo;
        Reason = version.Reason;
        SupersedesId = version.SupersedesId;
        RowVersion = version.RowVersion;
    }
}

public sealed record ValidationPolicyVersionDetails(
    Guid PolicyVersionId,
    string TaskCode,
    Guid TaskDefinitionVersionId,
    Guid ConfigurationReleaseId,
    int VersionNo,
    bool IsRequired,
    string ExecutorRole,
    string ValidatorRelation,
    string ValidatorRole,
    IReadOnlyList<string> AllowedResults,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    Guid? BasedOnPolicyVersionId,
    Guid? SupersedesPolicyVersionId,
    long RowVersion);

public sealed record PutValidationPolicyCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    string TaskCode,
    Guid ReleaseId,
    bool IsRequired,
    string ExecutorRole,
    string ValidatorRelation,
    string ValidatorRole,
    IReadOnlyCollection<string> AllowedResults,
    long? ExpectedRowVersion);

public interface IValidationPolicyService
{
    Task<ValidationPolicyVersionDetails> PutAsync(
        PutValidationPolicyCommand command,
        CancellationToken cancellationToken = default);

    Task RecordRejectionAsync(
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class ValidationPolicyAccessDeniedException()
    : Exception($"{ValidationPolicyAuthorization.Administer} is required for LOR-001.");
public sealed class ValidationPolicyReleaseNotDraftException() : Exception("The configuration release must remain BORRADOR for LOR-001.");
public sealed class ValidationPolicyDefinitionPreconditionException() : Exception("The release must target its TAR draft or the applicable TAR version.");
public sealed class ValidationPolicyCoverageException() : Exception("A configuration publication must leave exactly eight complete validation policies.");
public sealed class ValidationPolicyIfMatchRequiredException() : Exception("If-Match is required to supersede the current validation policy.");
public sealed class ValidationPolicyIdempotencyConflictException() : Exception("The idempotency key was already used with different content.");
public sealed class ValidationPolicyOverlapException() : Exception("A policy already exists for this TAR in the release.");
public sealed class ValidationPolicyValidationException(string message) : Exception(message);
