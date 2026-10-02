using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Sgol.Configuration.Contracts;
using Sgol.Testing;
using Sgol.Web.Presentation.MyWork;
using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed partial class TechFront005BrowserTests
{
    private async Task WorkAsync()
    {
        foreach (var (page, index) in pages.Select((p, i) => (p, i)))
        {
            await page.GotoAsync("/mi-trabajo");
            await Capture(page, "R7-bandeja-" + fixture.Accounts[index].Role);
        }
        using var store = owner.CreateSourceS3();
        var inspection = owner.FrontendInspectionPipeline(store);
        foreach (var (code, obligation) in obligations)
        {
            await using var db = fixture.TechFront005Context();
            var responsible = await db.AssignmentVersions.AsNoTracking().SingleAsync(a => a.ObligationId == obligation && a.Status == "VIGENTE");
            var index = Array.FindIndex(fixture.Accounts.ToArray(), a => a.PersonId == responsible.PersonId);
            Assert.InRange(index, 0, 3);
            var page = pages[index];
            var path = "/mi-trabajo/tareas/" + obligation;
            var before = await fixture.MyWorkRowsAsync();
            Assert.Equal(200, (await page.GotoAsync(path))!.Status);
            Same(before, await fixture.MyWorkRowsAsync());
            var rejected = await page.Context.APIRequest.PostAsync(path + "?handler=ReviewEvidence", new() { Form = page.Context.APIRequest.CreateFormData() });
            Assert.Equal(400, rejected.Status);
            Same(before, await fixture.MyWorkRowsAsync());
            await Submit(page, page.GetByRole(AriaRole.Button, new() { Name = "Consultar revisión", Exact = true }), "ReviewEvidence");
            await Assertions.Expect(page.Locator("#revision-evidencia")).ToContainTextAsync("Incompleta");
            await Assertions.Expect(page.Locator("#conclusion-tarea button[type=submit]")).ToBeDisabledAsync();
            foreach (var requirement in EvidencePolicyCatalog.Require(code))
            {
                await page.Locator("#evidence-requirement").SelectOptionAsync(requirement.Code);
                await page.GetByRole(AriaRole.Button, new() { Name = "Seleccionar requisito" }).ClickAsync();
                if (requirement.Kind is "FOTOGRAFIA" or "DOCUMENTO_REFERENCIADO")
                {
                    var photo = requirement.Kind == "FOTOGRAFIA";
                    await page.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
                    { Name = photo ? "fotografia-sintetica.png" : "documento-sintetico.pdf", MimeType = photo ? "image/png" : "application/pdf", Buffer = photo ? EvidenceCorpus.Png() : EvidenceCorpus.Pdf() });
                    await page.Locator("[data-upload-start]").ClickAsync();
                    await page.WaitForFunctionAsync("() => document.querySelector('[data-upload-status]').textContent.includes('Esperando análisis') || !document.querySelector('[data-upload-error]').hidden");
                    Assert.True(await page.Locator("[data-upload-error]").IsHiddenAsync(), "Synthetic private upload failed.");
                    await Assertions.Expect(page.Locator("[data-upload-contribute]")).ToBeHiddenAsync();
                    await fixture.InspectEvidenceAsync(obligation, inspection);
                    await page.Locator("[data-upload-refresh]").ClickAsync();
                    await Assertions.Expect(page.Locator("[data-upload-contribute]")).ToBeEnabledAsync();
                    await page.Locator("[data-upload-contribute]").ClickAsync();
                    await Assertions.Expect(page.Locator("[data-upload-status]")).ToContainTextAsync("Se aportó la evidencia");
                }
                else
                {
                    foreach (var field in EvidenceContributionPresentation.Inputs(requirement.Code))
                    {
                        if (field.Nullable) continue;
                        var value = field.Type switch
                        {
                            "number" => field.Name == "expectedTarget" ? "100" : "95",
                            "datetime-local" => Local(DateTimeOffset.UtcNow.AddMinutes(-1)),
                            "boolean" => "true",
                            "select" => field.Name == "outcome" ? "CONFORMIDAD" : "CAMBIO",
                            _ => "Referencia sintética integral"
                        };
                        var input = page.Locator("[name='" + field.Name + "']");
                        if (field.Type is "boolean" or "select") await input.SelectOptionAsync(value);
                        else await input.FillAsync(value);
                    }
                    await Submit(page, page.Locator("form[action*='PrepareEvidence'] button[type=submit]"), "PrepareEvidence");
                    await Submit(page, page.Locator("form[action*='SendEvidence'] button[type=submit]"), "SendEvidence");
                    await Assertions.Expect(page.Locator("[data-evidence-success]")).ToBeVisibleAsync();
                }
                await page.GotoAsync(path);
            }
            if (code == "TAR-0007")
            {
                await page.Locator("#versiones-evidencia tr").Filter(new() { HasTextString = "MERCANCIA" })
                    .GetByRole(AriaRole.Button, new() { Name = "Preparar sustitución" }).ClickAsync();
                await page.Locator("[name=merchandiseReference]").FillAsync("Mercancía sintética corregida");
                await Submit(page, page.Locator("form[action*='PrepareReplacement'] button[type=submit]"), "PrepareReplacement");
                await page.Locator("form[data-front-confirm] > button").ClickAsync();
                await Assertions.Expect(page.Locator("dialog[open]")).ToBeVisibleAsync();
                await Submit(page, page.Locator("dialog[open] button[type=submit]"), "SendReplacement");
                await Assertions.Expect(page.Locator("[data-front-success]")).ToContainTextAsync("Sustitución confirmada");
                await page.GotoAsync(path);
                await Assertions.Expect(page.Locator("#versiones-evidencia")).ToContainTextAsync("Sustituida");
            }
            await Submit(page, page.GetByRole(AriaRole.Button, new() { Name = "Consultar revisión", Exact = true }), "ReviewEvidence");
            await Assertions.Expect(page.Locator("#revision-evidencia")).ToContainTextAsync("Completa");
            await Submit(page, page.Locator("form[action*='PrepareConclusion'] button[type=submit]"), "PrepareConclusion");
            await page.Locator("form[data-front-confirm] > button").ClickAsync();
            await Assertions.Expect(page.Locator("dialog[open]")).ToBeVisibleAsync();
            await Submit(page, page.Locator("dialog[open] button[type=submit]"), "Conclude");
            await page.GotoAsync(path);
            await Assertions.Expect(page.Locator("#conclusion-tarea")).ToContainTextAsync("concluida");
            await Capture(page, "R7-" + code + "-concluida");
        }
    }

    private async Task ValidationAsync()
    {
        foreach (var (code, obligation) in obligations)
        {
            var page = pages[code is "TAR-0007" or "TAR-0018" ? 2 : 1];
            var path = "/mi-trabajo/tareas/" + obligation;
            await page.GotoAsync(path);
            await ValidateThroughInterface(page, "CUMPLIDA", false);
            if (code == "TAR-0007")
            {
                await page.GotoAsync(path); await ValidateThroughInterface(page, "INCOMPLETA", true);
                await page.GotoAsync(path); await ValidateThroughInterface(page, "NO_CUMPLIDA", true);
                await page.GotoAsync(path);
                await Assertions.Expect(page.Locator("#validaciones-historia")).ToContainTextAsync("Sustituida");
                Assert.Equal(3, await page.Locator("#validaciones-historia tbody tr").CountAsync());
            }
            await Capture(page, "R8-" + code + "-validacion");
        }
        foreach (var (page, index) in pages.Select((p, i) => (p, i)))
        {
            var response = await page.GotoAsync("/validaciones");
            Assert.Equal(index == 3 ? 403 : 200, response!.Status);
            if (index != 3) await Capture(page, "R8-supervision-" + fixture.Accounts[index].Role);
        }
    }

    private async Task ValidateThroughInterface(IPage page, string result, bool replacement)
    {
        await page.Locator("#result-" + result).CheckAsync();
        await page.Locator("#foundation").FillAsync("Fundamento sintético integral documentado");
        if (replacement) await page.Locator("#reason").FillAsync("Sustitución sintética fundada y motivada");
        await Submit(page, page.Locator("form[action*='PrepareValidation'] button[type=submit]"), "PrepareValidation");
        await page.Locator("form[data-front-confirm] > button").ClickAsync();
        await Assertions.Expect(page.Locator("dialog[open]")).ToBeVisibleAsync();
        await Submit(page, page.Locator("dialog[open] button[type=submit]"), "SendValidation");
        await Assertions.Expect(page.Locator("#validaciones-historia [data-front-success]")).ToBeVisibleAsync();
    }
}
