using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class AccessBrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task AccessErrors_CsrfLockoutRateLimit_AndKeyboardRemainSafe()
    {
        var fixture = new BrowserFixture();
        try
        {
            await fixture.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            await using var context = await NewContextAsync(browser);
            var page = await context.NewPageAsync();
            var account = fixture.Accounts[1];
            await page.GotoAsync(new Uri(fixture.BaseAddress, "/acceso").AbsoluteUri);
            await page.Keyboard.PressAsync("Tab");
            Assert.True(await page.Locator(".salto-contenido").EvaluateAsync<bool>(
                "element => document.activeElement === element"));
            await page.Keyboard.PressAsync("Tab");
            Assert.True(await page.Locator("#userName").EvaluateAsync<bool>(
                "element => document.activeElement === element && getComputedStyle(element).outlineStyle !== 'none'"));
            Assert.True(await page.EvaluateAsync<bool>("""
                () => document.querySelectorAll('main').length === 1 &&
                    document.querySelectorAll('h1').length === 1 &&
                    [...document.querySelectorAll('input:not([type=hidden])')].every(input =>
                        input.labels && input.labels.length > 0) &&
                    new Set([...document.querySelectorAll('[id]')].map(item => item.id)).size ===
                        document.querySelectorAll('[id]').length
                """));

            await page.Locator("#userName").FillAsync(account.UserName);
            await page.Locator("#password").FillAsync(account.TemporaryPassword);
            await page.Locator("input[name='__RequestVerificationToken']").EvaluateAsync(
                "element => element.remove()");
            var rejected = page.WaitForResponseAsync(response => response.Request.Method == "POST" &&
                response.Url.EndsWith("/acceso", StringComparison.Ordinal));
            await page.GetByRole(AriaRole.Button, new() { Name = "Iniciar sesión" }).ClickAsync();
            Assert.Equal(400, (await rejected).Status);
            await AssertNoFullSessionAsync(context);
            Assert.DoesNotContain(await context.CookiesAsync(), item => item.Name == "__Host-SGOL-PreAuth");

            await LoginAsync(page, fixture, "usuario.inexistente", account.TemporaryPassword);
            var unknown = await page.Locator("#acceso-error").InnerTextAsync();
            await LoginAsync(page, fixture, account.UserName, "incorrecta");
            var wrong = await page.Locator("#acceso-error").InnerTextAsync();
            Assert.Equal(unknown, wrong);
            Assert.Contains("No se pudo iniciar sesión", wrong);
            Assert.True(await page.Locator("#acceso-error").EvaluateAsync<bool>(
                "element => document.activeElement === element"));
            for (var attempt = 0; attempt < 4; attempt++)
                await LoginAsync(page, fixture, account.UserName, "incorrecta");
            await LoginAsync(page, fixture, account.UserName, account.TemporaryPassword);
            Assert.Contains("El acceso está bloqueado temporalmente", await page.Locator("#acceso-error").InnerTextAsync());
            await AssertNoFullSessionAsync(context);

            var rateLimited = false;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                await LoginAsync(page, fixture, "usuario.inexistente", account.TemporaryPassword);
                if ((await page.Locator("#acceso-error").InnerTextAsync()).Contains(
                    "Demasiadas solicitudes", StringComparison.Ordinal))
                {
                    rateLimited = true;
                    break;
                }
            }
            Assert.True(rateLimited);
            await AssertNoFullSessionAsync(context);
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupComplete);
        }
    }

    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task FirstAccess_RecurrentTotp_AndRecoveryReachFullSessionOnlyAfterMfa()
    {
        var fixture = new BrowserFixture();
        try
        {
            await fixture.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var account = fixture.Accounts[0];

            await using var first = await NewContextAsync(browser);
            var page = await first.NewPageAsync();
            await LoginAsync(page, fixture, account.UserName, account.TemporaryPassword);
            Assert.True(page.Url.Contains("/acceso/cambiar-contrasena", StringComparison.Ordinal),
                $"Unexpected access route; safe alert: {(await page.Locator("#acceso-error").AllInnerTextsAsync()).SingleOrDefault() ?? "none"}");
            await AssertNoFullSessionAsync(first);
            Assert.Equal("current-password", await page.Locator("#currentPassword").GetAttributeAsync("autocomplete"));
            Assert.Equal("new-password", await page.Locator("#newPassword").GetAttributeAsync("autocomplete"));
            await page.Locator("#currentPassword").FillAsync(account.TemporaryPassword);
            await page.Locator("#newPassword").FillAsync(account.NewPassword);
            await page.GetByRole(AriaRole.Button, new() { Name = "Cambiar contraseña" }).ClickAsync();
            await page.WaitForURLAsync("**/acceso/mfa/enrolar?flow=*");
            await AssertNoFullSessionAsync(first);
            await page.GetByRole(AriaRole.Button, new() { Name = "Mostrar clave de configuración" }).ClickAsync();
            var key = await page.Locator(".acceso__codigos code").InnerTextAsync();
            Assert.False(string.IsNullOrWhiteSpace(key));
            await page.Locator("#totpCode").FillAsync(BrowserFixture.Totp(key, DateTimeOffset.UtcNow));
            await page.GetByRole(AriaRole.Button, new() { Name = "Confirmar MFA" }).ClickAsync();
            Assert.Equal(10, await page.Locator(".acceso__codigos li").CountAsync());
            Assert.Contains("/acceso/codigos-recuperacion", page.Url, StringComparison.Ordinal);
            await AssertFullSessionAsync(first, fixture);
            var recovery = await page.Locator(".acceso__codigos li code").First.InnerTextAsync();
            var direct = await first.NewPageAsync();
            await direct.GotoAsync(new Uri(fixture.BaseAddress, "/acceso/codigos-recuperacion").AbsoluteUri);
            Assert.EndsWith("/acceso", direct.Url, StringComparison.Ordinal);
            Assert.Equal(0, await direct.Locator(".acceso__codigos li").CountAsync());

            await using var recurrent = await NewContextAsync(browser);
            page = await recurrent.NewPageAsync();
            await LoginAsync(page, fixture, account.UserName, account.NewPassword);
            await page.WaitForURLAsync("**/acceso/mfa/verificar?flow=*");
            await AssertNoFullSessionAsync(recurrent);
            Assert.Equal("one-time-code", await page.Locator("#totpCode").GetAttributeAsync("autocomplete"));
            var valid = BrowserFixture.Totp(key, DateTimeOffset.UtcNow.AddSeconds(30));
            await page.Locator("#totpCode").FillAsync(valid == "000000" ? "999999" : "000000");
            await page.GetByRole(AriaRole.Button, new() { Name = "Verificar código" }).ClickAsync();
            Assert.Contains("No se pudo verificar el código", await page.Locator("#acceso-error").InnerTextAsync());
            await AssertNoFullSessionAsync(recurrent);
            await page.Locator("#totpCode").FillAsync(valid);
            await page.GetByRole(AriaRole.Button, new() { Name = "Verificar código" }).ClickAsync();
            Assert.Equal(1, await page.GetByText("Sesión iniciada.").CountAsync());
            await AssertFullSessionAsync(recurrent, fixture);

            await using var recoveryContext = await NewContextAsync(browser);
            page = await recoveryContext.NewPageAsync();
            await LoginAsync(page, fixture, account.UserName, account.NewPassword);
            await page.WaitForURLAsync("**/acceso/mfa/verificar?flow=*");
            await page.Locator("#recoveryCode").FillAsync("CODIGO-INVALIDO");
            await page.GetByRole(AriaRole.Button, new() { Name = "Usar código de recuperación" }).ClickAsync();
            Assert.Contains("No se pudo verificar el código", await page.Locator("#acceso-error").InnerTextAsync());
            await AssertNoFullSessionAsync(recoveryContext);
            await page.Locator("#recoveryCode").FillAsync(recovery);
            await page.GetByRole(AriaRole.Button, new() { Name = "Usar código de recuperación" }).ClickAsync();
            await page.WaitForURLAsync("**/acceso/codigos-recuperacion?flow=*");
            await AssertNoFullSessionAsync(recoveryContext);
            await page.Locator("#currentPassword").FillAsync(account.NewPassword);
            await page.GetByRole(AriaRole.Button, new() { Name = "Generar códigos nuevos" }).ClickAsync();
            Assert.Equal(10, await page.Locator(".acceso__codigos li").CountAsync());
            await AssertFullSessionAsync(recoveryContext, fixture);

            await using var expiredContext = await NewContextAsync(browser);
            page = await expiredContext.NewPageAsync();
            var anotherAccount = fixture.Accounts[2];
            await LoginAsync(page, fixture, anotherAccount.UserName, anotherAccount.TemporaryPassword);
            await page.WaitForURLAsync("**/acceso/cambiar-contrasena?flow=*");
            await expiredContext.AddCookiesAsync([new Microsoft.Playwright.Cookie
            {
                Name = "__Host-SGOL-PreAuth", Value = "invalid-fixture-ticket",
                Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true,
                SameSite = SameSiteAttribute.Strict,
            }]);
            await page.Locator("#currentPassword").FillAsync(anotherAccount.TemporaryPassword);
            await page.Locator("#newPassword").FillAsync(anotherAccount.NewPassword);
            await page.GetByRole(AriaRole.Button, new() { Name = "Cambiar contraseña" }).ClickAsync();
            Assert.Contains("El paso de acceso venció", await page.Locator("#acceso-error").InnerTextAsync());
            await AssertNoFullSessionAsync(expiredContext);

            await using var mobileBrowser = await playwright.Webkit.LaunchAsync(new() { Headless = true });
            await using var mobile = await mobileBrowser.NewContextAsync(new()
            {
                ViewportSize = new() { Width = 390, Height = 844 },
                IsMobile = true,
                HasTouch = true,
                IgnoreHTTPSErrors = true,
                Locale = "es-MX",
                ServiceWorkers = ServiceWorkerPolicy.Block,
            });
            var mobilePage = await mobile.NewPageAsync();
            await mobilePage.GotoAsync(new Uri(fixture.BaseAddress, "/acceso").AbsoluteUri);
            Assert.False(await mobilePage.EvaluateAsync<bool>(
                "() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
            Assert.Equal(1, await mobilePage.GetByRole(AriaRole.Heading, new() { Level = 1 }).CountAsync());
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupComplete);
        }
    }

    private static Task<IBrowserContext> NewContextAsync(IBrowser browser) => browser.NewContextAsync(new()
    {
        IgnoreHTTPSErrors = true,
        Locale = "es-MX",
        ServiceWorkers = ServiceWorkerPolicy.Block,
    });

    private static async Task LoginAsync(IPage page, BrowserFixture fixture, string userName, string password)
    {
        await page.GotoAsync(new Uri(fixture.BaseAddress, "/acceso").AbsoluteUri);
        Assert.Equal("username", await page.Locator("#userName").GetAttributeAsync("autocomplete"));
        await page.Locator("#userName").FillAsync(userName);
        await page.Locator("#password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Iniciar sesión" }).ClickAsync();
    }

    private static async Task AssertNoFullSessionAsync(IBrowserContext context) =>
        Assert.DoesNotContain(await context.CookiesAsync(), item => item.Name == "__Host-SGOL-Session");

    private static async Task AssertFullSessionAsync(IBrowserContext context, BrowserFixture fixture)
    {
        Assert.Contains(await context.CookiesAsync(), item => item.Name == "__Host-SGOL-Session");
        var response = await context.APIRequest.GetAsync(new Uri(fixture.BaseAddress, "/api/v1/auth/session").AbsoluteUri);
        Assert.Equal(200, response.Status);
    }
}
