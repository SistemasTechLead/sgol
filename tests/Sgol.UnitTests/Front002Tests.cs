using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Identity.Contracts;
using Sgol.Web.Pages.MyWork;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front002Tests
{
    [Fact]
    public async Task EntryAndMyWorkRequireAFullSession()
    {
        using var services = Services();
        var notice = new AccessNotice(services.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        var anonymous = new StubSessionState(hasSession: false);
        var context = Context(services, "", "GET");
        var entry = new Sgol.Web.Pages.EntryModel(anonymous, notice)
        { PageContext = new PageContext { HttpContext = context } };
        Assert.Equal("/acceso", Assert.IsType<RedirectResult>(await entry.OnGetAsync(CancellationToken.None)).Url);

        var bridge = new RazorAntiforgeryBridge(services.GetRequiredService<IAntiforgery>(),
            new StubApi(204, false, false), anonymous);
        var protectedPage = new IndexModel(anonymous, bridge, notice)
        { PageContext = new PageContext { HttpContext = context } };
        Assert.Equal("/acceso", Assert.IsType<RedirectResult>(await protectedPage.OnGetAsync(CancellationToken.None)).Url);

        var valid = new StubSessionState();
        context = Context(services, "__Host-SGOL-Session=synthetic", "GET");
        entry = new Sgol.Web.Pages.EntryModel(valid, notice)
        { PageContext = new PageContext { HttpContext = context } };
        Assert.Equal("/mi-trabajo", Assert.IsType<RedirectResult>(await entry.OnGetAsync(CancellationToken.None)).Url);
        protectedPage = new IndexModel(valid, bridge, notice)
        { PageContext = new PageContext { HttpContext = context } };
        Assert.IsType<PageResult>(await protectedPage.OnGetAsync(CancellationToken.None));

        valid.Invalidate();
        protectedPage = new IndexModel(valid, bridge, notice)
        { PageContext = new PageContext { HttpContext = context } };
        var redirect = Assert.IsType<RedirectResult>(await protectedPage.OnGetAsync(CancellationToken.None));
        Assert.Equal(AccessNoticeKind.Ended,
            notice.Read(Uri.UnescapeDataString(redirect.Url!.Split("notice=", 2)[1])));
    }

    [Theory]
    [InlineData("DIRECCION", "Dirección")]
    [InlineData("ADMINISTRACION", "Administración")]
    [InlineData("SUBCOORDINACION", "Subcoordinación")]
    [InlineData("PISO_VENTAS", "Piso de ventas")]
    public void RoleNamesAreClosed(string code, string expected) =>
        Assert.Equal(expected, SessionPresentation.RoleName(code));

    [Fact]
    public void UnknownRoleAndExpirationRulesFailClosed()
    {
        Assert.Null(SessionPresentation.RoleName("UNKNOWN"));
        var now = new DateTimeOffset(2026, 9, 24, 14, 0, 0, TimeSpan.Zero);
        var session = Snapshot() with
        {
            IdleExpiresAt = now.AddHours(2),
            AbsoluteExpiresAt = now.AddHours(10),
        };
        Assert.Equal(now.AddHours(2), SessionPresentation.EffectiveExpiration(session));
        Assert.Equal("10:00", SessionPresentation.ExpirationText(session, now));
        session = session with { IdleExpiresAt = now.AddDays(2), AbsoluteExpiresAt = now.AddDays(1) };
        Assert.Equal(now.AddDays(1), SessionPresentation.EffectiveExpiration(session));
        Assert.Matches(@"^25 sept? 2026, 08:00$", SessionPresentation.ExpirationText(session, now));
    }

    [Fact]
    public void AccessNoticeAcceptsOnlyProtectedShortLivedClosedValues()
    {
        using var services = Services();
        var notice = new AccessNotice(services.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        foreach (var kind in Enum.GetValues<AccessNoticeKind>())
            Assert.Equal(kind, notice.Read(notice.Protect(kind)));
        Assert.Null(notice.Read("Closed"));
        Assert.Null(notice.Read(notice.Protect(AccessNoticeKind.Closed) + "altered"));
        Assert.Equal("Sesión cerrada.", AccessNotice.Message(AccessNoticeKind.Closed));
        Assert.Equal("Tu sesión terminó. Inicia sesión nuevamente.", AccessNotice.Message(AccessNoticeKind.Ended));
        Assert.Equal("La sesión se cerró en este dispositivo, pero no se pudo confirmar el cierre en el servidor.",
            AccessNotice.Message(AccessNoticeKind.Unconfirmed));
    }

    [Theory]
    [InlineData(204, false, false, AccessNoticeKind.Closed)]
    [InlineData(503, false, false, AccessNoticeKind.Unconfirmed)]
    [InlineData(0, true, false, AccessNoticeKind.Unconfirmed)]
    [InlineData(400, false, true, null)]
    public async Task LogoutUsesCsrfAndContainsRemoteFailure(
        int status, bool throws, bool csrfError, AccessNoticeKind? expectedNotice)
    {
        using var services = Services();
        var notice = new AccessNotice(services.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        var state = new StubSessionState();
        var api = new StubApi(status, throws, csrfError);
        var bridge = new RazorAntiforgeryBridge(services.GetRequiredService<IAntiforgery>(), api, state);
        var get = Context(services, "", "GET");
        var token = bridge.Issue(get);
        var csrfCookie = get.Response.Headers.SetCookie.ToString().Split(';')[0];
        var post = FormContext(services, "__Host-SGOL-Session=synthetic; " + csrfCookie, token);
        var model = new IndexModel(state, bridge, notice) { PageContext = new PageContext { HttpContext = post } };

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.Equal(1, api.Calls);
        if (expectedNotice is { } kind)
        {
            var redirect = Assert.IsType<RedirectResult>(result);
            var protectedValue = Uri.UnescapeDataString(redirect.Url!.Split("notice=", 2)[1]);
            Assert.Equal(kind, notice.Read(protectedValue));
            Assert.True(state.IsInvalid);
            var deletions = post.Response.Headers.SetCookie.ToString();
            Assert.Contains("__Host-SGOL-Session=", deletions, StringComparison.Ordinal);
            Assert.Contains("__Host-SGOL-PreAuth=", deletions, StringComparison.Ordinal);
            Assert.Contains("__Host-SGOL-CSRF=", deletions, StringComparison.Ordinal);
            Assert.DoesNotContain("unrelated=", deletions, StringComparison.Ordinal);
        }
        else
        {
            Assert.IsType<PageResult>(result);
            Assert.False(state.IsInvalid);
            Assert.Equal("No se pudo verificar la solicitud", model.ErrorTitle);
            Assert.Equal("Recarga la página antes de volver a enviarla.", model.ErrorMessage);
        }
    }

    [Fact]
    public async Task MissingCsrfNeverCallsLogoutOrClearsValidSession()
    {
        using var services = Services();
        var state = new StubSessionState();
        var api = new StubApi(204, false, false);
        var bridge = new RazorAntiforgeryBridge(services.GetRequiredService<IAntiforgery>(), api, state);
        var get = Context(services, "", "GET");
        bridge.Issue(get);
        var csrfCookie = get.Response.Headers.SetCookie.ToString().Split(';')[0];
        var post = FormContext(services, "__Host-SGOL-Session=synthetic; " + csrfCookie, "");
        var notice = new AccessNotice(services.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        var model = new IndexModel(state, bridge, notice) { PageContext = new PageContext { HttpContext = post } };

        Assert.IsType<PageResult>(await model.OnPostAsync(CancellationToken.None));
        Assert.Equal(0, api.Calls);
        Assert.False(state.IsInvalid);
        Assert.Equal("No se pudo verificar la solicitud", model.ErrorTitle);
    }

    private static ServiceProvider Services()
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

    private static DefaultHttpContext Context(IServiceProvider provider, string cookie, string method)
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Scheme = "https";
        context.Request.Method = method;
        context.Request.Headers.Cookie = cookie;
        return context;
    }

    private static DefaultHttpContext FormContext(IServiceProvider provider, string cookie, string token)
    {
        var context = Context(provider, cookie, "POST");
        context.Request.ContentType = "application/x-www-form-urlencoded";
        var body = Encoding.UTF8.GetBytes("__RequestVerificationToken=" + Uri.EscapeDataString(token));
        context.Request.ContentLength = body.Length;
        context.Request.Body = new MemoryStream(body);
        return context;
    }

    private static SessionSnapshot Snapshot() => new(Guid.NewGuid(), Guid.NewGuid(),
        "synthetic", "Persona sintética", "LOR-001", "DIRECCION", [],
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow.AddHours(8));

    private sealed class StubSessionState(bool hasSession = true) : IRazorSessionState
    {
        public bool IsInvalid { get; private set; }
        public void Invalidate() => IsInvalid = true;
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<SessionSnapshot?>(IsInvalid || !hasSession ? null : Snapshot());
    }

    private sealed class StubApi(int status, bool throws, bool csrfError) : ISgolApiClient
    {
        public int Calls { get; private set; }
        public Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal("/api/v1/auth/logout", request.Path);
            Calls++;
            if (throws) throw new HttpRequestException("synthetic outage");
            var code = csrfError ? "CSRF_INVALID" : status < 400 ? null : "SYNTHETIC_FAILURE";
            var error = status < 400 ? null : new Sgol.Web.Presentation.ProblemDetails.ProblemDetailsPresentation(
                "Synthetic error", "Synthetic detail", null);
            return Task.FromResult(new ApiResponse<T>(status, default, null, Guid.NewGuid().ToString("D"),
                null, null, null, false, code, error));
        }
    }
}
