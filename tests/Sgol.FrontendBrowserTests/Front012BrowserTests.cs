using System.Buffers.Binary;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front012BrowserTests
{
    private static readonly string[] Codes =
        ["TAR-0005", "TAR-0007", "TAR-0008", "TAR-0011", "TAR-0018", "TAR-0026", "TAR-0092", "TAR-0093"];

    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task EightPublishedPoliciesShowExactRequirementsAuthorityAndHistoryOnDesktopAndMobile()
    {
        var output = OutputDirectory();
        using var playwright = await Playwright.CreateAsync();
        foreach (var mobile in new[] { false, true })
        {
            var fixture = new BrowserFixture();
            try
            {
                await fixture.StartAsync();
                await fixture.SeedPublishedEvidenceValidationAsync(fixture.Accounts[0].UserId);
                var ticket = await fixture.AuthenticateAsync(fixture.Accounts[0]);
                await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                    .LaunchAsync(new() { Headless = true });
                await using var context = await NewContextAsync(browser, mobile);
                await SetSessionAsync(context, fixture, ticket);
                var page = await context.NewPageAsync();
                foreach (var code in Codes)
                {
                    Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                        $"/configuracion?taskCode={code}").AbsoluteUri))?.Status);
                    Assert.Equal(8, await page.Locator("#task-definition-catalog tbody tr").CountAsync());
                    Assert.Contains("Versión vigente", await page.Locator("#evidence-policy").InnerTextAsync());
                    Assert.Contains("Versión vigente", await page.Locator("#validation-policy").InnerTextAsync());
                    Assert.Equal(1, await page.Locator("#evidence-policy-history tbody tr").CountAsync());
                    Assert.Equal(1, await page.Locator("#validation-policy-history tbody tr").CountAsync());
                    Assert.Equal(code == "TAR-0092" ? 3 : code == "TAR-0007" ? 4 :
                        code is "TAR-0008" or "TAR-0011" ? 5 :
                        code is "TAR-0018" or "TAR-0093" ? 3 : 2,
                        await page.Locator("#evidence-current-requirements li").CountAsync());
                    Assert.Contains("SUPERIOR_INMEDIATO", await page.Locator("#validation-policy").InnerTextAsync());
                    Assert.Contains("CUMPLIDA, INCOMPLETA, NO_CUMPLIDA",
                        await page.Locator("#validation-policy").InnerTextAsync());
                    if (code == "TAR-0092")
                        Assert.Contains("DIFERENCIA_O_DANO", await page.Locator("#evidence-policy").InnerTextAsync());
                    await AssertWidthAsync(page);
                }
                await page.GetByRole(AriaRole.Link, new() { Name = "Ver detalle de TAR-0092" }).ClickAsync();
                await CaptureAsync(page, output, mobile, "published-and-conditional");
                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador", Exact = true }),
                    "handler=Create", 200);
                await page.GetByRole(AriaRole.Link, new() { Name = "Ver detalle de TAR-0092" }).ClickAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Configurar evidencia" }).ClickAsync();
                var dialog = page.Locator("#evidence-confirm[open]");
                var release = await page.Locator("#evidence-release option:nth-child(2)").GetAttributeAsync("value");
                await page.Locator("#evidence-release").SelectOptionAsync(release!);
                await dialog.Locator("input[name=policyEtag]").EvaluateAsync("el => el.value = '\"999\"'");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de evidencia" }), "handler=EvidencePolicy", 412);
                Assert.Contains("Esta política cambió", await page.Locator("#evidence-policy-error").InnerTextAsync());
                Assert.Equal(0, await page.Locator("#evidence-policy button[data-dialog-open]").CountAsync());
                Assert.Equal(1, await page.Locator("#evidence-policy-history tbody tr").CountAsync());
                await CaptureAsync(page, output, mobile, "conflict-412");
                await page.GetByRole(AriaRole.Link, new() { Name = "Recargar política de evidencia" }).ClickAsync();
                Assert.Equal(1, await page.Locator("#evidence-policy button[data-dialog-open]").CountAsync());
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
    public async Task ClosedEditorsReplayConflictAndDeniedProfilesHaveNoDirectionControls()
    {
        var output = OutputDirectory();
        using var playwright = await Playwright.CreateAsync();
        foreach (var mobile in new[] { false, true })
        {
            var fixture = new BrowserFixture();
            try
            {
                await fixture.StartAsync();
                await fixture.SeedPublishedPoliciesAsync(fixture.Accounts[0].UserId);
                var tickets = new List<string>();
                foreach (var account in fixture.Accounts) tickets.Add(await fixture.AuthenticateAsync(account));
                await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                    .LaunchAsync(new() { Headless = true });
                await using var context = await NewContextAsync(browser, mobile);
                await SetSessionAsync(context, fixture, tickets[0]);
                var page = await context.NewPageAsync();
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    "/configuracion?taskCode=TAR-0092").AbsoluteUri))?.Status);
                Assert.Contains("Aún no hay versiones de esta política", await page.Locator("#evidence-policy").InnerTextAsync());
                Assert.Contains("Aún no hay versiones de esta política", await page.Locator("#validation-policy").InnerTextAsync());
                await CaptureAsync(page, output, mobile, "empty");

                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador", Exact = true }),
                    "handler=Create", 200);
                await page.GetByRole(AriaRole.Link, new() { Name = "Ver detalle de TAR-0092" }).ClickAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Configurar evidencia" }).ClickAsync();
                var dialog = page.Locator("#evidence-confirm[open]");
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                Assert.Contains("obligaciones existentes no cambiarán", await dialog.InnerTextAsync());
                await CaptureAsync(page, output, mobile, "evidence-confirmation");
                await page.Keyboard.PressAsync("Escape");
                Assert.False(await page.Locator("#evidence-confirm").EvaluateAsync<bool>("el => el.open"));
                await page.GetByRole(AriaRole.Button, new() { Name = "Configurar evidencia" }).ClickAsync();
                await page.Locator("#evidence-release").EvaluateAsync(
                    "el => { const option = new Option('No válida', 'bad'); el.add(option); el.value = 'bad'; }");
                await SubmitAsync(page, page.Locator("#evidence-confirm[open]").GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de evidencia" }), "handler=EvidencePolicy", 400);
                Assert.Equal("true", await page.Locator("#evidence-release").GetAttributeAsync("aria-invalid"));
                Assert.Contains("Revisa la TAR y la release", await page.Locator("#evidence-confirm[open]").InnerTextAsync());
                await CaptureAsync(page, output, mobile, "validation-error");
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    "/configuracion?taskCode=TAR-0092").AbsoluteUri))?.Status);
                await page.GetByRole(AriaRole.Button, new() { Name = "Configurar evidencia" }).ClickAsync();
                var release = await page.Locator("#evidence-release option:nth-child(2)").GetAttributeAsync("value");
                Assert.False(string.IsNullOrWhiteSpace(release));
                await page.Locator("#evidence-release").SelectOptionAsync(release!);
                var intent = await dialog.Locator("input[name=intentKey]").InputValueAsync();
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de evidencia" }), "handler=EvidencePolicy", 200);
                Assert.Contains("Borrador", await page.Locator("#evidence-policy-history").InnerTextAsync());
                Assert.Contains("DIFERENCIA_O_DANO", await page.Locator("#evidence-policy-history").InnerTextAsync());
                await CaptureAsync(page, output, mobile, "evidence-draft");

                await page.GetByRole(AriaRole.Button, new() { Name = "Configurar evidencia" }).ClickAsync();
                dialog = page.Locator("#evidence-confirm[open]");
                await page.Locator("#evidence-release").SelectOptionAsync(release!);
                await dialog.Locator("input[name=intentKey]").EvaluateAsync("(el, key) => el.value = key", intent);
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de evidencia" }), "handler=EvidencePolicy", 200);
                Assert.Equal(1, await page.Locator("#evidence-policy-history tbody tr").CountAsync());
                await CaptureAsync(page, output, mobile, "evidence-replay");

                await page.GetByRole(AriaRole.Button, new() { Name = "Configurar validación" }).ClickAsync();
                dialog = page.Locator("#validation-confirm[open]");
                Assert.Contains("SUBCOORDINACION", await page.Locator("#validation-policy").InnerTextAsync());
                Assert.Contains("ADMINISTRACION", await page.Locator("#validation-policy").InnerTextAsync());
                Assert.Contains("CUMPLIDA, INCOMPLETA, NO_CUMPLIDA",
                    await page.Locator("#validation-policy").InnerTextAsync());
                await page.Locator("#validation-release").SelectOptionAsync(release!);
                await CaptureAsync(page, output, mobile, "validation-confirmation");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de validación" }), "handler=ValidationPolicy", 200);
                Assert.Contains("Borrador", await page.Locator("#validation-policy-history").InnerTextAsync());
                await CaptureAsync(page, output, mobile, "validation-draft");

                for (var index = 1; index < tickets.Count; index++)
                {
                    await using var deniedContext = await NewContextAsync(browser, mobile);
                    await SetSessionAsync(deniedContext, fixture, tickets[index]);
                    var denied = await deniedContext.NewPageAsync();
                    Assert.Equal(200, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        "/configuracion?taskCode=TAR-0092").AbsoluteUri))?.Status);
                    Assert.Equal(0, await denied.Locator("#evidence-policy").CountAsync());
                    Assert.Equal(0, await denied.Locator("#validation-policy").CountAsync());
                    if (index == 1) await CaptureAsync(denied, output, mobile, "denied-profile");
                    Assert.Equal(403, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        "/api/v1/task-definitions/TAR-0092/evidence-policy").AbsoluteUri))?.Status);
                    Assert.Equal(403, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        "/api/v1/task-definitions/TAR-0092/validation-policy").AbsoluteUri))?.Status);
                    await AssertWidthAsync(denied);
                }
            }
            finally
            {
                await fixture.DisposeAsync();
                Assert.True(fixture.CleanupComplete);
            }
        }
    }

    private static string OutputDirectory()
    {
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-012-evidence");
        Directory.CreateDirectory(output);
        return output;
    }

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

    private static Task SetSessionAsync(IBrowserContext context, BrowserFixture fixture, string ticket) =>
        context.AddCookiesAsync([new Cookie
        {
            Name = "__Host-SGOL-Session", Value = ticket, Url = fixture.BaseAddress.AbsoluteUri,
            Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict,
        }]);

    private static async Task SubmitAsync(IPage page, ILocator button, string handler, int expectedStatus)
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object? sender, IPage loadedPage) => loaded.TrySetResult();
        page.DOMContentLoaded += OnLoaded;
        try
        {
            var response = page.WaitForResponseAsync(item => item.Request.Method == "POST" &&
                item.Url.Contains(handler, StringComparison.Ordinal));
            await button.ClickAsync();
            Assert.Equal(expectedStatus, (await response).Status);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { page.DOMContentLoaded -= OnLoaded; }
    }

    private static async Task AssertWidthAsync(IPage page)
    {
        var widths = await page.EvaluateAsync<int[]>(
            "() => [window.innerWidth, document.documentElement.scrollWidth, document.body.scrollWidth]");
        Assert.True(widths[1] <= widths[0] && widths[2] <= widths[0], string.Join('/', widths));
    }

    private static async Task CaptureAsync(IPage page, string output, bool mobile, string state)
    {
        await AssertWidthAsync(page);
        var bytes = await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(output, $"{(mobile ? "mobile" : "desktop")}-{state}.png"),
            FullPage = true,
            Mask = [page.Locator(".encabezado-aplicacion")],
        });
        Assert.Equal(mobile ? 390 : 1440, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
    }
}
