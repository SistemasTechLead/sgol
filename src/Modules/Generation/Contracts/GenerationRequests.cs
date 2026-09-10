namespace Sgol.Generation.Contracts;

public static class GenerationRequestAuthorization
{
    public const string Create = "PER-OBLIGACION-CREAR";
}

public static class GenerationRequestResults
{
    public const string Accepted = "ACEPTADA";
    public const string Recovered = "RECUPERADA";
    public const string Rejected = "RECHAZADA";
}

public sealed class GenerationRequest
{
    private GenerationRequest()
    {
    }

    public GenerationRequest(
        Guid id,
        Guid idempotencyKey,
        string requestHash,
        Guid ruleVersionId,
        Guid branchId,
        Guid periodId,
        string originType,
        string originReference,
        Guid? requestedBy,
        DateTimeOffset requestedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(originType);
        ArgumentException.ThrowIfNullOrWhiteSpace(originReference);

        Id = id;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        RuleVersionId = ruleVersionId;
        BranchId = branchId;
        PeriodId = periodId;
        OriginType = originType;
        OriginReference = originReference;
        Result = GenerationRequestResults.Accepted;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt;
    }

    public Guid Id { get; private init; }
    public Guid IdempotencyKey { get; private init; }
    public string RequestHash { get; private init; } = null!;
    public Guid RuleVersionId { get; private init; }
    public Guid BranchId { get; private init; }
    public Guid PeriodId { get; private init; }
    public string OriginType { get; private init; } = null!;
    public string OriginReference { get; private init; } = null!;
    public string Result { get; private init; } = null!;
    public Guid? RequestedBy { get; private init; }
    public DateTimeOffset RequestedAt { get; private init; }
    public Guid? ObligationId { get; private set; }
    public string? ErrorCode { get; private set; }

    public void LinkObligation(Guid obligationId)
    {
        if (ObligationId is not null && ObligationId != obligationId)
        {
            throw new GenerationRequestAlreadyMaterializedException();
        }

        ObligationId = obligationId;
    }
}

public static class WorkObligationStatuses
{
    public const string Pending = "PENDIENTE";
    public const string Concluded = "CONCLUIDA";
}

public sealed class WorkObligation
{
    private WorkObligation()
    {
    }

    public WorkObligation(
        Guid id,
        Guid taskDefinitionVersionId,
        Guid branchId,
        Guid periodId,
        Guid generationRequestId,
        string originReference,
        Guid? evidencePolicyVersionId = null,
        Guid? validationPolicyVersionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originReference);

        Id = id;
        TaskDefinitionVersionId = taskDefinitionVersionId;
        BranchId = branchId;
        PeriodId = periodId;
        GenerationRequestId = generationRequestId;
        OriginReference = originReference;
        EvidencePolicyVersionId = evidencePolicyVersionId;
        ValidationPolicyVersionId = validationPolicyVersionId;
        ExecutionStatus = WorkObligationStatuses.Pending;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }
    public Guid TaskDefinitionVersionId { get; private init; }
    public Guid BranchId { get; private init; }
    public Guid PeriodId { get; private init; }
    public Guid GenerationRequestId { get; private init; }
    public string OriginReference { get; private init; } = null!;
    public Guid? EvidencePolicyVersionId { get; private init; }
    public Guid? ValidationPolicyVersionId { get; private init; }
    public System.Text.Json.JsonDocument? InputPayload { get; private init; }
    public DateTimeOffset? DueAt { get; private init; }
    public string ExecutionStatus { get; private set; } = null!;
    public DateTimeOffset? ConcludedAt { get; private set; }
    public Guid? ConcludedBy { get; private set; }
    public long RowVersion { get; private set; }

    public void AdvanceRowVersion(long expectedRowVersion)
    {
        if (RowVersion != expectedRowVersion)
        {
            throw new InvalidOperationException("VERSION_CONFLICT");
        }

        RowVersion = checked(RowVersion + 1);
    }

    public void Conclude(Guid actorUserId, DateTimeOffset concludedAt, long expectedRowVersion)
    {
        if (actorUserId == Guid.Empty || ExecutionStatus != WorkObligationStatuses.Pending ||
            ConcludedAt is not null || ConcludedBy is not null)
        {
            throw new InvalidOperationException("CONCLUSION_NO_PERMITIDA");
        }

        AdvanceRowVersion(expectedRowVersion);
        ExecutionStatus = WorkObligationStatuses.Concluded;
        ConcludedAt = concludedAt;
        ConcludedBy = actorUserId;
    }
}

public sealed record MaterializeWorkObligationCommand(
    Guid GenerationRequestId,
    Guid CorrelationId);

public sealed record WorkObligationDetails(
    Guid ObligationId,
    Guid GenerationRequestId,
    Guid TaskDefinitionVersionId,
    Guid BranchId,
    Guid PeriodId,
    string OriginReference,
    Guid? EvidencePolicyVersionId,
    Guid? ValidationPolicyVersionId,
    string ExecutionStatus,
    long RowVersion);

public interface IWorkObligationMaterializer
{
    Task<WorkObligationDetails> MaterializeAsync(
        MaterializeWorkObligationCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record CreateGenerationRequestCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    Guid RuleVersionId,
    Guid BranchId,
    Guid PeriodId,
    string OriginType,
    string OriginReference);

public sealed record GenerationRequestDetails(
    Guid GenerationRequestId,
    Guid RuleVersionId,
    Guid BranchId,
    Guid PeriodId,
    string OriginType,
    string OriginReference,
    string Result,
    Guid? RequestedBy,
    DateTimeOffset RequestedAt,
    Guid? ObligationId,
    string? ErrorCode);

public interface IGenerationRequestService
{
    Task<GenerationRequestDetails> CreateAsync(
        CreateGenerationRequestCommand command,
        CancellationToken cancellationToken = default);

    Task<GenerationRequestDetails> GetAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid generationRequestId,
        CancellationToken cancellationToken = default);
}

public sealed class GenerationRequestAccessDeniedException()
    : Exception($"{GenerationRequestAuthorization.Create} is required for the requested LOR-001 scope.");

public sealed class GenerationRequestNotFoundException() : Exception("The generation request does not exist or is outside the visible scope.");
public sealed class GenerationRequestRuleNotFoundException() : Exception("The activation rule does not exist.");
public sealed class GenerationRequestRuleNotManualException() : Exception("Only a current MANUAL activation rule accepts this request.");
public sealed class GenerationRequestTaskInactiveException() : Exception("The task definition is not active for new generation.");
public sealed class GenerationRequestPeriodNotFoundException() : Exception("The requested period does not exist in LOR-001.");
public sealed class GenerationRequestOriginInvalidException() : Exception("The origin does not satisfy the current originKeySchema.");
public sealed class GenerationRequestIdempotencyConflictException() : Exception("The idempotency key was already used with different content.");
public sealed class GenerationRequestValidationException(string message) : Exception(message);
public sealed class GenerationRequestNotAcceptedException() : Exception("Only an accepted generation request can be materialized.");
public sealed class GenerationRequestAlreadyMaterializedException() : Exception("The generation request already points to a different obligation.");
