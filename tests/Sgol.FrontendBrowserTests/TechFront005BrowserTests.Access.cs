using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed partial class TechFront005BrowserTests
{
    private async Task AccessAsync()
    {
        foreach (var account in fixture.Accounts)
        {
            var context = await ContextAsync(); contexts.Add(context);
            var page = await context.NewPageAsync(); pages.Add(page);
            await FirstAccess(page, account);
            Assert.Equal(200, (await context.APIRequest.GetAsync("/api/v1/auth/session")).Status);
            var navigation = page.Locator(".navegacion-lateral--escritorio");
            var expected = account.Role == "DIRECCION" ? 8 : account.Role == "PISO_VENTAS" ? 5 : 6;
            Assert.Equal(expected, await navigation.Locator("a").CountAsync());
            Assert.Equal(1, await navigation.Locator("a[href='/indicadores']").CountAsync());
            Assert.Equal(1, await navigation.Locator("a[href='/auditoria']").CountAsync());
            if (mobile)
            {
                var open = page.GetByRole(AriaRole.Button, new() { Name = "Navegación principal" });
                await open.ClickAsync(); await Assertions.Expect(page.Locator("#navegacion-movil")).ToBeVisibleAsync();
                await page.Keyboard.PressAsync("Escape"); await Assertions.Expect(page.Locator("#navegacion-movil")).ToBeHiddenAsync();
                await Assertions.Expect(open).ToBeFocusedAsync();
            }
            await Capture(page, "R1-" + account.Role);
            if (account.Role != "DIRECCION")
                Assert.Equal(403, (await page.GotoAsync("/personas-y-accesos"))!.Status);
            await page.GotoAsync("/mi-trabajo");
        }
    }

    private async Task FirstAccess(IPage page, BrowserAccount account)
    {
        await page.GotoAsync("/acceso");
        await page.Locator("#userName").FillAsync(account.UserName);
        await page.Locator("#password").FillAsync(account.TemporaryPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Iniciar sesión" }).ClickAsync();
        await page.WaitForURLAsync("**/acceso/cambiar-contrasena?flow=*");
        Assert.DoesNotContain(await page.Context.CookiesAsync(), c => c.Name == "__Host-SGOL-Session");
        await page.Locator("#currentPassword").FillAsync(account.TemporaryPassword);
        await page.Locator("#newPassword").FillAsync(account.NewPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Cambiar contraseña" }).ClickAsync();
        await page.WaitForURLAsync("**/acceso/mfa/enrolar?flow=*");
        await page.GetByRole(AriaRole.Button, new() { Name = "Mostrar clave de configuración" }).ClickAsync();
        var secret = await page.Locator(".acceso__codigos code").InnerTextAsync();
        secrets.Add(account.UserId, secret);
        await page.Locator("#totpCode").FillAsync(BrowserFixture.Totp(secret, DateTimeOffset.UtcNow));
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirmar MFA" }).ClickAsync();
        await Assertions.Expect(page.Locator(".acceso__codigos li")).ToHaveCountAsync(10);
        // Never capture the one-time credential screen, including synthetic credentials.
        await page.GotoAsync("/mi-trabajo");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Mis tareas");
    }
}
