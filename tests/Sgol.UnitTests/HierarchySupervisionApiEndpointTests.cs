using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Reporting.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class HierarchySupervisionApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(CanonicalRole.Direction, true)]
    [InlineData(CanonicalRole.Administration, true)]
    [InlineData(CanonicalRole.Subcoordination, true)]
    [InlineData(CanonicalRole.SalesFloor, false)]
    [InlineData("Administrador", false)]
    public void SupervisionPermissionUsesOnlyCanonicalSuperiorRoles(string role, bool expected)
    {
        Assert.Equal(expected, RoleHierarchy.GrantsSupervisionView(role));
    }

    [Fact]
    public async Task SupervisionUsesApprovedDefaultsAndNoStore()
    {
        var actor = Guid.CreateVersion7();
        var queriedAt = new DateTimeOffset(2026, 9, 10, 20, 0, 0, TimeSpan.Zero);
        var context = Context(actor, "/api/v1/supervision/obligations");
        var reader = new RecordingReader(new([], null, queriedAt), null);

        var result = await HierarchySupervisionApiEndpoints.ReadSupervisionAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(new SupervisionRequest(actor, null, null, null, null, null, null, 25), reader.SupervisionRequest);
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        var json = JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value, JsonOptions);
        Assert.Contains("\"data\":[]", json, StringComparison.Ordinal);
        Assert.Contains("\"count\":0", json, StringComparison.Ordinal);
        Assert.DoesNotContain("indicator", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PendingParsesClosedFiltersWithoutExecutionStatus()
    {
        var actor = Guid.CreateVersion7();
        var person = Guid.CreateVersion7();
        var context = Context(actor, "/api/v1/validations/pending");
        context.Request.QueryString = new($"?level=PISO_VENTAS&responsiblePersonId={person:D}&isoYear=2026&isoWeek=37&limit=10");
        var reader = new RecordingReader(null, new([], null, DateTimeOffset.UtcNow));

        var result = await HierarchySupervisionApiEndpoints.ReadPendingAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(new PendingValidationsRequest(actor, "PISO_VENTAS", person, 2026, 37, null, 10), reader.PendingRequest);
    }

    [Theory]
    [InlineData("/api/v1/supervision/obligations", "?unknown=1")]
    [InlineData("/api/v1/supervision/obligations", "?isoYear=2026")]
    [InlineData("/api/v1/supervision/obligations", "?limit=101")]
    [InlineData("/api/v1/validations/pending", "?executionStatus=CONCLUIDA")]
    [InlineData("/api/v1/validations/pending", "?isoWeek=37")]
    public async Task InvalidFiltersAreRejectedBeforeReader(string path, string query)
    {
        var context = Context(Guid.CreateVersion7(), path);
        context.Request.QueryString = new(query);
        var reader = new RecordingReader(null, null);

        var result = path.Contains("supervision", StringComparison.Ordinal)
            ? await HierarchySupervisionApiEndpoints.ReadSupervisionAsync(context, reader, CancellationToken.None)
            : await HierarchySupervisionApiEndpoints.ReadPendingAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.SupervisionRequest);
        Assert.Null(reader.PendingRequest);
        Assert.Equal("application/problem+json", Assert.IsAssignableFrom<IContentTypeHttpResult>(result).ContentType);
    }

    [Theory]
    [InlineData("/api/v1/supervision/obligations")]
    [InlineData("/api/v1/validations/pending")]
    public async Task UnauthenticatedRequestsAreRejectedWithoutReading(string path)
    {
        var context = Context(null, path);
        var reader = new RecordingReader(null, null);

        var result = path.Contains("supervision", StringComparison.Ordinal)
            ? await HierarchySupervisionApiEndpoints.ReadSupervisionAsync(context, reader, CancellationToken.None)
            : await HierarchySupervisionApiEndpoints.ReadPendingAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.SupervisionRequest);
        Assert.Null(reader.PendingRequest);
    }

    [Fact]
    public async Task SalesFloorStyleAccessDenialConvergesToForbidden()
    {
        var context = Context(Guid.CreateVersion7(), "/api/v1/supervision/obligations");
        var reader = new RecordingReader(null, null) { Deny = true };

        var result = await HierarchySupervisionApiEndpoints.ReadSupervisionAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    private static DefaultHttpContext Context(Guid? actor, string path)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.TraceIdentifier = Guid.CreateVersion7().ToString("D");
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        if (actor.HasValue)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Value.ToString("D"))], "test"));
        return context;
    }

    private sealed class RecordingReader(SupervisionPage? supervision, PendingValidationsPage? pending)
        : IHierarchySupervisionReader
    {
        public bool Deny { get; init; }
        public SupervisionRequest? SupervisionRequest { get; private set; }
        public PendingValidationsRequest? PendingRequest { get; private set; }

        public Task<SupervisionPage> ReadSupervisionAsync(SupervisionRequest request,
            CancellationToken cancellationToken = default)
        {
            SupervisionRequest = request;
            if (Deny) throw new SupervisionAccessDeniedException();
            return Task.FromResult(supervision!);
        }

        public Task<PendingValidationsPage> ReadPendingValidationsAsync(PendingValidationsRequest request,
            CancellationToken cancellationToken = default)
        {
            PendingRequest = request;
            if (Deny) throw new SupervisionAccessDeniedException();
            return Task.FromResult(pending!);
        }
    }
}
