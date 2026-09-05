using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sgol.Generation.Contracts;

public static class RecurringGenerationContract
{
    public const string JobName = "GENERATE_DUE_RECURRENCES";
    public const string TaskCode = "TAR-0005";
    public const string OriginType = "WORKING_DAY_WINDOW_V1";
    public const string CheckpointKind = "RECURRENCE_CHECKPOINT_V1";
    public const string TimeZone = "America/Mexico_City";
    public const string Generated = "GENERADA";
    public const string Recovered = "RECUPERADA";
    public const string Omitted = "OMITIDA";
    public const string Rejected = "RECHAZADA";
    public static readonly TimeSpan RecoveryHorizon = TimeSpan.FromDays(7);
    public static readonly TimeSpan DelayedThreshold = TimeSpan.FromMinutes(30);
    public static readonly IReadOnlyList<TimeOnly> Windows =
        [new(12, 0), new(17, 0)];

    public static IReadOnlyList<RecurringWindow> DueWindows(
        DateTimeOffset fromExclusive,
        DateTimeOffset throughInclusive,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentOutOfRangeException.ThrowIfLessThan(throughInclusive, fromExclusive);

        var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fromExclusive, timeZone).DateTime);
        var lastDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(throughInclusive, timeZone).DateTime);
        var result = new List<RecurringWindow>();
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            foreach (var window in Windows)
            {
                var occurrenceInstant = ToUtc(date, window, timeZone);
                if (fromExclusive < occurrenceInstant && occurrenceInstant <= throughInclusive)
                {
                    result.Add(new RecurringWindow(date, window, occurrenceInstant));
                }
            }
        }

        return result;
    }

    public static DateTimeOffset StartOfLocalDay(DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);
        return ToUtc(localDate, TimeOnly.MinValue, timeZone);
    }

    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    public static string OriginReference(DateOnly localDate, TimeOnly window) => string.Create(
        CultureInfo.InvariantCulture,
        $"LOR-001|{localDate:yyyy-MM-dd}|{window:HH\\:mm}");

    public static RecurringIdentity Identity(
        Guid ruleVersionId,
        Guid branchId,
        Guid periodId,
        DateOnly localDate,
        TimeOnly window)
    {
        var originReference = OriginReference(localDate, window);
        var canonical = string.Join(
            '\n',
            "SGOL_RECURRENCE_OCCURRENCE_V1",
            ruleVersionId.ToString("N", CultureInfo.InvariantCulture),
            branchId.ToString("N", CultureInfo.InvariantCulture),
            periodId.ToString("N", CultureInfo.InvariantCulture),
            OriginType,
            originReference);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new RecurringIdentity(DeterministicUuid(digest), Convert.ToHexStringLower(digest), originReference);
    }

    public static Guid PurposeId(string purpose, params string[] values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        var canonical = string.Join('\n', new[] { purpose }.Concat(values));
        return DeterministicUuid(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static Guid DeterministicUuid(ReadOnlySpan<byte> digest)
    {
        Span<byte> bytes = stackalloc byte[16];
        digest[..16].CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }
}

public sealed record RecurringWindow(
    DateOnly LocalDate,
    TimeOnly Window,
    DateTimeOffset OccurrenceInstant);

public sealed record RecurringIdentity(
    Guid IdempotencyKey,
    string RequestHash,
    string OriginReference);

public sealed record RecurringOccurrenceResult(
    string Result,
    Guid? RuleVersionId,
    Guid? PeriodId,
    Guid? ObligationId,
    string? ErrorCode,
    bool CreatedFunctionalFact);
