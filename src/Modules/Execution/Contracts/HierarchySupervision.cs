namespace Sgol.Reporting.Contracts;

public static class SupervisionAuthorization
{
    public const string View = "PER-SUPERVISION-VER";
}

public static class PendingMaterializationStatuses
{
    public const string Materialized = "MATERIALIZED";
    public const string Derived = "DERIVED";
}

public static class EvidenceSourceKinds
{
    public const string File = "FILE";
    public const string Structured = "STRUCTURED";
}

public sealed record CurrentEvidenceRequirement(Guid RequirementVersionId, string RequirementCode, string Kind);
public sealed record CurrentEvidenceVersion(Guid EvidenceVersionId, int VersionNo, string Status,
    Guid SubmittedByUserId, DateTimeOffset SubmittedAt, string? Reason, Guid? SupersedesEvidenceVersionId);
public sealed record CurrentEvidenceSummary(Guid EvidenceItemId, long ItemRowVersion,
    CurrentEvidenceRequirement Requirement, CurrentEvidenceVersion Version, string SourceKind);
public sealed record SupervisionValidation(Sgol.Validation.Contracts.ValidationRequirementDetails? Requirement,
    Sgol.Validation.Contracts.ValidationDecisionDetails? CurrentDecision);

public sealed record SupervisionObligationItem(Sgol.Execution.Contracts.ObligationListItem Obligation,
    string ResponsibleLevel, IReadOnlyList<CurrentEvidenceSummary> CurrentEvidence,
    SupervisionValidation Validation, IReadOnlyDictionary<string, string> Links);

public sealed record AvailableValidationAuthority(string AuthorityType, string ValidatorRole,
    bool EscalationReasonRequired);
public sealed record PendingValidationItem(Sgol.Execution.Contracts.ObligationListItem Obligation,
    string ResponsibleLevel, DateTimeOffset PendingSince, string MaterializationStatus,
    Sgol.Validation.Contracts.ValidationRequirementDetails? ValidationRequirement,
    AvailableValidationAuthority AvailableAuthority, string DecisionEtag,
    IReadOnlyDictionary<string, string> Links);

public sealed record SupervisionRequest(Guid ActorUserId, string? Level, Guid? ResponsiblePersonId,
    int? IsoYear, int? IsoWeek, string? ExecutionStatus, string? Cursor, int Limit);
public sealed record PendingValidationsRequest(Guid ActorUserId, string? Level, Guid? ResponsiblePersonId,
    int? IsoYear, int? IsoWeek, string? Cursor, int Limit);
public sealed record SupervisionPage(IReadOnlyList<SupervisionObligationItem> Items, string? NextCursor,
    DateTimeOffset QueriedAt);
public sealed record PendingValidationsPage(IReadOnlyList<PendingValidationItem> Items, string? NextCursor,
    DateTimeOffset QueriedAt);

public interface IHierarchySupervisionReader
{
    Task<SupervisionPage> ReadSupervisionAsync(SupervisionRequest request,
        CancellationToken cancellationToken = default);
    Task<PendingValidationsPage> ReadPendingValidationsAsync(PendingValidationsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class SupervisionAccessDeniedException() : Exception;
public sealed class SupervisionFilterInvalidException() : Exception;
public sealed class PendingValidationsFilterInvalidException() : Exception;
public sealed class SupervisionQueryInconsistentException() : Exception;
public sealed class PendingValidationQueryInconsistentException() : Exception;
