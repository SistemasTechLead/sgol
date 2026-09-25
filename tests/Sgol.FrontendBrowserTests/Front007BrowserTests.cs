using System.Buffers.Binary;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front007BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task DirectionManagesRoleHistoryAndResetWhileOtherRolesAreDenied()
    {
        var fixture = new BrowserFixture();
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-007-evidence");
        Directory.CreateDirectory(output);
        try
        {
            await fixture.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            var tickets = new List<string>();
            foreach (var account in fixture.Accounts) tickets.Add(await fixture.AuthenticateAsync(account));
            foreach (var mobile in new[] { false, true })
            {
                var viewport = mobile ? "mobile" : "desktop";
                var personId = await fixture.SeedUnlinkedPersonAsync($"FRONT007-{viewport}", active: true);
                var userName = $"front007.{viewport}.synthetic";
                await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                    .LaunchAsync(new() { Headless = true });
                await using var context = await browser.NewContextAsync(new()
                {
                    ViewportSize = mobile ? new() { Width = 390, Height = 844 } : new() { Width = 1440, Height = 900 },
                    Locale = "es-MX",
                    IsMobile = mobile,
                    HasTouch = mobile,
                    IgnoreHTTPSErrors = true,
                    ServiceWorkers = ServiceWorkerPolicy.Block,
                });
                await SetSessionAsync(context, fixture, tickets[0]);
                var page = await context.NewPageAsync();
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    "/personas-y-accesos").AbsoluteUri))?.Status);
                Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-initial.png", mobile);

                await page.Locator("#account-person").SelectOptionAsync(personId.ToString("D"));
                await page.Locator("#account-user").FillAsync(userName);
                await SubmitAsync(page, page.Locator("#alta-cuenta").GetByRole(AriaRole.Button,
                    new() { Name = "Crear cuenta" }), "Cuenta creada");
                var rolePanel = RolePanel(page, userName);
                Assert.Contains("Sin rol vigente", await rolePanel.InnerTextAsync());
                Assert.Contains("Aún no hay historial de roles", await rolePanel.InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-no-role.png", mobile);

                await rolePanel.GetByRole(AriaRole.Button, new() { Name = "Asignar rol" }).ClickAsync();
                var dialog = page.Locator("dialog[open]");
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await dialog.GetByLabel("Nuevo rol").SelectOptionAsync("ADMINISTRACION");
                await dialog.GetByLabel("Motivo").FillAsync("Asignación sintética");
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-assign-confirm.png", mobile);
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Asignar rol" }),
                    "Rol asignado");
                rolePanel = RolePanel(page, userName);
                Assert.Contains("Rol vigente: ADMINISTRACION", await rolePanel.InnerTextAsync());
                Assert.Contains("Vigente", await rolePanel.InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-assigned.png", mobile);

                var stalePage = await context.NewPageAsync();
                await stalePage.GotoAsync(new Uri(fixture.BaseAddress, "/personas-y-accesos").AbsoluteUri);

                await rolePanel.GetByRole(AriaRole.Button, new() { Name = "Cambiar rol" }).ClickAsync();
                dialog = page.Locator("dialog[open]");
                await dialog.GetByLabel("Nuevo rol").SelectOptionAsync("SUBCOORDINACION");
                await dialog.GetByLabel("Motivo").FillAsync("Cambio sintético");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Cambiar rol" }),
                    "Rol cambiado");
                rolePanel = RolePanel(page, userName);
                Assert.Contains("Rol vigente: SUBCOORDINACION", await rolePanel.InnerTextAsync());
                Assert.Contains("Sustituido", await rolePanel.InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-changed-history.png", mobile);

                await RolePanel(stalePage, userName).GetByRole(AriaRole.Button,
                    new() { Name = "Cambiar rol" }).ClickAsync();
                var staleDialog = stalePage.Locator("dialog[open]");
                await staleDialog.GetByLabel("Nuevo rol").SelectOptionAsync("PISO_VENTAS");
                await staleDialog.GetByLabel("Motivo").FillAsync("Cambio obsoleto sintético");
                var conflictResponse = stalePage.WaitForResponseAsync(response => response.Request.Method == "POST" &&
                    response.Url.Contains("/personas-y-accesos", StringComparison.OrdinalIgnoreCase));
                await staleDialog.GetByRole(AriaRole.Button, new() { Name = "Cambiar rol" }).ClickAsync();
                Assert.Equal(412, (await conflictResponse).Status);
                await stalePage.Locator("#role-error").WaitForAsync();
                Assert.Contains("Este rol cambió", await stalePage.Locator("#role-error").InnerTextAsync());
                Assert.Contains("SUBCOORDINACION", await RolePanel(stalePage, userName).InnerTextAsync());
                Assert.Equal(0, await RolePanel(stalePage, userName).GetByRole(AriaRole.Button,
                    new() { Name = "Cambiar rol" }).CountAsync());
                await CheckWidthAndCaptureAsync(stalePage, output, $"{viewport}-conflict-412.png", mobile);
                await stalePage.CloseAsync();

                await rolePanel.GetByRole(AriaRole.Button, new() { Name = "Revocar rol" }).ClickAsync();
                dialog = page.Locator("dialog[open]");
                await dialog.GetByLabel("Motivo").FillAsync("Revocación sintética");
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-revoke-confirm.png", mobile);
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Revocar rol" }),
                    "Rol revocado");
                rolePanel = RolePanel(page, userName);
                Assert.Contains("Sin rol vigente", await rolePanel.InnerTextAsync());
                Assert.Contains("Sustituido", await rolePanel.InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-revoked.png", mobile);

                await rolePanel.GetByRole(AriaRole.Button, new() { Name = "Restablecer MFA" }).ClickAsync();
                dialog = page.Locator("dialog[open]");
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Restablecer MFA" }).ClickAsync();
                Assert.Equal("true", await dialog.GetByLabel("Motivo").GetAttributeAsync("aria-invalid"));
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-reset-validation.png", mobile);
                await dialog.GetByLabel("Motivo").FillAsync("Recuperación sintética");
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-reset-confirm.png", mobile);
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Restablecer MFA" }),
                    "MFA restablecido");
                Assert.Contains("MFA restablecido", await page.Locator("#cuentas [role=status]").InnerTextAsync());
                Assert.Equal(1, await page.Locator("[data-sensitive-activation]").CountAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-reset.png", mobile);
                await page.ReloadAsync();
                Assert.Equal(0, await page.Locator("[data-sensitive-activation]").CountAsync());

                for (var index = 1; index < tickets.Count; index++)
                {
                    await using var deniedContext = await browser.NewContextAsync(new()
                    {
                        ViewportSize = mobile ? new() { Width = 390, Height = 844 } : new() { Width = 1440, Height = 900 },
                        Locale = "es-MX",
                        IsMobile = mobile,
                        HasTouch = mobile,
                        IgnoreHTTPSErrors = true,
                        ServiceWorkers = ServiceWorkerPolicy.Block,
                    });
                    await SetSessionAsync(deniedContext, fixture, tickets[index]);
                    var denied = await deniedContext.NewPageAsync();
                    Assert.Equal(403, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        "/personas-y-accesos").AbsoluteUri))?.Status);
                    Assert.Equal(0, await denied.Locator("#roles, #cuentas").CountAsync());
                    if (index == 1)
                        await CheckWidthAndCaptureAsync(denied, output, $"{viewport}-denied.png", mobile);
                    Assert.Equal(403, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        $"/api/v1/users/{fixture.Accounts[0].UserId:D}/role-assignments").AbsoluteUri))?.Status);
                    Assert.DoesNotContain("\"history\"", await denied.Locator("body").InnerTextAsync(),
                        StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupComplete);
        }
    }

    private static ILocator RolePanel(IPage page, string userName) =>
        page.Locator("#roles > section.formulario-persona").Filter(new() { HasTextString = userName });

    private static async Task SubmitAsync(IPage page, ILocator submit, string expectedStatus)
    {
        var responseTask = page.WaitForResponseAsync(response => response.Request.Method == "POST" &&
            response.Url.Contains("/personas-y-accesos", StringComparison.OrdinalIgnoreCase));
        await submit.ClickAsync();
        var response = await responseTask;
        if (response.Status != 200)
        {
            await page.Locator("#role-error").WaitForAsync();
            Assert.Fail($"POST devolvió {response.Status}: {await page.Locator("#role-error").InnerTextAsync()}");
        }
        await page.WaitForFunctionAsync("expected => document.querySelector('#cuentas [role=status]')?.textContent.includes(expected)",
            expectedStatus);
    }

    private static Task SetSessionAsync(IBrowserContext context, BrowserFixture fixture, string ticket) =>
        context.AddCookiesAsync([new Cookie
        {
            Name = "__Host-SGOL-Session", Value = ticket,
            Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true,
            SameSite = SameSiteAttribute.Strict,
        }]);

    private static async Task CheckWidthAndCaptureAsync(IPage page, string output, string name, bool mobile)
    {
        var widths = await page.EvaluateAsync<int[]>("""
            () => [window.innerWidth, document.documentElement.scrollWidth, document.body.scrollWidth]
            """);
        Assert.True(widths[1] <= widths[0] && widths[2] <= widths[0],
            $"Viewport/root/body: {string.Join('/', widths)}");
        var bytes = await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(output, name),
            FullPage = true,
            Mask = [page.Locator(".encabezado-aplicacion"), page.Locator("[data-sensitive-activation]")],
        });
        var imageWidth = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        Assert.Equal(mobile ? 390 : 1440, imageWidth);
    }

    private const string AccessibilityCheck = """
        () => document.documentElement.lang === 'es-MX' && !!document.title.trim() &&
          document.querySelectorAll('main').length === 1 && document.querySelectorAll('h1').length === 1 &&
          [...document.querySelectorAll('input:not([type=hidden]), textarea, select')]
            .every(node => !!node.labels?.length)
        """;
}
