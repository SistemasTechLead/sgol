using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front002BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task ProtectedEntryLogoutCsrfAndInvalidationRemainSafe()
    {
        var fixture = new BrowserFixture();
        try
        {
            await fixture.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            var accounts = fixture.Accounts;
            var tickets = new[]
            {
                await fixture.AuthenticateAsync(accounts[0]),
                await fixture.AuthenticateAsync(accounts[1]),
                await fixture.AuthenticateAsync(accounts[2]),
            };

            foreach (var viewport in new[] { (Mobile: false, Account: 0), (Mobile: true, Account: 1) })
            {
                var browserType = viewport.Mobile ? playwright.Webkit : playwright.Chromium;
                await using var browser = await browserType.LaunchAsync(new() { Headless = true });
                await using var context = await NewContextAsync(browser, viewport.Mobile);
                await SetSessionAsync(context, fixture, tickets[viewport.Account]);
                var page = await context.NewPageAsync();
                await page.GotoAsync(new Uri(fixture.BaseAddress, "/branches/LOR-001").AbsoluteUri);
                Assert.Equal(0, await page.Locator(".encabezado-aplicacion__sesion").CountAsync());
                await page.GotoAsync(new Uri(fixture.BaseAddress, "/").AbsoluteUri);
                Assert.EndsWith("/mi-trabajo", page.Url, StringComparison.Ordinal);
                Assert.Equal("Mi trabajo", await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).InnerTextAsync());
                Assert.Equal(0, await page.Locator("nav a").CountAsync());

                await page.Locator(".salto-contenido").FocusAsync();
                Assert.True(await page.Locator(".salto-contenido").EvaluateAsync<bool>(
                    "element => document.activeElement === element"));
                await page.Keyboard.PressAsync("Tab");
                var logout = page.GetByRole(AriaRole.Button, new() { Name = "Cerrar sesión" });
                Assert.True(await logout.EvaluateAsync<bool>(
                    "element => document.activeElement === element && getComputedStyle(element).outlineStyle !== 'none'"));

                await page.Locator("input[name='__RequestVerificationToken']").EvaluateAsync("element => element.remove()");
                await logout.ClickAsync();
                Assert.Equal("/mi-trabajo", new Uri(page.Url).AbsolutePath);
                Assert.Contains("No se pudo verificar la solicitud", await page.Locator("#session-error").InnerTextAsync());
                Assert.True(await page.Locator("#session-error").EvaluateAsync<bool>(
                    "element => document.activeElement === element"));
                Assert.Equal(200, (await context.APIRequest.GetAsync(
                    new Uri(fixture.BaseAddress, "/api/v1/auth/session").AbsoluteUri)).Status);

                var redirect = page.WaitForURLAsync("**/acceso?notice=*");
                await page.GetByRole(AriaRole.Button, new() { Name = "Cerrar sesión" }).ClickAsync();
                await redirect;
                Assert.Equal("/acceso", new Uri(page.Url).AbsolutePath);
                Assert.Equal("Sesión cerrada.", await page.Locator("#access-notice").InnerTextAsync());
                Assert.True(await page.Locator("#access-notice").EvaluateAsync<bool>(
                    "element => document.activeElement === element"));
                Assert.DoesNotContain(await context.CookiesAsync(), item => item.Name is
                    "__Host-SGOL-Session" or "__Host-SGOL-PreAuth");
                Assert.Equal(401, (await context.APIRequest.GetAsync(
                    new Uri(fixture.BaseAddress, "/api/v1/auth/session").AbsoluteUri)).Status);
                await context.ClearCookiesAsync();
                Assert.Empty(await context.CookiesAsync());
            }

            await using (var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true }))
            await using (var context = await NewContextAsync(browser, mobile: false))
            {
                await SetSessionAsync(context, fixture, tickets[2]);
                await fixture.InvalidateAsync(accounts[2]);
                var page = await context.NewPageAsync();
                await page.GotoAsync(new Uri(fixture.BaseAddress, "/mi-trabajo").AbsoluteUri);
                Assert.Equal("/acceso", new Uri(page.Url).AbsolutePath);
                Assert.Equal("Tu sesión terminó. Inicia sesión nuevamente.",
                    await page.Locator("#access-notice").InnerTextAsync());
                Assert.Equal(0, await page.Locator(".encabezado-aplicacion__sesion").CountAsync());
                Assert.DoesNotContain(await context.CookiesAsync(), item => item.Name == "__Host-SGOL-Session");
                await context.ClearCookiesAsync();
                Assert.Empty(await context.CookiesAsync());
            }
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupComplete);
        }
    }

    private static Task<IBrowserContext> NewContextAsync(IBrowser browser, bool mobile) =>
        browser.NewContextAsync(new()
        {
            ViewportSize = mobile ? new() { Width = 390, Height = 844 } : new() { Width = 1440, Height = 900 },
            Locale = "es-MX",
            IsMobile = mobile,
            HasTouch = mobile,
            IgnoreHTTPSErrors = true,
            ServiceWorkers = ServiceWorkerPolicy.Block,
        });

    private static Task SetSessionAsync(IBrowserContext context, BrowserFixture fixture, string ticket) =>
        context.AddCookiesAsync([new Microsoft.Playwright.Cookie
        {
            Name = "__Host-SGOL-Session", Value = ticket,
            Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true,
            SameSite = SameSiteAttribute.Strict,
        }]);
}
