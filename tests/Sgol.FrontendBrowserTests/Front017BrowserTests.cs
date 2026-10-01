using System.Text.Json;
using Microsoft.Playwright;
using Sgol.Testing;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front017BrowserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task PrivateUploadStructuredContributionRecoveryAndAuthorizationUseRealServices(bool mobile)
    {
        await using var evidence = new Front017EvidenceServices();
        await using var fixture = new BrowserFixture { ConfigureEvidence = evidence.StartAsync, Progress = stage => Progress(mobile, stage) };
        await fixture.StartAsync();
        Progress(mobile, "HOST_STARTED");
        var tickets = new Dictionary<Guid, string>();
        foreach (var author in fixture.Accounts) tickets.Add(author.UserId, await fixture.AuthenticateAsync(author));
        Progress(mobile, "ACCOUNTS_AUTHENTICATED");
        var tasks = await fixture.SeedMyWorkAsync(taskCode: mobile ? "TAR-0092" : "TAR-0008");
        using var playwright = await Playwright.CreateAsync();
        // Default coverage is desktop/mobile Chromium. WebKit remains opt-in until this
        // local HTTP S3 fixture has a TLS endpoint compatible with its HTTPS page transport.
        var engine = Environment.GetEnvironmentVariable("SGOL_FRONT017_WEBKIT") == "true" ? playwright.Webkit : playwright.Chromium;
        await using var browser = await engine.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { BaseURL = fixture.BaseAddress.AbsoluteUri, ViewportSize = new() { Width = mobile ? 390 : 1440, Height = mobile ? 844 : 900 }, IsMobile = mobile, HasTouch = mobile, Locale = "es-MX", IgnoreHTTPSErrors = true, ReducedMotion = ReducedMotion.Reduce, ServiceWorkers = ServiceWorkerPolicy.Block });
        var account = fixture.Accounts[3];
        await context.AddCookiesAsync([new Cookie { Name = "__Host-SGOL-Session", Value = tickets[account.UserId], Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]);
        var task = tasks.Single(t => t.UserId == account.UserId && t.State == "DISPONIBLE");
        var path = "/mi-trabajo/tareas/" + task.Id.ToString("D");
        var page = await context.NewPageAsync();
        var putStatus = 0;
        var putFailure = false;
        page.Response += (_, response) => { if (response.Request.Method == "PUT") putStatus = response.Status; };
        page.RequestFailed += (_, request) => { if (request.Method == "PUT") putFailure = true; };
        Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress, path).AbsoluteUri))!.Status);
        Assert.True(await page.Locator("#aportar-evidencia").IsVisibleAsync());
        await CaptureAsync(page, mobile, "selector");
        await CheckAccessibilityAsync(page);
        var before = await fixture.MyWorkRowsAsync();
        var csrf = await page.Locator("input[name=__RequestVerificationToken]").First.InputValueAsync();
        var denied = await context.APIRequest.PostAsync(path + "?handler=PrepareEvidence", new() { Form = context.APIRequest.CreateFormData().Set("requirementCode", "SECUENCIA").Set("sequenceSummary", "Sintética") });
        Assert.Equal(400, denied.Status);
        Assert.Equal(before, await fixture.MyWorkRowsAsync());
        if (mobile)
        {
            await SelectAsync(page, "FOTO_DIFERENCIA_DANO");
            Assert.Contains("Primero aporta un formulario F-ENT-001 válido", await page.Locator("#aportar-evidencia").InnerTextAsync());
            Assert.Equal(0, await page.Locator("input[type=file]").CountAsync());
            await CaptureAsync(page, mobile, "condicion-no-resuelta");
        }
        var structured = mobile ? "F_ENT_001" : "SECUENCIA";
        await SelectAsync(page, structured);
        csrf = await page.Locator("input[name=__RequestVerificationToken]").First.InputValueAsync();
        var extraField = await context.APIRequest.PostAsync(path + "?handler=PrepareEvidence", new()
        {
            Form = context.APIRequest.CreateFormData()
            .Set("__RequestVerificationToken", csrf).Set("requirementCode", structured).Set("schemaVersion", "2")
        });
        Assert.Equal(422, extraField.Status);
        Assert.Equal(before, await fixture.MyWorkRowsAsync());
        if (mobile)
        {
            await page.Locator("[name=formReference]").FillAsync("RECEPCION-SINTETICA-01");
            await page.Locator("[name=completedAt]").FillAsync("2026-09-07T14:00");
            await page.Locator("[name=hasDifference][value=true]").CheckAsync();
            await page.Locator("[name=hasDamage][value=false]").CheckAsync();
        }
        else await page.Locator("[name=sequenceSummary]").FillAsync("Secuencia sintética para FRONT-017.");
        await CaptureAsync(page, mobile, "formulario");
        await CheckAccessibilityAsync(page);
        await page.Locator("form[action*='PrepareEvidence'] button[type=submit]").ClickAsync();
        await page.WaitForSelectorAsync("input[name=intention]", new() { State = WaitForSelectorState.Attached });
        var intention = await page.Locator("input[name=intention]").InputValueAsync();
        csrf = await page.Locator("input[name=__RequestVerificationToken]").First.InputValueAsync();
        Assert.DoesNotContain("Secuencia sintética", intention);
        Assert.Equal(before, await fixture.MyWorkRowsAsync());
        if (!mobile)
        {
            await fixture.RejectContributionAuditAsync(true);
            await page.Locator("form[action*='SendEvidence'] button[type=submit]").ClickAsync();
            await page.WaitForSelectorAsync("[data-evidence-error]");
            Assert.Equal(before, await fixture.MyWorkRowsAsync());
            await CaptureAsync(page, mobile, "error-auditoria");
            await fixture.RejectContributionAuditAsync(false);
        }
        await page.Locator("form[action*='SendEvidence'] button[type=submit]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-evidence-success]")).ToContainTextAsync("Se aportó la evidencia");
        await Assertions.Expect(page.Locator("[data-evidence-success]")).ToBeFocusedAsync();
        var committed = await fixture.MyWorkRowsAsync();
        var replay = await context.APIRequest.PostAsync(path + "?handler=SendEvidence", new() { Form = context.APIRequest.CreateFormData().Set("__RequestVerificationToken", csrf).Set("intention", intention) });
        Assert.Equal(200, replay.Status);
        Assert.True(System.Net.WebUtility.HtmlDecode(await replay.TextAsync()).Contains("Se recuperó la aportación registrada.", StringComparison.Ordinal));
        Assert.Equal(committed, await fixture.MyWorkRowsAsync());
        Assert.Equal(1, (await fixture.EvidenceCountsAsync(task.Id)).Contributions);
        await CaptureAsync(page, mobile, "aporte-estructurado");

        var binary = mobile ? "DOCUMENTO_RECEPCION" : "EXPEDIENTE";
        await SelectAsync(page, binary);
        if (mobile) await page.Locator("[name=documentSubtype]").SelectOptionAsync("REMISION");
        await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "sintetico.txt", MimeType = "text/plain", Buffer = [1] });
        await Assertions.Expect(page.Locator("[data-upload-error]")).ToContainTextAsync("tipo de archivo");
        await CaptureAsync(page, mobile, "tipo-rechazado");
        Assert.Equal(committed, await fixture.MyWorkRowsAsync());
        await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "sintetico.pdf", MimeType = "application/pdf", Buffer = new byte[15728641] });
        await Assertions.Expect(page.Locator("[data-upload-error]")).ToContainTextAsync("supera 15 MiB");
        Assert.Equal(committed, await fixture.MyWorkRowsAsync());
        await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "expediente-sintetico.pdf", MimeType = "application/pdf", Buffer = EvidenceCorpus.Pdf() });
        var uploadResponse = await page.RunAndWaitForResponseAsync(() => page.Locator("[data-upload-start]").ClickAsync(),
            r => r.Url.Contains("handler=Upload", StringComparison.Ordinal));
        using var issued = JsonDocument.Parse(await uploadResponse.BodyAsync());
        var flow = issued.RootElement.GetProperty("intention").GetString()!;
        await page.WaitForFunctionAsync("() => document.querySelector('[data-upload-status]').textContent.includes('Esperando análisis') || !document.querySelector('[data-upload-error]').hidden", null, new() { Timeout = 35000 });
        Assert.True(await page.Locator("[data-upload-error]").IsHiddenAsync(), $"Synthetic signed PUT: HTTP {putStatus}; transport failure {putFailure}.");
        Assert.True(await page.Locator("[data-upload-contribute]").IsHiddenAsync());
        Assert.Equal(0, (await fixture.EvidenceCountsAsync(task.Id)).Linked);
        var markup = await page.ContentAsync();
        Assert.False(markup.Contains("X-Amz-", StringComparison.OrdinalIgnoreCase));
        Assert.False(markup.Contains("sgol-sha256", StringComparison.Ordinal));
        var pendingBefore = await fixture.MyWorkRowsAsync();
        csrf = await page.Locator("input[name=__RequestVerificationToken]").First.InputValueAsync();
        var premature = await context.APIRequest.PostAsync(path + "?handler=ContributeFile", new() { Form = context.APIRequest.CreateFormData().Set("__RequestVerificationToken", csrf).Set("intention", flow) });
        Assert.Equal(409, premature.Status);
        Assert.Equal(pendingBefore, await fixture.MyWorkRowsAsync());
        await CaptureAsync(page, mobile, "pendiente-escaneo");
        await fixture.InspectEvidenceAsync(task.Id, evidence.Pipeline);
        Progress(mobile, "REAL_INSPECTION_FINISHED");
        await page.Locator("[data-upload-refresh]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-upload-contribute]")).ToBeEnabledAsync();
        await CaptureAsync(page, mobile, "limpio");
        await CheckAccessibilityAsync(page);
        var cleanBefore = await fixture.MyWorkRowsAsync();
        await page.Locator("[data-upload-refresh]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-upload-contribute]")).ToBeEnabledAsync();
        Assert.Equal(cleanBefore, await fixture.MyWorkRowsAsync());
        var lostResponse = false;
        const string contributionRoute = "**/*handler=ContributeFile*";
        await context.RouteAsync(contributionRoute, async route =>
        {
            if (lostResponse) { await route.ContinueAsync(); return; }
            lostResponse = true;
            var received = await route.FetchAsync(); await received.DisposeAsync(); await route.AbortAsync();
        });
        await page.Locator("[data-upload-contribute]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-upload-recover]")).ToBeVisibleAsync();
        Assert.Equal(1, (await fixture.EvidenceCountsAsync(task.Id)).Linked);
        await context.UnrouteAsync(contributionRoute);
        await page.Locator("[data-upload-recover]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-upload-status]")).ToContainTextAsync("Se recuperó la aportación registrada");
        await Assertions.Expect(page.Locator(".subida")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("subida--error"));
        await Assertions.Expect(page.Locator("[data-upload-error]")).ToBeHiddenAsync();
        Assert.True(await page.Locator("[data-evidence-empty]").IsHiddenAsync());
        var counts = await fixture.EvidenceCountsAsync(task.Id);
        Assert.Equal((2, 2, 1, 1, 1, 2, 1), counts);
        await CaptureAsync(page, mobile, "aporte-archivo");
        if (mobile)
        {
            await SelectAsync(page, "FOTO_DIFERENCIA_DANO");
            await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "fotografia-sintetica.png", MimeType = "image/png", Buffer = EvidenceCorpus.Png() });
            await page.Locator("[data-upload-start]").ClickAsync();
            await Assertions.Expect(page.Locator("[data-upload-status]")).ToContainTextAsync("Esperando análisis", new() { Timeout = 30000 });
            await fixture.InspectEvidenceAsync(task.Id, evidence.Pipeline);
            await page.Locator("[data-upload-refresh]").ClickAsync();
            await Assertions.Expect(page.Locator("[data-upload-contribute]")).ToBeEnabledAsync();
            await page.Locator("[data-upload-contribute]").ClickAsync();
            await Assertions.Expect(page.Locator("[data-upload-status]")).ToContainTextAsync("Se aportó la evidencia");
            await CaptureAsync(page, mobile, "fotografia-condicional-aportada");
            await page.GotoAsync(new Uri(fixture.BaseAddress, path).AbsoluteUri);
            await Assertions.Expect(page.Locator("#aportar-evidencia")).ToContainTextAsync("No hay requisitos disponibles para una primera aportación.");
            await CaptureAsync(page, mobile, "vacio-sin-primeras-aportaciones");
        }
        else
        {
            var future = tasks.Single(t => t.UserId == account.UserId && t.State == "FUTURA");
            await page.GotoAsync(new Uri(fixture.BaseAddress, "/mi-trabajo/tareas/" + future.Id).AbsoluteUri);
            await SelectAsync(page, "EXPEDIENTE");
            await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "firma-invalida-sintetica.pdf", MimeType = "application/pdf", Buffer = EvidenceCorpus.InvalidSignature() });
            await page.Locator("[data-upload-start]").ClickAsync();
            await Assertions.Expect(page.Locator("[data-upload-status]")).ToContainTextAsync("Esperando análisis", new() { Timeout = 30000 });
            await fixture.InspectEvidenceAsync(future.Id, evidence.Pipeline);
            await page.Locator("[data-upload-refresh]").ClickAsync();
            await Assertions.Expect(page.Locator("[data-upload-status]")).ToContainTextAsync("no cumple el tipo");
            Assert.True(await page.Locator("[data-upload-contribute]").IsHiddenAsync());
            Assert.Equal(0, (await fixture.EvidenceCountsAsync(future.Id)).Linked);
            Assert.Equal(0, (await fixture.EvidenceCountsAsync(future.Id)).Items);
            await CaptureAsync(page, mobile, "firma-real-invalida");
        }
        // Permission alone does not authorize contribution on another person's task or on a concluded task.
        var other = tasks.First(t => t.UserId == fixture.Accounts[0].UserId && t.State == "DISPONIBLE");
        Assert.Equal(404, (await page.GotoAsync(new Uri(fixture.BaseAddress, "/mi-trabajo/tareas/" + other.Id).AbsoluteUri))!.Status);
        Assert.Equal(0, await page.Locator("#aportar-evidencia").CountAsync());
        var concluded = tasks.Single(t => t.UserId == account.UserId && t.State == "CONCLUIDA");
        await page.GotoAsync(new Uri(fixture.BaseAddress, "/mi-trabajo/tareas/" + concluded.Id).AbsoluteUri);
        Assert.Equal(0, await page.Locator("#aportar-evidencia").CountAsync());
        foreach (var own in fixture.Accounts.Take(3))
        {
            await context.ClearCookiesAsync();
            await context.AddCookiesAsync([new Cookie { Name = "__Host-SGOL-Session", Value = tickets[own.UserId], Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]);
            var ownTask = tasks.Single(t => t.UserId == own.UserId && t.State == "DISPONIBLE");
            await page.GotoAsync(new Uri(fixture.BaseAddress, "/mi-trabajo/tareas/" + ownTask.Id).AbsoluteUri);
            await SelectAsync(page, structured);
            Assert.True(await page.Locator("form[action*='PrepareEvidence']").IsVisibleAsync());
        }
    }

    private static async Task SelectAsync(IPage page, string requirement)
    {
        await page.Locator("#evidence-requirement").SelectOptionAsync(requirement);
        await page.GetByRole(AriaRole.Button, new() { Name = "Seleccionar requisito" }).ClickAsync();
        await Assertions.Expect(page.Locator("#aportar-evidencia")).ToHaveAttributeAsync("data-selected-requirement", requirement);
    }

    private static async Task CheckAccessibilityAsync(IPage page)
    {
        Assert.True(await page.EvaluateAsync<bool>("() => { const ids = [...document.querySelectorAll('[id]')].map(e => e.id); return new Set(ids).size === ids.length && [...document.querySelectorAll('[aria-describedby]')].every(e => e.getAttribute('aria-describedby').split(' ').every(id => document.getElementById(id))); }"));
        var dimensions = await page.EvaluateAsync<int[]>("() => [innerWidth, document.documentElement.scrollWidth]");
        Assert.True(dimensions[1] <= dimensions[0]);
        var control = page.Locator("#evidence-requirement");
        if (await control.IsEnabledAsync())
        {
            await control.FocusAsync(); await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Seleccionar requisito" })).ToBeFocusedAsync();
            Assert.True(await control.EvaluateAsync<bool>("e => e.getBoundingClientRect().height >= 44"));
        }
        Assert.True(await page.EvaluateAsync<bool>(ContrastCheck));
        Assert.True(await page.EvaluateAsync<bool>("() => matchMedia('(prefers-reduced-motion: reduce)').matches"));
        await page.EvaluateAsync("() => { const nodes = [...document.querySelectorAll('#aportar-evidencia p, #aportar-evidencia li, #aportar-evidencia h2, #aportar-evidencia h3, #aportar-evidencia .campo__etiqueta, #aportar-evidencia .campo__control, #aportar-evidencia .boton, #aportar-evidencia legend, #aportar-evidencia .grupo-radio__opcion span')].map(e => [e, parseFloat(getComputedStyle(e).fontSize)]); nodes.forEach(([e,size]) => { e.dataset.front017Enlarged = ''; e.style.fontSize = (size * 2) + 'px'; }); }");
        dimensions = await page.EvaluateAsync<int[]>("() => [innerWidth, document.documentElement.scrollWidth]");
        Assert.True(dimensions[1] <= dimensions[0]);
        await page.EvaluateAsync("() => document.querySelectorAll('[data-front017-enlarged]').forEach(e => { e.style.removeProperty('font-size'); delete e.dataset.front017Enlarged; })");
        var viewport = page.ViewportSize!;
        await page.SetViewportSizeAsync(320, 844);
        dimensions = await page.EvaluateAsync<int[]>("() => [innerWidth, document.documentElement.scrollWidth]");
        Assert.True(dimensions[1] <= dimensions[0]);
        await page.SetViewportSizeAsync(viewport.Width, viewport.Height);
    }

    private const string ContrastCheck = """
        () => {
          const luminance = color => { const v = color.match(/[\d.]+/g).slice(0,3).map(Number).map(x => { const c=x/255; return c<=.04045 ? c/12.92 : ((c+.055)/1.055)**2.4; }); return v[0]*.2126+v[1]*.7152+v[2]*.0722; };
          return [...document.querySelectorAll('#aportar-evidencia .campo__etiqueta, #aportar-evidencia .boton:not(:disabled), #aportar-evidencia .alerta')].filter(e => e.getClientRects().length).every(e => {
            let parent=e, background=getComputedStyle(e).backgroundColor;
            while (background === 'rgba(0, 0, 0, 0)' && parent.parentElement) { parent=parent.parentElement; background=getComputedStyle(parent).backgroundColor; }
            const a=luminance(getComputedStyle(e).color), b=luminance(background);
            return (Math.max(a,b)+.05)/(Math.min(a,b)+.05)>=4.5;
          });
        }
        """;

    private static async Task CaptureAsync(IPage page, bool mobile, string state)
    {
        var directory = Environment.GetEnvironmentVariable("SGOL_FRONT017_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        var path = Path.GetFullPath(directory);
        if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Fuentes", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Protected output path.");
        Directory.CreateDirectory(path);
        await page.Locator("#aportar-evidencia").ScreenshotAsync(new() { Path = Path.Combine(path, $"{(mobile ? "movil" : "escritorio")}-{state}.png") });
    }

    private static void Progress(bool mobile, string stage)
    {
        var directory = Environment.GetEnvironmentVariable("SGOL_FRONT017_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        var path = Path.GetFullPath(directory);
        if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("Fuentes", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Protected output path.");
        Directory.CreateDirectory(path);
        File.AppendAllText(Path.Combine(path, mobile ? "movil-etapas.txt" : "escritorio-etapas.txt"), stage + Environment.NewLine);
    }
}
