using System.Security.Claims;
using System.Text;
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

    private sealed class RecordingEvidenceService : IEvidenceContributionService
    {
        public bool WasCalled { get; private set; }

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
        public Task<EvidenceDetails> ContributeAsync(ContributeEvidenceCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidenceDetails> ReplaceAsync(ReplaceEvidenceCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidencePage> ListAsync(EvidenceQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
