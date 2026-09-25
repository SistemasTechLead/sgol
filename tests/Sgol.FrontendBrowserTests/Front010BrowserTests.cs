using System.Buffers.Binary;
using System.Globalization;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front010BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task DirectionManagesEightDefinitionsAndOtherProfilesCannotMutateFromDeepLink()
    {
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-010-evidence");
        Directory.CreateDirectory(output);
        using var playwright = await Playwright.CreateAsync();
        foreach (var mobile in new[] { false, true })
        {
            var viewport = mobile ? "mobile" : "desktop";
            var fixture = new BrowserFixture();
            try
            {
                await fixture.StartAsync();
                var tickets = new List<string>();
                foreach (var account in fixture.Accounts) tickets.Add(await fixture.AuthenticateAsync(account));
                await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                    .LaunchAsync(new() { Headless = true });
                await using var context = await NewContextAsync(browser, mobile);
                await SetSessionAsync(context, fixture, tickets[0]);
                var page = await context.NewPageAsync();
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress, "/configuracion").AbsoluteUri))?.Status);
                Assert.Equal(8, await page.Locator("#task-definition-catalog tbody tr").CountAsync());
                Assert.Equal(0, await page.Locator("#task-definition-catalog").GetByText("TAR-0001").CountAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-catalog.png", mobile);

                var detailResponse = page.WaitForResponseAsync(response => response.Request.Method == "GET" &&
                    response.Url.Contains("taskCode=TAR-0005", StringComparison.Ordinal));
                await page.GetByRole(AriaRole.Link, new() { Name = "Ver detalle de TAR-0005" }).ClickAsync();
                Assert.Equal(200, (await detailResponse).Status);
                await page.Locator("#task-detail").WaitForAsync();
                Assert.Contains("Aún no hay versiones de esta TAR", await page.Locator("#task-detail").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-detail-empty.png", mobile);

                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador", Exact = true }),
                    "handler=Create", 200);
                var tarSelect = page.Locator("#task-code-select");
                await tarSelect.SelectOptionAsync("TAR-0005");
                await tarSelect.FocusAsync();
                await tarSelect.PressAsync("Tab");
                Assert.True(await page.Locator("#task-release-select")
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                var release = await page.Locator("#task-release-select option:nth-child(2)").GetAttributeAsync("value");
                Assert.False(string.IsNullOrWhiteSpace(release));
                await page.Locator("#task-release-select").SelectOptionAsync(release!);
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-closed-editor.png", mobile);
                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear versión TAR" }),
                    "handler=CreateTaskVersion", 200);
                Assert.Contains("Borrador de TAR disponible", await page.GetByRole(AriaRole.Status).InnerTextAsync());
                Assert.Contains("Borrador", await page.Locator("#task-definition-history").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-draft.png", mobile);
                await using (var readerContext = await NewContextAsync(browser, mobile))
                {
                    await SetSessionAsync(readerContext, fixture, tickets[1]);
                    var reader = await readerContext.NewPageAsync();
                    Assert.Equal(200, (await reader.GotoAsync(new Uri(fixture.BaseAddress,
                        "/configuracion?taskCode=TAR-0005").AbsoluteUri))?.Status);
                    Assert.Contains("Aún no hay versiones de esta TAR", await reader.Locator("#task-detail").InnerTextAsync());
                    Assert.Equal(0, await reader.Locator("#task-new-version").CountAsync());
                    await CheckWidthAndCaptureAsync(reader, output, $"{viewport}-draft-hidden.png", mobile);
                    var deniedPost = reader.WaitForResponseAsync(response => response.Request.Method == "POST" &&
                        response.Url.Contains("handler=CreateTaskVersion", StringComparison.Ordinal));
                    await reader.EvaluateAsync("""
                        ({ releaseId, intentKey }) => {
                          const token = document.querySelector('.encabezado-aplicacion__logout input[name="__RequestVerificationToken"]').value;
                          const form = document.createElement('form');
                          form.method = 'post'; form.action = '/configuracion?handler=CreateTaskVersion';
                          for (const [name, value] of Object.entries({ __RequestVerificationToken: token, taskCode: 'TAR-0005', releaseId, intentKey })) {
                            const input = document.createElement('input');
                            input.type = 'hidden'; input.name = name; input.value = value; form.append(input);
                          }
                          document.body.append(form); form.submit();
                        }
                        """, new { releaseId = release, intentKey = Guid.CreateVersion7().ToString("D") });
                    Assert.Equal(403, (await deniedPost).Status);
                    await reader.Locator("#task-definitions-error").WaitForAsync();
                    Assert.Contains("No tienes permiso para administrar definiciones TAR",
                        await reader.Locator("#task-definitions-error").InnerTextAsync());
                    Assert.Equal(0, await reader.Locator("#task-new-version").CountAsync());
                    await CheckWidthAndCaptureAsync(reader, output, $"{viewport}-authorization-error.png", mobile);
                }

                var openPublish = page.GetByRole(AriaRole.Button, new() { Name = "Publicar versión 1" });
                await openPublish.ClickAsync();
                var dialog = page.Locator("dialog[open]");
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-publish-confirmation.png", mobile);
                await page.Keyboard.PressAsync("Escape");
                Assert.True(await openPublish.EvaluateAsync<bool>("el => document.activeElement === el"));
                await openPublish.ClickAsync();
                dialog = page.Locator("dialog[open]");
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Publicar versión de TAR" }).ClickAsync();
                Assert.True(await dialog.GetByLabel("Fecha y hora de vigencia (America/Mexico_City)")
                    .EvaluateAsync<bool>("el => el.validity.valueMissing"));
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-validation.png", mobile);
                await dialog.GetByLabel("Fecha y hora de vigencia (America/Mexico_City)").FillAsync(LocalFuture(3));
                await dialog.GetByLabel("Motivo").FillAsync("Publicación TAR sintética");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Publicar versión de TAR" }),
                    "handler=PublishTaskVersion", 200);
                Assert.Contains("Versión de TAR publicada", await page.GetByRole(AriaRole.Status).InnerTextAsync());
                Assert.Contains("Vigente", await page.Locator("#task-definition-history").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-published.png", mobile);

                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador", Exact = true }),
                    "handler=Create", 200);
                await page.GetByRole(AriaRole.Link, new() { Name = "Ver detalle de TAR-0005" }).ClickAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Desactivar nuevas generaciones de TAR-0005" }).ClickAsync();
                dialog = page.Locator("dialog[open]");
                release = await dialog.Locator("select[name=releaseId] option:nth-child(2)").GetAttributeAsync("value");
                await dialog.Locator("select[name=releaseId]").SelectOptionAsync(release!);
                await dialog.GetByLabel("Fecha y hora de vigencia (America/Mexico_City)").FillAsync(LocalFuture(5));
                await dialog.GetByLabel("Motivo").FillAsync("Pausa TAR sintética");
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-deactivate-confirmation.png", mobile);
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Desactivar nuevas generaciones", Exact = true }),
                    "handler=DeactivateTask", 200);
                Assert.Contains("Se detuvieron las nuevas generaciones", await page.GetByRole(AriaRole.Status).InnerTextAsync());
                Assert.Contains("Inactiva para nuevas generaciones", await page.Locator("#task-detail").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-inactive.png", mobile);

                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador", Exact = true }),
                    "handler=Create", 200);
                await page.Locator("#task-code-select").SelectOptionAsync("TAR-0005");
                release = await page.Locator("#task-release-select option:nth-child(2)").GetAttributeAsync("value");
                await page.Locator("#task-release-select").SelectOptionAsync(release!);
                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear versión TAR" }),
                    "handler=CreateTaskVersion", 200);
                await page.GetByRole(AriaRole.Button, new() { Name = "Publicar versión 3" }).ClickAsync();
                dialog = page.Locator("dialog[open]");
                await dialog.GetByLabel("Fecha y hora de vigencia (America/Mexico_City)").FillAsync(LocalFuture(7));
                await dialog.GetByLabel("Motivo").FillAsync("Conflicto TAR sintético");
                await dialog.Locator("input[name=versionEtag]").EvaluateAsync("el => el.value = '\"999\"'");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Publicar versión de TAR" }),
                    "handler=PublishTaskVersion", 412);
                Assert.Contains("Esta versión cambió", await page.Locator("#task-definitions-error").InnerTextAsync());
                Assert.Equal(0, await page.Locator("#task-detail button[data-dialog-open]").CountAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-conflict-412.png", mobile);
                await page.GetByRole(AriaRole.Link, new() { Name = "Recargar definiciones" }).ClickAsync();
                Assert.Equal(1, await page.GetByRole(AriaRole.Button, new() { Name = "Publicar versión 3" }).CountAsync());

                Assert.Equal(404, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    "/configuracion?taskCode=TAR-0001").AbsoluteUri))?.Status);
                Assert.Equal(0, await page.Locator("#task-detail").CountAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-not-mvp.png", mobile);

                for (var index = 1; index < tickets.Count; index++)
                {
                    await using var deniedContext = await NewContextAsync(browser, mobile);
                    await SetSessionAsync(deniedContext, fixture, tickets[index]);
                    var denied = await deniedContext.NewPageAsync();
                    Assert.Equal(200, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        "/configuracion?taskCode=TAR-0005").AbsoluteUri))?.Status);
                    Assert.Equal(8, await denied.Locator("#task-definition-catalog tbody tr").CountAsync());
                    Assert.Equal(0, await denied.Locator("#task-new-version, #task-detail button[data-dialog-open]").CountAsync());
                    Assert.DoesNotContain("Borrador", await denied.Locator("#task-definition-history").InnerTextAsync());
                    if (index == 1) await CheckWidthAndCaptureAsync(denied, output, $"{viewport}-read-only.png", mobile);
                }
            }
            finally
            {
                await fixture.DisposeAsync();
                Assert.True(fixture.CleanupComplete);
            }
        }
    }

    private static async Task<IBrowserContext> NewContextAsync(IBrowser browser, bool mobile) =>
        await browser.NewContextAsync(new()
        {
            ViewportSize = mobile ? new() { Width = 390, Height = 844 } : new() { Width = 1440, Height = 900 },
            Locale = "es-MX",
            IsMobile = mobile,
            HasTouch = mobile,
            IgnoreHTTPSErrors = true,
            ServiceWorkers = ServiceWorkerPolicy.Block,
        });

    private static Task SetSessionAsync(IBrowserContext context, BrowserFixture fixture, string ticket) =>
        context.AddCookiesAsync([new Cookie
        {
            Name = "__Host-SGOL-Session", Value = ticket, Url = fixture.BaseAddress.AbsoluteUri,
            Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict,
        }]);

    private static string LocalFuture(int days) => TimeZoneInfo.ConvertTime(
        DateTimeOffset.UtcNow.AddDays(days), TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"))
        .ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    private static async Task SubmitAsync(IPage page, ILocator submit, string handler, int expectedStatus)
    {
        var responseTask = page.WaitForResponseAsync(response => response.Request.Method == "POST" &&
            response.Url.Contains(handler, StringComparison.Ordinal));
        await submit.ClickAsync();
        Assert.Equal(expectedStatus, (await responseTask).Status);
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    private static async Task CheckWidthAndCaptureAsync(IPage page, string output, string name, bool mobile)
    {
        var widths = await page.EvaluateAsync<int[]>(
            "() => [window.innerWidth, document.documentElement.scrollWidth, document.body.scrollWidth]");
        var overflow = widths[1] > widths[0] || widths[2] > widths[0]
            ? await page.EvaluateAsync<string[]>(
                "() => [...document.querySelectorAll('main, section, form, label, select, .tabla-contenedor')].filter(el => el.scrollWidth > 390 || el.getBoundingClientRect().right > 390).slice(-18).map(el => `${el.tagName}.${el.className}: rect=${Math.round(el.getBoundingClientRect().width)}/${Math.round(el.getBoundingClientRect().right)} scroll=${el.scrollWidth} client=${el.clientWidth}`)")
            : [];
        Assert.True(widths[1] <= widths[0] && widths[2] <= widths[0],
            $"Viewport/root/body: {string.Join('/', widths)}; overflow: {string.Join(" | ", overflow)}");
        var bytes = await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(output, name),
            FullPage = true,
            Mask = [page.Locator(".encabezado-aplicacion")],
        });
        Assert.Equal(mobile ? 390 : 1440, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
    }
}
