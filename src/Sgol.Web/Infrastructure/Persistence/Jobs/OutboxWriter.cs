using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.JobInfrastructure;

public sealed partial class OutboxWriter(
    SgolDbContext dbContext,
    OutboxHandlerRegistry registry,
    IClock clock,
    IUuidGenerator uuidGenerator) : IOutboxWriter
{
    public OutboxEvent Enqueue(
        string eventType,
        Guid? aggregateId,
        JsonElement data,
        Guid correlationId,
        DateTimeOffset? availableAt = null)
    {
        if (!EventTypeRegex().IsMatch(eventType) || !registry.TryGet(eventType, out var handler) || handler is null)
        {
            throw new ArgumentException("Event type is not registered.", nameof(eventType));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Correlation identifier is required.", nameof(correlationId));
        }

        ValidateJson(data);
        if (!handler.IsPayloadValid(data))
        {
            throw new ArgumentException("Event payload does not match the registered schema.", nameof(data));
        }

        var now = clock.UtcNow;
        var eligibility = availableAt ?? now;
        if (eligibility < now)
        {
            throw new ArgumentOutOfRangeException(nameof(availableAt));
        }

        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            correlationId,
            data
        });
        if (Encoding.UTF8.GetByteCount(payload) > JobContract.MaximumJsonBytes)
        {
            throw new ArgumentException("Event payload exceeds the approved limit.", nameof(data));
        }

        var outboxEvent = new OutboxEvent
        {
            Id = uuidGenerator.NewUuid(),
            EventType = eventType,
            AggregateId = aggregateId,
            Payload = payload,
            CreatedAt = now,
            AvailableAt = eligibility
        };
        dbContext.OutboxEvents.Add(outboxEvent);
        return outboxEvent;
    }

    private static void ValidateJson(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Event data must be a JSON object.", nameof(data));
        }

        using var document = JsonDocument.Parse(
            data.GetRawText(),
            new JsonDocumentOptions { MaxDepth = JobContract.MaximumJsonDepth });
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_.]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex EventTypeRegex();
}
