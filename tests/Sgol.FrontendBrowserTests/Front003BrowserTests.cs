using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front003BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task DirectionCanListCreateAndReadHistory_WhileOtherRolesSeeNoPeople()
    {
        var fixture = new BrowserFixture();
        var output = Path.Combine(BrowserFixture.RepositoryRoot(), ".artifacts", "front-003");
        Directory.CreateDirectory(output);
        try
        {
            await fixture.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            var tickets = new List<string>();
            foreach (var account in fixture.Accounts) tickets.Add(await fixture.AuthenticateAsync(account));
            foreach (var mobile in new[] { false, true })
            {
                await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                    .LaunchAsync(new() { Headless = true });
                var viewport = mobile ? "mobile" : "desktop";
                await using (var context = await NewContextAsync(browser, mobile))
                {
                    await SetSessionAsync(context, fixture, tickets[0]);
                    var page = await context.NewPageAsync();
                    var listResponse = await page.GotoAsync(new Uri(fixture.BaseAddress, "/personas-y-accesos").AbsoluteUri);
                    Assert.Equal(200, listResponse?.Status);
                    Assert.Equal("Personas y accesos", await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).InnerTextAsync());
                    Assert.Equal(1, await page.GetByRole(AriaRole.Table, new() { Name = "Personas registradas" }).CountAsync());
                    Assert.True(await page.Locator("tbody tr").CountAsync() >= 4);
                    Assert.Equal(1, await page.Locator("nav[aria-label='Navegación principal'] a[href='/personas-y-accesos']").CountAsync());
                    Assert.Equal("page", await page.Locator(".navegacion-lateral--escritorio a").GetAttributeAsync("aria-current"));
                    Assert.False(await page.EvaluateAsync<bool>(
                        "() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
                    await CaptureAsync(page, output, $"{viewport}-list.png");

                    // Visual preview only: a real authenticated Dirección account is itself a person,
                    // so a full-session database fixture cannot have zero rows. The empty API/page
                    // contract is tested separately without changing production behavior.
                    await page.EvaluateAsync("""
                    () => {
                        const container = document.querySelector('.tabla-contenedor');
                        container.querySelector('tbody')?.remove();
                        const empty = document.createElement('div');
                        empty.className = 'estado-vacio';
                        empty.innerHTML = '<span class="estado-vacio__icono" aria-hidden="true">○</span><p class="estado-vacio__titulo">Aún no hay personas registradas</p><p class="estado-vacio__texto">Registra la primera persona para comenzar.</p><a class="boton boton--secundario" href="#alta-persona">Registrar persona</a>';
                        container.append(empty);
                    }
                    """);
                    Assert.Equal("Aún no hay personas registradas", await page.Locator(".estado-vacio__titulo").InnerTextAsync());
                    await CaptureAsync(page, output, $"{viewport}-empty-preview.png");
                    await page.ReloadAsync();

                    if (mobile)
                    {
                        var opener = page.GetByRole(AriaRole.Button, new() { Name = "Navegación principal" });
                        await opener.ClickAsync();
                        Assert.True(await page.Locator("#navegacion-movil").EvaluateAsync<bool>("el => el.open"));
                        await page.Keyboard.PressAsync("Escape");
                        await page.Locator("#navegacion-movil").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
                        await page.WaitForFunctionAsync("() => document.activeElement?.hasAttribute('data-dialog-open')");
                        Assert.True(await opener.EvaluateAsync<bool>("el => document.activeElement === el"));
                    }

                    var code = $"FRONT003-{Guid.CreateVersion7():N}";
                    var codeInput = page.GetByLabel("Código de persona");
                    await codeInput.FillAsync(code);
                    await codeInput.FocusAsync();
                    Assert.True(await codeInput.EvaluateAsync<bool>(
                        "el => document.activeElement === el && getComputedStyle(el).outlineStyle !== 'none'"));
                    await page.Keyboard.PressAsync("Tab");
                    Assert.True(await page.GetByLabel("Nombre").EvaluateAsync<bool>(
                        "el => document.activeElement === el"));
                    await page.GetByLabel("Nombre").FillAsync("Persona sintética FRONT-003");
                    await CaptureAsync(page, output, $"{viewport}-form.png");
                    await page.GetByRole(AriaRole.Button, new() { Name = "Registrar persona" }).ClickAsync();
                    await page.WaitForURLAsync("**/personas-y-accesos/personas/*");
                    Assert.Equal("Persona sintética FRONT-003", await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).InnerTextAsync());
                    Assert.Equal(code, await page.Locator(".detalle-persona dd").First.InnerTextAsync());
                    Assert.Contains("Activa", await page.Locator("tbody tr").First.InnerTextAsync());
                    Assert.Equal(1, await page.Locator("tbody tr").CountAsync());
                    await CaptureAsync(page, output, $"{viewport}-detail.png");

                    await page.GetByRole(AriaRole.Link, new() { Name = "Volver a personas" }).ClickAsync();
                    await page.GetByLabel("Código de persona").FillAsync(code);
                    await page.GetByLabel("Nombre").FillAsync("Otra persona sintética");
                    await page.GetByRole(AriaRole.Button, new() { Name = "Registrar persona" }).ClickAsync();
                    Assert.Equal("/personas-y-accesos", new Uri(page.Url).AbsolutePath);
                    Assert.Contains("El código de persona ya está registrado", await page.Locator("#people-error").InnerTextAsync());
                    Assert.Equal("true", await page.GetByLabel("Código de persona").GetAttributeAsync("aria-invalid"));
                    Assert.Equal(1, await page.Locator($"tbody tr:has-text('{code}')").CountAsync());
                    await CaptureAsync(page, output, $"{viewport}-duplicate.png");

                    var missing = await page.GotoAsync(new Uri(fixture.BaseAddress,
                        $"/personas-y-accesos/personas/{Guid.CreateVersion7():D}").AbsoluteUri);
                    Assert.Equal(404, missing?.Status);
                    Assert.Contains("No existe o no está disponible en tu alcance", await page.Locator("#person-detail-error").InnerTextAsync());
                    Assert.Equal(0, await page.Locator(".detalle-persona").CountAsync());
                    await context.ClearCookiesAsync();
                }

                for (var accountIndex = 1; accountIndex < fixture.Accounts.Count; accountIndex++)
                {
                    await using var context = await NewContextAsync(browser, mobile);
                    await SetSessionAsync(context, fixture, tickets[accountIndex]);
                    var page = await context.NewPageAsync();
                    var denied = await page.GotoAsync(new Uri(fixture.BaseAddress, "/personas-y-accesos").AbsoluteUri);
                    Assert.Equal(403, denied?.Status);
                    Assert.Contains("No tienes permiso para ver personas y accesos", await page.Locator("#people-error").InnerTextAsync());
                    Assert.Equal(0, await page.Locator("table, #alta-persona, nav a[href='/personas-y-accesos']").CountAsync());
                    var deepLink = await page.GotoAsync(new Uri(fixture.BaseAddress,
                        $"/personas-y-accesos/personas/{fixture.Accounts[0].PersonId:D}").AbsoluteUri);
                    Assert.Equal(404, deepLink?.Status);
                    Assert.Equal(0, await page.Locator(".detalle-persona, table").CountAsync());
                    if (accountIndex == 1) await CaptureAsync(page, output, $"{viewport}-denied.png");
                    await context.ClearCookiesAsync();
                }
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

    private static Task<byte[]> CaptureAsync(IPage page, string output, string name) =>
        page.ScreenshotAsync(new()
        {
            Path = Path.Combine(output, name),
            FullPage = true,
            Mask = [page.Locator(".encabezado-aplicacion__identidad")],
        });
}
