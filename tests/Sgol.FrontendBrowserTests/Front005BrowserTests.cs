using System.Globalization;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front005BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task DirectionManagesBinaryDaysAndOtherRolesSeeNoAvailability()
    {
        var fixture = new BrowserFixture();
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-005-evidence");
        Directory.CreateDirectory(output);
        try
        {
            await fixture.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            var tickets = new List<string>();
            foreach (var account in fixture.Accounts) tickets.Add(await fixture.AuthenticateAsync(account));
            var path = $"/personas-y-accesos/personas/{fixture.Accounts[1].PersonId:D}";
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).DateTime);
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            foreach (var mobile in new[] { false, true })
            {
                var viewport = mobile ? "mobile" : "desktop";
                var rangeStart = monday.AddDays(mobile ? 7 : 0);
                var first = rangeStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var second = rangeStart.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var rangeEnd = rangeStart.AddDays(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
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
                    $"{path}?fromDate={first}&toDate={rangeEnd}&day={first}").AbsoluteUri))?.Status);
                Assert.Equal(1, await page.GetByRole(AriaRole.Heading, new() { Name = "Disponibilidad" }).CountAsync());
                Assert.Equal(7, await page.Locator(".disponibilidad-dia").CountAsync());
                Assert.Contains("Sin registro", await page.Locator(".disponibilidad-dia").First.InnerTextAsync());
                Assert.Contains("Aún no hay disponibilidad registrada en este rango",
                    await page.Locator(".estado-vacio__titulo").InnerTextAsync());
                Assert.False(await page.EvaluateAsync<bool>(
                    "() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
                Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                await CaptureAsync(page, output, $"{viewport}-empty.png");

                var navigation = mobile ? page.Locator("#navegacion-movil nav") :
                    page.Locator(".navegacion-lateral--escritorio");
                if (mobile)
                {
                    var opener = page.GetByRole(AriaRole.Button, new() { Name = "Navegación principal" });
                    await opener.ClickAsync();
                    Assert.True(await page.Locator("#navegacion-movil").EvaluateAsync<bool>("el => el.open"));
                }
                Assert.Equal(1, await navigation.Locator("a[href='/personas-y-accesos']").CountAsync());
                Assert.Equal(1, await navigation.Locator("a[href='/configuracion']").CountAsync());
                Assert.Equal(1, await navigation.Locator("a[href='/planificacion']").CountAsync());
                Assert.Equal(3, await navigation.Locator("a").CountAsync());
                if (mobile)
                {
                    await page.Keyboard.PressAsync("Escape");
                    Assert.False(await page.Locator("#navegacion-movil").EvaluateAsync<bool>("el => el.open"));
                }

                await SelectDayAsync(page, first);
                var dayInput = page.GetByLabel("Día", new() { Exact = true });
                await dayInput.FocusAsync();
                await page.Keyboard.PressAsync("Shift+Tab");
                await page.Keyboard.PressAsync("Tab");
                Assert.True(await dayInput.EvaluateAsync<bool>(
                    "el => document.activeElement === el && getComputedStyle(el).outlineStyle !== 'none'"));
                for (var tab = 0; tab < 4 && !await page.GetByRole(AriaRole.Button,
                         new() { Name = "Consultar disponibilidad" }).EvaluateAsync<bool>(
                         "el => document.activeElement === el"); tab++)
                    await page.Keyboard.PressAsync("Tab");
                Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Consultar disponibilidad" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await CaptureAsync(page, output, $"{viewport}-date-focus.png");
                await page.GetByLabel("Disponible", new() { Exact = true }).CheckAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Guardar disponibilidad" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('[role=status]')?.textContent.includes('Disponibilidad registrada')");
                Assert.Contains("Disponible", await DayAsync(page, first));
                await CaptureAsync(page, output, $"{viewport}-available.png");

                var stalePage = await context.NewPageAsync();
                await stalePage.GotoAsync(new Uri(fixture.BaseAddress,
                    $"{path}?fromDate={first}&toDate={rangeEnd}&day={first}").AbsoluteUri);
                await page.GetByLabel("No disponible", new() { Exact = true }).CheckAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Guardar disponibilidad" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('[role=status]')?.textContent.includes('Disponibilidad corregida')");
                Assert.Contains("No disponible", await DayAsync(page, first));
                await CaptureAsync(page, output, $"{viewport}-corrected-unavailable.png");

                await stalePage.GetByLabel("No disponible", new() { Exact = true }).CheckAsync();
                await stalePage.GetByRole(AriaRole.Button, new() { Name = "Guardar disponibilidad" }).ClickAsync();
                Assert.Contains("Este registro cambió mientras lo editabas",
                    await stalePage.Locator("#availability-error").InnerTextAsync());
                Assert.Equal(0, await stalePage.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar disponibilidad" }).CountAsync());
                Assert.Equal(1, await stalePage.GetByRole(AriaRole.Link,
                    new() { Name = "Recargar disponibilidad" }).CountAsync());
                await CaptureAsync(stalePage, output, $"{viewport}-conflict.png");
                await stalePage.GetByRole(AriaRole.Link, new() { Name = "Recargar disponibilidad" }).ClickAsync();
                Assert.Contains("No disponible", await DayAsync(stalePage, first));
                await stalePage.CloseAsync();

                await SelectDayAsync(page, second);
                await page.GetByLabel("No disponible", new() { Exact = true }).CheckAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Guardar disponibilidad" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('[role=status]')?.textContent.includes('Disponibilidad registrada')");
                Assert.Contains("No disponible", await DayAsync(page, second));
                await CaptureAsync(page, output, $"{viewport}-created-unavailable.png");
                await page.GetByLabel("Disponible", new() { Exact = true }).CheckAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Guardar disponibilidad" }).ClickAsync();
                await page.WaitForFunctionAsync("() => document.querySelector('[role=status]')?.textContent.includes('Disponibilidad corregida')");
                Assert.Contains("Disponible", await DayAsync(page, second));
                await CaptureAsync(page, output, $"{viewport}-corrected-available.png");

                var invalid = await page.GotoAsync(new Uri(fixture.BaseAddress,
                    $"{path}?fromDate=2026-02-30&toDate={second}&day={second}").AbsoluteUri);
                Assert.Equal(200, invalid?.Status);
                Assert.Contains("Revisa el rango y el día", await page.Locator("#availability-error").InnerTextAsync());
                Assert.Equal("true", await page.GetByLabel("Desde").GetAttributeAsync("aria-invalid"));
                Assert.Equal(0, await page.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar disponibilidad" }).CountAsync());
                await CaptureAsync(page, output, $"{viewport}-invalid-date.png");
                var reversed = await page.GotoAsync(new Uri(fixture.BaseAddress,
                    $"{path}?fromDate={second}&toDate={first}&day={first}").AbsoluteUri);
                Assert.Equal(200, reversed?.Status);
                Assert.Contains("Revisa el rango y el día", await page.Locator("#availability-error").InnerTextAsync());
                await context.ClearCookiesAsync();

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
                    Assert.Equal(404, (await deniedPage.GotoAsync(new Uri(fixture.BaseAddress,
                        $"{path}?fromDate={first}&toDate={second}&day={first}").AbsoluteUri))?.Status);
                    Assert.Equal(0, await deniedPage.Locator(".disponibilidad-lista, #disponibilidad-titulo, .detalle-persona").CountAsync());
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

    private static async Task SelectDayAsync(IPage page, string date)
    {
        await page.GetByLabel("Día", new() { Exact = true }).FillAsync(date);
        await page.EvaluateAsync("() => window.__front005PendingNavigation = true");
        await page.GetByRole(AriaRole.Button, new() { Name = "Consultar disponibilidad" }).ClickAsync();
        await page.WaitForFunctionAsync(
            "date => !window.__front005PendingNavigation && document.querySelector('#availability-day')?.value === date",
            date);
    }

    private static Task<string> DayAsync(IPage page, string date) =>
        page.Locator($".disponibilidad-dia:has(time[datetime='{date}'])").InnerTextAsync();

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
            Mask = [page.Locator(".encabezado-aplicacion")],
        });

    private const string AccessibilityCheck = """
        () => {
          if (document.documentElement.lang !== 'es-MX' || !document.title.trim()) return false;
          if (document.querySelectorAll('main').length !== 1 || document.querySelectorAll('h1').length !== 1) return false;
          const ids = [...document.querySelectorAll('[id]')].map(node => node.id);
          if (new Set(ids).size !== ids.length) return false;
          if ([...document.querySelectorAll('input:not([type=hidden])')].some(node => !node.labels?.length && node.type !== 'radio')) return false;
          if ([...document.querySelectorAll('[aria-labelledby], [aria-describedby]')].some(node =>
            ['aria-labelledby', 'aria-describedby'].some(attribute =>
              (node.getAttribute(attribute) || '').split(/\s+/).filter(Boolean)
                .some(id => !document.getElementById(id))))) return false;
          return true;
        }
        """;
}
