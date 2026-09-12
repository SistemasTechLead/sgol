using System.Globalization;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Idempotency;

internal static class IdempotencyProtocol
{
    public const short CurrentVersion = 1;
    public const string CompletedStatus = "COMPLETED";

    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false
    };

    public static IdempotencyRecord Completed(
        string scope,
        Guid key,
        string requestHash,
        string resourceType,
        Guid resourceId,
        int responseCode,
        object responsePayload,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string? responseEtag = null,
        string? responseLocation = null)
    {
        var started = Stopwatch.GetTimestamp();
        var record = new IdempotencyRecord
        {
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            Status = CompletedStatus,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ResponseCode = responseCode,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            ProtocolVersion = CurrentVersion,
            ResponsePayload = JsonSerializer.SerializeToDocument(responsePayload, SnapshotOptions),
            ResponseEtag = responseEtag,
            ResponseLocation = responseLocation
        };
        IdempotencyTelemetry.RecordRequest(OperationFromScope(scope), "accepted", started);
        return record;
    }

    public static T ReadPayload<T>(IdempotencyRecord record)
    {
        var started = Stopwatch.GetTimestamp();
        var operation = OperationFromScope(record.Scope);
        if (record.ProtocolVersion != CurrentVersion || record.ResponsePayload is null)
        {
            IdempotencyTelemetry.ReplayUnavailable.Add(1,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("protocol", Convert.ToString(record.ProtocolVersion, CultureInfo.InvariantCulture)));
            IdempotencyTelemetry.RecordRequest(operation, "failed", started);
            throw new IdempotencyReplayUnavailableException();
        }

        var payload = record.ResponsePayload.Deserialize<T>(SnapshotOptions);
        if (payload is null)
        {
            IdempotencyTelemetry.ReplayUnavailable.Add(1,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("protocol", Convert.ToString(record.ProtocolVersion, CultureInfo.InvariantCulture)));
            IdempotencyTelemetry.RecordRequest(operation, "failed", started);
            throw new IdempotencyReplayUnavailableException();
        }

        IdempotencyTelemetry.RecordRequest(operation, "recovered", started);
        return payload;
    }

    public static string HashCanonical(
        string operation,
        string actor,
        string resource,
        object? body,
        long? ifMatch = null)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = CurrentVersion,
            ["apiVersion"] = "v1",
            ["operation"] = Normalize(operation),
            ["actor"] = Normalize(actor),
            ["resource"] = Normalize(resource),
            ["ifMatch"] = ifMatch,
            ["body"] = body is null
                ? null
                : JsonSerializer.SerializeToNode(body, SnapshotOptions)
        };

        var canonical = Canonicalize(root);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string Scope(string actor, string operation, string resource) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"idem:v1|api:v1|actor:{Normalize(actor)}|operation:{Normalize(operation)}|resource:{Normalize(resource)}");

    public static AuditEvent ConflictAudit(
        Guid id,
        DateTimeOffset occurredAt,
        Guid? actorUserId,
        string resourceType,
        Guid? resourceId,
        Guid branchId,
        Guid correlationId,
        Guid requestId,
        string operation) => new()
        {
            Id = id,
            OccurredAt = occurredAt,
            ActorUserId = actorUserId,
            ActorType = actorUserId.HasValue ? "USER" : "SYSTEM",
            Action = "IDEMPOTENCY_CONFLICT_REJECTED",
            ResourceType = resourceType,
            ResourceId = resourceId,
            BranchId = branchId,
            CorrelationId = correlationId,
            RequestId = requestId.ToString("D"),
            AfterData = JsonSerializer.SerializeToDocument(new
            {
                operation,
                protocolVersion = CurrentVersion,
                reasonCode = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_CONTENT",
            }, SnapshotOptions),
            Outcome = "REJECTED",
        };

    public static async Task PersistConflictAsync(
        AuditTransaction auditTransaction,
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var operation = auditEvent.AfterData?.RootElement.GetProperty("operation").GetString() ?? "UNKNOWN";
        try
        {
            await auditTransaction.ExecuteAsync(
                auditEvent,
                _ => Task.CompletedTask,
                cancellationToken);
            IdempotencyTelemetry.RecordRequest(operation, "rejected", started);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            IdempotencyTelemetry.RecordRequest(operation, "failed", started);
            throw new IdempotencyConflictAuditException(exception);
        }
    }

    public static async Task DelayBeforeRetryAsync(
        string operation,
        Exception exception,
        int attempt,
        CancellationToken cancellationToken)
    {
        var sqlState = FindSqlState(exception);
        IdempotencyTelemetry.Retries.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("sqlstate_class", sqlState is { Length: >= 2 } ? sqlState[..2] : "unknown"));
        var baseDelayMs = Math.Min(20 * (1 << Math.Max(0, attempt - 1)), 80);
        await Task.Delay(TimeSpan.FromMilliseconds(baseDelayMs + Random.Shared.Next(0, 16)), cancellationToken);
    }

    private static string Canonicalize(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject value => "{" + string.Join(',', value
            .OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => JsonSerializer.Serialize(Normalize(property.Key), SnapshotOptions) + ":" + Canonicalize(property.Value))) + "}",
        JsonArray value => "[" + string.Join(',', value.Select(Canonicalize)) + "]",
        JsonValue value => CanonicalizeValue(value),
        _ => throw new InvalidOperationException("Unsupported JSON node for idempotency canonicalization.")
    };

    private static string CanonicalizeValue(JsonValue value)
    {
        if (value.TryGetValue<string>(out var text))
        {
            return JsonSerializer.Serialize(Normalize(text), SnapshotOptions);
        }

        return value.ToJsonString(SnapshotOptions);
    }

    private static string Normalize(string value) => value.Normalize(NormalizationForm.FormC);

    private static string OperationFromScope(string scope)
    {
        const string marker = "|operation:";
        var start = scope.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return "UNKNOWN";
        }

        start += marker.Length;
        var end = scope.IndexOf("|resource:", start, StringComparison.Ordinal);
        return end < 0 ? scope[start..] : scope[start..end];
    }

    private static string? FindSqlState(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException postgresException)
            {
                return postgresException.SqlState;
            }
        }

        return null;
    }
}

internal static class IdempotencyTelemetry
{
    private static readonly Meter Meter = new("Sgol.Idempotency");

    internal static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("idempotency_requests_total");
    internal static readonly Counter<long> Retries =
        Meter.CreateCounter<long>("idempotency_retries_total");
    internal static readonly Counter<long> ReplayUnavailable =
        Meter.CreateCounter<long>("idempotency_replay_unavailable_total");
    internal static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("idempotency_request_duration_seconds", "s");

    internal static void RecordRequest(string operation, string outcome, long started)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "outcome", outcome },
        };
        Requests.Add(1, tags);
        Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
    }
}

internal sealed class IdempotencyReplayUnavailableException()
    : Exception("The confirmed idempotency response cannot be reconstructed safely.");

public sealed class IdempotencyConflictAuditException(Exception innerException)
    : Exception("The idempotency conflict audit could not be persisted.", innerException);
