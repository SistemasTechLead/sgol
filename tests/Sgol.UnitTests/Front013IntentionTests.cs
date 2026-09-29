using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Pages.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front013IntentionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredIntentNeverPostsAndOnlyReadsAnExplicitKnownId(bool knownId)
    {
        using var services = Services();
        var actor = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();
        var api = new ReadOnlyApi(requestId);
        var form = await FormAsync(services, actor, DateTimeOffset.UtcNow.AddMinutes(-1), knownId ? requestId : null);
        var model = Model(services, actor, api, form);
        Assert.IsType<PageResult>(await model.OnPostCreateManualAsync(CancellationToken.None));
        Assert.Null(model.ManualIntentToken);
        Assert.Equal(!knownId, model.ManualExpired);
        Assert.Equal(knownId, api.ReadResult);
        Assert.Equal(knownId ? requestId : null, model.ManualResult?.GenerationRequestId);
        Assert.Equal(knownId ? 200 : 409, form.Response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IntentCannotMoveAcrossActorsOrBeModified(bool modified)
    {
        using var services = Services();
        var actor = Guid.CreateVersion7();
        var api = new ReadOnlyApi(Guid.CreateVersion7());
        var form = await FormAsync(services, modified ? actor : Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddHours(1), null, modified);
        var model = Model(services, actor, api, form);
        Assert.IsType<PageResult>(await model.OnPostCreateManualAsync(CancellationToken.None));
        Assert.Equal(400, form.Response.StatusCode);
        Assert.Null(model.ManualIntentToken);
        Assert.Null(model.PreparedManual);
        Assert.False(api.ReadResult);
    }

    [Fact]
    public async Task MissingCsrfRejectsBeforeAnyApiCall()
    {
        using var services = Services();
        var api = new ReadOnlyApi(Guid.CreateVersion7());
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = "POST";
        context.Request.Scheme = "https";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        Assert.IsType<BadRequestResult>(await Model(services, Guid.CreateVersion7(), api, context).OnPostCreateManualAsync(CancellationToken.None));
        Assert.Equal(0, api.Calls);
    }

    [Theory]
    [InlineData(409, "GENERACION_LEGACY_REQUIERE_REVISION", "Hay orígenes históricos que requieren revisión antes de nuevas altas")]
    [InlineData(422, "CALENDARIO_GENERACION_INCOMPLETO", "El calendario publicado no permite calcular el plazo")]
    [InlineData(409, "ORIGEN_PADRE_NO_DISPONIBLE", "La recepción ya no admite esta incidencia")]
    [InlineData(422, "ORIGEN_INVALIDO", "Revisa los campos de origen")]
    [InlineData(400, "ORIGEN_PADRE_NO_DISPONIBLE", null)]
    [InlineData(409, "UNKNOWN_SYNTHETIC_CODE", null)]
    public void ConsumerMessagesRequireApprovedCodeAndMatchingStatus(int status, string code, string? expected) =>
        Assert.Equal(expected, IndexModel.ManualMessage(status, code));
    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddAntiforgery();
        return services.BuildServiceProvider();
    }
    private static IndexModel Model(IServiceProvider services, Guid actor, ReadOnlyApi api, HttpContext context)
    {
        var session = new Session(actor);
        var csrf = services.GetRequiredService<IAntiforgery>();
        return new(session, api, new RazorAntiforgeryBridge(csrf, api, session), services.GetRequiredService<IDataProtectionProvider>(), csrf)
        { PageContext = new PageContext { HttpContext = context } };
    }
    private static async Task<DefaultHttpContext> FormAsync(IServiceProvider services, Guid actor, DateTimeOffset expires, Guid? known, bool modified = false)
    {
        var issuance = new DefaultHttpContext { RequestServices = services };
        issuance.Request.Scheme = "https";
        var csrf = services.GetRequiredService<IAntiforgery>().GetAndStoreTokens(issuance);
        var protector = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("SGOL.FRONT-013.manual-intention.v2");
        var body = new IndexModel.ManualBody(2, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "MANUAL_REFERENCE_V1",
            JsonSerializer.SerializeToElement(new { taskCode = "TAR-0018", eventReference = "SYN-E", zoneReference = "SYN-Z", planogramReference = "SYN-P" }));
        var intention = protector.Protect(JsonSerializer.Serialize(new { Actor = actor, Key = Guid.CreateVersion7(), ExpiresAt = expires, Body = body }));
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = "POST";
        context.Request.Scheme = "https";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Headers.Cookie = issuance.Response.Headers.SetCookie.ToString().Split(';')[0];
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrf.RequestToken!,
            ["manualIntent"] = modified ? "invalid" : intention,
            ["taskCode"] = "TAR-0018",
            ["isoYear"] = "2026",
            ["isoWeek"] = "40",
            ["from"] = "2026-09-28",
            ["to"] = "2026-10-04",
            ["knownGenerationRequestId"] = known?.ToString("D") ?? ""
        });
        var bytes = await content.ReadAsByteArrayAsync();
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        return context;
    }
    private sealed class Session(Guid actor) : IRazorSessionState
    {
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(
            new(actor, Guid.CreateVersion7(), "synthetic", "Sintética", "LOR-001", "DIRECCION", [GenerationRequestAuthorization.Create],
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow.AddHours(8)));
    }
    private sealed class ReadOnlyApi(Guid requestId) : ISgolApiClient
    {
        public bool ReadResult { get; private set; }
        public int Calls { get; private set; }
        public Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            object? data = null;
            if (request.Path == $"/api/v1/generation-requests/{requestId:D}")
            {
                ReadResult = true;
                data = new GenerationRequestDetails(requestId, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
                    "MANUAL_REFERENCE_V1", "SYN", "ACEPTADA", Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.CreateVersion7(), null);
            }
            return Task.FromResult(new ApiResponse<T>(200, data is null ? default : (T)data, [], Guid.CreateVersion7().ToString("D"), null, 0, null, false, null, null));
        }
    }
}
