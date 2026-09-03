using System.Globalization;
using Sgol.Configuration.Contracts;

namespace Sgol.Planning.Contracts;

public static class WeekAuthorization
{
    public const string View = "PER-PLAN-VER";
}

public static class WeekContract
{
    public const string Current = "VIGENTE";
    public const string Elapsed = "TRANSCURRIDA";

    public static WeekRange Calculate(int isoYear, int isoWeek)
    {
        if (isoYear < 1 || isoYear > 9999)
        {
            throw new WeekValidationException("isoYear must identify a supported ISO year.");
        }

        var weeksInYear = ISOWeek.GetWeeksInYear(isoYear);
        if (isoWeek < 1 || isoWeek > weeksInYear)
        {
            throw new WeekValidationException("isoWeek does not exist in isoYear.");
        }

        try
        {
            var startsOn = DateOnly.FromDateTime(ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday));
            return new WeekRange(startsOn, startsOn.AddDays(6));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new WeekValidationException("The ISO week is outside the supported date range.", exception);
        }
    }

    public static DateOnly LocalToday(DateTimeOffset utcNow)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(CalendarContract.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, timeZone).DateTime);
    }

    public static string DeriveStatus(DateOnly endsOn, DateOnly localToday) =>
        endsOn < localToday ? Elapsed : Current;
}

public readonly record struct WeekRange(DateOnly StartsOn, DateOnly EndsOn);

public sealed class WeekPeriod
{
    private WeekPeriod()
    {
    }

    public WeekPeriod(
        Guid id,
        Guid branchId,
        int isoYear,
        int isoWeek,
        DateOnly startsOn,
        DateOnly endsOn,
        string derivedStatus)
    {
        var expected = WeekContract.Calculate(isoYear, isoWeek);
        if (startsOn != expected.StartsOn || endsOn != expected.EndsOn)
        {
            throw new WeekValidationException("startsOn and endsOn must match the ISO week.");
        }

        ValidateStatus(derivedStatus);
        Id = id;
        BranchId = branchId;
        IsoYear = isoYear;
        IsoWeek = isoWeek;
        StartsOn = startsOn;
        EndsOn = endsOn;
        DerivedStatus = derivedStatus;
    }

    public Guid Id { get; private init; }

    public Guid BranchId { get; private init; }

    public int IsoYear { get; private init; }

    public int IsoWeek { get; private init; }

    public DateOnly StartsOn { get; private init; }

    public DateOnly EndsOn { get; private init; }

    public string DerivedStatus { get; private set; } = null!;

    public void RefreshDerivedStatus(string derivedStatus)
    {
        ValidateStatus(derivedStatus);
        DerivedStatus = derivedStatus;
    }

    private static void ValidateStatus(string derivedStatus)
    {
        if (derivedStatus is not (WeekContract.Current or WeekContract.Elapsed))
        {
            throw new WeekValidationException("derivedStatus is not supported by HU-010.");
        }
    }
}

public sealed record WeekPeriodDetails(
    Guid Id,
    string BranchCode,
    int IsoYear,
    int IsoWeek,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string DerivedStatus);

public interface IWeekPeriodService
{
    Task<WeekPeriodDetails> GetAsync(
        Guid actorUserId,
        Guid correlationId,
        int isoYear,
        int isoWeek,
        CancellationToken cancellationToken = default);
}

public sealed class WeekAccessDeniedException()
    : Exception($"{WeekAuthorization.View} is required for LOR-001.");

public sealed class WeekValidationException : Exception
{
    public WeekValidationException(string message)
        : base(message)
    {
    }

    public WeekValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
