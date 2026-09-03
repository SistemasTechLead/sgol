using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class WeekPeriodTests
{
    private static readonly Guid ActorUserId = Guid.Parse("019d2d67-2c00-7000-8000-000000000a01");

    [Theory]
    [InlineData(2026, 1, "2025-12-29", "2026-01-04")]
    [InlineData(2020, 53, "2020-12-28", "2021-01-03")]
    [InlineData(2026, 37, "2026-09-07", "2026-09-13")]
    public void Calculate_UsesIsoMondayThroughSunday(
        int isoYear,
        int isoWeek,
        string expectedStart,
        string expectedEnd)
    {
        var range = WeekContract.Calculate(isoYear, isoWeek);

        Assert.Equal(DateOnly.Parse(expectedStart, CultureInfo.InvariantCulture), range.StartsOn);
        Assert.Equal(DateOnly.Parse(expectedEnd, CultureInfo.InvariantCulture), range.EndsOn);
        Assert.Equal(DayOfWeek.Monday, range.StartsOn.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, range.EndsOn.DayOfWeek);
    }

    [Theory]
    [InlineData(2021, 53)]
    [InlineData(2026, 0)]
    [InlineData(0, 1)]
    public void Calculate_RejectsNonexistentIsoWeeks(int isoYear, int isoWeek)
    {
        Assert.Throws<WeekValidationException>(() => WeekContract.Calculate(isoYear, isoWeek));
    }

    [Fact]
    public void LocalToday_UsesMexicoCityAtUtcDayBoundary()
    {
        Assert.Equal(
            new DateOnly(2026, 9, 6),
            WeekContract.LocalToday(new DateTimeOffset(2026, 9, 7, 5, 59, 59, TimeSpan.Zero)));
        Assert.Equal(
            new DateOnly(2026, 9, 7),
            WeekContract.LocalToday(new DateTimeOffset(2026, 9, 7, 6, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void DeriveStatus_UsesOnlyCurrentOrElapsedWithoutFormalTransitions()
    {
        var sunday = new DateOnly(2026, 9, 13);

        Assert.Equal(WeekContract.Current, WeekContract.DeriveStatus(sunday, sunday));
        Assert.Equal(WeekContract.Current, WeekContract.DeriveStatus(sunday, sunday.AddDays(-30)));
        Assert.Equal(WeekContract.Elapsed, WeekContract.DeriveStatus(sunday, sunday.AddDays(1)));
    }

    [Fact]
    public async Task Endpoint_UsesStrictIntegerParametersAndPassesActor()
    {
        var details = new WeekPeriodDetails(
            Guid.CreateVersion7(),
            BranchScope.LorettaCode,
            2026,
            37,
            new DateOnly(2026, 9, 7),
            new DateOnly(2026, 9, 13),
            WeekContract.Current);
        var service = new RecordingWeekService(details);

        var result = await WeekApiEndpoints.HandleGetAsync(
            "2026",
            "37",
            CreateContext(authenticated: true),
            service,
            CancellationToken.None);

        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(ActorUserId, service.ActorUserId);
        Assert.Equal(2026, service.IsoYear);
        Assert.Equal(37, service.IsoWeek);
    }

    [Theory]
    [InlineData("2026x", "37", true, 422)]
    [InlineData("2026", "+37", true, 422)]
    [InlineData("2026", "37", false, 401)]
    public async Task Endpoint_RejectsMalformedOrUnauthenticatedWithoutCallingService(
        string isoYear,
        string isoWeek,
        bool authenticated,
        int expectedStatus)
    {
        var service = new RecordingWeekService(null);

        var result = await WeekApiEndpoints.HandleGetAsync(
            isoYear,
            isoWeek,
            CreateContext(authenticated),
            service,
            CancellationToken.None);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.ActorUserId);
    }

    private static DefaultHttpContext CreateContext(bool authenticated)
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.CreateVersion7().ToString("D") };
        if (authenticated)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, ActorUserId.ToString("D"))],
                "synthetic"));
        }

        return context;
    }

    private sealed class RecordingWeekService(WeekPeriodDetails? details) : IWeekPeriodService
    {
        public Guid? ActorUserId { get; private set; }

        public int? IsoYear { get; private set; }

        public int? IsoWeek { get; private set; }

        public Task<WeekPeriodDetails> GetAsync(
            Guid actorUserId,
            Guid correlationId,
            int isoYear,
            int isoWeek,
            CancellationToken cancellationToken = default)
        {
            ActorUserId = actorUserId;
            IsoYear = isoYear;
            IsoWeek = isoWeek;
            return Task.FromResult(details ?? throw new InvalidOperationException("Service should not be called."));
        }
    }
}
