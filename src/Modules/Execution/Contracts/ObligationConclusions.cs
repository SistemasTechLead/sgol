using System.Text.Json;

namespace Sgol.Execution.Contracts;

public static class ObligationConclusionAuthorization
{
    public const string Execute = "PER-TAREA-EJECUTAR";
}

public static class ObligationConclusionResultCodes
{
    public const string Concluded = "CONCLUIDA";
}

public sealed class ExecutionResult
{
    private ExecutionResult()
    {
    }

    public ExecutionResult(
        Guid id,
        Guid obligationId,
        Guid evidenceReviewId,
        Guid recordedBy,
        DateTimeOffset recordedAt)
    {
        if (id == Guid.Empty || obligationId == Guid.Empty || evidenceReviewId == Guid.Empty || recordedBy == Guid.Empty)
        {
            throw new ArgumentException("Execution-result identifiers are required.");
        }

        Id = id;
        ObligationId = obligationId;
        ResultCode = ObligationConclusionResultCodes.Concluded;
        ResultPayload = JsonDocument.Parse("{\"schemaVersion\":1}");
        EvidenceReviewId = evidenceReviewId;
        RecordedBy = recordedBy;
        RecordedAt = recordedAt;
    }

    public Guid Id { get; private init; }
    public Guid ObligationId { get; private init; }
    public string ResultCode { get; private init; } = null!;
    public JsonDocument ResultPayload { get; private init; } = null!;
    public Guid EvidenceReviewId { get; private init; }
    public Guid RecordedBy { get; private init; }
    public DateTimeOffset RecordedAt { get; private init; }
}

public sealed record ConcludeObligationCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    Guid ObligationId,
    long ExpectedRowVersion);

public sealed record ObligationConclusionExecutionResult(
    Guid Id,
    string ResultCode,
    JsonDocument ResultPayload,
    Guid EvidenceReviewSnapshotId,
    Guid RecordedBy,
    DateTimeOffset RecordedAt);

public sealed record ObligationConclusionResult(
    Guid ObligationId,
    string ExecutionStatus,
    DateTimeOffset ConcludedAt,
    Guid ConcludedBy,
    long RowVersion,
    ObligationConclusionExecutionResult ExecutionResult);

public interface IObligationConclusionService
{
    Task<ObligationConclusionResult> ConcludeAsync(
        ConcludeObligationCommand command,
        CancellationToken cancellationToken = default);
}

public abstract class ObligationConclusionException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class ObligationConclusionNotFoundException()
    : ObligationConclusionException("OBLIGACION_NO_ENCONTRADA");

public sealed class ObligationConclusionIdempotencyConflictException()
    : ObligationConclusionException("IDEMPOTENCY_CONFLICT");

public sealed class ObligationAlreadyConcludedException()
    : ObligationConclusionException("OBLIGACION_YA_CONCLUIDA");

public sealed class ObligationConclusionVersionConflictException()
    : ObligationConclusionException("VERSION_CONFLICT");

public sealed class ObligationConclusionInconsistentException()
    : ObligationConclusionException("CONCLUSION_INCONSISTENTE");

public sealed class ObligationConclusionConcurrencyException()
    : ObligationConclusionException("CONCLUSION_CONCURRENCIA_CONFLICTO");

public sealed class ObligationEvidenceMissingException(IReadOnlyList<string> requirementCodes)
    : ObligationConclusionException("EVIDENCIA_FALTANTE")
{
    public IReadOnlyList<string> RequirementCodes { get; } = requirementCodes;
}
