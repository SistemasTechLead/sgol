using System.Net;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Xunit;

namespace Sgol.UnitTests;

public sealed class TechFront003Tests
{
    private static readonly string[] MixedSetCookieHeaders =
    [
        "__Host-SGOL-Session=opaque; Path=/; Secure; HttpOnly; SameSite=Strict",
        "unexpected=opaque; Path=/; Secure; HttpOnly; SameSite=Strict",
    ];

    [Theory]
    [InlineData("DIRECCION")]
    [InlineData("ADMINISTRACION")]
    [InlineData("SUBCOORDINACION")]
    [InlineData("PISO_VENTAS")]
    public async Task SessionIsQueriedOnceWithinRequestAndAgainForNextRequest(string role)
    {
        var api = new RecordingApi(Success(role));
        var first = new RazorSessionState(api);
        Assert.Equal(role, (await first.GetAsync())!.RoleCode);
        Assert.Equal(role, (await first.GetAsync())!.RoleCode);
        Assert.Equal(1, api.Calls);
        var second = new RazorSessionState(api);
        Assert.Equal(role, (await second.GetAsync())!.RoleCode);
        Assert.Equal(2, api.Calls);
    }

    [Fact]
    public async Task PreauthOrInvalidSessionNeverBuildsShellIdentity()
    {
        var preauth = new RazorSessionState(new RecordingApi(Error(401)));
        Assert.Null(await preauth.GetAsync());
        Assert.True(preauth.IsInvalid);
        var expired = new RazorSessionState(new RecordingApi(Success("DIRECCION") with
        {
            Data = Snapshot("DIRECCION") with { IdleExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) }
        }));
        Assert.Null(await expired.GetAsync());
        Assert.True(expired.IsInvalid);
        var changed = new RazorSessionState(new RecordingApi(Success("DIRECCION")));
        Assert.NotNull(await changed.GetAsync());
        changed.Invalidate();
        Assert.Null(await changed.GetAsync());
    }

    [Theory]
    [InlineData("DIRECCION")]
    [InlineData("ADMINISTRACION")]
    [InlineData("SUBCOORDINACION")]
    [InlineData("PISO_VENTAS")]
    public void NavigationRequiresCanonicalRolePermissionAndImplementedRazorRoute(string role)
    {
        var item = new NavigationItem("Synthetic", "/synthetic-protected",
            new HashSet<string>([role], StringComparer.Ordinal),
            new HashSet<string>(["SYNTHETIC_PERMISSION"], StringComparer.Ordinal));
        var session = Snapshot(role) with { Permissions = ["SYNTHETIC_PERMISSION"] };
        var implemented = new HashSet<string>(["/synthetic-protected"], StringComparer.Ordinal);

        Assert.Single(RoleAwareNavigation.VisibleTo(session, [item], implemented));
        Assert.Empty(RoleAwareNavigation.VisibleTo(session with { Permissions = [] }, [item], implemented));
        Assert.Empty(RoleAwareNavigation.VisibleTo(session, [item], new HashSet<string>()));
        Assert.Empty(RoleAwareNavigation.VisibleTo(null, [item], implemented));
        Assert.Empty(RoleAwareNavigation.VisibleTo(session, [item]));
    }

    [Fact]
    public void CookieBridgeForwardsOnlyAllowedCookiesAndRequiresCsrfPair()
    {
        var context = Context("__Host-SGOL-Session=one; __Host-SGOL-PreAuth=two; __Host-SGOL-CSRF=three");
        Assert.Equal("__Host-SGOL-Session=one", ApiCookieBridge.RequestCookie(context, HttpMethod.Get,
            "/api/v1/auth/session", false));
        Assert.Equal("__Host-SGOL-PreAuth=two; __Host-SGOL-CSRF=three",
            ApiCookieBridge.RequestCookie(context, HttpMethod.Post, "/api/v1/auth/mfa/verify", true));
        Assert.Null(ApiCookieBridge.RequestCookie(context, HttpMethod.Get, "/api/v1/auth/mfa/verify", false));
        Assert.Equal("__Host-SGOL-Session=one; __Host-SGOL-CSRF=three",
            ApiCookieBridge.RequestCookie(context, HttpMethod.Post, "/api/v1/obligations/1/conclusion", true));
        context.Request.Headers.Cookie = "__Host-SGOL-Session=one";
        Assert.Throws<ApiProtocolException>(() => ApiCookieBridge.RequestCookie(context, HttpMethod.Post,
            "/api/v1/auth/logout", true));
        context.Request.Headers.Cookie = "__Host-SGOL-Session=one; __Host-SGOL-Session=two";
        Assert.Throws<ApiProtocolException>(() => ApiCookieBridge.RequestCookie(context, HttpMethod.Get,
            "/api/v1/auth/session", false));
        context.Request.Headers.Cookie = "__Host-SGOL-Session=one; unexpected=four";
        Assert.Throws<ApiProtocolException>(() => ApiCookieBridge.RequestCookie(context, HttpMethod.Get,
            "/api/v1/auth/session", false));
    }

    [Theory]
    [InlineData("__Host-SGOL-Session=opaque; Path=/; Secure; HttpOnly; SameSite=Strict", true)]
    [InlineData("__Host-SGOL-Session=opaque; Path=/; Secure; HttpOnly; SameSite=Lax", false)]
    [InlineData("__Host-SGOL-Session=opaque; Path=/; Secure; HttpOnly; SameSite=Strict; Domain=example.com", false)]
    [InlineData("unexpected=opaque; Path=/; Secure; HttpOnly; SameSite=Strict", false)]
    public void SetCookieIsValidatedBeforePropagation(string header, bool allowed)
    {
        var context = Context("");
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Set-Cookie", header);
        if (allowed)
            ApiCookieBridge.ApplyResponseCookies(context,
                ApiCookieBridge.ValidateResponseCookies(response, HttpMethod.Get, "/api/v1/auth/session"));
        else
            Assert.Throws<ApiProtocolException>(() => ApiCookieBridge.ValidateResponseCookies(response,
                HttpMethod.Get, "/api/v1/auth/session"));
        Assert.Equal(allowed, context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public void SetCookieRouteAndBatchMustBeAllowedBeforeAnyCookieIsApplied()
    {
        var context = Context("");
        using var wrongRoute = new HttpResponseMessage(HttpStatusCode.OK);
        wrongRoute.Headers.TryAddWithoutValidation("Set-Cookie",
            "__Host-SGOL-PreAuth=opaque; Path=/; Secure; HttpOnly; SameSite=Strict");
        Assert.Throws<ApiProtocolException>(() => ApiCookieBridge.ValidateResponseCookies(wrongRoute,
            HttpMethod.Get, "/api/v1/branches/LOR-001"));

        using var mixed = new HttpResponseMessage(HttpStatusCode.OK);
        mixed.Headers.TryAddWithoutValidation("Set-Cookie", MixedSetCookieHeaders);
        Assert.Throws<ApiProtocolException>(() => ApiCookieBridge.ValidateResponseCookies(mixed,
            HttpMethod.Get, "/api/v1/auth/session"));
        Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
    }

    [Fact]
    public void ReturnDestinationRejectsExternalApiMutationAndTampering()
    {
        var services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        var protector = new SafeReturnDestination(services.GetRequiredService<IDataProtectionProvider>());
        var implemented = new HashSet<string> { "/synthetic-protected" };
        var token = protector.Protect("/synthetic-protected", implemented);
        Assert.Equal("/synthetic-protected", protector.Read(token, implemented, _ => true));
        Assert.Null(protector.Read(token + "altered", implemented, _ => true));
        Assert.Null(protector.Protect("//external.example", implemented));
        Assert.Null(protector.Protect("https://external.example", implemented));
        Assert.Null(protector.Protect("/api/v1/auth/session", implemented));
        Assert.Null(protector.Protect("/synthetic-protected?secret=x", implemented));
        Assert.Null(protector.Read(token, implemented, _ => false));
        Assert.Null(protector.Read(token, new HashSet<string>(), _ => true));
        Assert.Null(protector.Protect("/synthetic-protected"));
    }

    [Fact]
    public async Task RazorCsrfPairMustValidateBeforeApiMutation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-SGOL-CSRF";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });
        using var provider = services.BuildServiceProvider();
        var api = new RecordingMutationApi();
        var bridge = new RazorAntiforgeryBridge(provider.GetRequiredService<IAntiforgery>(), api);
        var get = Context("");
        get.RequestServices = provider;
        var token = bridge.Issue(get);
        var cookie = get.Response.Headers.SetCookie.ToString().Split(';')[0];

        var valid = FormContext(provider, cookie, token);
        var request = new ApiRequest(HttpMethod.Post, "/api/v1/auth/logout", ApiResponseShape.NoContent);
        Assert.NotNull(await bridge.SendValidatedAsync<object>(valid, request));
        Assert.Equal(token, api.LastToken);
        Assert.Equal(1, api.Calls);

        var absent = FormContext(provider, cookie, "");
        Assert.Null(await bridge.SendValidatedAsync<object>(absent, request));
        var mixed = FormContext(provider, cookie, token + "altered");
        Assert.Null(await bridge.SendValidatedAsync<object>(mixed, request));
        var expiredPair = FormContext(provider, "", token);
        Assert.Null(await bridge.SendValidatedAsync<object>(expiredPair, request));
        Assert.Equal(1, api.Calls);
    }

    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/auth/password/change")]
    [InlineData("/api/v1/auth/mfa/confirm")]
    [InlineData("/api/v1/auth/mfa/verify")]
    [InlineData("/api/v1/auth/logout")]
    public async Task AuthenticationTransitionsInvalidateSnapshotAndCsrf(string path)
    {
        using var provider = AntiforgeryServices();
        var state = new RecordingSessionState();
        var bridge = new RazorAntiforgeryBridge(provider.GetRequiredService<IAntiforgery>(),
            new RecordingMutationApi(), state);
        var get = Context("");
        get.RequestServices = provider;
        var token = bridge.Issue(get);
        var post = FormContext(provider, get.Response.Headers.SetCookie.ToString().Split(';')[0], token);

        Assert.NotNull(await bridge.SendValidatedAsync<object>(post,
            new ApiRequest(HttpMethod.Post, path, ApiResponseShape.NoContent)));
        Assert.True(state.IsInvalid);
        Assert.Contains("__Host-SGOL-CSRF=", post.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(401, false)]
    [InlineData(503, false)]
    [InlineData(0, true)]
    public async Task LogoutFailureClearsBrowserCookiesAndSnapshot(int status, bool throws)
    {
        using var provider = AntiforgeryServices();
        var state = new RecordingSessionState();
        var bridge = new RazorAntiforgeryBridge(provider.GetRequiredService<IAntiforgery>(),
            new RecordingMutationApi(status, throws), state);
        var get = Context("");
        get.RequestServices = provider;
        var token = bridge.Issue(get);
        var post = FormContext(provider, get.Response.Headers.SetCookie.ToString().Split(';')[0], token);
        if (throws)
            await Assert.ThrowsAsync<HttpRequestException>(() => bridge.SendValidatedAsync<object>(post,
                new ApiRequest(HttpMethod.Post, "/api/v1/auth/logout", ApiResponseShape.NoContent)));
        else
            Assert.NotNull(await bridge.SendValidatedAsync<object>(post,
                new ApiRequest(HttpMethod.Post, "/api/v1/auth/logout", ApiResponseShape.NoContent)));
        Assert.True(state.IsInvalid);
        var cookies = post.Response.Headers.SetCookie.ToString();
        Assert.Contains("__Host-SGOL-Session=", cookies, StringComparison.Ordinal);
        Assert.Contains("__Host-SGOL-PreAuth=", cookies, StringComparison.Ordinal);
        Assert.Contains("__Host-SGOL-CSRF=", cookies, StringComparison.Ordinal);
    }

    private static ServiceProvider AntiforgeryServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-SGOL-CSRF";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext FormContext(IServiceProvider provider, string cookie, string token)
    {
        var context = Context(cookie);
        context.RequestServices = provider;
        context.Request.Method = "POST";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        var content = Encoding.UTF8.GetBytes("__RequestVerificationToken=" + Uri.EscapeDataString(token));
        context.Request.ContentLength = content.Length;
        context.Request.Body = new MemoryStream(content);
        return context;
    }

    private static DefaultHttpContext Context(string cookies)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Headers.Cookie = cookies;
        return context;
    }

    private static SessionSnapshot Snapshot(string role) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "synthetic", "Synthetic", "LOR-001", role, [],
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow.AddHours(8));

    private static ApiResponse<SessionSnapshot> Success(string role) =>
        new(200, Snapshot(role), null, Guid.CreateVersion7().ToString("D"), null, null, null, false, null, null);

    private static ApiResponse<SessionSnapshot> Error(int status) =>
        new(status, null, null, Guid.CreateVersion7().ToString("D"), null, null, null, false,
            "SESSION_INVALID", new("Sesión no válida", "Vuelve a iniciar sesión.", null));

    private sealed class RecordingApi(ApiResponse<SessionSnapshot> response) : ISgolApiClient
    {
        public int Calls { get; private set; }
        public Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal("/api/v1/auth/session", request.Path);
            Calls++;
            return Task.FromResult((ApiResponse<T>)(object)response);
        }
    }

    private sealed class RecordingMutationApi(int status = 204, bool throws = false) : ISgolApiClient
    {
        public int Calls { get; private set; }
        public string? LastToken { get; private set; }
        public Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastToken = request.CsrfToken;
            if (throws) throw new HttpRequestException("Synthetic outage");
            return Task.FromResult(new ApiResponse<T>(status, default, null, Guid.CreateVersion7().ToString("D"),
                null, null, null, false, status < 400 ? null : "SYNTHETIC_ERROR",
                status < 400 ? null : new ProblemDetailsPresentation("Error", "Synthetic failure", null)));
        }
    }

    private sealed class RecordingSessionState : IRazorSessionState
    {
        public bool IsInvalid { get; private set; }
        public void Invalidate() => IsInvalid = true;
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<SessionSnapshot?>(null);
    }
}
