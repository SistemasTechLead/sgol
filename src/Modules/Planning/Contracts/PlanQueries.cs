namespace Sgol.Planning.Contracts;

public sealed record WorkPlanDetails(Guid PlanId, Guid BranchId, string BranchCode, Guid PeriodId,
    int IsoYear, int IsoWeek, string Status, long RowVersion);
public sealed record WorkPlanRead(WorkPlanDetails Plan, DateTimeOffset QueriedAt);
public sealed record PlanVersionDetails(Guid PublicationId, Guid PlanId, int VersionNo, string VersionStatus,
    string ScopeRole, Guid PublishedBy, DateTimeOffset PublishedAt, Guid? SupersedesId, long PlanRowVersion);
public sealed record PlanSnapshotDetails(PlanVersionDetails Publication, IReadOnlyList<PlanPublicationItem> Obligations);

// Internal pagination state; only its protected representation crosses the HTTP boundary.
public sealed record PlanReadCursor(Guid ActorUserId, string RoleCode, Guid PlanId, Guid? PublicationId,
    int Limit, int? AfterVersionNo, Guid? AfterObligationId);
public sealed record PlanVersionsRequest(Guid ActorUserId, Guid PlanId, Guid? PublicationId = null,
    int Limit = 50, PlanReadCursor? Cursor = null);
public sealed record PlanVersionsRead(IReadOnlyList<PlanVersionDetails> Versions, PlanSnapshotDetails? Snapshot,
    PlanReadCursor? NextCursor, long RowVersion, DateTimeOffset QueriedAt);

public interface IPlanQueryReader
{
    Task<WorkPlanRead> ReadPlanAsync(Guid actorUserId, int isoYear, int isoWeek,
        CancellationToken cancellationToken = default);
    Task<PlanVersionsRead> ReadVersionsAsync(PlanVersionsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PlanQueryException(string errorCode, int responseCode)
    : WorkPlanException(errorCode, responseCode, "The plan query could not be completed.");
