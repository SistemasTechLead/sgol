using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Continuity.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ContinuityApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ContinuityPermissionIsExclusiveToCanonicalDirection()
    {
        Assert.True(RoleHierarchy.GrantsContinuityView(CanonicalRole.Direction));
        Assert.False(RoleHierarchy.GrantsContinuityView(CanonicalRole.Administration));
        Assert.False(RoleHierarchy.GrantsContinuityView(CanonicalRole.Subcoordination));
        Assert.False(RoleHierarchy.GrantsContinuityView(CanonicalRole.SalesFloor));
        Assert.False(RoleHierarchy.GrantsContinuityView("Dirección"));
    }

    [Fact]
    public async Task CreateUsesClosedContractAndDoesNotExposeArtifactLocations()
    {
        var actor = Guid.CreateVersion7();
        var key = Guid.CreateVersion7();
        var id = Guid.CreateVersion7();
        var context = Context(actor, HttpMethods.Post, "/api/v1/continuity/reconciliations",
            "{\"reason\":\"Simulacro sintético\"}");
        context.Request.Headers["Idempotency-Key"] = key.ToString("D");
        var service = new RecordingService(Details(id));

        var result = await ContinuityApiEndpoints.CreateAsync(context, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(actor, service.Created?.ActorUserId);
        Assert.Equal(key, service.Created?.IdempotencyKey);
        Assert.Equal("\"1\"", context.Response.Headers.ETag);
        Assert.Equal($"/api/v1/continuity/reconciliations/{id:D}", context.Response.Headers.Location);
        var json = JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value, JsonOptions);
        Assert.DoesNotContain("s3", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manifestUri", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, "{\"reason\":\"x\"}")]
    [InlineData("not-a-uuid", "{\"reason\":\"x\"}")]
    [InlineData("00000000-0000-0000-0000-000000000000", "{\"reason\":\"x\"}")]
    [InlineData("valid", "{\"reason\":\"x\",\"extra\":true}")]
    public async Task InvalidCreateInputHasNoEffect(string? header, string body)
    {
        var context = Context(Guid.CreateVersion7(), HttpMethods.Post,
            "/api/v1/continuity/reconciliations", body);
        if (header is not null)
            context.Request.Headers["Idempotency-Key"] = header == "valid" ? Guid.CreateVersion7().ToString("D") : header;
        var service = new RecordingService(Details(Guid.CreateVersion7()));

        var result = await ContinuityApiEndpoints.CreateAsync(context, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Created);
    }

    [Fact]
    public async Task GetMapsNotFoundWithoutLeakingExistence()
    {
        var context = Context(Guid.CreateVersion7(), HttpMethods.Get,
            "/api/v1/continuity/reconciliations/00000000-0000-0000-0000-000000000001");
        var service = new RecordingService(null) { Failure = "not-found" };

        var result = await ContinuityApiEndpoints.GetAsync(context, Guid.CreateVersion7(), service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("application/problem+json", Assert.IsAssignableFrom<IContentTypeHttpResult>(result).ContentType);
    }

    [Fact]
    public async Task ApprovalRequiresVersionBeforeCallingService()
    {
        var context = Context(Guid.CreateVersion7(), HttpMethods.Post,
            "/api/v1/continuity/reconciliations/x/approval", "{\"reason\":\"Conforme\"}");
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        var service = new RecordingService(Details(Guid.CreateVersion7()));

        var result = await ContinuityApiEndpoints.ApproveAsync(context, Guid.CreateVersion7(), service,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status428PreconditionRequired,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Approved);
    }

    private static RecoveryReconciliationDetails Details(Guid id) => new(id,
        Guid.Parse("0199f36f-a033-7b52-b775-e30d7084f513"), RecoveryReconciliationStatuses.Requested,
        DateTimeOffset.UnixEpoch, null, 1, null, null, 0, false, null, null, null, null, []);

    private static DefaultHttpContext Context(Guid? actor, string method, string path, string body = "")
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.TraceIdentifier = Guid.CreateVersion7().ToString("D");
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (actor.HasValue)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Value.ToString("D"))], "test"));
        return context;
    }

    private sealed class RecordingService(RecoveryReconciliationDetails? details) : IRecoveryReconciliationService
    {
        public string? Failure { get; init; }
        public CreateRecoveryReconciliationCommand? Created { get; private set; }
        public ApproveRecoveryReconciliationCommand? Approved { get; private set; }

        public Task<RecoveryReconciliationDetails> CreateAsync(CreateRecoveryReconciliationCommand command,
            CancellationToken cancellationToken = default)
        {
            Created = command;
            return Task.FromResult(details!);
        }

        public Task<RecoveryReconciliationDetails> GetAsync(RecoveryReconciliationQuery query,
            CancellationToken cancellationToken = default) => Failure == "not-found"
                ? Task.FromException<RecoveryReconciliationDetails>(new RecoveryReconciliationNotFoundException())
                : Task.FromResult(details!);

        public Task<RecoveryReconciliationDetails> ApproveAsync(ApproveRecoveryReconciliationCommand command,
            CancellationToken cancellationToken = default)
        {
            Approved = command;
            return Task.FromResult(details!);
        }
    }
}
