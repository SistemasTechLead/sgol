using System.Text.Json;

namespace Sgol.Auditing.Contracts;

public static class AuditAuthorization
{
    public const string View = "PER-AUDITORIA-VER";
}

public sealed record AuditQueryRequest(
    Guid ActorUserId,
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? ActorFilter,
    string? ResourceType,
    Guid? ResourceId,
    string? Action,
    string? Outcome,
    Guid? CorrelationId,
    string BranchCode,
    string? Level,
    Guid? TraceObligationId,
    string? Cursor,
    int Limit);

public sealed record AuditActor(string Type, Guid? UserId);
public sealed record AuditResource(string Type, Guid? Id, string? BranchCode);
public sealed record AuditScope(Guid? SubjectPersonId, string? SubjectLevel, string? Relation, Guid? ObligationId);
public sealed record AuditChange(
    IReadOnlyDictionary<string, JsonElement>? Before,
    IReadOnlyDictionary<string, JsonElement>? After,
    int OmittedFieldCount);
public sealed record AuditReason(bool Provided);

public sealed record AuditEventDetails(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditActor Actor,
    string Action,
    AuditResource Resource,
    AuditScope Scope,
    AuditChange Change,
    AuditReason Reason,
    string Outcome,
    Guid CorrelationId);

public sealed record AuditSnapshot(DateTimeOffset? UpperOccurredAt, Guid? UpperEventId);
public sealed record AuditCompleteness(bool Configuration, bool Assignment, bool Evidence, bool Validation);
public sealed record AuditPage(
    IReadOnlyList<AuditEventDetails> Items,
    string? NextCursor,
    DateTimeOffset QueriedAt,
    AuditSnapshot Snapshot,
    AuditCompleteness? Completeness);
public sealed record AuditDetail(AuditEventDetails Event, DateTimeOffset QueriedAt);

public interface IAuditEventReader
{
    Task<AuditPage> ReadAsync(AuditQueryRequest request, CancellationToken cancellationToken = default);
    Task<AuditDetail> FindAsync(Guid actorUserId, Guid eventId, CancellationToken cancellationToken = default);
}

public interface IAuditSecurityEventWriter
{
    Task WriteDeleteAttemptAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid? resourceId,
        CancellationToken cancellationToken = default);
}

public sealed class AuditAccessDeniedException() : Exception;
public sealed class AuditFilterInvalidException() : Exception;
public sealed class AuditCursorInvalidException() : Exception;
public sealed class AuditEventNotFoundException() : Exception;
public sealed class AuditScopeInconsistentException() : Exception;
