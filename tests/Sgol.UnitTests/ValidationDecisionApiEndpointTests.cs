using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Validation.Contracts;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ValidationDecisionApiEndpointTests
{
    [Fact]
    public async Task IssueReturnsCreatedLocationAndRequirementEtag()
    {
        using var services = Services();
        var actor = Guid.CreateVersion7(); var obligation = Guid.CreateVersion7(); var requirement = Guid.CreateVersion7();
        var decision = new ValidationDecisionDetails(Guid.CreateVersion7(), 1, "CUMPLIDA", "Fundamento válido.",
            "VIGENTE", "ORDINARIA", actor, "ADMINISTRACION", DateTimeOffset.UtcNow, null, null,
            Guid.CreateVersion7(), [Guid.CreateVersion7()]);
        var history = new ValidationHistoryDetails(obligation, "CONCLUIDA", 1,
            new(requirement, Guid.CreateVersion7(), "RESUELTA", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1), [decision]);
        var context = Context(services, actor, obligation, "{\"result\":\"CUMPLIDA\",\"foundation\":\"Fundamento válido.\",\"escalationReason\":null}");
        AddCsrf(services, context, actor, obligation);
        var service = new RecordingService(new(history, decision, false));

        var result = await ValidationDecisionApiEndpoints.IssueAsync(context, obligation.ToString("D"),
            services.GetRequiredService<IAntiforgery>(), service, CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("\"1\"", context.Response.Headers.ETag);
        Assert.Equal($"/api/v1/obligations/{obligation:D}/validations", context.Response.Headers.Location);
        Assert.Equal("CUMPLIDA", service.Issue!.Result);
    }

    [Theory]
    [InlineData("{\"result\":\"CUMPLIDA\"}")]
    [InlineData("{\"result\":\"CUMPLIDA\",\"foundation\":\"x\",\"extra\":true}")]
    [InlineData("{\"result\":\"CUMPLIDA\",\"result\":\"INCOMPLETA\",\"foundation\":\"x\",\"escalationReason\":null}")]
    public async Task InvalidShapeIsRejectedBeforeBusiness(string json)
    {
        using var services = Services(); var actor = Guid.CreateVersion7(); var obligation = Guid.CreateVersion7();
        var context = Context(services, actor, obligation, json); var service = new RecordingService(null);
        var result = await ValidationDecisionApiEndpoints.IssueAsync(context, obligation.ToString("D"),
            services.GetRequiredService<IAntiforgery>(), service, CancellationToken.None);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Null(service.Issue);
    }

    [Theory]
    [InlineData("AUTOVALIDACION_NO_PERMITIDA", 422)]
    [InlineData("VERSION_CONFLICT", 412)]
    [InlineData("DECISION_VALIDACION_YA_EXISTE", 409)]
    [InlineData("OBLIGACION_NO_ENCONTRADA", 404)]
    public async Task FunctionalFailuresMapToApprovedStatus(string code, int status)
    {
        using var services = Services(); var actor = Guid.CreateVersion7(); var obligation = Guid.CreateVersion7();
        var context = Context(services, actor, obligation, "{\"result\":\"CUMPLIDA\",\"foundation\":\"Fundamento válido.\",\"escalationReason\":null}");
        AddCsrf(services, context, actor, obligation);
        var result = await ValidationDecisionApiEndpoints.IssueAsync(context, obligation.ToString("D"),
            services.GetRequiredService<IAntiforgery>(), new RecordingService(null, new ValidationDecisionException(code)), CancellationToken.None);
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN"; options.Cookie.Name = "__Host-SGOL-CSRF";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always; options.Cookie.Path = "/";
        });
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext Context(IServiceProvider services, Guid actor, Guid obligation, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json); var context = new DefaultHttpContext { RequestServices = services };
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString("D"))], "test"));
        context.Request.Scheme = "https"; context.Request.Method = "POST";
        context.Request.Path = $"/api/v1/obligations/{obligation:D}/validation-decisions";
        context.Request.Body = new MemoryStream(bytes); context.Request.ContentLength = bytes.Length; context.Request.ContentType = "application/json";
        context.Request.Headers["Idempotency-Key"] = Guid.CreateVersion7().ToString("D"); context.Request.Headers.IfMatch = "\"1\"";
        return context;
    }

    private static void AddCsrf(IServiceProvider services, DefaultHttpContext context, Guid actor, Guid obligation)
    {
        var antiforgery = services.GetRequiredService<IAntiforgery>(); var issuance = Context(services, actor, obligation, "{}");
        var tokens = antiforgery.GetAndStoreTokens(issuance);
        context.Request.Headers.Cookie = Assert.Single(issuance.Response.Headers.SetCookie)!.Split(';', 2)[0];
        context.Request.Headers["X-CSRF-TOKEN"] = tokens.RequestToken!;
    }

    private sealed class RecordingService(ValidationMutationResult? result, Exception? exception = null) : IValidationDecisionService
    {
        public IssueValidationDecisionCommand? Issue { get; private set; }
        public Task<ValidationMutationResult> IssueAsync(IssueValidationDecisionCommand command, CancellationToken cancellationToken = default)
        { Issue = command; return exception is null ? Task.FromResult(result!) : Task.FromException<ValidationMutationResult>(exception); }
        public Task<ValidationMutationResult> ReplaceAsync(ReplaceValidationDecisionCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ValidationHistoryDetails> GetAsync(GetValidationHistoryQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
