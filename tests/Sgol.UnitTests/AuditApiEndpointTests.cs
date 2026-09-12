using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Auditing.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class AuditApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset From = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CanonicalRole.Direction)]
    [InlineData(CanonicalRole.Administration)]
    [InlineData(CanonicalRole.Subcoordination)]
    [InlineData(CanonicalRole.SalesFloor)]
    public void PermissionIsVisibleOnlyToCanonicalRoles(string role) =>
        Assert.True(RoleHierarchy.GrantsAuditView(role));

    [Theory]
    [InlineData("Dirección")]
    [InlineData("GERENTE")]
    [InlineData("")]
    public void TextualPositionDoesNotGrantAudit(string role) =>
        Assert.False(RoleHierarchy.GrantsAuditView(role));

    [Fact]
    public async Task CollectionParsesClosedContractAndReturnsExactEnvelope()
    {
        var actor = Guid.CreateVersion7();
        var filteredActor = Guid.CreateVersion7();
        var resourceId = Guid.CreateVersion7();
        var correlationId = Guid.CreateVersion7();
        var context = Context(actor, "/api/v1/audit-events");
        context.Request.QueryString = new($"?from=2026-09-01T00:00:00Z&to=2026-09-12T00:00:00Z" +
            $"&actorUserId={filteredActor:D}&resourceType=ASSIGNMENT_VERSION&resourceId={resourceId:D}" +
            $"&action=ASSIGNMENT_CORRECTED&outcome=SUCCESS&correlationId={correlationId:D}" +
            "&branchCode=LOR-001&level=PISO_VENTAS&limit=10");
        var reader = new RecordingReader(Page(actor, resourceId));

        var result = await AuditApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(new AuditQueryRequest(actor, From, To, filteredActor, "ASSIGNMENT_VERSION", resourceId,
            "ASSIGNMENT_CORRECTED", "SUCCESS", correlationId, "LOR-001", CanonicalRole.SalesFloor,
            null, null, 10), reader.Request);
        Assert.Equal("private, no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value, JsonOptions));
        Assert.Equal(["data", "meta"], json.RootElement.EnumerateObject().Select(item => item.Name));
        var item = json.RootElement.GetProperty("data")[0];
        Assert.Equal(["id", "occurredAt", "actor", "action", "resource", "scope", "change", "reason", "outcome", "correlationId"],
            item.EnumerateObject().Select(property => property.Name));
        Assert.False(item.TryGetProperty("requestId", out _));
        Assert.False(item.TryGetProperty("sourceIpHash", out _));
        Assert.False(item.GetProperty("reason").TryGetProperty("text", out _));
        Assert.EndsWith("Z", item.GetProperty("occurredAt").GetString());
        Assert.EndsWith("Z", json.RootElement.GetProperty("meta").GetProperty("queriedAt").GetString());
        Assert.EndsWith("Z", json.RootElement.GetProperty("meta").GetProperty("snapshot")
            .GetProperty("upperOccurredAt").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-09-01T00:00:00Z")]
    [InlineData("?to=2026-09-12T00:00:00Z")]
    [InlineData("?from=2026-09-01T00:00:00Z&to=2026-09-12T00:00:00Z&unknown=1")]
    [InlineData("?from=2026-09-01T00:00:00Z&from=2026-09-02T00:00:00Z&to=2026-09-12T00:00:00Z")]
    [InlineData("?from=2026-09-01T00:00:00-06:00&to=2026-09-12T00:00:00Z")]
    [InlineData("?from=2026-09-01T00:00:00Z&to=2026-10-03T00:00:01Z")]
    [InlineData("?from=2026-09-01T00:00:00Z&to=2026-09-12T00:00:00Z&resourceId=01900000-0000-7000-8000-000000000001")]
    [InlineData("?from=2026-09-01T00:00:00Z&to=2026-09-12T00:00:00Z&traceObligationId=01900000-0000-7000-8000-000000000001&level=PISO_VENTAS")]
    [InlineData("?from=2026-09-01T00:00:00Z&to=2026-09-12T00:00:00Z&limit=101")]
    [InlineData("?from=2026-09-01T00:00:00Z&to=2026-09-12T00:00:00Z&cursor=")]
    [InlineData("?from=2026-09-01%2000:00:00Z&to=2026-09-12T00:00:00Z")]
    [InlineData("?from=2026-09-01T00:00:00Z&to=2026-09-12T00:00:00Z&action=lowercase")]
    public async Task InvalidCollectionParametersNeverReachReader(string query)
    {
        var context = Context(Guid.CreateVersion7(), "/api/v1/audit-events");
        context.Request.QueryString = new(query);
        var reader = new RecordingReader(null);

        var result = await AuditApiEndpoints.ReadAsync(context, reader, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.Request);
    }

    [Fact]
    public async Task DetailRejectsQueryAndConvergesNotFound()
    {
        var eventId = Guid.CreateVersion7();
        var context = Context(Guid.CreateVersion7(), $"/api/v1/audit-events/{eventId:D}");
        context.Request.QueryString = new("?action=ANY");
        var reader = new RecordingReader(null);
        var invalid = await AuditApiEndpoints.FindAsync(context, reader, eventId, CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(invalid).StatusCode);
        Assert.Null(reader.EventId);

        context.Request.QueryString = QueryString.Empty;
        reader.Failure = "not-found";
        var hidden = await AuditApiEndpoints.FindAsync(context, reader, eventId, CancellationToken.None);
        Assert.Equal(404, Assert.IsAssignableFrom<IStatusCodeHttpResult>(hidden).StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedCollectionIsRejectedWithoutReading()
    {
        var context = Context(null, "/api/v1/audit-events");
        context.Request.QueryString = new("?from=2026-09-01T00:00:00Z&to=2026-09-12T00:00:00Z");
        var reader = new RecordingReader(null);
        var result = await AuditApiEndpoints.ReadAsync(context, reader, CancellationToken.None);
        Assert.Equal(401, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.Request);
    }

    [Fact]
    public async Task CollectionRejectsBodyWithoutReading()
    {
        var context = Context(Guid.CreateVersion7(), "/api/v1/audit-events");
        context.Request.QueryString = new("?from=2026-09-01T00:00:00Z&to=2026-09-12T00:00:00Z");
        context.Request.ContentLength = 2;
        var reader = new RecordingReader(null);
        var result = await AuditApiEndpoints.ReadAsync(context, reader, CancellationToken.None);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(reader.Request);
    }

    private static AuditPage Page(Guid actor, Guid resourceId)
    {
        var eventId = Guid.CreateVersion7();
        var value = JsonDocument.Parse("\"VIGENTE\"").RootElement.Clone();
        var item = new AuditEventDetails(eventId, From.AddDays(1), new("USER", actor), "ASSIGNMENT_CORRECTED",
            new("ASSIGNMENT_VERSION", resourceId, "LOR-001"),
            new(Guid.CreateVersion7(), CanonicalRole.SalesFloor, "LOWER", Guid.CreateVersion7()),
            new(new Dictionary<string, JsonElement> { ["status"] = value },
                new Dictionary<string, JsonElement> { ["status"] = value }, 3),
            new(true), "SUCCESS", Guid.CreateVersion7());
        return new([item], null, To, new(item.OccurredAt, item.Id), null);
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

    private sealed class RecordingReader(AuditPage? page) : IAuditEventReader
    {
        public string? Failure { get; set; }
        public AuditQueryRequest? Request { get; private set; }
        public Guid? EventId { get; private set; }

        public Task<AuditPage> ReadAsync(AuditQueryRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Failure switch
            {
                "deny" => Task.FromException<AuditPage>(new AuditAccessDeniedException()),
                "cursor" => Task.FromException<AuditPage>(new AuditCursorInvalidException()),
                "not-found" => Task.FromException<AuditPage>(new AuditEventNotFoundException()),
                _ => Task.FromResult(page!),
            };
        }

        public Task<AuditDetail> FindAsync(Guid actorUserId, Guid eventId, CancellationToken cancellationToken = default)
        {
            EventId = eventId;
            return Failure == "not-found"
                ? Task.FromException<AuditDetail>(new AuditEventNotFoundException())
                : Task.FromResult(new AuditDetail(page!.Items[0], page.QueriedAt));
        }
    }
}

public sealed class AuditDeleteAttemptMiddlewareTests
{
    [Fact]
    public async Task AuthenticatedDirectionDeleteIsRejectedAndAuditedExactlyOnce()
    {
        var actor = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        var correlation = Guid.CreateVersion7();
        var writer = new RecordingSecurityWriter();
        var nextCalled = false;
        var middleware = new AuditDeleteAttemptMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = Context(actor, correlation, $"/api/v1/audit-events/{eventId:D}", writer);

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(405, context.Response.StatusCode);
        Assert.Equal(HttpMethods.Get, context.Response.Headers.Allow);
        Assert.Equal(actor, writer.ActorUserId);
        Assert.Equal(eventId, writer.ResourceId);
        Assert.Equal(1, writer.Calls);
    }

    [Fact]
    public async Task UnauthenticatedDeleteIsRejectedWithoutFunctionalAudit()
    {
        var writer = new RecordingSecurityWriter();
        var middleware = new AuditDeleteAttemptMiddleware(_ => Task.CompletedTask);
        var context = Context(null, Guid.CreateVersion7(), "/api/v1/audit-events", writer);
        await middleware.InvokeAsync(context);
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal(0, writer.Calls);
    }

    [Fact]
    public async Task InvalidAuditPathContinuesWithoutAudit()
    {
        var writer = new RecordingSecurityWriter();
        var nextCalled = false;
        var middleware = new AuditDeleteAttemptMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = Context(Guid.CreateVersion7(), Guid.CreateVersion7(),
            "/api/v1/audit-events/not-a-uuid", writer);
        await middleware.InvokeAsync(context);
        Assert.True(nextCalled);
        Assert.Equal(0, writer.Calls);
    }

    private static DefaultHttpContext Context(
        Guid? actor,
        Guid correlation,
        string path,
        IAuditSecurityEventWriter writer)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails()
            .AddSingleton(writer).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Delete;
        context.Request.Path = path;
        context.TraceIdentifier = correlation.ToString("D");
        if (actor.HasValue)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Value.ToString("D"))], "test"));
        return context;
    }

    private sealed class RecordingSecurityWriter : IAuditSecurityEventWriter
    {
        public int Calls { get; private set; }
        public Guid ActorUserId { get; private set; }
        public Guid? ResourceId { get; private set; }

        public Task WriteDeleteAttemptAsync(Guid actorUserId, Guid correlationId, Guid? resourceId,
            CancellationToken cancellationToken = default)
        {
            Calls++; ActorUserId = actorUserId; ResourceId = resourceId;
            return Task.CompletedTask;
        }
    }
}
