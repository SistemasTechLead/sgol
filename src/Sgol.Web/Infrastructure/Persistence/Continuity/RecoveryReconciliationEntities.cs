namespace Sgol.Web.Infrastructure.Persistence.Continuity;

public sealed class RecoveryReconciliation
{
    public Guid Id { get; init; }
    public Guid BranchId { get; init; }
    public Guid RequestedBy { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
}

public sealed class RecoveryReconciliationEvent
{
    public Guid Id { get; init; }
    public Guid ReconciliationId { get; init; }
    public long Sequence { get; init; }
    public required string EventType { get; init; }
    public required string Status { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? TechnicalActor { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid CorrelationId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ReferenceManifestSha256 { get; init; }
    public string? ReportManifestSha256 { get; init; }
    public string? RestoreEvidenceSha256 { get; init; }
    public string? ReferenceRootSha256 { get; init; }
    public string? ActualRootSha256 { get; init; }
    public int? DifferenceCount { get; init; }
    public bool? DifferencesTruncated { get; init; }
    public long? ObservedRpoSeconds { get; init; }
    public long? ObservedRtoSeconds { get; init; }
    public string? Reason { get; init; }
}

public sealed class RecoveryReconciliationDifference
{
    public Guid Id { get; init; }
    public Guid ReconciliationId { get; init; }
    public int Ordinal { get; init; }
    public required string Group { get; init; }
    public required string ResourceType { get; init; }
    public required string StableKey { get; init; }
    public string? Field { get; init; }
    public required string Kind { get; init; }
    public string? ExpectedSha256 { get; init; }
    public string? ActualSha256 { get; init; }
}
