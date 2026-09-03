using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class CalendarTests
{
    private static readonly Guid ActorUserId = Guid.Parse("019d2d67-2c00-7000-8000-000000000901");
    private static readonly Guid ReleaseId = Guid.Parse("019d2d67-2c00-7000-8000-000000000902");
    private static readonly DateOnly LocalDate = new(2026, 9, 16);
    private static readonly DateTimeOffset EffectiveFrom = new(2026, 9, 10, 6, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CalendarContract.WorkingDay, true)]
    [InlineData(CalendarContract.Holiday, false)]
    [InlineData(CalendarContract.ExtraordinaryClosure, false)]
    public void Day_UsesApprovedTypesAndVersioningCore(string dayType, bool isWorkingDay)
    {
        var day = new CalendarDayVersion(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            LocalDate,
            dayType,
            isWorkingDay,
            ReleaseId,
            "Calendario aprobado");
        var plan = VersioningRules.PlanPublication(
            day.ToVersionRecord(),
            current: null,
            publishedHistory: [],
            expectedRowVersion: 1,
            EffectiveFrom,
            "Calendario aprobado");

        day.ApplyPublished(plan.Published);

        Assert.Equal(VersionStatuses.Current, day.Status);
        Assert.Equal(EffectiveFrom, day.EffectiveFrom);
        Assert.Equal("Calendario aprobado", day.Reason);
        Assert.Null(day.PendingReason);
    }

    [Theory]
    [InlineData("LABORABLE", false)]
    [InlineData("FESTIVO", true)]
    [InlineData("CIERRE_EXTRAORDINARIO", true)]
    [InlineData("INHABIL", false)]
    public void Day_RejectsUnsupportedOrInconsistentValues(string dayType, bool isWorkingDay)
    {
        Assert.Throws<CalendarValidationException>(() => new CalendarDayVersion(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            LocalDate,
            dayType,
            isWorkingDay,
            ReleaseId,
            "Motivo"));
    }

    [Fact]
    public void DraftCorrection_RequiresCurrentEtagAndKeepsPublishedHistoryUntouched()
    {
        var draft = NewDraft();

        Assert.Throws<VersionConflictException>(() =>
            draft.CorrectDraft(CalendarContract.Holiday, false, "Festivo", expectedRowVersion: 2));

        draft.CorrectDraft(CalendarContract.Holiday, false, "Festivo", expectedRowVersion: 1);
        Assert.Equal(2, draft.RowVersion);
        Assert.Equal(VersionStatuses.Draft, draft.Status);
        Assert.Equal("Festivo", draft.PendingReason);
        Assert.Null(draft.EffectiveFrom);
    }

    [Fact]
    public async Task Put_UsesStrictBodyLocalDateAndQuotedIfMatch()
    {
        var service = new RecordingCalendarService(CreateDetails(rowVersion: 2));
        var context = CreateContext();
        context.Request.Headers.IfMatch = "\"1\"";
        var request = Parse($$"""
            {"releaseId":"{{ReleaseId:D}}","dayType":"FESTIVO","isWorkingDay":false,"reason":"Día festivo"}
            """);

        var result = await CalendarApiEndpoints.HandlePutAsync(
            "2026-09-16",
            request,
            context,
            service,
            CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(LocalDate, service.Command?.LocalDate);
        Assert.Equal(ReleaseId, service.Command?.ReleaseId);
        Assert.Equal(1, service.Command?.ExpectedRowVersion);
        Assert.Equal("\"2\"", context.Response.Headers.ETag);
    }

    [Theory]
    [InlineData("2026-02-30", "{\"releaseId\":\"019d2d67-2c00-7000-8000-000000000902\",\"dayType\":\"FESTIVO\",\"isWorkingDay\":false,\"reason\":\"Festivo\"}")]
    [InlineData("2026-09-16", "{\"releaseId\":\"019d2d67-2c00-7000-8000-000000000902\",\"dayType\":\"FESTIVO\",\"isWorkingDay\":false,\"reason\":\"Festivo\",\"timeZone\":\"UTC\"}")]
    public async Task Put_RejectsInvalidDateOrAClientSuppliedZone(string date, string json)
    {
        var service = new RecordingCalendarService(CreateDetails(rowVersion: 1));

        var result = await CalendarApiEndpoints.HandlePutAsync(
            date,
            Parse(json),
            CreateContext(),
            service,
            CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Command);
    }

    [Fact]
    public async Task Get_UsesInclusiveRangeAndReturnsOnlyTheServerZoneContract()
    {
        var service = new RecordingCalendarService(CreateDetails(rowVersion: 2));
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?from=2026-09-14&to=2026-09-20");

        var result = await CalendarApiEndpoints.HandleGetAsync(context, service, CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(new DateOnly(2026, 9, 14), service.From);
        Assert.Equal(new DateOnly(2026, 9, 20), service.To);
        Assert.Equal(CalendarContract.TimeZone, service.Details.TimeZone);
    }

    private static CalendarDayVersion NewDraft() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        LocalDate,
        CalendarContract.WorkingDay,
        true,
        ReleaseId,
        "Inicial");

    private static CalendarDayDetails CreateDetails(long rowVersion) => new(
        Guid.CreateVersion7(),
        LocalDate,
        CalendarContract.Holiday,
        false,
        CalendarContract.TimeZone,
        ReleaseId,
        VersionStatuses.Draft,
        null,
        null,
        null,
        null,
        rowVersion);

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static DefaultHttpContext CreateContext() => new()
    {
        TraceIdentifier = Guid.CreateVersion7().ToString("D"),
        User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, ActorUserId.ToString("D"))],
            "synthetic")),
    };

    private sealed class RecordingCalendarService(CalendarDayDetails details) : ICalendarService
    {
        public CalendarDayDetails Details { get; } = details;

        public PutCalendarDayCommand? Command { get; private set; }

        public DateOnly? From { get; private set; }

        public DateOnly? To { get; private set; }

        public Task<IReadOnlyList<CalendarDayDetails>> GetAsync(
            Guid actorUserId,
            Guid correlationId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default)
        {
            From = fromDate;
            To = toDate;
            return Task.FromResult<IReadOnlyList<CalendarDayDetails>>([Details]);
        }

        public Task<CalendarDayDetails> PutAsync(
            PutCalendarDayCommand command,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(Details);
        }
    }
}
