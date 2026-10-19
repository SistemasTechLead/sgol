using System.Text.Json;

namespace Sgol.Web.Infrastructure.Persistence.Auditing;

public sealed class AuditEvent
{
    public Guid Id { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public Guid? ActorUserId { get; init; }

    public required string ActorType { get; init; }

    public required string Action { get; init; }

    public required string ResourceType { get; init; }

    public Guid? ResourceId { get; init; }

    public Guid? BranchId { get; init; }

    public Guid CorrelationId { get; init; }

    public string? RequestId { get; init; }

    public JsonDocument? BeforeData { get; init; }

    public JsonDocument? AfterData { get; init; }

    public string? Reason { get; init; }

    public required string Outcome { get; init; }

    public string? SourceIpHash { get; init; }
}
