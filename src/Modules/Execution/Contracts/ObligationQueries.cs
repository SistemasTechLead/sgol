namespace Sgol.Execution.Contracts;

public static class ObligationQueryAuthorization
{
    public const string View = "PER-TAREA-VER";
}

public static class ObligationConditions
{
    public const string Overdue = "VENCIDA";
    public const string NotOverdue = "NO_VENCIDA";
}

public static class ObligationOriginKinds
{
    public const string Manual = "MANUAL";
    public const string Recurring = "RECURRENTE";
}

public static class ObligationHistoryEventTypes
{
    public const string GenerationRequested = "GENERACION_SOLICITADA";
    public const string AutomaticAssignment = "ASIGNACION_AUTOMATICA";
    public const string CorrectedAssignment = "ASIGNACION_CORREGIDA";
    public const string IncludedInPublication = "PUBLICACION_INCLUIDA";
}

public static class ObligationHistoryActorTypes
{
    public const string Human = "HUMAN";
    public const string System = "SYSTEM";
}

public sealed record ObligationTaskVersion(
    Guid TaskDefinitionVersionId,
    int VersionNo,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int SchemaVersion);

public sealed record ObligationTask(
    Guid TaskDefinitionId,
    string TaskCode,
    string Name,
    ObligationTaskVersion Version);

public sealed record ObligationOrigin(
    string Kind,
    string Schema,
    string Reference,
    Guid GenerationRequestId,
    Guid ActivationRuleVersionId,
    DateTimeOffset RequestedAt);

public sealed record ObligationPeriod(
    Guid PeriodId,
    int IsoYear,
    int IsoWeek,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string TimeZone);

public sealed record ObligationDates(
    DateTimeOffset? DueAt,
    DateOnly? DueLocalDate,
    DateTimeOffset? ConcludedAt);

public sealed record ObligationPerson(
    Guid PersonId,
    string StableCode,
    string DisplayName);

public sealed record ObligationAssignmentSummary(
    Guid AssignmentId,
    ObligationPerson Responsible,
    string AssignmentType,
    DateTimeOffset AssignedAt);

public sealed record ObligationListItem(
    Guid ObligationId,
    ObligationTask Task,
    ObligationOrigin Origin,
    ObligationPeriod Period,
    ObligationDates Dates,
    string ExecutionStatus,
    string Condition,
    ObligationAssignmentSummary? CurrentAssignment,
    IReadOnlyDictionary<string, string> Links);

public sealed record ObligationGenerationRequest(
    Guid GenerationRequestId,
    string Result,
    DateTimeOffset RequestedAt,
    Guid? RequestedByUserId);

public sealed record ObligationHistoryAssignment(
    Guid AssignmentId,
    string Status,
    string AssignmentType,
    ObligationPerson Responsible,
    Guid? SupersedesAssignmentId);

public sealed record ObligationHistoryPublication(
    Guid PlanId,
    Guid PublicationId,
    int VersionNo,
    string Status,
    string ScopeRole,
    Guid AssignmentVersionId,
    Guid? SupersedesPublicationId);

public sealed record ObligationHistoryEvent(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    string ActorType,
    Guid? ActorUserId,
    string? Reason,
    ObligationHistoryAssignment? Assignment,
    ObligationHistoryPublication? Publication);

public sealed record ObligationDetail(
    Guid ObligationId,
    ObligationTask Task,
    ObligationOrigin Origin,
    ObligationPeriod Period,
    ObligationDates Dates,
    string ExecutionStatus,
    string Condition,
    ObligationAssignmentSummary? CurrentAssignment,
    IReadOnlyDictionary<string, string> Links,
    ObligationGenerationRequest GenerationRequest,
    IReadOnlyList<ObligationHistoryEvent> History);

public sealed record ObligationPage(
    IReadOnlyList<ObligationListItem> Items,
    string? NextCursor,
    DateTimeOffset QueriedAt);

public sealed record ObligationDetailPage(
    ObligationDetail Detail,
    string? HistoryNextCursor,
    DateTimeOffset QueriedAt);

public sealed record ObligationListRequest(
    Guid ActorUserId,
    Guid? PeriodId,
    string? TaskCode,
    string? ExecutionStatus,
    string? Condition,
    Guid? ResponsiblePersonId,
    string? Cursor,
    int Limit);

public sealed record ObligationDetailRequest(
    Guid ActorUserId,
    Guid ObligationId,
    string? HistoryCursor,
    int HistoryLimit);

public interface IObligationQueryReader
{
    Task<ObligationPage> ListAsync(
        ObligationListRequest request,
        CancellationToken cancellationToken = default);

    Task<ObligationDetailPage> GetAsync(
        ObligationDetailRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ObligationQueryAccessDeniedException()
    : Exception($"{ObligationQueryAuthorization.View} is required for LOR-001.");

public sealed class ObligationQueryNotFoundException()
    : Exception("The obligation does not exist or is outside the visible scope.");

public sealed class ObligationQueryFilterInvalidException()
    : Exception("The obligation query parameters are invalid.");

public sealed class ObligationHistoryFilterInvalidException()
    : Exception("The obligation history parameters are invalid.");

public sealed class ObligationQueryInconsistentException()
    : Exception("Persisted obligation data is inconsistent with the approved query contract.");
