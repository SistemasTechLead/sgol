using System.Text.Json;
using Microsoft.Playwright;
using Sgol.Testing;
using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed partial class Front018BrowserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task SuperiorBinaryReplacementReusesRealPrivateUploadAndLinksOnlyAfterClean(bool mobile)
    {
        await using var evidence = new Front017EvidenceServices();
        await using var fixture = new BrowserFixture { ConfigureEvidence = evidence.StartAsync };
        await fixture.StartAsync(); var author = fixture.Accounts[0]; var ticket = await fixture.AuthenticateAsync(author);
        var tasks = await fixture.SeedMyWorkAsync(enrolledUsers: true, taskCode: mobile ? "TAR-0092" : "TAR-0008");
        var task = tasks.Single(t => t.UserId == fixture.Accounts[3].UserId && t.State == "CONCLUIDA");
        using var playwright = await Playwright.CreateAsync(); await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { BaseURL = fixture.BaseAddress.AbsoluteUri, IgnoreHTTPSErrors = true, ViewportSize = new() { Width = mobile ? 390 : 1440, Height = mobile ? 844 : 900 }, ReducedMotion = ReducedMotion.Reduce });
        await context.AddCookiesAsync([new Cookie { Name = "__Host-SGOL-Session", Value = ticket, Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]);
        var page = await context.NewPageAsync(); var path = "/mi-trabajo/tareas/" + task.Id.ToString("D");
        var initial = await page.GotoAsync(path); await CaptureAsync(page, mobile, "binaria-inicio");
        Assert.Equal(200, initial!.Status);
        var code = mobile ? "DOCUMENTO_RECEPCION" : "EXPEDIENTE";
        await page.Locator("#versiones-evidencia tr").Filter(new() { HasText = code }).GetByRole(AriaRole.Button, new() { Name = "Preparar sustitución" }).ClickAsync();
        await page.Locator("textarea[name=reason]").FillAsync("Sustitución binaria sintética autorizada");
        if (mobile) await page.Locator("select[name=documentSubtype]").SelectOptionAsync("FACTURA");
        var before = await fixture.EvidenceCountsAsync(task.Id);
        await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "sustitucion-sintetica.pdf", MimeType = "application/pdf", Buffer = EvidenceCorpus.Pdf() });
        var uploaded = await page.RunAndWaitForResponseAsync(() => page.Locator("[data-upload-start]").ClickAsync(), r => r.Url.Contains("handler=Upload", StringComparison.Ordinal));
        using var result = JsonDocument.Parse(await uploaded.BodyAsync()); var flow = result.RootElement.GetProperty("intention").GetString()!;
        await page.WaitForFunctionAsync("() => document.querySelector('[data-upload-status]').textContent.includes('Esperando análisis') || !document.querySelector('[data-upload-error]').hidden", null, new() { Timeout = 35000 });
        await Assertions.Expect(page.Locator("[data-upload-error]")).ToBeHiddenAsync();
        Assert.DoesNotContain("X-Amz-", await page.ContentAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before.Linked, (await fixture.EvidenceCountsAsync(task.Id)).Linked);
        var pending = await fixture.MyWorkRowsAsync(); var csrf = await CsrfAsync(page);
        Assert.Equal(409, (await PostIntentAsync(context, path, "ContributeFile", csrf, flow)).Status);
        Assert.Equal(pending, await fixture.MyWorkRowsAsync()); await CaptureAsync(page, mobile, "sustitucion-binaria-en-escaneo");
        await fixture.InspectEvidenceAsync(task.Id, evidence.Pipeline);
        await page.Locator("[data-upload-refresh]").ClickAsync(); await Assertions.Expect(page.Locator("[data-upload-contribute]")).ToBeEnabledAsync();
        await page.Locator("[data-upload-contribute]").ClickAsync(); await Assertions.Expect(page.Locator("dialog[open] button").First).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("dialog[open]")).ToContainTextAsync((mobile ? "TAR-0092" : "TAR-0008") + " · " + code + " · Versión 1");
        await page.Keyboard.PressAsync("Escape"); await Assertions.Expect(page.Locator("[data-upload-contribute]")).ToBeFocusedAsync();
        Assert.Equal(before.Versions, (await fixture.EvidenceCountsAsync(task.Id)).Versions);
        await page.Locator("[data-upload-contribute]").ClickAsync(); await page.Locator("dialog[open] button").Last.ClickAsync();
        await Assertions.Expect(page.Locator("[data-upload-status]")).ToContainTextAsync("Sustitución confirmada");
        var after = await fixture.EvidenceCountsAsync(task.Id); Assert.Equal(before.Versions + 1, after.Versions); Assert.Equal(before.Linked + 1, after.Linked);
        var committed = await fixture.MyWorkRowsAsync(); Assert.Equal(200, (await PostIntentAsync(context, path, "ContributeFile", csrf, flow)).Status);
        Assert.Equal(committed, await fixture.MyWorkRowsAsync());
        await page.GotoAsync(path); await Assertions.Expect(page.Locator("#conclusion-tarea")).ToContainTextAsync("La tarea está concluida");
        await CaptureAsync(page, mobile, "sustitucion-binaria-confirmada");
    }
}
