namespace Sgol.Reporting.Contracts;

public static class IndicatorAuthorization
{
    public const string View = "PER-INDICADOR-VER";
}

public static class DirectionOverviewAuthorization
{
    public const string View = "PER-DIRECCION-VER";
}

public sealed record IndicatorRequest(
    Guid ActorUserId,
    int IsoYear,
    int IsoWeek,
    string? Level,
    Guid? ResponsiblePersonId,
    string? Cursor,
    int Limit);

public sealed record IndicatorPeriod(
    int IsoYear,
    int IsoWeek,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string TimeZone);

public sealed record IndicatorScope(
    string BranchCode,
    string ActorRole,
    IReadOnlyList<string> IncludedLevels,
    string? Level,
    Guid? ResponsiblePersonId);

public sealed record IndicatorCount(long Count, long Denominator);
public sealed record IndicatorPerson(Guid Id, string StableCode, string DisplayName);
public sealed record ActiveLoadByPersonItem(IndicatorPerson Person, string Level, long Count);
public sealed record ActiveLoadByPerson(long Denominator, IReadOnlyList<ActiveLoadByPersonItem> Items);

public sealed record IndicatorSnapshot(
    IndicatorPeriod Period,
    IndicatorScope Scope,
    long BaseObligationsCount,
    IndicatorCount Pending,
    IndicatorCount Concluded,
    IndicatorCount Validated,
    IndicatorCount NonCompliant,
    ActiveLoadByPerson ActiveLoadByPerson);

public sealed record IndicatorResult(
    IndicatorSnapshot Snapshot,
    string? NextCursor,
    DateTimeOffset QueriedAt);

public interface IIndicatorReader
{
    Task<IndicatorResult> ReadAsync(
        IndicatorRequest request,
        CancellationToken cancellationToken = default);

    Task<IndicatorResult> ReadDirectionOverviewAsync(
        IndicatorRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class IndicatorAccessDeniedException() : Exception;
public sealed class IndicatorFilterInvalidException() : Exception;
public sealed class IndicatorQueryInconsistentException() : Exception;
public sealed class DirectionOverviewAccessDeniedException() : Exception;
public sealed class DirectionOverviewFilterInvalidException() : Exception;
public sealed class DirectionOverviewQueryInconsistentException() : Exception;
