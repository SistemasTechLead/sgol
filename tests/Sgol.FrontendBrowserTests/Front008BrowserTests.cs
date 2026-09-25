using System.Buffers.Binary;
using System.Globalization;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front008BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task FirstDraftCreatedInConfigurationCanBeEditedInPlanningAfterReload()
    {
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-008-evidence");
        Directory.CreateDirectory(output);
        using var playwright = await Playwright.CreateAsync();
        foreach (var mobile in new[] { false, true })
        {
            var fixture = new BrowserFixture();
            try
            {
                await fixture.StartAsync();
                var ticket = await fixture.AuthenticateAsync(fixture.Accounts[0]);
                await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                    .LaunchAsync(new() { Headless = true });
                await using var context = await NewContextAsync(browser, mobile);
                await SetSessionAsync(context, fixture, ticket);
                var page = await context.NewPageAsync();
                var viewport = mobile ? "mobile" : "desktop";

                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    "/planificacion").AbsoluteUri))?.Status);
                Assert.Contains("No hay una release en borrador disponible",
                    await page.Locator("section[aria-labelledby='draft-title']").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-first-draft-empty.png", mobile);
                var configuration = page.WaitForResponseAsync(response => response.Request.Method == "GET" &&
                    response.Url.EndsWith("/configuracion", StringComparison.OrdinalIgnoreCase));
                await page.GetByRole(AriaRole.Link, new() { Name = "Crear borrador" }).ClickAsync();
                Assert.Equal(200, (await configuration).Status);
                Assert.Contains("Sucursal Loretta", await page.Locator("main").InnerTextAsync());
                Assert.Contains("Aún no hay releases de configuración", await page.Locator("main").InnerTextAsync());
                var creation = page.WaitForResponseAsync(response => response.Request.Method == "POST" &&
                    response.Url.Contains("/configuracion", StringComparison.OrdinalIgnoreCase));
                await page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador" }).ClickAsync();
                Assert.Equal(200, (await creation).Status);
                await page.Locator("#release-history tbody tr").WaitForAsync();
                Assert.Contains("Borrador", await page.Locator("#release-history").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-first-draft-config.png", mobile);

                var planning = page.WaitForResponseAsync(response => response.Request.Method == "GET" &&
                    response.Url.Contains("/planificacion?releaseId=", StringComparison.OrdinalIgnoreCase));
                await page.GetByRole(AriaRole.Link, new() { Name = "Editar días del borrador" })
                    .First.ClickAsync();
                Assert.Equal(200, (await planning).Status);
                Assert.Contains("Aún no hay días en este borrador", await page.Locator("#draft-days").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-first-draft-planning.png", mobile);

                await page.Locator("#draft-type").SelectOptionAsync("FESTIVO");
                await page.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador" }).ClickAsync();
                var dialog = page.Locator("#calendar-confirm[open]");
                await dialog.GetByLabel("Motivo").FillAsync("Primera release sintética FRONT-008");
                var save = page.WaitForResponseAsync(response => response.Request.Method == "POST" &&
                    response.Url.Contains("/planificacion", StringComparison.OrdinalIgnoreCase));
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador" }).ClickAsync();
                Assert.Equal(200, (await save).Status);
                await page.Locator("[role=status]").Filter(new() { HasTextString = "Día agregado al borrador" }).WaitForAsync();
                Assert.Contains("Festivo", await page.Locator("#draft-days").InnerTextAsync());
                Assert.Contains("No hay días publicados en este rango", await page.Locator("#published-calendar").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-first-draft-day.png", mobile);

                Assert.Equal(200, (await page.ReloadAsync())?.Status);
                Assert.Contains("Festivo", await page.Locator("#draft-days").InnerTextAsync());
            }
            finally
            {
                await fixture.DisposeAsync();
                Assert.True(fixture.CleanupComplete);
            }
        }
    }

    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task BranchWeekCalendarAndExistingDraftRespectAuthorityConflictAndViewport()
    {
        var fixture = new BrowserFixture();
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-008-evidence");
        Directory.CreateDirectory(output);
        try
        {
            await fixture.StartAsync();
            using var playwright = await Playwright.CreateAsync();
            var tickets = new List<string>();
            foreach (var account in fixture.Accounts) tickets.Add(await fixture.AuthenticateAsync(account));
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).DateTime);
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            foreach (var mobile in new[] { false, true })
            {
                var viewport = mobile ? "mobile" : "desktop";
                var publishedDate = monday.AddDays(mobile ? 7 : 0);
                var published = Iso(publishedDate);
                var last = Iso(publishedDate.AddDays(6));
                var draftId = await fixture.SeedCalendarScenarioAsync(fixture.Accounts[0].UserId, publishedDate);
                await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                    .LaunchAsync(new() { Headless = true });
                await using var context = await NewContextAsync(browser, mobile);
                await SetSessionAsync(context, fixture, tickets[0]);
                var page = await context.NewPageAsync();

                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    "/configuracion").AbsoluteUri))?.Status);
                Assert.Contains("LOR-001", await page.Locator("main").InnerTextAsync());
                Assert.Contains("America/Mexico_City", await page.Locator("main").InnerTextAsync());
                Assert.Equal(0, await page.GetByRole(AriaRole.Button, new() { Name = "Editar sucursal" }).CountAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-branch.png", mobile);

                var navigation = mobile ? page.Locator("#navegacion-movil nav") :
                    page.Locator(".navegacion-lateral--escritorio");
                if (mobile) await page.GetByRole(AriaRole.Button, new() { Name = "Navegación principal" }).ClickAsync();
                Assert.Equal(1, await navigation.Locator("a[href='/configuracion']").CountAsync());
                Assert.Equal(1, await navigation.Locator("a[href='/planificacion']").CountAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-navigation.png", mobile);
                if (mobile) await page.Keyboard.PressAsync("Escape");

                var planPath = $"/planificacion?from={published}&to={last}&day={published}&releaseId={draftId:D}";
                var firstPlanResponse = await page.GotoAsync(new Uri(fixture.BaseAddress, planPath).AbsoluteUri);
                if (firstPlanResponse?.Status != 200)
                    Assert.Fail($"Plan HTTP {firstPlanResponse?.Status}: {await page.Locator("main").InnerTextAsync()}");
                Assert.Contains("Período único", await page.Locator("main").InnerTextAsync());
                Assert.Contains("Festivo", await page.Locator("#published-calendar").InnerTextAsync());
                Assert.Contains("Aún no hay días en este borrador", await page.Locator("#draft-days").InnerTextAsync());
                Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-calendar.png", mobile);

                var elapsedMonday = monday.AddDays(-7);
                var elapsedYear = ISOWeek.GetYear(elapsedMonday.ToDateTime(TimeOnly.MinValue));
                var elapsedWeek = ISOWeek.GetWeekOfYear(elapsedMonday.ToDateTime(TimeOnly.MinValue));
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    $"/planificacion?isoYear={elapsedYear}&isoWeek={elapsedWeek}&from={published}&to={last}").AbsoluteUri))?.Status);
                Assert.Contains("Transcurrida", await page.Locator(".planificacion__resultado").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-elapsed-week.png", mobile);
                await page.GotoAsync(new Uri(fixture.BaseAddress, planPath).AbsoluteUri);

                var typeControl = page.Locator("#draft-type");
                await typeControl.FocusAsync();
                await typeControl.PressAsync("Tab");
                Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-focus.png", mobile);

                var emptyFrom = Iso(publishedDate.AddDays(14));
                await page.GetByLabel("Desde").FillAsync(emptyFrom);
                await page.GetByLabel("Hasta").FillAsync(Iso(publishedDate.AddDays(20)));
                var emptyNavigation = page.WaitForResponseAsync(response =>
                    response.Request.Method == "GET" && response.Url.Contains("/planificacion?", StringComparison.OrdinalIgnoreCase) &&
                    response.Url.Contains($"from={emptyFrom}", StringComparison.Ordinal));
                await page.GetByRole(AriaRole.Button, new() { Name = "Consultar calendario" }).ClickAsync();
                await emptyNavigation;
                Assert.Contains("No hay días publicados en este rango", await page.Locator("#published-calendar").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-empty.png", mobile);

                await page.GotoAsync(new Uri(fixture.BaseAddress, planPath).AbsoluteUri);
                await page.Locator("#draft-type").SelectOptionAsync("LABORABLE");
                await page.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador" }).ClickAsync();
                var dialog = page.Locator("#calendar-confirm[open]");
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await dialog.GetByLabel("Motivo").FillAsync("Día sintético FRONT-008");
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-confirm.png", mobile);
                var saveResponse = page.WaitForResponseAsync(response => response.Request.Method == "POST" &&
                    response.Url.Contains("/planificacion", StringComparison.OrdinalIgnoreCase));
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador" }).ClickAsync();
                Assert.Equal(200, (await saveResponse).Status);
                await page.Locator("[role=status]").Filter(new() { HasTextString = "Día agregado al borrador" }).WaitForAsync();
                Assert.Contains("Laborable", await page.Locator("#draft-days").InnerTextAsync());
                Assert.Contains("Festivo", await page.Locator("#published-calendar").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-draft.png", mobile);

                var stalePage = await context.NewPageAsync();
                await stalePage.GotoAsync(new Uri(fixture.BaseAddress, planPath).AbsoluteUri);
                await page.Locator("#draft-type").SelectOptionAsync("CIERRE_EXTRAORDINARIO");
                await SubmitCorrectionAsync(page, "Corrección sintética FRONT-008");
                Assert.Contains("Cierre extraordinario", await page.Locator("#draft-days").InnerTextAsync());
                await stalePage.Locator("#draft-type").SelectOptionAsync("FESTIVO");
                await stalePage.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador" }).ClickAsync();
                var staleDialog = stalePage.Locator("#calendar-confirm[open]");
                await staleDialog.GetByLabel("Motivo").FillAsync("Versión obsoleta sintética");
                var conflictResponse = stalePage.WaitForResponseAsync(response => response.Request.Method == "POST" &&
                    response.Url.Contains("/planificacion", StringComparison.OrdinalIgnoreCase));
                await staleDialog.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador" }).ClickAsync();
                Assert.Equal(412, (await conflictResponse).Status);
                await stalePage.Locator("#draft-error").WaitForAsync();
                Assert.Contains("El borrador cambió", await stalePage.Locator("#draft-error").InnerTextAsync());
                Assert.Equal(0, await stalePage.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar día en borrador" }).CountAsync());
                await CheckWidthAndCaptureAsync(stalePage, output, $"{viewport}-conflict.png", mobile);
                await stalePage.CloseAsync();

                Assert.Equal(400, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    "/planificacion?from=2026-02-30&to=2026-03-01").AbsoluteUri))?.Status);
                Assert.Contains("Revisa el rango de calendario", await page.Locator("#calendar-error").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-invalid.png", mobile);

                for (var index = 1; index < tickets.Count; index++)
                {
                    await using var deniedContext = await NewContextAsync(browser, mobile);
                    await SetSessionAsync(deniedContext, fixture, tickets[index]);
                    var denied = await deniedContext.NewPageAsync();
                    Assert.Equal(200, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        "/configuracion").AbsoluteUri))?.Status);
                    Assert.Equal(200, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        planPath).AbsoluteUri))?.Status);
                    Assert.Equal(0, await denied.Locator("#draft-title").CountAsync());
                    if (index == 1) await CheckWidthAndCaptureAsync(denied, output, $"{viewport}-denied.png", mobile);
                    Assert.Equal(403, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        $"/api/v1/calendar/drafts/{draftId:D}?from={published}&to={last}").AbsoluteUri))?.Status);
                }
            }
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupComplete);
        }
    }

    private static async Task SubmitCorrectionAsync(IPage page, string reason)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador" }).ClickAsync();
        var dialog = page.Locator("#calendar-confirm[open]");
        await dialog.GetByLabel("Motivo").FillAsync(reason);
        var responseTask = page.WaitForResponseAsync(response => response.Request.Method == "POST" &&
            response.Url.Contains("/planificacion", StringComparison.OrdinalIgnoreCase));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Guardar día en borrador" }).ClickAsync();
        Assert.Equal(200, (await responseTask).Status);
        await page.Locator("[role=status]").Filter(new() { HasTextString = "Día corregido en el borrador" }).WaitForAsync();
    }

    private static string Iso(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Task SetSessionAsync(IBrowserContext context, BrowserFixture fixture, string ticket) =>
        context.AddCookiesAsync([new Cookie
        {
            Name = "__Host-SGOL-Session", Value = ticket, Url = fixture.BaseAddress.AbsoluteUri,
            Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict,
        }]);

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

    private static async Task CheckWidthAndCaptureAsync(IPage page, string output, string name, bool mobile)
    {
        var widths = await page.EvaluateAsync<int[]>(
            "() => [window.innerWidth, document.documentElement.scrollWidth, document.body.scrollWidth]");
        Assert.True(widths[1] <= widths[0] && widths[2] <= widths[0],
            $"Viewport/root/body: {string.Join('/', widths)}");
        var bytes = await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(output, name),
            FullPage = true,
            Mask = [page.Locator(".encabezado-aplicacion")],
        });
        Assert.Equal(mobile ? 390 : 1440, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
    }

    private const string AccessibilityCheck = """
        () => document.documentElement.lang === 'es-MX' && !!document.title.trim() &&
          document.querySelectorAll('main').length === 1 && document.querySelectorAll('h1').length === 1 &&
          [...document.querySelectorAll('input:not([type=hidden]), textarea, select')]
            .every(node => !!node.labels?.length)
        """;
}
