using System.Text.Json;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.JobInfrastructure;

public interface IOutboxWriter
{
    OutboxEvent Enqueue(
        string eventType,
        Guid? aggregateId,
        JsonElement data,
        Guid correlationId,
        DateTimeOffset? availableAt = null);
}

public interface IOutboxHandler
{
    string EventType { get; }

    bool IsPayloadValid(JsonElement data);

    Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken);
}

public sealed record OutboxDeliveryContext(
    Guid EventId,
    string EventType,
    Guid? AggregateId,
    Guid CorrelationId,
    JsonElement Data,
    SgolDbContext DbContext);

public interface IScheduledJob
{
    string Name { get; }

    string ConcurrencyExhaustedErrorCode => "POSTGRES_CONCURRENCY_EXHAUSTED";

    Task ExecuteAsync(ScheduledJobContext context, CancellationToken cancellationToken);
}

public sealed record ScheduledJobContext(
    Guid RunId,
    DateTimeOffset ScheduledFor,
    string? Checkpoint,
    SgolDbContext DbContext,
    Guid CorrelationId = default,
    int Attempt = 1);

public sealed class JobExecutionException(string errorCode) : Exception
{
    public string ErrorCode { get; } = JobContract.ValidateErrorCode(errorCode);
}

internal static class JobContract
{
    internal const int MaximumAttempts = 5;
    internal const int MaximumJsonBytes = 65_536;
    internal const int MaximumJsonDepth = 16;

    internal static string ValidateErrorCode(string errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 128 ||
            errorCode.Any(character => !(character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')))
        {
            throw new ArgumentException("Error code must use the approved uppercase allowlist.", nameof(errorCode));
        }

        return errorCode;
    }
}
