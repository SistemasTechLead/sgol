using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Identity.Contracts;
using Sgol.Reporting.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class DirectionOverviewApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void DirectionPermissionIsExclusiveToCanonicalDirection()
    {
        Assert.True(RoleHierarchy.GrantsDirectionOverviewView(CanonicalRole.Direction));
        Assert.False(RoleHierarchy.GrantsDirectionOverviewView(CanonicalRole.Administration));
        Assert.False(RoleHierarchy.GrantsDirectionOverviewView(CanonicalRole.Subcoordination));
        Assert.False(RoleHierarchy.GrantsDirectionOverviewView(CanonicalRole.SalesFloor));
        Assert.False(RoleHierarchy.GrantsDirectionOverviewView("Dirección"));
    }

    [Fact]
    public async Task GetParsesClosedContractAndReturnsOnlyFiveIndicators()
    {
        var actor = Guid.CreateVersion7();
        var person = Guid.CreateVersion7();
        var queriedAt = new DateTimeOffset(2026, 9, 11, 18, 0, 0, TimeSpan.Zero);
        var context = Context(actor);
        context.Request.QueryString = new(
            $"?isoYear=2026&isoWeek=37&level=DIRECCION&responsiblePersonId={person:D}&limit=10");
        var reader = new RecordingReader(Result(queriedAt, person));

        var result = await DirectionOverviewApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(new IndicatorRequest(actor, 2026, 37, CanonicalRole.Direction, person, null, 10), reader.Request);
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value, JsonOptions));
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(
            ["period", "scope", "baseObligationsCount", "pending", "concluded", "validated", "nonCompliant", "activeLoadByPerson"],
            data.EnumerateObject().Select(property => property.Name));
        Assert.Equal(CanonicalRole.Direction, data.GetProperty("scope").GetProperty("actorRole").GetString());
        Assert.Equal(9, data.GetProperty("baseObligationsCount").GetInt64());
        Assert.Equal(4, data.GetProperty("pending").GetProperty("count").GetInt64());
        Assert.Equal(4, data.GetProperty("activeLoadByPerson").GetProperty("denominator").GetInt64());
        Assert.Equal(3, data.GetProperty("activeLoadByPerson").GetProperty("items")[0].GetProperty("count").GetInt64());
        var serialized = json.RootElement.GetRawText();
        Assert.DoesNotContain("amount", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payroll", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audit", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recovery", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("export", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?isoYear=2026")]
    [InlineData("?isoWeek=37")]
    [InlineData("?isoYear=2026&isoWeek=37&unknown=1")]
    [InlineData("?isoYear=2026&isoWeek=37&isoWeek=38")]
    [InlineData("?isoYear=2026&isoWeek=54")]
    [InlineData("?isoYear=2026&isoWeek=37&level=Dirección")]
    [InlineData("?isoYear=2026&isoWeek=37&responsiblePersonId=")]
    [InlineData("?isoYear=2026&isoWeek=37&limit=0")]
    [InlineData("?isoYear=2026&isoWeek=37&cursor=")]
    public async Task InvalidFiltersAreRejectedBeforeReader(string query)
    {
        var context = Context(Guid.CreateVersion7());
        context.Request.QueryString = new(query);
        var reader = new RecordingReader(null);

        var result = await DirectionOverviewApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("application/problem+json", Assert.IsAssignableFrom<IContentTypeHttpResult>(result).ContentType);
        Assert.Null(reader.Request);
    }

    [Fact]
    public async Task UnauthenticatedRequestIsRejectedWithoutReading()
    {
        var context = Context(null);
        context.Request.QueryString = new("?isoYear=2026&isoWeek=37");
        var reader = new RecordingReader(null);

        var result = await DirectionOverviewApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.Request);
    }

    [Theory]
    [InlineData("deny", StatusCodes.Status403Forbidden)]
    [InlineData("invalid", StatusCodes.Status400BadRequest)]
    [InlineData("inconsistent", StatusCodes.Status500InternalServerError)]
    public async Task ReaderFailuresUseDirectionProblemDetails(string failure, int status)
    {
        var context = Context(Guid.CreateVersion7());
        context.Request.QueryString = new("?isoYear=2026&isoWeek=37");
        var reader = new RecordingReader(null) { Failure = failure };

        var result = await DirectionOverviewApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("application/problem+json", Assert.IsAssignableFrom<IContentTypeHttpResult>(result).ContentType);
    }

    private static IndicatorResult Result(DateTimeOffset queriedAt, Guid personId)
    {
        var snapshot = new IndicatorSnapshot(
            new(2026, 37, new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13), "America/Mexico_City"),
            new("LOR-001", CanonicalRole.Direction, [CanonicalRole.Direction], CanonicalRole.Direction, personId),
            9,
            new(4, 9),
            new(5, 9),
            new(4, 9),
            new(1, 9),
            new(4,
            [
                new(new(personId, "DIR-001", "Dirección sintética"), CanonicalRole.Direction, 3),
            ]));
        return new(snapshot, null, queriedAt);
    }

    private static DefaultHttpContext Context(Guid? actor)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.TraceIdentifier = Guid.CreateVersion7().ToString("D");
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/v1/direction/overview";
        if (actor.HasValue)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Value.ToString("D"))], "test"));
        }

        return context;
    }

    private sealed class RecordingReader(IndicatorResult? result) : IIndicatorReader
    {
        public string? Failure { get; init; }
        public IndicatorRequest? Request { get; private set; }

        public Task<IndicatorResult> ReadAsync(
            IndicatorRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IndicatorResult> ReadDirectionOverviewAsync(
            IndicatorRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Failure switch
            {
                "deny" => Task.FromException<IndicatorResult>(new DirectionOverviewAccessDeniedException()),
                "invalid" => Task.FromException<IndicatorResult>(new DirectionOverviewFilterInvalidException()),
                "inconsistent" => Task.FromException<IndicatorResult>(new DirectionOverviewQueryInconsistentException()),
                _ => Task.FromResult(result!),
            };
        }
    }
}
