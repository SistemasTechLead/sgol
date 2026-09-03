using Sgol.BuildingBlocks.Versioning;

namespace Sgol.Configuration.Contracts;

public static class CalendarAuthorization
{
    public const string Administer = "PER-CALENDARIO-ADMIN";
}

public static class CalendarContract
{
    public const string TimeZone = "America/Mexico_City";
    public const string WorkingDay = "LABORABLE";
    public const string Holiday = "FESTIVO";
    public const string ExtraordinaryClosure = "CIERRE_EXTRAORDINARIO";

    public static void ValidateDay(string dayType, bool isWorkingDay)
    {
        if (dayType is not (WorkingDay or Holiday or ExtraordinaryClosure))
        {
            throw new CalendarValidationException("dayType is not supported by HU-009.");
        }

        var expectedWorkingDay = dayType == WorkingDay;
        if (isWorkingDay != expectedWorkingDay)
        {
            throw new CalendarValidationException("isWorkingDay does not match dayType.");
        }
    }
}

public sealed class CalendarDayVersion : IVersionedEntity
{
    private CalendarDayVersion()
    {
    }

    public CalendarDayVersion(
        Guid id,
        Guid branchId,
        DateOnly localDate,
        string dayType,
        bool isWorkingDay,
        Guid releaseId,
        string pendingReason)
    {
        CalendarContract.ValidateDay(dayType, isWorkingDay);
        Id = id;
        BranchId = branchId;
        LocalDate = localDate;
        DayType = dayType;
        IsWorkingDay = isWorkingDay;
        ReleaseId = releaseId;
        PendingReason = VersioningRules.NormalizeRequiredReason(pendingReason);
        Status = VersionStatuses.Draft;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }

    public Guid BranchId { get; private init; }

    public DateOnly LocalDate { get; private init; }

    public string DayType { get; private set; } = null!;

    public bool IsWorkingDay { get; private set; }

    public Guid ReleaseId { get; private init; }

    public string? PendingReason { get; private set; }

    public string Status { get; private set; } = null!;

    public DateTimeOffset? EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public string? Reason { get; private set; }

    public Guid? SupersedesId { get; private set; }

    public long RowVersion { get; private set; }

    public void CorrectDraft(string dayType, bool isWorkingDay, string pendingReason, long expectedRowVersion)
    {
        if (Status != VersionStatuses.Draft)
        {
            throw new VersioningStateException("Only a BORRADOR calendar day can be corrected.");
        }

        VersioningRules.RequireExpectedRowVersion(RowVersion, expectedRowVersion);
        CalendarContract.ValidateDay(dayType, isWorkingDay);
        DayType = dayType;
        IsWorkingDay = isWorkingDay;
        PendingReason = VersioningRules.NormalizeRequiredReason(pendingReason);
        try
        {
            RowVersion = checked(RowVersion + 1);
        }
        catch (OverflowException exception)
        {
            throw new CalendarVersionConflictException(exception);
        }
    }

    public void ApplyPublished(VersionRecord published)
    {
        if (published.Id != Id || published.Status != VersionStatuses.Current)
        {
            throw new VersioningStateException("The publication plan does not match this calendar day.");
        }

        Apply(published);
        PendingReason = null;
    }

    public void ApplySuperseded(VersionRecord superseded)
    {
        if (superseded.Id != Id || superseded.Status != VersionStatuses.Superseded)
        {
            throw new VersioningStateException("The substitution plan does not match this calendar day.");
        }

        Apply(superseded);
    }

    public VersionRecord ToVersionRecord() => new(
        Id,
        Status,
        EffectiveFrom,
        EffectiveTo,
        Reason,
        SupersedesId,
        RowVersion);

    private void Apply(VersionRecord version)
    {
        Status = version.Status;
        EffectiveFrom = version.EffectiveFrom;
        EffectiveTo = version.EffectiveTo;
        Reason = version.Reason;
        SupersedesId = version.SupersedesId;
        RowVersion = version.RowVersion;
    }
}

public sealed record CalendarDayDetails(
    Guid Id,
    DateOnly LocalDate,
    string DayType,
    bool IsWorkingDay,
    string TimeZone,
    Guid ReleaseId,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    Guid? SupersedesId,
    long RowVersion);

public sealed record PutCalendarDayCommand(
    Guid ActorUserId,
    Guid CorrelationId,
    DateOnly LocalDate,
    Guid ReleaseId,
    string DayType,
    bool IsWorkingDay,
    string Reason,
    long? ExpectedRowVersion);

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarDayDetails>> GetAsync(
        Guid actorUserId,
        Guid correlationId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<CalendarDayDetails> PutAsync(
        PutCalendarDayCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class CalendarAccessDeniedException()
    : Exception($"{CalendarAuthorization.Administer} is required for LOR-001.");

public sealed class CalendarReleaseNotFoundException()
    : Exception("The configuration release does not exist or is not a BORRADOR for LOR-001.");

public sealed class CalendarIfMatchRequiredException()
    : Exception("If-Match is required to correct a calendar draft.");

public sealed class CalendarVersionConflictException : Exception
{
    public CalendarVersionConflictException()
        : base("VERSION_CONFLICT")
    {
    }

    public CalendarVersionConflictException(Exception innerException)
        : base("VERSION_CONFLICT", innerException)
    {
    }
}

public sealed class CalendarValidationException(string message) : Exception(message);
