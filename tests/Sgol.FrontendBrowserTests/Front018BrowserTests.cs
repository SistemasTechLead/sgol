using Microsoft.Playwright;
using System.Text.Json;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed partial class Front018BrowserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task VersionsExplicitReviewReplacementAndConclusionPreserveAuthorityHistoryAndRecovery(bool mobile)
    {
        await using var fixture = new BrowserFixture(); await fixture.StartAsync();
        var tickets = new Dictionary<Guid, string>();
        foreach (var author in fixture.Accounts) tickets.Add(author.UserId, await fixture.AuthenticateAsync(author));
        var tasks = await fixture.SeedMyWorkAsync(taskCode: "TAR-0007");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { BaseURL = fixture.BaseAddress.AbsoluteUri, ViewportSize = new() { Width = mobile ? 390 : 1440, Height = mobile ? 844 : 900 }, IsMobile = mobile, HasTouch = mobile, Locale = "es-MX", IgnoreHTTPSErrors = true, ReducedMotion = ReducedMotion.Reduce, ServiceWorkers = ServiceWorkerPolicy.Block });
        var account = fixture.Accounts[3];
        async Task LoginAsync(int index) { await context.ClearCookiesAsync(); await context.AddCookiesAsync([new Cookie { Name = "__Host-SGOL-Session", Value = tickets[fixture.Accounts[index].UserId], Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]); }
        await LoginAsync(3);
        var task = tasks.Single(t => t.UserId == account.UserId && t.State == "DISPONIBLE");
        var path = "/mi-trabajo/tareas/" + task.Id.ToString("D"); var page = await context.NewPageAsync();
        var before = await fixture.MyWorkRowsAsync();
        Assert.Equal(200, (await page.GotoAsync(path))!.Status);
        await Assertions.Expect(page.Locator("#versiones-evidencia")).ToContainTextAsync("Aún no hay versiones");
        await Assertions.Expect(page.Locator("#conclusion-tarea button")).ToBeDisabledAsync();
        Assert.Equal(before, await fixture.MyWorkRowsAsync()); await CaptureAsync(page, mobile, "vacio");
        await CheckAccessibilityAsync(page);
        var csrf = await CsrfAsync(page);
        var denied = await context.APIRequest.PostAsync(path + "?handler=ReviewEvidence", new() { Form = context.APIRequest.CreateFormData() });
        Assert.Equal(400, denied.Status); Assert.Equal(before, await fixture.MyWorkRowsAsync());
        var business = await fixture.Front018BusinessRowsAsync(); var counts = await fixture.Front018CountsAsync(task.Id);
        await page.GetByRole(AriaRole.Button, new() { Name = "Consultar revisión", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("#revision-evidencia")).ToContainTextAsync("Incompleta");
        Assert.Equal(business, await fixture.Front018BusinessRowsAsync());
        var reviewed = await fixture.Front018CountsAsync(task.Id);
        Assert.Equal(counts.Snapshots + 1, reviewed.Snapshots); Assert.Equal(counts.Reviews + 1, reviewed.Reviews);
        before = await fixture.MyWorkRowsAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Consultar revisión", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("#revision-evidencia")).ToContainTextAsync("Incompleta");
        Assert.Equal(before, await fixture.MyWorkRowsAsync()); await CaptureAsync(page, mobile, "faltantes");
        foreach (var requirement in new[] { "LIBERACION", "MERCANCIA", "FECHA_HORA", "RETORNO_EXHIBICION" })
        {
            await page.Locator("#evidence-requirement").SelectOptionAsync(requirement);
            await page.GetByRole(AriaRole.Button, new() { Name = "Seleccionar requisito" }).ClickAsync();
            foreach (var field in Sgol.Web.Presentation.MyWork.EvidenceContributionPresentation.Inputs(requirement))
                await page.Locator("[name=" + field.Name + "]").FillAsync(field.Type == "datetime-local" ? "2026-09-07T14:00" : "SINTETICA-01");
            await page.Locator("form[action*='PrepareEvidence'] button[type=submit]").ClickAsync();
            await page.Locator("form[action*='SendEvidence'] button[type=submit]").ClickAsync();
            await Assertions.Expect(page.Locator("[data-evidence-success]")).ToBeVisibleAsync();
            await page.GotoAsync(path);
        }
        before = await fixture.MyWorkRowsAsync();
        await page.Locator("#versions-status").SelectOptionAsync("SUSTITUIDA");
        await page.GetByRole(AriaRole.Button, new() { Name = "Consultar versiones" }).ClickAsync();
        await Assertions.Expect(page.Locator("#versiones-evidencia")).ToContainTextAsync("No hay versiones que coincidan");
        Assert.Equal(before, await fixture.MyWorkRowsAsync());
        await page.GotoAsync(path);
        await PrepareReplacementAsync(page, "PENDIENTE-02", null);
        var intent = await page.Locator("form[action*='SendReplacement'] input[name=intention]").InputValueAsync(); csrf = await CsrfAsync(page);
        await Assertions.Expect(page.Locator("dialog[open] button").First).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab"); await Assertions.Expect(page.Locator("dialog[open] button[type=submit]")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Shift+Tab"); await Assertions.Expect(page.Locator("dialog[open] button").First).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("form[data-front-confirm] > button")).ToBeFocusedAsync();
        Assert.Equal(before, await fixture.MyWorkRowsAsync());
        // A competing decision replaces the same original version; the first intent must freeze on 412.
        var competitor = await context.NewPageAsync(); await competitor.GotoAsync(path);
        await PrepareReplacementAsync(competitor, "COMPETIDORA-02", null);
        await competitor.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(competitor.Locator("[data-front-success]")).ToContainTextAsync("Sustitución confirmada");
        var committed = await fixture.MyWorkRowsAsync();
        var stale = await PostIntentAsync(context, path, "SendReplacement", csrf, intent);
        Assert.Equal(412, stale.Status); Assert.Equal(committed, await fixture.MyWorkRowsAsync());
        Assert.Contains("Recargar detalle", await stale.TextAsync());
        await competitor.CloseAsync(); await page.GotoAsync(path);
        await PrepareReplacementAsync(page, "PENDIENTE-03", null);
        intent = await page.Locator("form[action*='SendReplacement'] input[name=intention]").InputValueAsync(); csrf = await CsrfAsync(page);
        await CaptureAsync(page, mobile, "confirmacion-sustitucion"); await CheckAccessibilityAsync(page);
        await fixture.Front018RejectAuditAsync("EVIDENCE_REPLACED", true); before = await fixture.MyWorkRowsAsync();
        await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-front-error]")).ToBeVisibleAsync();
        Assert.Equal(before, await fixture.MyWorkRowsAsync());
        await fixture.Front018RejectAuditAsync("EVIDENCE_REPLACED", false);
        await page.Locator("form[data-front-confirm] > button").ClickAsync(); await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-front-success]")).ToContainTextAsync("Sustitución confirmada");
        committed = await fixture.MyWorkRowsAsync();
        Assert.Equal(200, (await PostIntentAsync(context, path, "SendReplacement", csrf, intent)).Status); Assert.Equal(committed, await fixture.MyWorkRowsAsync());
        await CaptureAsync(page, mobile, "versiones");
        await page.GetByRole(AriaRole.Button, new() { Name = "Consultar revisión", Exact = true }).ClickAsync();
        await Assertions.Expect(page.Locator("#revision-evidencia")).ToContainTextAsync("Completa"); await CaptureAsync(page, mobile, "completa");
        before = await fixture.MyWorkRowsAsync();
        await page.Locator("form[action*='PrepareConclusion'] button").ClickAsync();
        await Assertions.Expect(page.Locator("dialog[open] button").First).ToBeFocusedAsync();
        Assert.Equal(before, await fixture.MyWorkRowsAsync());
        var conclusion = await page.Locator("form[action*='Conclude'] input[name=intention]").InputValueAsync(); csrf = await CsrfAsync(page);
        await CaptureAsync(page, mobile, "confirmacion-conclusion");
        await fixture.Front018RejectAuditAsync("OBLIGATION_CONCLUDED", true);
        await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-front-error]")).ToBeVisibleAsync(); Assert.Equal(before, await fixture.MyWorkRowsAsync());
        await fixture.Front018RejectAuditAsync("OBLIGATION_CONCLUDED", false);
        await page.Locator("form[data-front-confirm] > button").ClickAsync(); await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-front-success]")).ToContainTextAsync("Conclusión confirmada");
        Assert.Equal(0, await page.Locator("#aportar-evidencia").CountAsync()); Assert.Equal(0, await page.GetByRole(AriaRole.Button, new() { Name = "Preparar sustitución" }).CountAsync());
        committed = await fixture.MyWorkRowsAsync(); Assert.Equal(200, (await PostIntentAsync(context, path, "Conclude", csrf, conclusion)).Status);
        Assert.Equal(committed, await fixture.MyWorkRowsAsync()); await CaptureAsync(page, mobile, "concluida");
        Assert.Equal(404, (await PostIntentAsync(context, path, "SendReplacement", csrf, intent)).Status);
        Assert.Equal(committed, await fixture.MyWorkRowsAsync());
        var tampered = await PostIntentAsync(context, path, "Conclude", csrf, conclusion[..^8] + "tampered");
        Assert.Equal(400, tampered.Status); Assert.Contains("data-front-error", await tampered.TextAsync()); Assert.Equal(committed, await fixture.MyWorkRowsAsync());
        await LoginAsync(0); await page.GotoAsync(path);
        Assert.Equal(0, await page.Locator("form[action*='PrepareConclusion']").CountAsync());
        await SelectReplacementAsync(page); await page.Locator("[name=merchandiseReference]").FillAsync("SUPERIOR-04");
        var required = page.Locator("textarea[name=reason]"); Assert.True(await required.EvaluateAsync<bool>("e => e.required"));
        csrf = await CsrfAsync(page); var selected = await page.Locator("input[name=replacement]").InputValueAsync();
        before = await fixture.MyWorkRowsAsync();
        var invalidReason = await context.APIRequest.PostAsync(path + "?handler=PrepareReplacement", new() { Form = context.APIRequest.CreateFormData()
            .Set("__RequestVerificationToken", csrf).Set("replacement", selected).Set("requirementCode", "MERCANCIA").Set("merchandiseReference", "SUPERIOR-04").Set("reason", "") });
        Assert.Equal(422, invalidReason.Status); Assert.Equal(before, await fixture.MyWorkRowsAsync());
        await required.FillAsync("Revisión sintética autorizada"); await page.Locator("form[action*='PrepareReplacement'] button[type=submit]").ClickAsync();
        await page.Locator("dialog[open] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-front-success]")).ToContainTextAsync("Sustitución confirmada");
        await Assertions.Expect(page.Locator("#conclusion-tarea")).ToContainTextAsync("La tarea está concluida");
        await CaptureAsync(page, mobile, "sustitucion-superior"); await CheckAccessibilityAsync(page);
        await using (var noJs = await browser.NewContextAsync(new() { BaseURL = fixture.BaseAddress.AbsoluteUri, JavaScriptEnabled = false, IgnoreHTTPSErrors = true }))
        {
            await noJs.AddCookiesAsync([new Cookie { Name = "__Host-SGOL-Session", Value = tickets[fixture.Accounts[0].UserId], Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]);
            var fallback = await noJs.NewPageAsync(); await fallback.GotoAsync(path); before = await fixture.MyWorkRowsAsync();
            await PrepareReplacementAsync(fallback, "SIN-JS-05", "Decisión sintética");
            await Assertions.Expect(fallback.Locator("form[data-front-confirm] button[type=submit]")).ToBeVisibleAsync();
            Assert.Equal(0, await fallback.Locator("form[data-front-confirm] dialog").CountAsync()); Assert.Equal(before, await fixture.MyWorkRowsAsync());
        }
        Assert.Equal(counts.Conclusions + 1, (await fixture.Front018CountsAsync(task.Id)).Conclusions);
        if (!mobile)
        {
            csrf = await CsrfAsync(page);
            using var listed = JsonDocument.Parse(await (await context.APIRequest.GetAsync($"/api/v1/obligations/{task.Id:D}/evidence?requirementCode=MERCANCIA&status=VIGENTE")).TextAsync());
            var current = listed.RootElement.GetProperty("data")[0]; var itemId = current.GetProperty("evidenceItemId").GetGuid(); var version = current.GetProperty("itemRowVersion").GetInt64();
            for (var index = 0; index < 26; index++)
            {
                var replaced = await context.APIRequest.PostAsync($"/api/v1/obligations/{task.Id:D}/evidence/{itemId:D}/replacements", new()
                {
                    Headers = new Dictionary<string, string> { ["X-CSRF-TOKEN"] = csrf, ["If-Match"] = $"\"{version}\"", ["Idempotency-Key"] = Guid.NewGuid().ToString("D") },
                    DataObject = new { structuredPayload = new { schemaVersion = 1, merchandiseReference = "CURSOR-SINTETICO-" + index }, reason = "Historia sintética para cursor" }
                });
                Assert.Equal(201, replaced.Status); using var payload = JsonDocument.Parse(await replaced.TextAsync()); version = payload.RootElement.GetProperty("data").GetProperty("itemRowVersion").GetInt64();
            }
            before = await fixture.MyWorkRowsAsync(); await page.GotoAsync(path + "?evidenceRequirement=MERCANCIA");
            var next = page.Locator("#versiones-evidencia [rel=next]"); var href = await next.GetAttributeAsync("href");
            await next.ClickAsync(); Assert.True(await page.Locator("#versiones-evidencia tbody tr").CountAsync() > 0);
            await Assertions.Expect(page.Locator("#versiones-evidencia [rel=prev]")).ToBeVisibleAsync(); Assert.Equal(before, await fixture.MyWorkRowsAsync());
            await CaptureAsync(page, mobile, "cursor");
            await page.GotoAsync(href!.Replace("evidenceRequirement=MERCANCIA", "evidenceRequirement=LIBERACION", StringComparison.Ordinal));
            await Assertions.Expect(page.Locator("[data-front-error]")).ToContainTextAsync("Revisa los filtros"); Assert.Equal(before, await fixture.MyWorkRowsAsync());
        }
        await page.GotoAsync(path + "?evidenceStatus=INVALIDO");
        await Assertions.Expect(page.Locator("[data-front-error]")).ToContainTextAsync("Revisa los filtros"); await CaptureAsync(page, mobile, "filtro-invalido");
    }

    private static async Task<string> CsrfAsync(IPage page) => await page.Locator("input[name=__RequestVerificationToken]").First.InputValueAsync();
    private static Task<IAPIResponse> PostIntentAsync(IBrowserContext context, string path, string handler, string csrf, string token) =>
        context.APIRequest.PostAsync(path + "?handler=" + handler, new() { Form = context.APIRequest.CreateFormData().Set("__RequestVerificationToken", csrf).Set("intention", token) });
    private static async Task SelectReplacementAsync(IPage page) => await page.Locator("#versiones-evidencia tr").Filter(new() { HasText = "MERCANCIA" }).GetByRole(AriaRole.Button, new() { Name = "Preparar sustitución" }).ClickAsync();
    private static async Task PrepareReplacementAsync(IPage page, string reference, string? reason)
    {
        await SelectReplacementAsync(page); await page.Locator("[name=merchandiseReference]").FillAsync(reference);
        if (reason is not null) await page.Locator("[name=reason]").FillAsync(reason);
        await page.Locator("form[action*='PrepareReplacement'] button[type=submit]").ClickAsync();
    }
    private static async Task CheckAccessibilityAsync(IPage page)
    {
        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"));
        Assert.True(await page.EvaluateAsync<bool>("() => matchMedia('(prefers-reduced-motion: reduce)').matches"));
        Assert.True(await page.EvaluateAsync<bool>("() => [...document.querySelectorAll('.panel-seccion button:not(:disabled), .panel-seccion select, .panel-seccion textarea')].filter(e=>e.getClientRects().length).every(e=>e.getBoundingClientRect().height>=44)"));
        Assert.True(await page.EvaluateAsync<bool>(ContrastCheck));
        await page.EvaluateAsync("() => [...document.querySelectorAll('.evidencia__contenido p, .evidencia__contenido li, .evidencia__contenido .boton, .evidencia__contenido .campo__control, .modal p, .modal .boton')].forEach(e => { e.dataset.front018Enlarged=''; e.style.fontSize=(parseFloat(getComputedStyle(e).fontSize)*2)+'px'; })");
        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"));
        await page.EvaluateAsync("() => document.querySelectorAll('[data-front018-enlarged]').forEach(e=>{e.style.removeProperty('font-size');delete e.dataset.front018Enlarged;})");
        var size = page.ViewportSize!; await page.SetViewportSizeAsync(320, 844);
        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth")); await page.SetViewportSizeAsync(size.Width, size.Height);
    }
    private const string ContrastCheck = """
        () => {
          const luminance = color => { const v = color.match(/[\d.]+/g).slice(0,3).map(Number).map(x => { const c=x/255; return c<=.04045 ? c/12.92 : ((c+.055)/1.055)**2.4; }); return v[0]*.2126+v[1]*.7152+v[2]*.0722; };
          return [...document.querySelectorAll('.evidencia__contenido .campo__etiqueta, .evidencia__contenido .boton:not(:disabled), .evidencia__contenido .alerta, .modal .boton')].filter(e => e.getClientRects().length).every(e => {
            let parent=e, background=getComputedStyle(e).backgroundColor;
            while (background === 'rgba(0, 0, 0, 0)' && parent.parentElement) { parent=parent.parentElement; background=getComputedStyle(parent).backgroundColor; }
            const a=luminance(getComputedStyle(e).color), b=luminance(background);
            return (Math.max(a,b)+.05)/(Math.min(a,b)+.05)>=4.5;
          });
        }
        """;
    private static async Task CaptureAsync(IPage page, bool mobile, string state)
    {
        var directory = Environment.GetEnvironmentVariable("SGOL_FRONT018_CAPTURE_DIR"); if (string.IsNullOrEmpty(directory)) return;
        var path = Path.GetFullPath(directory);
        if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Fuentes", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Protected output path.");
        Directory.CreateDirectory(path); await page.ScreenshotAsync(new() { Path = Path.Combine(path, $"{(mobile ? "movil" : "escritorio")}-{state}.png"), FullPage = true });
    }
}
