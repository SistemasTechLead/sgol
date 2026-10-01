using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front019BrowserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task SuperiorValidatesReplacesSupervisesOnlyInferiorsAndPreservesHistory(bool mobile)
    {
        await using var fixture = new BrowserFixture(); await fixture.StartAsync();
        var tickets = new Dictionary<Guid, string>(); foreach (var account in fixture.Accounts) tickets.Add(account.UserId, await fixture.AuthenticateAsync(account));
        var tasks = await fixture.SeedMyWorkAsync(validationPolicies: true);
        using var playwright = await Playwright.CreateAsync(); await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { BaseURL = fixture.BaseAddress.AbsoluteUri, ViewportSize = new() { Width = mobile ? 390 : 1440, Height = mobile ? 844 : 900 }, IsMobile = mobile, HasTouch = mobile, Locale = "es-MX", IgnoreHTTPSErrors = true, ReducedMotion = ReducedMotion.Reduce, ServiceWorkers = ServiceWorkerPolicy.Block });
        async Task Login(int index) { await context.ClearCookiesAsync(); await context.AddCookiesAsync([new Cookie { Name = "__Host-SGOL-Session", Value = tickets[fixture.Accounts[index].UserId], Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]); }
        await Login(1); var page = await context.NewPageAsync(); var before = await fixture.Front019RowsAsync();
        Assert.Equal(200, (await page.GotoAsync("/validaciones"))!.Status);
        await Assertions.Expect(page.Locator("#pendientes tbody")).ToContainTextAsync("SUBCOORDINACION");
        Assert.DoesNotContain("Persona sintética DIRECCION", await page.Locator("#supervision tbody").InnerTextAsync());
        Assert.DoesNotContain("Persona sintética ADMINISTRACION", await page.Locator("#supervision tbody").InnerTextAsync());
        Assert.Equal(before, await fixture.Front019RowsAsync()); await Capture(page, mobile, "administracion-pendientes"); await Front018BrowserTests.CheckAccessibilityAsync(page);
        await page.GotoAsync("/validaciones?supervisionlimit=1");
        var firstRow = await page.Locator("#supervision tbody").InnerTextAsync();
        await page.Locator("#supervision a[rel=next]").ClickAsync();
        Assert.NotEqual(firstRow, await page.Locator("#supervision tbody").InnerTextAsync());
        await page.Locator("#supervision a[rel=prev]").ClickAsync();
        Assert.Equal(firstRow, await page.Locator("#supervision tbody").InnerTextAsync());
        await page.GotoAsync("/validaciones");
        // Pause only navigation after the application's real submit listeners render their loading state.
        await page.Locator("#pendientes form").EvaluateAsync("form => { form.addEventListener('submit', event => event.preventDefault(), { once: true }); form.requestSubmit(); }");
        await Assertions.Expect(page.Locator("#pendientes [role=status]")).ToContainTextAsync("Consultando pendientes");
        await Assertions.Expect(page.Locator("#pendientes button[type=submit]")).ToBeDisabledAsync();
        await Capture(page, mobile, "cargando"); await page.ReloadAsync();
        var task = tasks.Single(t => t.UserId == fixture.Accounts[2].UserId && t.State == "CONCLUIDA"); var path = "/mi-trabajo/tareas/" + task.Id.ToString("D");
        await page.Locator("#pending-" + task.Id.ToString("D")).ClickAsync();
        await Assertions.Expect(page.Locator("#validaciones-historia")).ToContainTextAsync("Aún no hay decisiones");
        Assert.Equal(before, await fixture.Front019RowsAsync()); await Capture(page, mobile, "historia-vacia");
        var invalid = await context.APIRequest.PostAsync(path + "?handler=PrepareValidation", new() { Form = context.APIRequest.CreateFormData().Set("__RequestVerificationToken", await Csrf(page)).Set("mode", "issue").Set("result", "OTHER").Set("foundation", "Fundamento sintético") });
        Assert.Equal(422, invalid.Status); Assert.Equal(before, await fixture.Front019RowsAsync());
        await Prepare(page, "INCOMPLETA", null); Assert.Equal(before, await fixture.Front019RowsAsync());
        await Assertions.Expect(page.Locator("dialog[open] button").First).ToBeFocusedAsync(); await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.Locator("dialog[open] button[type=submit]")).ToBeFocusedAsync(); await page.Keyboard.PressAsync("Shift+Tab");
        await Assertions.Expect(page.Locator("dialog[open] button").First).ToBeFocusedAsync();
        await Capture(page, mobile, "confirmacion-inicial"); await Front018BrowserTests.CheckAccessibilityAsync(page);
        var intent = await page.Locator("form[action*='SendValidation'] input[name=intention]").InputValueAsync(); var csrf = await Csrf(page);
        await fixture.Front019RejectAuditAsync(true); await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("#validaciones-historia [data-front-error]")).ToBeVisibleAsync(); Assert.Equal(before, await fixture.Front019RowsAsync());
        await Capture(page, mobile, "resultado-incierto"); await fixture.Front019RejectAuditAsync(false);
        await page.Locator("form[data-front-confirm] > button").ClickAsync(); await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("#validaciones-historia [data-front-success]")).ToContainTextAsync("misma solicitud");
        var committed = await fixture.Front019RowsAsync(); Assert.Equal(200, (await Post(context, path, csrf, intent)).Status); Assert.Equal(committed, await fixture.Front019RowsAsync());
        await page.GotoAsync(path); await Assertions.Expect(page.Locator("#validaciones-historia")).ToContainTextAsync("Incompleta"); await Capture(page, mobile, "incompleta");
        await Prepare(page, "NO_CUMPLIDA", "Corrección sintética fundada"); intent = await page.Locator("form[action*='SendValidation'] input[name=intention]").InputValueAsync(); csrf = await Csrf(page);
        var competitor = await context.NewPageAsync(); await competitor.GotoAsync(path); await Prepare(competitor, "CUMPLIDA", "Nueva revisión sintética");
        await competitor.Locator("dialog[open] button[type=submit]").ClickAsync(); var changed = await fixture.Front019RowsAsync();
        var stale = await Post(context, path, csrf, intent); Assert.Equal(412, stale.Status); Assert.Equal(changed, await fixture.Front019RowsAsync());
        Assert.Contains("Recargar validaciones", await stale.TextAsync()); await competitor.CloseAsync();
        await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("#validaciones-historia [data-front-error]")).ToContainTextAsync("cambió");
        await Capture(page, mobile, "conflicto-412"); Assert.Equal(changed, await fixture.Front019RowsAsync());
        await page.GotoAsync(path); await Prepare(page, "NO_CUMPLIDA", "Corrección sintética motivada");
        await Capture(page, mobile, "confirmacion-sustitucion"); await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("#validaciones-historia")).ToContainTextAsync("No cumplida");
        await Assertions.Expect(page.Locator("#validaciones-historia")).ToContainTextAsync("Sustituida"); await Capture(page, mobile, "historia-tres-resultados");
        Assert.Equal(3, await page.Locator("#validaciones-historia tbody tr").CountAsync()); await Front018BrowserTests.CheckAccessibilityAsync(page);
        await page.GotoAsync("/validaciones?pendingresponsiblePersonId=" + Guid.NewGuid().ToString("D")); await Capture(page, mobile, "filtro-sin-coincidencias");
        await Assertions.Expect(page.Locator("#pendientes")).ToContainTextAsync("No hay coincidencias");
        await page.GotoAsync("/validaciones?pendingisoYear=2026"); Assert.Equal(400, (await page.ReloadAsync())!.Status); await Capture(page, mobile, "error-filtro");
        await Login(2); await page.GotoAsync("/validaciones"); await Capture(page, mobile, "subcoordinacion-supervision");
        await Login(3); Assert.Equal(403, (await page.GotoAsync("/validaciones"))!.Status); await Capture(page, mobile, "piso-denegado");
        await Login(0); var escalated = tasks.Single(t => t.UserId == fixture.Accounts[2].UserId && t.State == "DISPONIBLE"); await fixture.Front019ConcludeAsync(escalated.Id);
        await page.GotoAsync("/validaciones"); await Capture(page, mobile, "direccion-supervision");
        await page.Locator("#pending-" + escalated.Id.ToString("D")).ClickAsync(); await Prepare(page, "CUMPLIDA", "Intervención sintética de Dirección");
        await Capture(page, mobile, "confirmacion-escalamiento"); await page.Locator("dialog[open] button[type=submit]").ClickAsync(); await Capture(page, mobile, "escalamiento-confirmado");
        var own = tasks.Single(t => t.UserId == fixture.Accounts[0].UserId && t.State == "CONCLUIDA"); await page.GotoAsync("/mi-trabajo/tareas/" + own.Id.ToString("D"));
        await Assertions.Expect(page.Locator("#validacion-decision")).ToContainTextAsync("Autovalidación excepcional de Dirección");
        await Prepare(page, "CUMPLIDA", null); await Capture(page, mobile, "confirmacion-autovalidacion-direccion");
        await page.Keyboard.PressAsync("Escape"); await Assertions.Expect(page.Locator("form[data-front-confirm] > button")).ToBeFocusedAsync();
        await page.Locator("form[data-front-confirm] > button").ClickAsync(); await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Capture(page, mobile, "autovalidacion-direccion-confirmada");
        await using var noJs = await browser.NewContextAsync(new() { BaseURL = fixture.BaseAddress.AbsoluteUri, JavaScriptEnabled = false, IgnoreHTTPSErrors = true });
        await noJs.AddCookiesAsync([new Cookie { Name = "__Host-SGOL-Session", Value = tickets[fixture.Accounts[1].UserId], Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]);
        var plain = await noJs.NewPageAsync(); await plain.GotoAsync(path); await Prepare(plain, "INCOMPLETA", "Revisión sintética sin JavaScript");
        await plain.Locator("form[action*='SendValidation'] button[type=submit]").ClickAsync(); await Assertions.Expect(plain.Locator("[data-front-success]")).ToContainTextAsync("sustituida");
    }
    private static async Task Prepare(IPage page, string result, string? reason)
    {
        await page.Locator("#result-" + result).CheckAsync(); await page.Locator("#foundation").FillAsync("Fundamento sintético de la revisión");
        if (reason is not null) await page.Locator("#reason").FillAsync(reason);
        await page.Locator("form[action*='PrepareValidation'] button[type=submit]").ClickAsync();
    }
    private static Task<string> Csrf(IPage page) => page.Locator("input[name=__RequestVerificationToken]").First.InputValueAsync();
    private static Task<IAPIResponse> Post(IBrowserContext context, string path, string csrf, string intent) => context.APIRequest.PostAsync(path + "?handler=SendValidation", new() { Form = context.APIRequest.CreateFormData().Set("__RequestVerificationToken", csrf).Set("intention", intent).Set("recovery", "true") });
    private static async Task Capture(IPage page, bool mobile, string state)
    {
        var directory = Environment.GetEnvironmentVariable("SGOL_FRONT019_CAPTURE_DIR"); if (string.IsNullOrEmpty(directory)) return;
        var path = Path.GetFullPath(directory); if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Fuentes", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Protected output path.");
        Directory.CreateDirectory(path); await page.ScreenshotAsync(new() { Path = Path.Combine(path, $"{(mobile ? "movil" : "escritorio")}-{state}.png"), FullPage = true });
    }
}
