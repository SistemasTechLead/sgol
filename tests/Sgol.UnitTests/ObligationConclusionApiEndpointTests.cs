using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Execution.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ObligationConclusionApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ValidEmptyRequestReturnsApprovedProjectionAndStrongEtag()
    {
        using var services = Services();
        var actor = Guid.CreateVersion7();
        var obligation = Guid.CreateVersion7();
        var recordedAt = new DateTimeOffset(2026, 9, 8, 22, 30, 0, TimeSpan.Zero);
        var expected = new ObligationConclusionResult(
            obligation, "CONCLUIDA", recordedAt, actor, 8,
            new(Guid.CreateVersion7(), "CONCLUIDA", JsonDocument.Parse("{\"schemaVersion\":1}"),
                Guid.CreateVersion7(), actor, recordedAt));
        var context = ValidContext(services, actor, obligation, 7);
        AddValidCsrf(services, context, actor, obligation, 7);
        var service = new RecordingService(expected);

        var result = await ObligationConclusionApiEndpoints.HandleAsync(
            context, obligation.ToString("D"), services.GetRequiredService<IAntiforgery>(), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("\"8\"", context.Response.Headers.ETag);
        Assert.Equal(actor, service.LastCommand!.ActorUserId);
        Assert.Equal(7, service.LastCommand.ExpectedRowVersion);
        var json = JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value, JsonOptions);
        Assert.Contains("\"resultPayload\":{\"schemaVersion\":1}", json, StringComparison.Ordinal);
        Assert.Contains($"\"evidenceReviewSnapshotId\":\"{expected.ExecutionResult.EvidenceReviewSnapshotId:D}\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing-idempotency")]
    [InlineData("invalid-idempotency")]
    [InlineData("missing-if-match")]
    [InlineData("weak-if-match")]
    [InlineData("noncanonical-if-match")]
    [InlineData("body")]
    [InlineData("query")]
    public async Task InvalidTransportContractRejectsBeforeBusiness(string defect)
    {
        using var services = Services();
        var actor = Guid.CreateVersion7();
        var obligation = Guid.CreateVersion7();
        var context = ValidContext(services, actor, obligation, 1);
        switch (defect)
        {
            case "missing-idempotency": context.Request.Headers.Remove("Idempotency-Key"); break;
            case "invalid-idempotency": context.Request.Headers["Idempotency-Key"] = "not-a-uuid"; break;
            case "missing-if-match": context.Request.Headers.Remove("If-Match"); break;
            case "weak-if-match": context.Request.Headers.IfMatch = "W/\"1\""; break;
            case "noncanonical-if-match": context.Request.Headers.IfMatch = "\"01\""; break;
            case "body": context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}")); context.Request.ContentLength = 2; break;
            case "query": context.Request.QueryString = new QueryString("?x=1"); break;
        }

        var service = new RecordingService(null);
        var result = await ObligationConclusionApiEndpoints.HandleAsync(
            context, obligation.ToString("D"), services.GetRequiredService<IAntiforgery>(), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.LastCommand);
    }

    [Fact]
    public async Task MissingCsrfRejectsAuthenticatedMutationBeforeBusiness()
    {
        using var services = Services();
        var obligation = Guid.CreateVersion7();
        var context = ValidContext(services, Guid.CreateVersion7(), obligation, 1);
        var service = new RecordingService(null);

        var result = await ObligationConclusionApiEndpoints.HandleAsync(
            context, obligation.ToString("D"), services.GetRequiredService<IAntiforgery>(), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.LastCommand);
    }

    [Theory]
    [InlineData("not-found", StatusCodes.Status404NotFound)]
    [InlineData("version", StatusCodes.Status412PreconditionFailed)]
    [InlineData("concluded", StatusCodes.Status409Conflict)]
    [InlineData("idempotency", StatusCodes.Status409Conflict)]
    [InlineData("incomplete", StatusCodes.Status422UnprocessableEntity)]
    public async Task FunctionalFailuresUseApprovedStatusAndProblemDetails(string failure, int expectedStatus)
    {
        using var services = Services();
        var actor = Guid.CreateVersion7();
        var obligation = Guid.CreateVersion7();
        var context = ValidContext(services, actor, obligation, 1);
        AddValidCsrf(services, context, actor, obligation, 1);
        Exception exception = failure switch
        {
            "not-found" => new ObligationConclusionNotFoundException(),
            "version" => new ObligationConclusionVersionConflictException(),
            "concluded" => new ObligationAlreadyConcludedException(),
            "idempotency" => new ObligationConclusionIdempotencyConflictException(),
            _ => new ObligationEvidenceMissingException(["F_ENT_001"]),
        };

        var result = await ObligationConclusionApiEndpoints.HandleAsync(
            context, obligation.ToString("D"), services.GetRequiredService<IAntiforgery>(),
            new RecordingService(null, exception), CancellationToken.None);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("application/problem+json", Assert.IsAssignableFrom<IContentTypeHttpResult>(result).ContentType);
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-SGOL-CSRF";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext ValidContext(
        IServiceProvider services, Guid actor, Guid obligation, long version)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actor.ToString("D"))], "test"));
        context.Request.Scheme = "https";
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = $"/api/v1/obligations/{obligation:D}/conclusion";
        context.Request.Body = new MemoryStream();
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.Request.Headers.IfMatch = $"\"{version}\"";
        return context;
    }

    private static void AddValidCsrf(
        IServiceProvider services, DefaultHttpContext context, Guid actor, Guid obligation, long version)
    {
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        var issuance = ValidContext(services, actor, obligation, version);
        var tokens = antiforgery.GetAndStoreTokens(issuance);
        context.Request.Headers.Cookie = Assert.Single(issuance.Response.Headers.SetCookie)!.Split(';', 2)[0];
        context.Request.Headers["X-CSRF-TOKEN"] = tokens.RequestToken!;
    }

    private sealed class RecordingService(ObligationConclusionResult? result, Exception? exception = null)
        : IObligationConclusionService
    {
        public ConcludeObligationCommand? LastCommand { get; private set; }

        public Task<ObligationConclusionResult> ConcludeAsync(
            ConcludeObligationCommand command, CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return exception is null
                ? Task.FromResult(result!)
                : Task.FromException<ObligationConclusionResult>(exception);
        }
    }
}
