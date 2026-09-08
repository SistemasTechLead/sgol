using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Evidence.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class EvidenceApiEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ValidBody =
        """{"obligationId":"019d2d67-2c00-7000-8000-000000000101","requirementCode":"FOTOGRAFIA_FINAL","originalFileName":"evidence.png","declaredMediaType":"image/png","sizeBytes":128,"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","documentSubtype":null}""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticatedMutationRejectsMissingOrRepeatedCsrfBeforeBusiness(bool repeated)
    {
        using var services = Services();
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        var actor = Guid.CreateVersion7();
        var context = Context(services, actor, ValidBody);
        if (repeated) context.Request.Headers["X-CSRF-TOKEN"] = new[] { "invalid-one", "invalid-two" };
        var service = new RecordingEvidenceService();

        var result = await EvidenceApiEndpoints.CreateUploadAsync(
            context, antiforgery, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task ValidSessionBoundCsrfReachesStrictContractAndUsesHardenedCookie()
    {
        using var services = Services();
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        var actor = Guid.CreateVersion7();
        var issuance = Context(services, actor, ValidBody);
        var tokens = antiforgery.GetAndStoreTokens(issuance);
        var setCookie = Assert.Single(issuance.Response.Headers.SetCookie);
        Assert.Contains("__Host-SGOL-CSRF=", setCookie, StringComparison.Ordinal);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);

        var context = Context(services, actor, ValidBody);
        context.Request.Headers.Cookie = setCookie!.Split(';', 2)[0];
        context.Request.Headers["X-CSRF-TOKEN"] = tokens.RequestToken!;
        var service = new RecordingEvidenceService();

        var result = await EvidenceApiEndpoints.CreateUploadAsync(
            context, antiforgery, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.True(service.WasCalled);
    }

    [Fact]
    public async Task StrictJsonRejectsUnknownUploadMemberWithoutCallingBusiness()
    {
        using var services = Services();
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        var actor = Guid.CreateVersion7();
        var issuance = Context(services, actor, ValidBody);
        var tokens = antiforgery.GetAndStoreTokens(issuance);
        var context = Context(services, actor, ValidBody[..^1] + ",\"unexpected\":true}");
        context.Request.Headers.Cookie = Assert.Single(issuance.Response.Headers.SetCookie)!.Split(';', 2)[0];
        context.Request.Headers["X-CSRF-TOKEN"] = tokens.RequestToken!;
        var service = new RecordingEvidenceService();

        var result = await EvidenceApiEndpoints.CreateUploadAsync(
            context, antiforgery, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.False(service.WasCalled);
    }

    [Fact]
    public async Task StructuredContributionUsesExistingRouteWithoutFile()
    {
        using var services = Services();
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        var actor = Guid.CreateVersion7();
        var obligation = Guid.CreateVersion7();
        const string body = """{"requirementCode":"F_ENT_001","structuredPayload":{"schemaVersion":1,"formCode":"F-ENT-001","formReference":"REC-01","completedAt":"2026-09-07T20:00:00Z","hasDifference":true,"hasDamage":false}}""";
        var issuance = Context(services, actor, body);
        var tokens = antiforgery.GetAndStoreTokens(issuance);
        var context = Context(services, actor, body);
        context.Request.Headers.Cookie = Assert.Single(issuance.Response.Headers.SetCookie)!.Split(';', 2)[0];
        context.Request.Headers["X-CSRF-TOKEN"] = tokens.RequestToken!;
        var service = new RecordingEvidenceService();

        var result = await EvidenceApiEndpoints.ContributeAsync(context, obligation, antiforgery, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.LastContribution!.FileId);
        Assert.NotNull(service.LastContribution.StructuredPayload);
    }

    [Fact]
    public async Task ContributionRejectsFileAndStructuredPayloadTogetherBeforeBusiness()
    {
        using var services = Services();
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        var actor = Guid.CreateVersion7();
        var obligation = Guid.CreateVersion7();
        var body = """{"requirementCode":"F_ENT_001","fileId":"FILE_ID","structuredPayload":{"schemaVersion":1}}"""
            .Replace("FILE_ID", Guid.CreateVersion7().ToString("D"), StringComparison.Ordinal);
        var issuance = Context(services, actor, body);
        var tokens = antiforgery.GetAndStoreTokens(issuance);
        var context = Context(services, actor, body);
        context.Request.Headers.Cookie = Assert.Single(issuance.Response.Headers.SetCookie)!.Split(';', 2)[0];
        context.Request.Headers["X-CSRF-TOKEN"] = tokens.RequestToken!;
        var service = new RecordingEvidenceService();

        var result = await EvidenceApiEndpoints.ContributeAsync(context, obligation, antiforgery, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.LastContribution);
    }

    [Fact]
    public async Task EvidenceReviewGetReturnsCompleteDeterministicProjectionWithoutMutationHeaders()
    {
        using var services = Services();
        var actor = Guid.CreateVersion7();
        var obligation = Guid.CreateVersion7();
        var requirement = new EvidenceReviewRequirementDetails(Guid.CreateVersion7(), "FOTOGRAFIA_FINAL", "FOTOGRAFIA",
            "SIEMPRE", 1, EvidenceReviewApplicability.Applicable, true, Guid.CreateVersion7(), null);
        var service = new RecordingReviewService(new(Guid.CreateVersion7(), obligation, Guid.CreateVersion7(),
            EvidenceReviewResults.Complete, new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero), [requirement], []));
        var context = ReviewContext(services, actor);

        var result = await EvidenceApiEndpoints.ReviewAsync(
            context, obligation.ToString("D"), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(actor, service.LastQuery!.ActorUserId);
        Assert.Equal(obligation, service.LastQuery.ObligationId);
        var json = JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value, JsonOptions);
        Assert.Contains("\"result\":\"COMPLETA\"", json, StringComparison.Ordinal);
        Assert.Contains("\"missingRequirements\":[]", json, StringComparison.Ordinal);
        Assert.False(context.Response.Headers.ContainsKey("ETag"));
    }

    [Theory]
    [InlineData("not-a-guid", false, false)]
    [InlineData("00000000-0000-0000-0000-000000000000", false, false)]
    [InlineData("019d2d67-2c00-7000-8000-000000000101", true, false)]
    [InlineData("019d2d67-2c00-7000-8000-000000000101", false, true)]
    public async Task EvidenceReviewRejectsInvalidShapeBeforeBusiness(string id, bool query, bool idempotency)
    {
        using var services = Services();
        var context = ReviewContext(services, Guid.CreateVersion7());
        if (query) context.Request.QueryString = new QueryString("?unexpected=true");
        if (idempotency) context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        var service = new RecordingReviewService(null);

        var result = await EvidenceApiEndpoints.ReviewAsync(context, id, service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.LastQuery);
    }

    [Theory]
    [InlineData(true, StatusCodes.Status403Forbidden)]
    [InlineData(false, StatusCodes.Status404NotFound)]
    public async Task EvidenceReviewMapsScopeAndExistenceWithoutReturningData(bool denied, int expectedStatus)
    {
        using var services = Services();
        var context = ReviewContext(services, Guid.CreateVersion7());
        var service = new RecordingReviewService(null,
            denied ? new EvidenceReviewAccessDeniedException() : new EvidenceReviewObligationNotFoundException());

        var result = await EvidenceApiEndpoints.ReviewAsync(
            context, Guid.CreateVersion7().ToString("D"), service, CancellationToken.None);

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
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

    private static DefaultHttpContext Context(IServiceProvider services, Guid actor, string json)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actor.ToString("D"))], "test"));
        context.Request.Scheme = "https";
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D");
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return context;
    }

    private static DefaultHttpContext ReviewContext(IServiceProvider services, Guid actor)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actor.ToString("D"))], "test"));
        context.Request.Scheme = "https";
        context.Request.Method = HttpMethods.Get;
        context.Request.Body = new MemoryStream();
        return context;
    }

    private sealed class RecordingReviewService(EvidenceReviewDetails? result, Exception? exception = null) : IEvidenceReviewService
    {
        public EvidenceReviewQuery? LastQuery { get; private set; }

        public Task<EvidenceReviewDetails> ReviewAsync(EvidenceReviewQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return exception is null
                ? Task.FromResult(result!)
                : Task.FromException<EvidenceReviewDetails>(exception);
        }
    }

    private sealed class RecordingEvidenceService : IEvidenceContributionService
    {
        public bool WasCalled { get; private set; }
        public ContributeEvidenceCommand? LastContribution { get; private set; }

        public Task<EvidenceUploadIntentResult> CreateUploadIntentAsync(CreateEvidenceUploadCommand command, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            var upload = new EvidenceUploadAuthorization(
                new Uri("https://upload.example.test/quarantine/object"),
                new DateTimeOffset(2026, 9, 7, 20, 0, 0, TimeSpan.Zero),
                new EvidenceUploadHeaders("image/png", "128", "*", new string('a', 64), "Png", "128"));
            return Task.FromResult(new EvidenceUploadIntentResult(Guid.CreateVersion7(), "PENDIENTE_CARGA", upload, false));
        }

        public Task<EvidenceFileStatusDetails> CompleteUploadAsync(CompleteEvidenceUploadCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidenceFileStatusDetails> GetFileStatusAsync(Guid actorUserId, Guid fileId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidenceDetails> ContributeAsync(ContributeEvidenceCommand command, CancellationToken cancellationToken = default)
        {
            LastContribution = command;
            return Task.FromResult(new EvidenceDetails(Guid.CreateVersion7(), 1,
                new(Guid.CreateVersion7(), command.RequirementCode, "FORMULARIO_REFERENCIADO"),
                new(Guid.CreateVersion7(), 1, EvidenceVersionStatuses.Current, command.ActorUserId,
                    new DateTimeOffset(2026, 9, 7, 20, 0, 0, TimeSpan.Zero), null, null),
                null, command.StructuredPayload));
        }
        public Task<EvidenceDetails> ReplaceAsync(ReplaceEvidenceCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidencePage> ListAsync(EvidenceQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
