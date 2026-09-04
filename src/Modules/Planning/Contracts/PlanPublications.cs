namespace Sgol.Planning.Contracts;

public static class PlanPublicationAuthorization
{
    public const string Publish = "PER-PLAN-PUBLICAR";
}

public static class PlanVersionStatuses
{
    public const string Current = "VIGENTE";
    public const string Superseded = "SUSTITUIDA";
}

public static class PlanPublicationResults
{
    public const string Initial = "PUBLICADA_INICIAL";
    public const string Incremental = "PUBLICADA_INCREMENTAL";
    public const string Recovered = "RECUPERADA";
}

public sealed class PlanVersion
{
    private PlanVersion()
    {
    }

    public PlanVersion(
        Guid id,
        Guid planId,
        int versionNo,
        string scopeRole,
        Guid publishedBy,
        DateTimeOffset publishedAt,
        Guid? supersedesId,
        long planRowVersion)
    {
        if (id == Guid.Empty || planId == Guid.Empty || publishedBy == Guid.Empty ||
            versionNo < 1 || planRowVersion < 2 || string.IsNullOrWhiteSpace(scopeRole) ||
            id == supersedesId)
        {
            throw new ArgumentException("Plan-publication values are invalid.");
        }

        Id = id;
        PlanId = planId;
        VersionNo = versionNo;
        Status = PlanVersionStatuses.Current;
        ScopeRole = scopeRole;
        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
        SupersedesId = supersedesId;
        PlanRowVersion = planRowVersion;
    }

    public Guid Id { get; private init; }
    public Guid PlanId { get; private init; }
    public int VersionNo { get; private init; }
    public string Status { get; private set; } = null!;
    public string ScopeRole { get; private init; } = null!;
    public Guid PublishedBy { get; private init; }
    public DateTimeOffset PublishedAt { get; private init; }
    public Guid? SupersedesId { get; private init; }
    public long PlanRowVersion { get; private init; }

    public void Supersede()
    {
        if (Status != PlanVersionStatuses.Current)
        {
            throw new InvalidOperationException("Only the current plan publication can be superseded.");
        }

        Status = PlanVersionStatuses.Superseded;
    }
}

public sealed class PlanVersionObligation
{
    private PlanVersionObligation()
    {
    }

    public PlanVersionObligation(Guid planVersionId, Guid obligationId, Guid assignmentVersionId)
    {
        if (planVersionId == Guid.Empty || obligationId == Guid.Empty || assignmentVersionId == Guid.Empty)
        {
            throw new ArgumentException("Publication-content identifiers are required.");
        }

        PlanVersionId = planVersionId;
        ObligationId = obligationId;
        AssignmentVersionId = assignmentVersionId;
    }

    public Guid PlanVersionId { get; private init; }
    public Guid ObligationId { get; private init; }
    public Guid AssignmentVersionId { get; private init; }
}

public sealed record PublishWorkPlanCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    Guid PlanId,
    long ExpectedRowVersion);

public sealed record PlanPublicationItem(Guid ObligationId, Guid AssignmentVersionId);

public sealed record PlanPublicationResult(
    string Result,
    Guid PlanId,
    string PlanStatus,
    Guid PublicationId,
    int VersionNo,
    string VersionStatus,
    string ScopeRole,
    Guid PublishedBy,
    DateTimeOffset PublishedAt,
    IReadOnlyList<PlanPublicationItem> Obligations,
    IReadOnlyList<PlanPublicationItem> AddedObligations,
    long RowVersion);

public interface IPlanPublicationService
{
    Task<PlanPublicationResult> PublishAsync(
        PublishWorkPlanCommand command,
        CancellationToken cancellationToken = default);
}

public abstract class PlanPublicationException(string errorCode, int responseCode, string message)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public int ResponseCode { get; } = responseCode;
}

public sealed class PlanPublicationAccessDeniedException()
    : PlanPublicationException("ACCESO_DENEGADO", 403, $"{PlanPublicationAuthorization.Publish} is required for LOR-001.");
public sealed class PlanPublicationNotFoundException()
    : PlanPublicationException("PLAN_NO_ENCONTRADO", 404, "The work plan does not exist in LOR-001.");
public sealed class PlanPublicationIdempotencyConflictException()
    : PlanPublicationException("IDEMPOTENCY_CONFLICT", 409, "The idempotency key was used with different content.");
public sealed class PlanPublicationStateConflictException()
    : PlanPublicationException("ESTADO_PLAN_INCOMPATIBLE", 409, "The work-plan state cannot be published.");
public sealed class PlanPublicationContentConflictException()
    : PlanPublicationException("CONTENIDO_PLAN_INCOMPATIBLE", 409, "The work-plan content is inconsistent.");
public sealed class PlanPublicationVersionConflictException()
    : PlanPublicationException("VERSION_CONFLICT", 412, "The work-plan version changed.");
public sealed class PlanPublicationUnassignedObligationException()
    : PlanPublicationException("OBLIGACION_SIN_ASIGNACION", 422, "An applicable obligation has no current assignment.");
public sealed class PlanPublicationAssignmentInvalidException()
    : PlanPublicationException("ASIGNACION_NO_PUBLICABLE", 422, "An applicable assignment is not publishable.");
public sealed class PlanPublicationEmptyException()
    : PlanPublicationException("SIN_OBLIGACIONES_PUBLICABLES", 422, "There are no publishable obligations for the initial publication.");
public sealed class PlanPublicationNoChangesException()
    : PlanPublicationException("SIN_NOVEDADES_PUBLICABLES", 422, "There are no new publishable obligations.");
public sealed class PlanPublicationConcurrencyException()
    : PlanPublicationException("PLAN_PUBLICATION_CONCURRENCY_CONFLICT", 409, "The publication could not be serialized.");
public sealed class PlanPublicationConflictException()
    : PlanPublicationException("PLAN_PUBLICATION_CONFLICT", 409, "The publication conflicts with persisted integrity.");
