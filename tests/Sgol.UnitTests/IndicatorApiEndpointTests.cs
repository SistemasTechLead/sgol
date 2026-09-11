using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Identity.Contracts;
using Sgol.Reporting.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class IndicatorApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(CanonicalRole.Direction)]
    [InlineData(CanonicalRole.Administration)]
    [InlineData(CanonicalRole.Subcoordination)]
    [InlineData(CanonicalRole.SalesFloor)]
    public void IndicatorPermissionUsesEveryCanonicalRole(string role)
    {
        Assert.True(RoleHierarchy.GrantsIndicatorView(role));
    }

    [Fact]
    public async Task GetParsesClosedContractAndReturnsExactFiveIndicators()
    {
        var actor = Guid.CreateVersion7();
        var person = Guid.CreateVersion7();
        var queriedAt = new DateTimeOffset(2026, 9, 11, 18, 0, 0, TimeSpan.Zero);
        var context = Context(actor);
        context.Request.QueryString = new(
            $"?isoYear=2026&isoWeek=37&level=PISO_VENTAS&responsiblePersonId={person:D}&limit=10");
        var resultValue = Result(queriedAt, person);
        var reader = new RecordingReader(resultValue);

        var result = await IndicatorApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(new IndicatorRequest(actor, 2026, 37, CanonicalRole.SalesFloor, person, null, 10), reader.Request);
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value, JsonOptions));
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(
            ["period", "scope", "baseObligationsCount", "pending", "concluded", "validated", "nonCompliant", "activeLoadByPerson"],
            data.EnumerateObject().Select(property => property.Name));
        Assert.Equal(8, data.GetProperty("baseObligationsCount").GetInt64());
        Assert.Equal(3, data.GetProperty("pending").GetProperty("count").GetInt64());
        Assert.Equal(8, data.GetProperty("pending").GetProperty("denominator").GetInt64());
        Assert.Equal(1, data.GetProperty("nonCompliant").GetProperty("count").GetInt64());
        Assert.Equal(3, data.GetProperty("activeLoadByPerson").GetProperty("denominator").GetInt64());
        Assert.Equal(1, json.RootElement.GetProperty("meta").GetProperty("count").GetInt32());
        var serialized = json.RootElement.GetRawText();
        Assert.DoesNotContain("amount", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payroll", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("percentage", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("direction/overview", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?isoYear=2026")]
    [InlineData("?isoWeek=37")]
    [InlineData("?isoYear=2026&isoWeek=37&unknown=1")]
    [InlineData("?isoYear=2026&isoWeek=37&isoWeek=38")]
    [InlineData("?isoYear=2026&isoWeek=54")]
    [InlineData("?isoYear=2026&isoWeek=37&level=Administrador")]
    [InlineData("?isoYear=2026&isoWeek=37&responsiblePersonId=")]
    [InlineData("?isoYear=2026&isoWeek=37&limit=101")]
    [InlineData("?isoYear=2026&isoWeek=37&cursor=")]
    public async Task InvalidFiltersAreRejectedBeforeReader(string query)
    {
        var context = Context(Guid.CreateVersion7());
        context.Request.QueryString = new(query);
        var reader = new RecordingReader(null);

        var result = await IndicatorApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

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

        var result = await IndicatorApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.Request);
    }

    [Theory]
    [InlineData("deny", StatusCodes.Status403Forbidden)]
    [InlineData("invalid", StatusCodes.Status400BadRequest)]
    [InlineData("inconsistent", StatusCodes.Status500InternalServerError)]
    public async Task ReaderFailuresUseApprovedProblemDetails(string failure, int status)
    {
        var context = Context(Guid.CreateVersion7());
        context.Request.QueryString = new("?isoYear=2026&isoWeek=37");
        var reader = new RecordingReader(null) { Failure = failure };

        var result = await IndicatorApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("application/problem+json", Assert.IsAssignableFrom<IContentTypeHttpResult>(result).ContentType);
    }

    private static IndicatorResult Result(DateTimeOffset queriedAt, Guid personId)
    {
        var snapshot = new IndicatorSnapshot(
            new(2026, 37, new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13), "America/Mexico_City"),
            new("LOR-001", CanonicalRole.Administration,
                [CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor],
                CanonicalRole.SalesFloor, personId),
            8,
            new(3, 8),
            new(5, 8),
            new(4, 8),
            new(1, 8),
            new(3,
            [
                new(new(personId, "EMP-001", "Persona sintética"), CanonicalRole.SalesFloor, 3),
            ]));
        return new(snapshot, null, queriedAt);
    }

    private static DefaultHttpContext Context(Guid? actor)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.TraceIdentifier = Guid.CreateVersion7().ToString("D");
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/v1/indicators";
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
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Failure switch
            {
                "deny" => Task.FromException<IndicatorResult>(new IndicatorAccessDeniedException()),
                "invalid" => Task.FromException<IndicatorResult>(new IndicatorFilterInvalidException()),
                "inconsistent" => Task.FromException<IndicatorResult>(new IndicatorQueryInconsistentException()),
                _ => Task.FromResult(result!),
            };
        }
    }
}
