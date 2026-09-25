using System.Buffers.Binary;
using System.Globalization;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front009BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task DirectionCreatesAndPublishesReleasesWithHistoryConflictsAndDeniedRoles()
    {
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-009-evidence");
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
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress, "/configuracion").AbsoluteUri))?.Status);
                Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                Assert.Contains("Aún no hay releases de configuración", await page.Locator("main").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-empty.png", mobile);

                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador" }), 200);
                Assert.Contains("Borrador disponible", await page.GetByRole(AriaRole.Status).InnerTextAsync());
                Assert.Equal(1, await page.Locator("#release-history tbody tr").CountAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-draft.png", mobile);

                var draft = DraftRow(page);
                var open = draft.GetByRole(AriaRole.Button, new() { Name = "Publicar borrador" });
                await open.ClickAsync();
                var dialog = page.Locator("dialog[open]");
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-confirmation.png", mobile);
                await page.Keyboard.PressAsync("Escape");
                Assert.True(await open.EvaluateAsync<bool>("el => document.activeElement === el"));

                await open.ClickAsync();
                dialog = page.Locator("dialog[open]");
                await dialog.GetByRole(AriaRole.Button, new() { Name = "Publicar release" }).ClickAsync();
                Assert.True(await dialog.GetByLabel("Fecha y hora de vigencia (America/Mexico_City)")
                    .EvaluateAsync<bool>("el => el.validity.valueMissing"));
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-validation.png", mobile);
                await dialog.GetByLabel("Fecha y hora de vigencia (America/Mexico_City)")
                    .FillAsync(LocalFuture(3));
                await dialog.GetByLabel("Motivo").FillAsync("Publicación inicial sintética");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Publicar release" }), 200);
                Assert.Contains("Release publicada", await page.GetByRole(AriaRole.Status).InnerTextAsync());
                Assert.Contains("Vigente", await page.Locator("#release-history tbody").InnerTextAsync());
                Assert.Contains("Publicación inicial sintética", await page.Locator("#release-history tbody").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-published-v1.png", mobile);

                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador" }), 200);
                draft = DraftRow(page);
                await draft.GetByRole(AriaRole.Button, new() { Name = "Publicar borrador" }).ClickAsync();
                dialog = page.Locator("dialog[open]");
                await dialog.GetByLabel("Fecha y hora de vigencia (America/Mexico_City)")
                    .FillAsync(LocalFuture(2));
                await dialog.GetByLabel("Motivo").FillAsync("Solapamiento sintético");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Publicar release" }), 422);
                Assert.Contains("La vigencia se solapa", await page.Locator("dialog[open]").InnerTextAsync());
                Assert.True(await page.Locator("dialog[open] [data-dialog-error]")
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-overlap.png", mobile);
                await page.Locator("dialog[open]").GetByLabel("Fecha y hora de vigencia (America/Mexico_City)")
                    .FillAsync(LocalFuture(5));
                await page.Locator("dialog[open]").GetByLabel("Motivo").FillAsync("Publicación sucesora sintética");
                await SubmitAsync(page, page.Locator("dialog[open]").GetByRole(AriaRole.Button,
                    new() { Name = "Publicar release" }), 200);
                Assert.Contains("Sustituida", await page.Locator("#release-history tbody").InnerTextAsync());
                Assert.Contains("Vigente", await page.Locator("#release-history tbody").InnerTextAsync());
                Assert.Contains("2", await page.Locator("#release-history tbody").InnerTextAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-history-v2.png", mobile);

                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador" }), 200);
                draft = DraftRow(page);
                await draft.GetByRole(AriaRole.Button, new() { Name = "Publicar borrador" }).ClickAsync();
                dialog = page.Locator("dialog[open]");
                await dialog.GetByLabel("Fecha y hora de vigencia (America/Mexico_City)")
                    .FillAsync(LocalFuture(7));
                await dialog.GetByLabel("Motivo").FillAsync("Conflicto sintético");
                await dialog.Locator("input[name=releaseEtag]")
                    .EvaluateAsync("el => el.value = '\"999\"'");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Publicar release" }), 412);
                Assert.Contains("Este borrador cambió", await page.Locator("#configuration-error").InnerTextAsync());
                Assert.Equal(0, await DraftRow(page).GetByRole(AriaRole.Button,
                    new() { Name = "Publicar borrador" }).CountAsync());
                await CheckWidthAndCaptureAsync(page, output, $"{viewport}-conflict-412.png", mobile);
                var reloadTask = page.WaitForResponseAsync(response => response.Request.Method == "GET" &&
                    response.Url.EndsWith("/configuracion", StringComparison.OrdinalIgnoreCase));
                await page.GetByRole(AriaRole.Link, new() { Name = "Recargar releases" }).ClickAsync();
                Assert.Equal(200, (await reloadTask).Status);
                Assert.Equal(1, await DraftRow(page).GetByRole(AriaRole.Button,
                    new() { Name = "Publicar borrador" }).CountAsync());

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
                    var denied = await deniedContext.NewPageAsync();
                    Assert.Equal(200, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        "/configuracion").AbsoluteUri))?.Status);
                    Assert.Equal(1, await denied.Locator("#branch-details").CountAsync());
                    Assert.Equal(0, await denied.Locator("#release-history, #nuevo-borrador").CountAsync());
                    if (index == 1) await CheckWidthAndCaptureAsync(denied, output, $"{viewport}-denied.png", mobile);
                    Assert.Equal(403, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        "/api/v1/configuration/releases").AbsoluteUri))?.Status);
                }
            }
            finally
            {
                await fixture.DisposeAsync();
                Assert.True(fixture.CleanupComplete);
            }
        }
    }

    private static ILocator DraftRow(IPage page) =>
        page.Locator("#release-history tbody tr").Filter(new() { HasTextString = "Borrador" }).Last;

    private static string LocalFuture(int days) => TimeZoneInfo.ConvertTime(
        DateTimeOffset.UtcNow.AddDays(days), TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"))
        .ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    private static async Task SubmitAsync(IPage page, ILocator submit, int expectedStatus)
    {
        var responseTask = page.WaitForResponseAsync(response => response.Request.Method == "POST" &&
            response.Url.Contains("/configuracion", StringComparison.OrdinalIgnoreCase));
        await submit.ClickAsync();
        Assert.Equal(expectedStatus, (await responseTask).Status);
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        if (expectedStatus == 200) await page.GetByRole(AriaRole.Status).WaitForAsync();
        else await page.Locator("#configuration-error").WaitForAsync();
    }

    private static Task SetSessionAsync(IBrowserContext context, BrowserFixture fixture, string ticket) =>
        context.AddCookiesAsync([new Cookie
        {
            Name = "__Host-SGOL-Session", Value = ticket,
            Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true,
            SameSite = SameSiteAttribute.Strict,
        }]);

    private static async Task CheckWidthAndCaptureAsync(IPage page, string output, string name, bool mobile)
    {
        var widths = await page.EvaluateAsync<int[]>("() => [window.innerWidth, document.documentElement.scrollWidth, document.body.scrollWidth]");
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
