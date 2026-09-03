using Sgol.BuildingBlocks.Versioning;

namespace Sgol.Configuration.Contracts;

public static class EligibilityPolicyAuthorization
{
    public const string Administer = "PER-POLITICA-ADMIN";
}

public static class EligibilityPolicyCatalog
{
    private static readonly Dictionary<string, string> RequiredRoles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TAR-0005"] = "SUBCOORDINACION",
            ["TAR-0007"] = "PISO_VENTAS",
            ["TAR-0008"] = "SUBCOORDINACION",
            ["TAR-0011"] = "SUBCOORDINACION",
            ["TAR-0018"] = "PISO_VENTAS",
            ["TAR-0026"] = "ADMINISTRACION",
            ["TAR-0092"] = "SUBCOORDINACION",
            ["TAR-0093"] = "SUBCOORDINACION",
        };

    public static IReadOnlyDictionary<string, string> All => RequiredRoles;

    public static string RequireRole(string taskCode)
    {
        _ = TaskDefinitionCatalog.Require(taskCode);
        return RequiredRoles[taskCode];
    }

    public static void Validate(string taskCode, string requiredRole, bool requiresAvailability, string? requiredShift)
    {
        var approvedRole = RequireRole(taskCode);
        if (!string.Equals(requiredRole, approvedRole, StringComparison.Ordinal))
        {
            throw new EligibilityPolicyValidationException("El rol requerido no coincide con el rol canónico aprobado para la TAR.");
        }

        if (!requiresAvailability)
        {
            throw new EligibilityPolicyValidationException("La disponibilidad positiva es obligatoria.");
        }

        if (requiredShift is not null)
        {
            throw new EligibilityPolicyValidationException("F05 no aprueba una restricción de turno para las ocho TAR MVP.");
        }
    }
}

public sealed class EligibilityPolicyVersion : IVersionedEntity
{
    private EligibilityPolicyVersion()
    {
    }

    public EligibilityPolicyVersion(
        Guid id,
        Guid taskDefinitionId,
        Guid taskDefinitionVersionId,
        Guid releaseId,
        Guid? basedOnId,
        int versionNo,
        string requiredRole,
        bool requiresAvailability,
        string? requiredShift)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNo, 1);
        EligibilityPolicyCatalog.Validate(
            TaskDefinitionCatalog.All.Single(item => item.Id == taskDefinitionId).TaskCode,
            requiredRole,
            requiresAvailability,
            requiredShift);
        Id = id;
        TaskDefinitionId = taskDefinitionId;
        TaskDefinitionVersionId = taskDefinitionVersionId;
        ReleaseId = releaseId;
        BasedOnId = basedOnId;
        VersionNo = versionNo;
        RequiredRole = requiredRole;
        RequiresAvailability = requiresAvailability;
        RequiredShift = requiredShift;
        Status = VersionStatuses.Draft;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }
    public Guid TaskDefinitionId { get; private init; }
    public Guid TaskDefinitionVersionId { get; private init; }
    public Guid ReleaseId { get; private init; }
    public Guid? BasedOnId { get; private init; }
    public int VersionNo { get; private init; }
    public string RequiredRole { get; private init; } = null!;
    public bool RequiresAvailability { get; private init; }
    public string? RequiredShift { get; private init; }
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
            throw new VersioningStateException("The publication plan does not match this eligibility policy version.");
        }

        Status = version.Status;
        EffectiveFrom = version.EffectiveFrom;
        EffectiveTo = version.EffectiveTo;
        Reason = version.Reason;
        SupersedesId = version.SupersedesId;
        RowVersion = version.RowVersion;
    }
}

public sealed record EligibilityPolicyVersionDetails(
    Guid Id,
    string TaskCode,
    Guid TaskDefinitionVersionId,
    Guid ReleaseId,
    int VersionNo,
    string RequiredRole,
    bool RequiresAvailability,
    string? RequiredShift,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    Guid? SupersedesId,
    long RowVersion);

public sealed record PutEligibilityPolicyCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    string TaskCode,
    Guid ReleaseId,
    string RequiredRole,
    bool RequiresAvailability,
    string? RequiredShift,
    long? ExpectedRowVersion);

public interface IEligibilityPolicyService
{
    Task<IReadOnlyList<EligibilityPolicyVersionDetails>> ListAsync(Guid actorUserId, Guid correlationId, CancellationToken cancellationToken = default);
    Task<EligibilityPolicyVersionDetails> PutAsync(PutEligibilityPolicyCommand command, CancellationToken cancellationToken = default);
}

public sealed class EligibilityPolicyAccessDeniedException()
    : Exception($"{EligibilityPolicyAuthorization.Administer} is required for LOR-001.");
public sealed class EligibilityPolicyNotFoundException() : Exception("The eligibility policy does not exist.");
public sealed class EligibilityPolicyReleaseNotDraftException() : Exception("The configuration release must remain BORRADOR for LOR-001.");
public sealed class EligibilityPolicyDefinitionPreconditionException() : Exception("The release must target its TAR draft or the current TAR version.");
public sealed class EligibilityPolicyCoverageException() : Exception("A configuration publication must leave exactly eight eligibility policies linked to the applicable TAR versions.");
public sealed class EligibilityPolicyIfMatchRequiredException() : Exception("If-Match is required to supersede the current eligibility policy.");
public sealed class EligibilityPolicyIdempotencyConflictException() : Exception("The idempotency key was already used with different content.");
public sealed class EligibilityPolicyValidationException(string message) : Exception(message);
