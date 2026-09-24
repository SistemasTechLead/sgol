using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front004BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task DirectionEditsEmploymentAndVigency_WhileOtherRolesAreDenied()
    {
        var fixture = new BrowserFixture();
        var output = Path.Combine(BrowserFixture.RepositoryRoot(), ".artifacts", "front-004");
        Directory.CreateDirectory(output);
        try
        {
            await fixture.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            var tickets = new List<string>();
            foreach (var account in fixture.Accounts) tickets.Add(await fixture.AuthenticateAsync(account));
            var path = $"/personas-y-accesos/personas/{fixture.Accounts[1].PersonId:D}";
            foreach (var mobile in new[] { false, true })
            {
                await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                    .LaunchAsync(new() { Headless = true });
                var viewport = mobile ? "mobile" : "desktop";
                var position = mobile ? "Subdirector" : "Director";
                var shift = mobile ? "Matutino" : "Vespertino";
                await using var context = await browser.NewContextAsync(new()
                {
                    ViewportSize = mobile ? new() { Width = 390, Height = 844 } : new() { Width = 1440, Height = 900 },
                    Locale = "es-MX",
                    IsMobile = mobile,
                    HasTouch = mobile,
                    IgnoreHTTPSErrors = true,
                    ServiceWorkers = ServiceWorkerPolicy.Block,
                });
                await SessionAsync(context, fixture, tickets[0]);
                var page = await context.NewPageAsync();
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress, path).AbsoluteUri))?.Status);
                Assert.True(await page.Locator("nav a[href='/personas-y-accesos']").CountAsync() >= 1);
                Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                Assert.False(await page.EvaluateAsync<bool>(
                    "() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
                var before = await page.Locator("tbody tr").CountAsync();
                await CaptureAsync(page, output, $"{viewport}-before.png");

                var stalePage = await context.NewPageAsync();
                await stalePage.GotoAsync(new Uri(fixture.BaseAddress, path).AbsoluteUri);
                await page.GetByLabel("Puesto").FillAsync(position);
                await page.GetByLabel("Turno").FillAsync(shift);
                await page.GetByLabel("Puesto").FocusAsync();
                Assert.True(await page.GetByLabel("Puesto").EvaluateAsync<bool>(
                    "el => document.activeElement === el && getComputedStyle(el).outlineStyle !== 'none'"));
                await page.GetByLabel("Motivo").First.FillAsync("Cambio laboral sintético privado");
                await CaptureAsync(page, output, $"{viewport}-employment-form.png");
                await page.GetByRole(AriaRole.Button, new() { Name = "Guardar empleo" }).ClickAsync();
                Assert.Contains("Empleo actualizado", await page.GetByRole(AriaRole.Status).InnerTextAsync());
                Assert.Equal(before + 1, await page.Locator("tbody tr").CountAsync());
                Assert.Contains(position, await page.Locator("tbody tr").Last.InnerTextAsync());
                await CaptureAsync(page, output, $"{viewport}-employment-after.png");

                await stalePage.GetByLabel("Puesto").FillAsync("Otro puesto");
                await stalePage.GetByLabel("Motivo").First.FillAsync("Motivo de conflicto privado");
                await stalePage.GetByRole(AriaRole.Button, new() { Name = "Guardar empleo" }).ClickAsync();
                Assert.Contains("Este registro cambió mientras lo editabas",
                    await stalePage.Locator("#person-detail-error").InnerTextAsync());
                Assert.Equal(0, await stalePage.Locator(".formulario-persona form, #deactivate-dialog, #reactivate-dialog").CountAsync());
                Assert.Equal(1, await stalePage.GetByRole(AriaRole.Link, new() { Name = "Recargar detalle" }).CountAsync());
                await CaptureAsync(stalePage, output, $"{viewport}-conflict.png");
                await stalePage.GetByRole(AriaRole.Link, new() { Name = "Recargar detalle" }).ClickAsync();
                Assert.Equal(before + 1, await stalePage.Locator("tbody tr").CountAsync());
                await stalePage.CloseAsync();

                await page.GetByRole(AriaRole.Button, new() { Name = "Dar de baja" }).ClickAsync();
                var dialog = page.Locator("#deactivate-dialog");
                Assert.True(await dialog.EvaluateAsync<bool>("el => el.open"));
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await page.Keyboard.PressAsync("Escape");
                Assert.False(await dialog.EvaluateAsync<bool>("el => el.open"));
                Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Dar de baja" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await page.GetByRole(AriaRole.Button, new() { Name = "Dar de baja" }).ClickAsync();
                await CaptureAsync(page, output, $"{viewport}-deactivate-dialog.png");
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Dar de baja" }).ClickAsync();
                Assert.False(await dialog.GetByLabel("Motivo").EvaluateAsync<bool>("el => el.validity.valid"));
                Assert.Equal("true", await dialog.GetByLabel("Motivo").GetAttributeAsync("aria-invalid"));
                Assert.True(await dialog.Locator("#deactivate-reason-error").IsVisibleAsync());
                Assert.True(await dialog.EvaluateAsync<bool>("el => el.open"));
                Assert.Equal(before + 1, await page.Locator("tbody tr").CountAsync());
                await dialog.GetByLabel("Motivo").FillAsync("Baja sintética privada");
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Dar de baja" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('[role=status]')?.textContent.includes('Persona dada de baja')");
                Assert.Contains("Persona dada de baja", await page.GetByRole(AriaRole.Status).InnerTextAsync());
                Assert.Equal(before + 2, await page.Locator("tbody tr").CountAsync());
                Assert.Contains("Inactiva", await page.Locator("tbody tr").Last.InnerTextAsync());
                await CaptureAsync(page, output, $"{viewport}-deactivated.png");

                await page.GetByRole(AriaRole.Button, new() { Name = "Reactivar" }).ClickAsync();
                dialog = page.Locator("#reactivate-dialog");
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await CaptureAsync(page, output, $"{viewport}-reactivate-dialog.png");
                await dialog.GetByLabel("Motivo").FillAsync("Reactivación sintética privada");
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Reactivar" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('[role=status]')?.textContent.includes('Persona reactivada')");
                Assert.Contains("Persona reactivada", await page.GetByRole(AriaRole.Status).InnerTextAsync());
                Assert.Equal(before + 3, await page.Locator("tbody tr").CountAsync());
                Assert.Contains("Activa", await page.Locator("tbody tr").Last.InnerTextAsync());
                Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                await CaptureAsync(page, output, $"{viewport}-reactivated.png");
                await context.ClearCookiesAsync();

                for (var index = 1; index < tickets.Count; index++)
                {
                    await using var deniedContext = await browser.NewContextAsync(new()
                    {
                        IgnoreHTTPSErrors = true,
                        ServiceWorkers = ServiceWorkerPolicy.Block,
                    });
                    await SessionAsync(deniedContext, fixture, tickets[index]);
                    var deniedPage = await deniedContext.NewPageAsync();
                    Assert.Equal(404, (await deniedPage.GotoAsync(new Uri(fixture.BaseAddress, path).AbsoluteUri))?.Status);
                    Assert.Equal(0, await deniedPage.Locator(".detalle-persona, .formulario-persona, #deactivate-dialog, #reactivate-dialog").CountAsync());
                    Assert.Contains("No existe o no está disponible en tu alcance",
                        await deniedPage.Locator("#person-detail-error").InnerTextAsync());
                    if (index == 1) await CaptureAsync(deniedPage, output, $"{viewport}-denied.png");
                    await deniedContext.ClearCookiesAsync();
                }
            }
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupComplete);
        }
    }

    private static Task SessionAsync(IBrowserContext context, BrowserFixture fixture, string ticket) =>
        context.AddCookiesAsync([new Microsoft.Playwright.Cookie
        {
            Name = "__Host-SGOL-Session", Value = ticket,
            Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true,
            SameSite = SameSiteAttribute.Strict,
        }]);

    private static async Task<byte[]> CaptureAsync(IPage page, string output, string name)
    {
        var masks = new List<ILocator> { page.Locator(".encabezado-aplicacion__identidad") };
        foreach (var field in await page.Locator("textarea").AllAsync())
            if (!string.IsNullOrEmpty(await field.InputValueAsync())) masks.Add(field);
        return await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(output, name),
            FullPage = true,
            Mask = masks,
        });
    }

    private const string AccessibilityCheck = """
        () => {
          if (document.documentElement.lang !== 'es-MX' || !document.title.trim()) return false;
          if (document.querySelectorAll('main').length !== 1 || document.querySelectorAll('h1').length !== 1) return false;
          const ids = [...document.querySelectorAll('[id]')].map(node => node.id);
          if (new Set(ids).size !== ids.length) return false;
          if ([...document.querySelectorAll('input:not([type=hidden]), textarea')].some(node => !node.labels?.length)) return false;
          if ([...document.querySelectorAll('[aria-labelledby], [aria-describedby]')].some(node =>
            ['aria-labelledby', 'aria-describedby'].some(attribute =>
              (node.getAttribute(attribute) || '').split(/\s+/).filter(Boolean)
                .some(id => !document.getElementById(id))))) return false;
          return true;
        }
        """;
}
