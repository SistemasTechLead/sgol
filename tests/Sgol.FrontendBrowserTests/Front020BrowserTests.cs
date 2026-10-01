using System.Globalization;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front020BrowserTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task QueriesArePureAndContinuityUsesExplicitAuditedConfirmation(bool mobile, bool webkit)
    {
        await using var fixture = new BrowserFixture(); await fixture.StartAsync();
        var tickets = new List<string>(); foreach (var account in fixture.Accounts) tickets.Add(await fixture.AuthenticateAsync(account));
        var tasks = await fixture.SeedMyWorkAsync();
        await fixture.Front020AuditAsync();
        var matched = await fixture.Front020ReportAsync("MATCHED"); var different = await fixture.Front020ReportAsync("DIFFERENT"); var failed = await fixture.Front020ReportAsync("FAILED"); var truncated = await fixture.Front020ReportAsync("MATCHED", true);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await (webkit ? playwright.Webkit : playwright.Chromium).LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { BaseURL = fixture.BaseAddress.AbsoluteUri, ViewportSize = new() { Width = mobile ? 390 : 1440, Height = mobile ? 844 : 900 }, IsMobile = mobile, HasTouch = mobile, IgnoreHTTPSErrors = true, Locale = "es-MX", ReducedMotion = ReducedMotion.Reduce });
        async Task Login(int index) { await context.ClearCookiesAsync(); await context.AddCookiesAsync([new() { Name = "__Host-SGOL-Session", Value = tickets[index], Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]); }
        var page = await context.NewPageAsync();
        var operationDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).DateTime;
        var year = ISOWeek.GetYear(operationDate); var week = ISOWeek.GetWeekOfYear(operationDate);
        var from = DateTimeOffset.UtcNow.AddDays(-1).ToString("O").Replace("+00:00", "Z", StringComparison.Ordinal); var to = DateTimeOffset.UtcNow.AddDays(1).ToString("O").Replace("+00:00", "Z", StringComparison.Ordinal);
        for (var role = 0; role < 4; role++)
        {
            await Login(role); var before = await fixture.Front020RowsAsync();
            Assert.Equal(200, (await page.GotoAsync("/indicadores"))!.Status);
            await Assertions.Expect(page.Locator("#operation")).ToContainTextAsync("Selecciona un año");
            await page.Locator("[name=operationisoYear]").FillAsync(year.ToString(CultureInfo.InvariantCulture)); await page.Locator("[name=operationisoWeek]").FillAsync(week.ToString(CultureInfo.InvariantCulture));
            await page.GetByRole(AriaRole.Button, new() { Name = "Consultar indicadores", Exact = true }).ClickAsync();
            await Assertions.Expect(page.Locator("#operation")).ToContainTextAsync("Base de obligaciones");
            Assert.Equal(role == 0 ? 1 : 0, await page.Locator("#direction").CountAsync());
            await Capture(page, mobile, "indicadores-" + role); await Front018BrowserTests.CheckAccessibilityAsync(page);
            Assert.Equal(before, await fixture.Front020RowsAsync());
            Assert.Equal(200, (await page.GotoAsync("/auditoria?from=" + Uri.EscapeDataString(from) + "&to=" + Uri.EscapeDataString(to)))!.Status);
            await Assertions.Expect(page.Locator("#audit")).ToContainTextAsync("Consulta UTC"); Assert.Equal(before, await fixture.Front020RowsAsync());
            if (role > 0) { Assert.Equal(403, (await page.GotoAsync("/continuidad"))!.Status); Assert.Equal(before, await fixture.Front020RowsAsync()); }
        }
        await Login(0);
        var baseline = await fixture.Front020RowsAsync();
        await page.GotoAsync("/indicadores");
        await page.Locator("[name=operationisoYear]").FillAsync(year.ToString(CultureInfo.InvariantCulture)); await page.Locator("[name=operationisoWeek]").FillAsync(week.ToString(CultureInfo.InvariantCulture));
        // Observe the progressive submit state without a navigation racing the DOM assertion.
        await page.EvaluateAsync("() => document.querySelector('[data-work-query=operation]').addEventListener('submit', e => e.preventDefault(), { once: true })");
        await page.GetByRole(AriaRole.Button, new() { Name = "Consultar indicadores", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("#operation [data-front-query-status]")).ToBeVisibleAsync(); await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Consultar indicadores", Exact = true })).ToBeDisabledAsync(); await Capture(page, mobile, "cargando");
        await page.GotoAsync($"/indicadores?directionisoYear={year}&directionisoWeek={week}"); await Assertions.Expect(page.Locator("#direction")).ToContainTextAsync("incluye pendientes sin asignación"); await Capture(page, mobile, "panorama");
        await page.GotoAsync("/indicadores?operationisoYear=2000&operationisoWeek=1"); await Assertions.Expect(page.Locator("#operation")).ToContainTextAsync("Los conteos y sus denominadores son cero"); await Capture(page, mobile, "ceros");
        Assert.Equal(400, (await page.GotoAsync("/indicadores?operationisoYear=2026&operationisoWeek=54"))!.Status); await Capture(page, mobile, "error-filtros");
        var auditPath = "/auditoria?mode=trace&from=" + Uri.EscapeDataString(from) + "&to=" + Uri.EscapeDataString(to) + "&traceObligationId=" + tasks[0].Id;
        Assert.Equal(200, (await page.GotoAsync(auditPath))!.Status); await Assertions.Expect(page.Locator("#audit")).ToContainTextAsync("Sin hechos registrados en la consulta"); await Capture(page, mobile, "traza"); await Front018BrowserTests.CheckAccessibilityAsync(page);
        await page.GotoAsync("/auditoria?from=" + Uri.EscapeDataString(from) + "&to=" + Uri.EscapeDataString(to)); await Capture(page, mobile, "auditoria-eventos");
        var detail = page.GetByRole(AriaRole.Link, new() { Name = "Detalle", Exact = true }).First;
        Assert.Equal(1, await detail.CountAsync()); await detail.ClickAsync(); Assert.DoesNotContain("SYNTHETIC-MUST-NOT-APPEAR", await page.ContentAsync()); await Capture(page, mobile, "auditoria-detalle"); await page.GetByRole(AriaRole.Link, new() { Name = "Regresar", Exact = true }).ClickAsync();
        Assert.Equal(baseline, await fixture.Front020RowsAsync());
        await page.GotoAsync("/continuidad"); await Capture(page, mobile, "continuidad-vacio");
        baseline = await fixture.Front020RowsAsync();
        await page.Locator("[name=reason]").FillAsync("Simulacro sintético"); await page.GetByRole(AriaRole.Button, new() { Name = "Preparar solicitud", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("dialog[open] button").First).ToBeFocusedAsync(); await Capture(page, mobile, "confirmacion-solicitud"); await Front018BrowserTests.CheckAccessibilityAsync(page);
        await page.Keyboard.PressAsync("Escape"); await Assertions.Expect(page.Locator("#prepare-continuity")).ToBeFocusedAsync(); Assert.Equal(baseline, await fixture.Front020RowsAsync());
        await page.Locator("#prepare-continuity").ClickAsync(); await page.Locator("dialog[open] form[data-continuity-send] button").ClickAsync(); await Assertions.Expect(page.Locator("[data-front-success]")).ToContainTextAsync("Solicitud de reconciliación registrada"); await Capture(page, mobile, "solicitada");
        foreach (var item in new[] { (different, "DIFFERENT", "diferencias"), (failed, "FAILED", "fallo"), (truncated, "MATCHED", "truncado") })
        { var views = await fixture.Front020ViewsAsync(); await page.GotoAsync("/continuidad/reconciliaciones/" + item.Item1); Assert.Equal(views + 1, await fixture.Front020ViewsAsync()); Assert.Equal(0, await page.Locator("[name=reason]").CountAsync()); await Capture(page, mobile, item.Item3); }
        foreach (var status in new[] { "REFERENCE_CAPTURING", "REFERENCE_READY", "RESTORE_STARTED", "RECONCILING" })
        { var staged = await fixture.Front020ReportAsync(status); Assert.Equal(200, (await page.GotoAsync("/continuidad/reconciliaciones/" + staged))!.Status); Assert.Equal(0, await page.Locator("[name=reason]").CountAsync()); await Capture(page, mobile, status.ToLowerInvariant()); }
        await page.GotoAsync("/continuidad/reconciliaciones/" + matched); await Capture(page, mobile, "coincidente"); baseline = await fixture.Front020RowsAsync();
        await page.Locator("[name=reason]").FillAsync("Aceptación sintética"); await page.GetByRole(AriaRole.Button, new() { Name = "Preparar aprobación", Exact = true }).ClickAsync(); Assert.Equal(baseline, await fixture.Front020RowsAsync()); await Capture(page, mobile, "confirmacion-aprobacion");
        var intent = await page.Locator("form[data-continuity-send] [name=intention]").InputValueAsync(); var csrf = await page.Locator("form[data-continuity-send] [name=__RequestVerificationToken]").InputValueAsync();
        var competitor = await context.NewPageAsync(); await competitor.GotoAsync("/continuidad/reconciliaciones/" + matched); await competitor.Locator("[name=reason]").FillAsync("Aceptación concurrente sintética"); await competitor.GetByRole(AriaRole.Button, new() { Name = "Preparar aprobación", Exact = true }).ClickAsync();
        var staleIntent = await competitor.Locator("form[data-continuity-send] [name=intention]").InputValueAsync(); var staleCsrf = await competitor.Locator("form[data-continuity-send] [name=__RequestVerificationToken]").InputValueAsync();
        await page.Locator("form[data-continuity-send] button").ClickAsync(); await Assertions.Expect(page.Locator("[data-front-success]")).ToContainTextAsync("Aceptación de la reconciliación registrada"); await Capture(page, mobile, "aprobada"); baseline = await fixture.Front020RowsAsync();
        var staleForm = context.APIRequest.CreateFormData(); staleForm.Set("intention", staleIntent); staleForm.Set("__RequestVerificationToken", staleCsrf);
        var stale = await context.APIRequest.PostAsync("/continuidad/reconciliaciones/" + matched + "?handler=Send", new() { Form = staleForm }); Assert.Equal(412, stale.Status); Assert.Equal(baseline, await fixture.Front020RowsAsync());
        await competitor.Locator("form[data-continuity-send] button").ClickAsync(); await Assertions.Expect(competitor.Locator("[data-work-error]")).ToBeVisibleAsync(); Assert.Equal(baseline, await fixture.Front020RowsAsync()); await Capture(competitor, mobile, "conflicto-412"); await competitor.CloseAsync();
        var form = context.APIRequest.CreateFormData(); form.Set("intention", intent); form.Set("__RequestVerificationToken", csrf);
        var replay = await context.APIRequest.PostAsync("/continuidad/reconciliaciones/" + matched + "?handler=Send", new() { Form = form }); Assert.Equal(200, replay.Status); Assert.Contains("Se recuperó la misma operación", System.Net.WebUtility.HtmlDecode(await replay.TextAsync())); Assert.Equal(baseline, await fixture.Front020RowsAsync());
        await using var noJs = await browser.NewContextAsync(new() { BaseURL = fixture.BaseAddress.AbsoluteUri, IgnoreHTTPSErrors = true, JavaScriptEnabled = false });
        await noJs.AddCookiesAsync([new() { Name = "__Host-SGOL-Session", Value = tickets[0], Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]); var plain = await noJs.NewPageAsync(); await plain.GotoAsync("/continuidad"); await plain.Locator("[name=reason]").FillAsync("Cancelación sintética"); await plain.GetByRole(AriaRole.Button, new() { Name = "Preparar solicitud", Exact = true }).ClickAsync(); await plain.GetByRole(AriaRole.Button, new() { Name = "Cancelar", Exact = true }).ClickAsync(); Assert.Equal(baseline, await fixture.Front020RowsAsync());
    }
    private static async Task Capture(IPage page, bool mobile, string state)
    {
        var directory = Environment.GetEnvironmentVariable("SGOL_FRONT020_CAPTURE_DIR"); if (string.IsNullOrEmpty(directory)) return;
        var path = Path.GetFullPath(directory); if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Fuentes", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Protected output path.");
        Directory.CreateDirectory(path); await page.ScreenshotAsync(new() { Path = Path.Combine(path, $"{(mobile ? "movil" : "escritorio")}-{state}.png"), FullPage = true });
    }
}
