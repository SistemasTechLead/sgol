using System.Globalization;

namespace Sgol.BuildingBlocks.Versioning;

public static class VersionStatuses
{
    public const string Draft = "BORRADOR";
    public const string Current = "VIGENTE";
    public const string Superseded = "SUSTITUIDA";

    public static bool IsPublished(string status) =>
        status is Current or Superseded;
}

public interface IVersionedEntity
{
    Guid Id { get; }

    string Status { get; }

    DateTimeOffset? EffectiveFrom { get; }

    DateTimeOffset? EffectiveTo { get; }

    string? Reason { get; }

    Guid? SupersedesId { get; }

    long RowVersion { get; }
}

public sealed record VersionRecord(
    Guid Id,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    Guid? SupersedesId,
    long RowVersion) : IVersionedEntity;

public sealed record VersionPublicationPlan(
    VersionRecord? Superseded,
    VersionRecord Published);

public readonly record struct VersionInterval
{
    public VersionInterval(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
    {
        VersioningRules.RequireUtc(effectiveFrom, nameof(effectiveFrom));
        if (effectiveTo is DateTimeOffset end)
        {
            VersioningRules.RequireUtc(end, nameof(effectiveTo));
            if (end <= effectiveFrom)
            {
                throw new VersioningValidationException("The effective interval must have a positive duration.");
            }
        }

        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public DateTimeOffset EffectiveFrom { get; }

    public DateTimeOffset? EffectiveTo { get; }

    public bool Overlaps(VersionInterval other) =>
        (EffectiveTo is null || other.EffectiveFrom < EffectiveTo.Value) &&
        (other.EffectiveTo is null || EffectiveFrom < other.EffectiveTo.Value);
}

public static class VersionEtag
{
    public static string Format(long rowVersion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowVersion, 1);
        return $"\"{rowVersion.ToString(CultureInfo.InvariantCulture)}\"";
    }

    public static long ParseRequired(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new VersionIfMatchRequiredException();
        }

        var value = ifMatch.AsSpan().Trim();
        if (value.Length < 3 || value[0] != '"' || value[^1] != '"' ||
            !long.TryParse(value[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var rowVersion) ||
            rowVersion < 1)
        {
            throw new VersionEtagInvalidException();
        }

        return rowVersion;
    }
}

public static class VersioningRules
{
    public static VersionRecord CreateDraft(Guid id) => new(
        id,
        VersionStatuses.Draft,
        EffectiveFrom: null,
        EffectiveTo: null,
        Reason: null,
        SupersedesId: null,
        RowVersion: 1);

    public static void RequireAuthorized(bool explicitlyAuthorized)
    {
        if (!explicitlyAuthorized)
        {
            throw new VersioningAccessDeniedException();
        }
    }

    public static VersionPublicationPlan PlanPublication(
        VersionRecord draft,
        VersionRecord? current,
        IEnumerable<VersionRecord> publishedHistory,
        long expectedRowVersion,
        DateTimeOffset effectiveFrom,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(publishedHistory);
        RequireUtc(effectiveFrom, nameof(effectiveFrom));
        RequireDraft(draft);
        RequireExpectedRowVersion(draft.RowVersion, expectedRowVersion);

        var normalizedReason = NormalizeRequiredReason(reason);
        VersionRecord? superseded = null;
        if (current is not null)
        {
            RequireCurrent(current);
            if (current.Id == draft.Id)
            {
                throw new VersioningStateException("A draft cannot supersede itself.");
            }

            if (effectiveFrom <= current.EffectiveFrom)
            {
                throw new VersioningOverlapException();
            }

            superseded = current with
            {
                Status = VersionStatuses.Superseded,
                EffectiveTo = effectiveFrom,
                RowVersion = Increment(current.RowVersion),
            };
        }

        var published = draft with
        {
            Status = VersionStatuses.Current,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = null,
            Reason = normalizedReason,
            SupersedesId = current?.Id,
            RowVersion = Increment(draft.RowVersion),
        };

        EnsureNoOverlap(published, superseded, current, publishedHistory);
        return new VersionPublicationPlan(superseded, published);
    }

    public static void RequireExpectedRowVersion(long actualRowVersion, long expectedRowVersion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(actualRowVersion, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedRowVersion, 1);
        if (actualRowVersion != expectedRowVersion)
        {
            throw new VersionConflictException();
        }
    }

    public static string NormalizeRequiredReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new VersioningValidationException("A reason is required to publish a version.");
        }

        return reason.Trim();
    }

    internal static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Version effective instants must be UTC.", parameterName);
        }
    }

    private static void EnsureNoOverlap(
        VersionRecord published,
        VersionRecord? superseded,
        VersionRecord? priorCurrent,
        IEnumerable<VersionRecord> history)
    {
        var intervals = new List<VersionInterval>();
        foreach (var item in history)
        {
            if (priorCurrent is not null && item.Id == priorCurrent.Id)
            {
                continue;
            }

            if (!VersionStatuses.IsPublished(item.Status))
            {
                throw new VersioningStateException("Published history contains an unsupported state.");
            }

            if (item.EffectiveFrom is not DateTimeOffset start)
            {
                throw new VersioningStateException("Published history requires effectiveFrom.");
            }

            intervals.Add(new VersionInterval(start, item.EffectiveTo));
        }

        if (superseded is not null)
        {
            intervals.Add(new VersionInterval(superseded.EffectiveFrom!.Value, superseded.EffectiveTo));
        }

        var proposed = new VersionInterval(published.EffectiveFrom!.Value, published.EffectiveTo);
        if (intervals.Any(proposed.Overlaps))
        {
            throw new VersioningOverlapException();
        }
    }

    private static void RequireDraft(VersionRecord draft)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(draft.RowVersion, 1);
        if (draft.Status != VersionStatuses.Draft ||
            draft.EffectiveFrom is not null ||
            draft.EffectiveTo is not null ||
            draft.SupersedesId is not null ||
            draft.Reason is not null)
        {
            throw new VersioningStateException("Only a clean BORRADOR can be published.");
        }
    }

    private static void RequireCurrent(VersionRecord current)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(current.RowVersion, 1);
        if (current.Status != VersionStatuses.Current ||
            current.EffectiveFrom is null ||
            current.EffectiveTo is not null ||
            string.IsNullOrWhiteSpace(current.Reason))
        {
            throw new VersioningStateException("Only a valid VIGENTE version can be superseded.");
        }
    }

    private static long Increment(long rowVersion)
    {
        try
        {
            return checked(rowVersion + 1);
        }
        catch (OverflowException exception)
        {
            throw new VersionConflictException(exception);
        }
    }
}

public sealed class VersionIfMatchRequiredException()
    : Exception("If-Match is required for version publication or substitution.");

public sealed class VersionEtagInvalidException()
    : Exception("If-Match must contain a quoted positive row version.");

public sealed class VersionConflictException : Exception
{
    public VersionConflictException()
        : base("VERSION_CONFLICT")
    {
    }

    internal VersionConflictException(Exception innerException)
        : base("VERSION_CONFLICT", innerException)
    {
    }
}

public sealed class VersioningAccessDeniedException()
    : Exception("Versioning access is denied by default.");

public sealed class VersioningStateException(string message) : Exception(message);

public sealed class VersioningValidationException(string message) : Exception(message);

public sealed class VersioningOverlapException()
    : Exception("Version effective intervals cannot overlap.");
