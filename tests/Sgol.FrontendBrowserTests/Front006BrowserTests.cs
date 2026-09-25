using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front006BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task DirectionManagesIndividualAccountsAndOtherRolesCannotViewThem()
    {
        var fixture = new BrowserFixture();
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-006-evidence");
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
                var personId = await fixture.SeedUnlinkedPersonAsync($"FRONT006-{viewport}-ACTIVE", active: true);
                var inactiveId = await fixture.SeedUnlinkedPersonAsync($"FRONT006-{viewport}-INACTIVE", active: false);
                var userName = $"front006.{viewport}.synthetic";
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
                Assert.Equal(1, await page.GetByRole(AriaRole.Heading, new() { Name = "Cuentas" }).CountAsync());
                Assert.True(await page.Locator(".tabla__fila").CountAsync() >= 4);
                Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                await AssertNoHorizontalOverflowAsync(page);
                await CaptureAsync(page, output, $"{viewport}-populated.png");

                // A full Dirección session itself has an account, so preview only the empty component.
                await page.EvaluateAsync("""
                    () => {
                      const section = document.querySelector('#cuentas');
                      section.querySelector('.tabla-contenedor')?.remove();
                      const empty = document.createElement('div');
                      empty.className = 'estado-vacio';
                      empty.innerHTML = '<p class="estado-vacio__titulo">Aún no hay cuentas registradas</p><p class="estado-vacio__texto">Crea una cuenta para una persona activa.</p><a class="boton boton--secundario" href="#alta-cuenta">Crear cuenta</a>';
                      section.insertBefore(empty, section.querySelector('#alta-cuenta'));
                    }
                    """);
                Assert.Equal("Aún no hay cuentas registradas",
                    await page.Locator("#cuentas > .estado-vacio .estado-vacio__titulo").InnerTextAsync());
                await CaptureAsync(page, output, $"{viewport}-empty-preview.png");
                await page.ReloadAsync();

                var navigation = mobile ? page.Locator("#navegacion-movil nav") :
                    page.Locator(".navegacion-lateral--escritorio");
                if (mobile) await page.GetByRole(AriaRole.Button, new() { Name = "Navegación principal" }).ClickAsync();
                Assert.Equal(1, await navigation.Locator("a[href='/personas-y-accesos']").CountAsync());
                Assert.Equal(1, await navigation.Locator("a[href='/configuracion']").CountAsync());
                Assert.Equal(1, await navigation.Locator("a[href='/planificacion']").CountAsync());
                Assert.Equal(3, await navigation.Locator("a").CountAsync());
                if (mobile) await page.Keyboard.PressAsync("Escape");

                await page.Locator("#account-person").FocusAsync();
                Assert.Equal("account-person", await page.EvaluateAsync<string>(
                    "() => document.activeElement?.id"));
                await page.Locator("#account-person").PressAsync("Tab");
                var focus = await page.EvaluateAsync<string>("""
                    () => `${document.activeElement?.id}|${getComputedStyle(document.activeElement).outlineStyle}`
                    """);
                Assert.True(focus.StartsWith("account-user|", StringComparison.Ordinal) &&
                    !focus.EndsWith("|none", StringComparison.Ordinal), $"{viewport}: {focus}");
                await CaptureAsync(page, output, $"{viewport}-focus.png");
                await page.Locator("#account-person").SelectOptionAsync(personId.ToString("D"));
                await page.Locator("#account-user").FillAsync(userName);
                await CaptureAsync(page, output, $"{viewport}-create-form.png");
                await page.Locator("#alta-cuenta").GetByRole(AriaRole.Button, new() { Name = "Crear cuenta" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('#cuentas [role=status]')?.textContent.includes('Cuenta creada')");
                Assert.Equal(1, await page.Locator("[data-sensitive-activation]").CountAsync());
                Assert.Equal(1, await AccountRow(page, userName).CountAsync());
                await CaptureAsync(page, output, $"{viewport}-created.png");
                await page.ReloadAsync();
                Assert.Equal(0, await page.Locator("[data-sensitive-activation]").CountAsync());

                await page.Locator("#account-person").SelectOptionAsync(personId.ToString("D"));
                await page.Locator("#account-user").FillAsync(userName);
                await page.Locator("#alta-cuenta").GetByRole(AriaRole.Button, new() { Name = "Crear cuenta" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('#account-error')?.textContent.includes('ya tiene una cuenta')");
                Assert.Equal(1, await AccountRow(page, userName).CountAsync());
                await AssertNoHorizontalOverflowAsync(page);
                await CaptureAsync(page, output, $"{viewport}-duplicate.png");

                var row = AccountRow(page, userName);
                await row.GetByRole(AriaRole.Button, new() { Name = "Desactivar cuenta" }).ClickAsync();
                var dialog = page.GetByRole(AriaRole.Dialog);
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await dialog.GetByLabel("Motivo").FillAsync("Baja sintética");
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Desactivar cuenta" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('#cuentas [role=status]')?.textContent.includes('Cuenta desactivada')");
                Assert.Contains("Inactiva", await AccountRow(page, userName).InnerTextAsync());
                await CaptureAsync(page, output, $"{viewport}-deactivated.png");

                await AccountRow(page, userName).GetByRole(AriaRole.Button, new() { Name = "Reactivar cuenta" }).ClickAsync();
                await page.GetByRole(AriaRole.Dialog).GetByLabel("Motivo").FillAsync("Reactivación sintética");
                await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Reactivar cuenta" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('#cuentas [role=status]')?.textContent.includes('Cuenta reactivada')");
                Assert.Contains("Activa", await AccountRow(page, userName).InnerTextAsync());
                Assert.Equal(1, await page.Locator("[data-sensitive-activation]").CountAsync());
                await AssertNoHorizontalOverflowAsync(page);
                await CaptureAsync(page, output, $"{viewport}-reactivated.png");
                await page.ReloadAsync();
                Assert.Equal(0, await page.Locator("[data-sensitive-activation]").CountAsync());

                await page.Locator("#account-person").SelectOptionAsync(inactiveId.ToString("D"));
                await page.Locator("#account-user").FillAsync($"inactive.{viewport}.synthetic");
                await page.Locator("#alta-cuenta").GetByRole(AriaRole.Button, new() { Name = "Crear cuenta" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('#account-error')?.textContent.includes('debe estar activa')");
                Assert.Equal(0, await AccountRow(page, $"inactive.{viewport}.synthetic").CountAsync());
                await AssertNoHorizontalOverflowAsync(page);
                await CaptureAsync(page, output, $"{viewport}-inactive-person.png");

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
                    var deniedPage = await deniedContext.NewPageAsync();
                    Assert.Equal(403, (await deniedPage.GotoAsync(new Uri(fixture.BaseAddress,
                        "/personas-y-accesos").AbsoluteUri))?.Status);
                    Assert.Equal(0, await deniedPage.Locator("#cuentas, #alta-cuenta, .tabla__fila").CountAsync());
                    Assert.Equal(0, await deniedPage.Locator("nav a[href='/personas-y-accesos']").CountAsync());
                    if (index == 1) await CaptureAsync(deniedPage, output, $"{viewport}-denied.png");
                    await deniedContext.ClearCookiesAsync();
                }
                await context.ClearCookiesAsync();
            }
            await using var invalidationBrowser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            await using var invalidationContext = await invalidationBrowser.NewContextAsync(new()
            {
                IgnoreHTTPSErrors = true,
                ServiceWorkers = ServiceWorkerPolicy.Block,
            });
            await SetSessionAsync(invalidationContext, fixture, tickets[0]);
            var invalidationPage = await invalidationContext.NewPageAsync();
            await invalidationPage.GotoAsync(new Uri(fixture.BaseAddress, "/personas-y-accesos").AbsoluteUri);
            await AccountRow(invalidationPage, fixture.Accounts[0].UserName)
                .GetByRole(AriaRole.Button, new() { Name = "Desactivar cuenta" }).ClickAsync();
            await invalidationPage.GetByRole(AriaRole.Dialog).GetByLabel("Motivo")
                .FillAsync("Baja de sesión sintética");
            await invalidationPage.GetByRole(AriaRole.Dialog)
                .GetByRole(AriaRole.Button, new() { Name = "Desactivar cuenta" }).ClickAsync();
            await invalidationPage.WaitForURLAsync("**/acceso**");
            Assert.Contains("/acceso", invalidationPage.Url, StringComparison.Ordinal);
            Assert.Equal(0, await invalidationPage.Locator("#cuentas").CountAsync());
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupComplete);
        }
    }

    private static ILocator AccountRow(IPage page, string userName) =>
        page.Locator("#cuentas tr.tabla__fila").Filter(new() { HasTextString = userName });

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
            Mask = [page.Locator(".encabezado-aplicacion"), page.Locator("[data-sensitive-activation]")],
        });

    private static async Task AssertNoHorizontalOverflowAsync(IPage page)
    {
        var widths = await page.EvaluateAsync<string>("""
            () => JSON.stringify({ viewport: innerWidth,
              root: document.documentElement.scrollWidth,
              body: document.body.scrollWidth,
              section: document.querySelector('#cuentas')?.scrollWidth,
              form: document.querySelector('#alta-cuenta')?.scrollWidth })
            """);
        Assert.False(await page.EvaluateAsync<bool>(HorizontalPageOverflow), widths);
    }

    private const string AccessibilityCheck = """
        () => {
          if (document.documentElement.lang !== 'es-MX' || !document.title.trim()) return false;
          if (document.querySelectorAll('main').length !== 1 || document.querySelectorAll('h1').length !== 1) return false;
          const ids = [...document.querySelectorAll('[id]')].map(node => node.id);
          if (new Set(ids).size !== ids.length) return false;
          if ([...document.querySelectorAll('input:not([type=hidden])')].some(node => !node.labels?.length)) return false;
          if ([...document.querySelectorAll('[aria-labelledby], [aria-describedby]')].some(node =>
            ['aria-labelledby', 'aria-describedby'].some(attribute =>
              (node.getAttribute(attribute) || '').split(/\s+/).filter(Boolean)
                .some(id => !document.getElementById(id))))) return false;
          return true;
        }
        """;

    private const string HorizontalPageOverflow = """
        () => Math.max(document.documentElement.scrollWidth, document.body.scrollWidth) > window.innerWidth
        """;
}
