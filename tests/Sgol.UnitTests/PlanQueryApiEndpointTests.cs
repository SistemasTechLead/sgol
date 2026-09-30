using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class PlanQueryApiEndpointTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Plan = Guid.NewGuid();
    private static readonly Guid Publication = Guid.NewGuid();
    private static readonly IDataProtectionProvider Protection = new EphemeralDataProtectionProvider();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UnauthenticatedAndInvalidActorCannotCallEitherReader()
    {
        var reader = new Reader();
        var context = new DefaultHttpContext();
        AssertProblem(await PlanQueryApiEndpoints.HandlePlanAsync("2026", "40", context, reader, default), 401, "AUTENTICACION_REQUERIDA");
        AssertProblem(await PlanQueryApiEndpoints.HandleVersionsAsync(Plan.ToString("D"), context, reader, Protection, default), 401, "AUTENTICACION_REQUERIDA");
        context.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "invalid")], "test"));
        AssertProblem(await PlanQueryApiEndpoints.HandlePlanAsync("2026", "40", context, reader, default), 403, "ACCESO_DENEGADO");
        Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData("+2026", "40", "")]
    [InlineData("2026", " 40", "")]
    [InlineData("2026", "40", "?branchId=other")]
    public async Task PlanRejectsBadRouteAndExtraQueryWithoutEffect(string year, string week, string query)
    {
        var reader = new Reader();
        AssertProblem(await PlanQueryApiEndpoints.HandlePlanAsync(year, week, Context(query), reader, default), 400, "SOLICITUD_PLAN_INVALIDA");
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task PlanReadReturnsOnlyMetadataCurrentEtagAndNoMutationResult()
    {
        var reader = new Reader(); var context = Context();
        var result = await PlanQueryApiEndpoints.HandlePlanAsync("2026", "40", context, reader, default);
        var json = JsonSerializer.Serialize(((IValueHttpResult)result).Value, JsonOptions);
        Assert.Contains("\"planId\"", json); Assert.Contains("\"queriedAt\"", json);
        Assert.DoesNotContain("\"result\"", json);
        Assert.Equal("\"3\"", context.Response.Headers.ETag);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(Actor, reader.Actor);
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?limit=50&limit=50")]
    [InlineData("?scopeRole=DIRECCION")]
    [InlineData("?publicationId=invalid")]
    public async Task VersionsRejectInvalidOrRepeatedFiltersBeforeReader(string query)
    {
        var reader = new Reader();
        AssertProblem(await PlanQueryApiEndpoints.HandleVersionsAsync(Plan.ToString("D"), Context(query), reader, Protection, default), 400, "CONSULTA_PLAN_INVALIDA");
        Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData("?cursor=")]
    [InlineData("?cursor=malformed")]
    public async Task InvalidCursorDoesNotProbePublication(string query)
    {
        var reader = new Reader();
        AssertProblem(await PlanQueryApiEndpoints.HandleVersionsAsync(Plan.ToString("D"), Context(query), reader, Protection, default), 400, "CURSOR_INVALIDO");
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task ProtectedCursorRoundTripsItsScopeAndNeverExposesState()
    {
        var reader = new Reader { Next = new(Actor, "SUBCOORDINACION", Plan, null, 50, 2, null) };
        var result = await PlanQueryApiEndpoints.HandleVersionsAsync(Plan.ToString("D"), Context(), reader, Protection, default);
        var json = JsonSerializer.Serialize(((IValueHttpResult)result).Value, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var cursor = document.RootElement.GetProperty("meta").GetProperty("nextCursor").GetString()!;
        Assert.DoesNotContain("SUBCOORDINACION", cursor);
        Assert.DoesNotContain("afterVersionNo", json);
        await PlanQueryApiEndpoints.HandleVersionsAsync(Plan.ToString("D"), Context("?cursor=" + Uri.EscapeDataString(cursor)), reader, Protection, default);
        Assert.Equal(reader.Next, reader.Request!.Cursor);
        var tampered = cursor[..^4] + "xxxx";
        AssertProblem(await PlanQueryApiEndpoints.HandleVersionsAsync(Plan.ToString("D"), Context("?cursor=" + Uri.EscapeDataString(tampered)), reader, Protection, default), 400, "CURSOR_INVALIDO");
        Assert.Equal(2, reader.Calls);
    }

    [Fact]
    public async Task SnapshotUsesFrozenPairsAndCurrentPlanEtag()
    {
        var version = new PlanVersionDetails(Publication, Plan, 1, "SUSTITUIDA", "SUBCOORDINACION", Actor, DateTimeOffset.UtcNow, null, 2);
        var pair = new PlanPublicationItem(Guid.NewGuid(), Guid.NewGuid());
        var reader = new Reader { Snapshot = new(version, [pair]) }; var context = Context("?publicationId=" + Publication.ToString("D") + "&limit=1");
        var result = await PlanQueryApiEndpoints.HandleVersionsAsync(Plan.ToString("D"), context, reader, Protection, default);
        var json = JsonSerializer.Serialize(((IValueHttpResult)result).Value, JsonOptions);
        Assert.Contains(pair.AssignmentVersionId.ToString("D"), json);
        Assert.Contains("\"planRowVersion\":2", json);
        Assert.Equal("\"3\"", context.Response.Headers.ETag);
        Assert.Equal(Publication, reader.Request!.PublicationId); Assert.Equal(1, reader.Request.Limit);
    }

    [Theory]
    [InlineData(403, "ACCESO_DENEGADO")]
    [InlineData(404, "PUBLICACION_NO_ENCONTRADA")]
    [InlineData(400, "CURSOR_INVALIDO")]
    public async Task ApprovedReadErrorsCarryCodeAndCorrelationWithoutPartialData(int status, string code)
    {
        var reader = new Reader { Error = new(code, status) };
        AssertProblem(await PlanQueryApiEndpoints.HandleVersionsAsync(Plan.ToString("D"), Context(), reader, Protection, default), status, code);
    }

    [Theory]
    [InlineData(CanonicalRole.Direction, true)]
    [InlineData(CanonicalRole.Administration, true)]
    [InlineData(CanonicalRole.Subcoordination, true)]
    [InlineData(CanonicalRole.SalesFloor, false)]
    public void SessionProjectionSeparatesPrepareAndPublishPermissions(string role, bool publish)
    {
        var permissions = RolePermissionProjection.ForRole(role);
        Assert.Contains("PER-PLAN-VER", permissions);
        Assert.Equal(publish, permissions.Contains("PER-PLAN-PUBLICAR"));
    }

    private static DefaultHttpContext Context(string query = "")
    {
        var context = new DefaultHttpContext { TraceIdentifier = Guid.NewGuid().ToString("D") };
        context.User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Actor.ToString("D"))], "test"));
        context.Request.QueryString = new(query); return context;
    }
    private static void AssertProblem(IResult result, int status, string code)
    {
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(status, problem.StatusCode); Assert.Equal(code, problem.ProblemDetails.Extensions["code"]);
        Assert.True(problem.ProblemDetails.Extensions.ContainsKey("correlationId"));
    }
    private sealed class Reader : IPlanQueryReader
    {
        public int Calls { get; private set; }
        public Guid Actor { get; private set; }
        public PlanVersionsRequest? Request { get; private set; }
        public PlanReadCursor? Next { get; init; }
        public PlanSnapshotDetails? Snapshot { get; init; }
        public PlanQueryException? Error { get; init; }
        public Task<WorkPlanRead> ReadPlanAsync(Guid actorUserId, int isoYear, int isoWeek, CancellationToken cancellationToken = default)
        { Calls++; Actor = actorUserId; return Task.FromResult(new WorkPlanRead(new(Plan, Plan, "LOR-001", Plan, isoYear, isoWeek, "PUBLICADO", 3), DateTimeOffset.UtcNow)); }
        public Task<PlanVersionsRead> ReadVersionsAsync(PlanVersionsRequest request, CancellationToken cancellationToken = default)
        { Calls++; Request = request; if (Error is not null) throw Error; return Task.FromResult(new PlanVersionsRead([], Snapshot, Next, 3, DateTimeOffset.UtcNow)); }
    }
}
