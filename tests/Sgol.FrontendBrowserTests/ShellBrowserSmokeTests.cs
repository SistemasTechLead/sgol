using System.Net;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class ShellBrowserSmokeTests
{
    private const string ShellPath = "/mi-trabajo";
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };
    private static readonly (string Name, int Width, int Height, bool Mobile)[] Viewports =
    [
        ("chromium-desktop", 1440, 900, false),
        ("webkit-mobile", 390, 844, true),
    ];

    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task FourRoleShellAndUnauthorizedState_AreAccessibleOnDesktopAndMobile()
    {
        var fixture = new BrowserFixture();
        var results = new List<object>();
        var browserVersions = new Dictionary<string, string>(StringComparer.Ordinal);
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-002-evidence");
        Directory.CreateDirectory(output);
        File.Delete(Path.Combine(output, "report.json"));
        try
        {
            await fixture.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            var sessions = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var account in fixture.Accounts)
                sessions.Add(account.Role, await fixture.AuthenticateAsync(account));
            foreach (var viewport in Viewports)
            {
                var type = viewport.Mobile ? playwright.Webkit : playwright.Chromium;
                await using var browser = await type.LaunchAsync(new() { Headless = true });
                browserVersions.Add(viewport.Name, browser.Version);
                await AssertAnonymousAsync(browser, fixture, viewport);
                foreach (var account in fixture.Accounts)
                {
                    // Keep the shell regression focused on each role's full session.
                    await using var context = await NewContextAsync(browser, viewport);
                    await context.AddCookiesAsync([new Microsoft.Playwright.Cookie
                    {
                        Name = "__Host-SGOL-Session", Value = sessions[account.Role],
                        Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true,
                        SameSite = SameSiteAttribute.Strict,
                    }]);
                    Assert.Contains(await context.CookiesAsync(), item => item.Name == "__Host-SGOL-Session");
                    var browserSession = await context.APIRequest.GetAsync(
                        new Uri(fixture.BaseAddress, "/api/v1/auth/session").AbsoluteUri);
                    Assert.Equal(200, browserSession.Status);
                    var page = await context.NewPageAsync();
                    var response = await page.GotoAsync(new Uri(fixture.BaseAddress, ShellPath).AbsoluteUri);
                    Assert.Equal(200, response?.Status);
                    Assert.Equal("Mi trabajo — SGOL", await page.TitleAsync());
                    Assert.Equal(1, await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).CountAsync());
                    Assert.Equal(1, await page.GetByRole(AriaRole.Main).CountAsync());
                    Assert.Equal(1, await page.GetByRole(AriaRole.Banner).CountAsync());
                    Assert.Equal(account.Role == "DIRECCION" ? 1 : 0,
                        await page.Locator("nav[aria-label='Navegación principal']").CountAsync());
                    Assert.Equal(0, await page.GetByRole(AriaRole.Link, new() { Name = "Mi trabajo" }).CountAsync());
                    Assert.Equal("Aún no hay secciones disponibles", await page.Locator(".estado-vacio__titulo").InnerTextAsync());
                    Assert.Equal(1, await page.Locator(".encabezado-aplicacion__sesion").CountAsync());
                    Assert.Equal($"Persona sintética {account.Role}", await page.Locator(".encabezado-aplicacion__sesion").InnerTextAsync());
                    Assert.Equal(account.Role switch
                    {
                        "DIRECCION" => "Dirección",
                        "ADMINISTRACION" => "Administración",
                        "SUBCOORDINACION" => "Subcoordinación",
                        _ => "Piso de ventas",
                    }, await page.Locator(".encabezado-aplicacion__rol").InnerTextAsync());
                    Assert.Equal(1, await page.GetByRole(AriaRole.Button, new() { Name = "Cerrar sesión" }).CountAsync());
                    Assert.Contains("hora de Ciudad de México", await page.Locator(".encabezado-aplicacion__expiracion").InnerTextAsync());
                    Assert.False(string.IsNullOrWhiteSpace(await page.Locator(".encabezado-aplicacion__expiracion time").GetAttributeAsync("datetime")));
                    Assert.Equal("grid", await page.Locator(".aplicacion").EvaluateAsync<string>(
                        "el => getComputedStyle(el).display"));
                    Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                    Assert.False(await page.EvaluateAsync<bool>(
                        "() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
                    var headerBox = await page.Locator(".encabezado-aplicacion").BoundingBoxAsync();
                    var mainBox = await page.Locator("main").BoundingBoxAsync();
                    Assert.NotNull(headerBox);
                    Assert.NotNull(mainBox);
                    var navigationHeight = viewport.Mobile && account.Role == "DIRECCION"
                        ? (await page.Locator(".barra-lateral").BoundingBoxAsync())?.Height ?? 0 : 0;
                    Assert.InRange(Math.Abs(mainBox.Y - headerBox.Y - headerBox.Height - navigationHeight), 0, 2);
                    if (viewport.Mobile)
                        await page.Locator(".salto-contenido").FocusAsync();
                    else
                        await page.Keyboard.PressAsync("Tab");
                    Assert.True(await page.Locator(".salto-contenido").EvaluateAsync<bool>(
                        "el => document.activeElement === el && getComputedStyle(el).transform === 'none'"));
                    await page.Keyboard.PressAsync("Enter");
                    Assert.True(await page.Locator("main").EvaluateAsync<bool>("el => document.activeElement === el"));
                    await page.EvaluateAsync("() => document.activeElement?.blur()");

                    // Mask the complete session identity before writing an image.
                    var imageName = $"{viewport.Name}-{account.Role}.png";
                    await page.ScreenshotAsync(new()
                    {
                        Path = Path.Combine(output, imageName),
                        FullPage = true,
                        Mask = [page.Locator(".encabezado-aplicacion")],
                    });
                    results.Add(new
                    {
                        role = account.Role,
                        browser = viewport.Name,
                        viewport =
                        $"{viewport.Width}x{viewport.Height}",
                        shell = "PASSED",
                        accessibility = "PASSED",
                        screenshot = imageName
                    });
                    await context.ClearCookiesAsync();
                    Assert.Empty(await context.CookiesAsync());
                }
            }
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupComplete);
        }

        // Explicit allowlist: no request, cookie, CSRF, credential, TOTP, recovery code,
        // trace, video, console log, or response body is serialized.
        var report = JsonSerializer.Serialize(new
        {
            task = "FRONT-002",
            status = "PASSED",
            browsers = browserVersions,
            cases = results,
            cleanup = "PASSED",
            evidence = "masked screenshots; no traces or logs",
        }, ReportOptions);
        Assert.DoesNotContain("__Host-SGOL", report, StringComparison.Ordinal);
        foreach (var account in fixture.Accounts)
        {
            Assert.DoesNotContain(account.UserName, report, StringComparison.Ordinal);
            Assert.DoesNotContain(account.TemporaryPassword, report, StringComparison.Ordinal);
            Assert.DoesNotContain(account.NewPassword, report, StringComparison.Ordinal);
        }
        var evidenceNames = Directory.EnumerateFiles(output).Select(Path.GetFileName).ToArray();
        Assert.Equal(8, evidenceNames.Length);
        Assert.All(evidenceNames, name => Assert.True(name is not null &&
            name.EndsWith(".png", StringComparison.Ordinal)));
        await File.WriteAllTextAsync(Path.Combine(output, "report.json"), report);
    }

    private static async Task AssertAnonymousAsync(IBrowser browser, BrowserFixture fixture,
        (string Name, int Width, int Height, bool Mobile) viewport)
    {
        await using var context = await NewContextAsync(browser, viewport);
        var page = await context.NewPageAsync();
        var api = await context.APIRequest.GetAsync(new Uri(fixture.BaseAddress, "/api/v1/auth/session").AbsoluteUri);
        Assert.Equal((int)HttpStatusCode.Unauthorized, api.Status);
        Assert.Equal("application/problem+json", api.Headers["content-type"].Split(';')[0]);
        var shell = await page.GotoAsync(new Uri(fixture.BaseAddress, ShellPath).AbsoluteUri);
        Assert.Equal(200, shell?.Status);
        Assert.EndsWith("/acceso", page.Url, StringComparison.Ordinal);
        Assert.Equal(0, await page.Locator(".encabezado-aplicacion__sesion").CountAsync());
        Assert.Equal(0, await page.Locator(".estado-vacio__titulo").CountAsync());
        Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
        await context.AddCookiesAsync([new Microsoft.Playwright.Cookie
        {
            Name = "__Host-SGOL-Session", Value = "invalid-fixture-ticket",
            Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true,
            SameSite = SameSiteAttribute.Strict,
        }]);
        var invalid = await context.APIRequest.GetAsync(
            new Uri(fixture.BaseAddress, "/api/v1/auth/session").AbsoluteUri);
        Assert.Equal((int)HttpStatusCode.Unauthorized, invalid.Status);
        await page.GotoAsync(new Uri(fixture.BaseAddress, ShellPath).AbsoluteUri);
        Assert.Contains("/acceso?notice=", page.Url, StringComparison.Ordinal);
        Assert.Equal("Tu sesión terminó. Inicia sesión nuevamente.", await page.Locator("#access-notice").InnerTextAsync());
        Assert.Equal(0, await page.Locator(".encabezado-aplicacion__sesion").CountAsync());
        Assert.DoesNotContain(await context.CookiesAsync(), item => item.Name == "__Host-SGOL-Session");
        await context.ClearCookiesAsync();
        Assert.Empty(await context.CookiesAsync());
    }

    private static Task<IBrowserContext> NewContextAsync(IBrowser browser,
        (string Name, int Width, int Height, bool Mobile) viewport) => browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = viewport.Width, Height = viewport.Height },
            Locale = "es-MX",
            IsMobile = viewport.Mobile,
            HasTouch = viewport.Mobile,
            IgnoreHTTPSErrors = true,
            ServiceWorkers = ServiceWorkerPolicy.Block,
        });

    // Focused equivalent to a critical axe smoke for the current shell. The
    // future functional pages must add their own full accessibility coverage.
    private const string AccessibilityCheck = """
        () => {
          const text = node => (node.getAttribute('aria-label') || node.innerText || '').trim();
          if (document.documentElement.lang !== 'es-MX' || !document.title.trim()) return false;
          if (document.querySelectorAll('main').length !== 1 ||
              document.querySelectorAll('h1').length !== 1 ||
              !document.querySelector('.encabezado-aplicacion')) return false;
          if ([...document.querySelectorAll('nav')].some(node => !text(node))) return false;
          if ([...document.querySelectorAll('a, button')].some(node => !text(node))) return false;
          if ([...document.querySelectorAll('img')].some(node => !node.hasAttribute('alt'))) return false;
          const ids = [...document.querySelectorAll('[id]')].map(node => node.id);
          if (new Set(ids).size !== ids.length) return false;
          if ([...document.querySelectorAll('[aria-labelledby], [aria-describedby]')].some(node =>
              ['aria-labelledby', 'aria-describedby'].some(attribute =>
                (node.getAttribute(attribute) || '').split(/\s+/).filter(Boolean)
                  .some(id => !document.getElementById(id))))) return false;
          return true;
        }
        """;
}
