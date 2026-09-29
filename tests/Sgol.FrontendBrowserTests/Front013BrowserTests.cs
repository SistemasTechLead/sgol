using System.Buffers.Binary;
using System.Globalization;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed partial class Front013BrowserTests
{
    [Theory, Trait("Category", "FRONT_BROWSER")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SixClosedFormsCreateAndRecoverStableResultsOnDesktopAndMobile(bool mobile)
    {
        using var playwright = await Playwright.CreateAsync();

        var fixture = new BrowserFixture();
        try
        {
            await fixture.StartAsync();
            await fixture.SeedPublishedEvidenceValidationAsync(fixture.Accounts[0].UserId);
            await fixture.SeedManualCalendarAsync(fixture.Accounts[0].UserId);
            var ticket = await fixture.AuthenticateAsync(fixture.Accounts[0]);
            await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium).LaunchAsync(new() { Headless = true });
            await using var context = await ContextAsync(browser, mobile);
            await SessionAsync(context, fixture, ticket);
            var page = await context.NewPageAsync();
            var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).AddDays(-1);
            var originTime = local.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            var expires = local.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            foreach (var code in new[] { "TAR-0007", "TAR-0008", "TAR-0011", "TAR-0018", "TAR-0092", "TAR-0093" })
            {
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress, $"/planificacion?taskCode={code}#alta-manual").AbsoluteUri))?.Status);
                Assert.Equal(7, await page.Locator("#manual-task option").CountAsync());
                Assert.DoesNotContain("TAR-0005", await page.Locator("#manual-task").InnerTextAsync());
                Assert.DoesNotContain("TAR-0026", await page.Locator("#manual-task").InnerTextAsync());
                var fields = page.Locator("#manual-form input:not([type=hidden])");
                for (var index = 0; index < await fields.CountAsync(); index++)
                {
                    var field = fields.Nth(index);
                    var name = await field.GetAttributeAsync("name");
                    var type = await field.GetAttributeAsync("type");
                    await field.FillAsync(type == "datetime-local" ? name == "expiresAt" ? expires : originTime : $"SYN-{code}-{index}");
                }
                if (code == "TAR-0008")
                {
                    await page.Locator("#add-claimant").ClickAsync();
                    Assert.True(await page.Locator("#manual-claimants input").Last.EvaluateAsync<bool>("e => document.activeElement === e"));
                    await page.Locator("#manual-claimants input").Last.FillAsync("SYN-THIRD");
                    await page.Locator("#manual-claimants [data-remove-claimant]").Last.ClickAsync();
                    Assert.Equal(2, await page.Locator("#manual-claimants input").CountAsync());
                }
                if (code == "TAR-0011") await page.Locator("#manual-solutionType").SelectOptionAsync("CAMBIO");
                if (code == "TAR-0093")
                {
                    await page.Locator("#manual-description").FillAsync("Daño sintético de recepción");
                    await page.Locator("#manual-incidentType").SelectOptionAsync("DANO");
                    var parent = await page.Locator("#manual-parentObligationId option:nth-child(2)").GetAttributeAsync("value");
                    await page.Locator("#manual-parentObligationId").SelectOptionAsync(parent!);
                    await page.Locator("#manual-parentObligationId").FocusAsync();
                    await page.Locator("#manual-parentObligationId").PressAsync("Tab");
                    Assert.Contains("Período heredado", await page.Locator("#parent-period").InnerTextAsync());
                }
                await CaptureAsync(page, mobile, code + "-form");
                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Revisar solicitud" }), "PrepareManual", 200);
                var dialog = page.Locator("#manual-confirm");
                Assert.True(await dialog.EvaluateAsync<bool>("e => e.open"));
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" }).EvaluateAsync<bool>("e => document.activeElement === e"));
                await CaptureAsync(page, mobile, code + "-confirm");
                await page.Keyboard.PressAsync("Escape");
                Assert.False(await dialog.EvaluateAsync<bool>("e => e.open"));
                await Assertions.Expect(page.Locator("#manual-open-confirm")).ToBeFocusedAsync();
                await page.Locator("#manual-open-confirm").ClickAsync();
                if (code == "TAR-0007")
                {
                    // Hold native submission before navigation, leaving the real progress listener active.
                    // A paused navigation makes Playwright locators wait for that same navigation.
                    await dialog.Locator("form").EvaluateAsync("f => f.addEventListener('submit', e => e.preventDefault(), {once:true})");
                    await dialog.GetByRole(AriaRole.Button, new() { Name = "Crear solicitud", Exact = true }).ClickAsync();
                    var submit = dialog.Locator("button[type=submit]");
                    Assert.True(await submit.IsDisabledAsync());
                    Assert.Equal("Enviando solicitud…", await submit.InnerTextAsync());
                    Assert.Equal("true", await dialog.Locator("form").GetAttributeAsync("aria-busy"));
                    await CaptureAsync(page, mobile, "loading");
                    await SubmitActionAsync(page, () => page.EvaluateAsync("() => HTMLFormElement.prototype.submit.call(document.querySelector('#manual-confirm form'))"), "CreateManual", 200);
                }
                else
                    await SubmitAsync(page, dialog.GetByRole(AriaRole.Button, new() { Name = "Crear solicitud", Exact = true }), "CreateManual", 200);
                Assert.Contains("Solicitud aceptada", await page.Locator("#manual-result").InnerTextAsync());
                var request = await page.Locator("#manual-result-id").InnerTextAsync();
                var obligation = await page.Locator("#manual-obligation-id").InnerTextAsync();
                Assert.True(Guid.TryParse(request, out _));
                Assert.True(Guid.TryParse(obligation, out _));
                await CaptureAsync(page, mobile, code + "-accepted");
                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Recuperar resultado" }), "CreateManual", 200);
                Assert.Contains("Se recuperó la misma solicitud", await page.Locator("#manual-result").InnerTextAsync());
                Assert.Equal(request, await page.Locator("#manual-result-id").InnerTextAsync());
                Assert.Equal(obligation, await page.Locator("#manual-obligation-id").InnerTextAsync());
                await CaptureAsync(page, mobile, code + "-replay");
                var get = page.WaitForResponseAsync(r => r.Request.Method == "GET" && r.Url.Contains("generationRequestId=", StringComparison.Ordinal));
                await page.GetByRole(AriaRole.Link, new() { Name = "Consultar resultado" }).ClickAsync();
                Assert.Equal(200, (await get).Status);
                Assert.Equal(request, await page.Locator("#manual-result-id").InnerTextAsync());
                await WidthAsync(page);
            }
        }
        finally { await fixture.DisposeAsync(); Assert.True(fixture.CleanupComplete); }
    }

    private static Task<IBrowserContext> ContextAsync(IBrowser browser, bool mobile) => browser.NewContextAsync(new()
    {
        ViewportSize = mobile ? new() { Width = 390, Height = 844 } : new() { Width = 1440, Height = 900 },
        Locale = "es-MX",
        IsMobile = mobile,
        HasTouch = mobile,
        IgnoreHTTPSErrors = true,
        ServiceWorkers = ServiceWorkerPolicy.Block
    });
    private static Task SessionAsync(IBrowserContext context, BrowserFixture fixture, string ticket) => context.AddCookiesAsync([new Cookie
    { Name = "__Host-SGOL-Session", Value = ticket, Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]);
    private static Task SubmitAsync(IPage page, ILocator button, string handler, int status) =>
        SubmitActionAsync(page, () => button.ClickAsync(), handler, status);
    private static async Task SubmitActionAsync(IPage page, Func<Task> action, string handler, int status)
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Loaded(object? sender, IPage value) => loaded.TrySetResult();
        page.DOMContentLoaded += Loaded;
        try
        {
            var response = page.WaitForResponseAsync(r => r.Request.Method == "POST" && r.Url.Contains("handler=" + handler, StringComparison.Ordinal));
            await action();
            Assert.Equal(status, (await response).Status);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { page.DOMContentLoaded -= Loaded; }
    }
    private static async Task WidthAsync(IPage page)
    {
        var widths = await page.EvaluateAsync<int[]>("() => [innerWidth, document.documentElement.scrollWidth, document.body.scrollWidth]");
        var offenders = await page.EvaluateAsync<string>("() => [...document.querySelectorAll('body *')].filter(e => e.getBoundingClientRect().right > innerWidth || e.scrollWidth > e.clientWidth + 1).map(e => e.tagName + '#' + e.id + ':' + Math.round(e.getBoundingClientRect().width) + '/' + e.scrollWidth + '/' + getComputedStyle(e).overflowX).slice(0, 18).join(',')");
        Assert.True(widths[1] <= widths[0] && widths[2] <= widths[0], string.Join('/', widths) + " " + offenders);
    }
    private static async Task CaptureAsync(IPage page, bool mobile, string state)
    {
        var directory = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName, "front-013-evidence");
        Directory.CreateDirectory(directory);
        var png = await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(directory, $"{(mobile ? "mobile" : "desktop")}-{state}.png"),
            FullPage = true,
            Mask = [page.Locator(".encabezado-aplicacion")]
        });
        await WidthAsync(page);
        Assert.Equal(mobile ? 390 : 1440, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
    }
}
