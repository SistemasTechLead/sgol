using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front016BrowserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task FourRolesSeeOwnInboxAllowedHistoryAndReadNoticeWithoutChangingTasks(bool mobile)
    {
        var fixture = new BrowserFixture();
        try
        {
            await fixture.StartAsync();
            var tickets = new Dictionary<Guid, string>();
            foreach (var account in fixture.Accounts) tickets.Add(account.UserId, await fixture.AuthenticateAsync(account));
            var tasks = await fixture.SeedMyWorkAsync();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium).LaunchAsync(new() { Headless = true });
            foreach (var account in fixture.Accounts)
            {
                await using var context = await browser.NewContextAsync(new()
                {
                    ViewportSize = mobile ? new() { Width = 390, Height = 844 } : new() { Width = 1440, Height = 900 },
                    IsMobile = mobile,
                    HasTouch = mobile,
                    Locale = "es-MX",
                    IgnoreHTTPSErrors = true,
                    ServiceWorkers = ServiceWorkerPolicy.Block
                });
                await context.AddCookiesAsync([new Cookie { Name = "__Host-SGOL-Session", Value = tickets[account.UserId], Url = fixture.BaseAddress.AbsoluteUri, Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict }]);
                var page = await context.NewPageAsync();
                var before = await fixture.MyWorkRowsAsync();
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress, "/mi-trabajo").AbsoluteUri))?.Status);
                Assert.Equal(3, await page.Locator("[data-own-task]").CountAsync());
                foreach (var other in tasks.Where(t => t.UserId != account.UserId))
                {
                    Assert.Equal(0, await page.Locator($"[data-own-task='{other.Id}']").CountAsync());
                    Assert.Equal(0, await page.Locator($"[data-own-notice='{other.NoticeId}']").CountAsync());
                }
                Assert.Contains("Disponible", await page.Locator("#mis-tareas").InnerTextAsync());
                Assert.Contains("Vencida", await page.Locator("#mis-tareas").InnerTextAsync());
                Assert.Contains("Concluida", await page.Locator("#mis-tareas").InnerTextAsync());
                Assert.Contains("Incompleta", await page.Locator("#mis-tareas").InnerTextAsync());
                Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                await page.Locator("#mis-tareas select").FocusAsync();
                await page.Keyboard.PressAsync("Tab");
                await Assertions.Expect(page.Locator("#mis-tareas button[type='submit']")).ToBeFocusedAsync();
                Assert.True(await page.Locator("#mis-tareas button[type='submit']").EvaluateAsync<bool>(
                    "el => { const s = getComputedStyle(el); return s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0; }"));
                await CaptureAsync(page, account.Role, mobile, "bandeja");
                var future = tasks.Single(t => t.UserId == account.UserId && t.State == "FUTURA");
                await page.Locator($"[data-visible-task='{future.Id}']").GetByRole(AriaRole.Link, new() { Name = "Ver este período en mi bandeja" }).ClickAsync();
                Assert.Equal(1, await page.Locator("[data-own-task]").CountAsync());
                Assert.Contains("Futura", await page.Locator("#mis-tareas").InnerTextAsync());
                await page.Locator("#mis-tareas").GetByRole(AriaRole.Link, new() { Name = "Ver tarea", Exact = true }).ClickAsync();
                Assert.Contains("Manual", await page.Locator("main").InnerTextAsync());
                Assert.Contains("Generación solicitada", await page.Locator("#historia").InnerTextAsync());
                Assert.Contains("Asignación automática", await page.Locator("#historia").InnerTextAsync());
                await CaptureAsync(page, account.Role, mobile, "detalle");
                Assert.Equal(before, await fixture.MyWorkRowsAsync());
                await page.GetByRole(AriaRole.Link, new() { Name = "Volver a Mi trabajo" }).ClickAsync();
                await page.Locator("#avisos select").SelectOptionAsync("UNREAD");
                await page.Locator("#avisos").GetByRole(AriaRole.Button, new() { Name = "Aplicar filtros" }).ClickAsync();
                var beforeTasks = await fixture.MyWorkRowsAsync(includeNoticeAndAudit: false);
                var notice = page.Locator($"[data-own-notice='{future.NoticeId}']");
                await notice.GetByRole(AriaRole.Button, new() { Name = "Marcar como leído" }).ClickAsync();
                await page.WaitForURLAsync(url => url.Contains("handler=ReadNotice", StringComparison.Ordinal), new() { WaitUntil = WaitUntilState.Load });
                Assert.Contains("Aviso marcado como leído. La tarea no cambió", await page.Locator("main").InnerTextAsync());
                Assert.Equal(0, await page.Locator($"[data-own-notice='{future.NoticeId}']").CountAsync());
                await CaptureAsync(page, account.Role, mobile, "aviso-leido");
                await Assertions.Expect(page.Locator("#avisos-title")).ToBeFocusedAsync();
                Assert.Equal(beforeTasks, await fixture.MyWorkRowsAsync(includeNoticeAndAudit: false));
                await page.Locator("#avisos select").SelectOptionAsync("READ");
                await page.Locator("#avisos").GetByRole(AriaRole.Button, new() { Name = "Aplicar filtros" }).ClickAsync();
                await page.WaitForURLAsync(url => url.Contains("noticeStatus=READ", StringComparison.Ordinal), new() { WaitUntil = WaitUntilState.Load });
                Assert.Contains("Leído", await page.Locator($"[data-own-notice='{future.NoticeId}']").InnerTextAsync());
                Assert.Equal(0, await page.Locator("#avisos").GetByRole(AriaRole.Button, new() { Name = "Marcar como leído" }).CountAsync());
                Assert.True(await page.EvaluateAsync<bool>(AccessibilityCheck));
                Assert.Equal(beforeTasks, await fixture.MyWorkRowsAsync(includeNoticeAndAudit: false));
                var dimensions = await page.EvaluateAsync<int[]>("() => [innerWidth, document.documentElement.scrollWidth]");
                Assert.True(dimensions[1] <= dimensions[0]);
                if (account.Role != "DIRECCION")
                {
                    var forbidden = tasks.First(t => t.UserId == fixture.Accounts[0].UserId);
                    var response = await page.GotoAsync(new Uri(fixture.BaseAddress, "/mi-trabajo/tareas/" + forbidden.Id.ToString("D")).AbsoluteUri);
                    Assert.Equal(404, response?.Status);
                    Assert.Contains("No existe o no está disponible en tu alcance", await page.Locator("main").InnerTextAsync());
                    Assert.Equal(0, await page.Locator("#historia").CountAsync());
                }
            }
        }
        finally { await fixture.DisposeAsync(); Assert.True(fixture.CleanupComplete); }
    }

    private static async Task CaptureAsync(IPage page, string role, bool mobile, string screen)
    {
        var directory = Environment.GetEnvironmentVariable("SGOL_FRONT016_PREVIEW_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        var path = Path.GetFullPath(directory);
        if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains("Fuentes", StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Preview output cannot use Fuentes.");
        Directory.CreateDirectory(path);
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(path, $"{role}-{(mobile ? "movil" : "escritorio")}-{screen}.png"),
            FullPage = true
        });
        if (screen == "bandeja")
        {
            foreach (var section in new[] { "mis-tareas", "avisos", "consulta-tareas" })
                await page.Locator("#" + section).ScreenshotAsync(new()
                { Path = Path.Combine(path, $"{role}-{(mobile ? "movil" : "escritorio")}-{section}.png") });
        }
        if (screen == "aviso-leido")
            await page.GetByRole(AriaRole.Status).ScreenshotAsync(new()
            { Path = Path.Combine(path, $"{role}-{(mobile ? "movil" : "escritorio")}-confirmacion.png") });
    }

    private const string AccessibilityCheck = """
        () => {
          const ids = [...document.querySelectorAll('[id]')].map(el => el.id);
          if (new Set(ids).size !== ids.length) return false;
          if ([...document.querySelectorAll('main select')].some(el => !el.closest('label')?.innerText.trim())) return false;
          if ([...document.querySelectorAll('main table')].some(el => !el.querySelector('caption')?.innerText.trim())) return false;
          if ([...document.querySelectorAll('main thead th')].some(el => el.scope !== 'col')) return false;
          const luminance = color => {
            const values = color.match(/[\d.]+/g).slice(0, 3).map(Number).map(v => {
              const c = v / 255; return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
            });
            return values[0] * 0.2126 + values[1] * 0.7152 + values[2] * 0.0722;
          };
          return [...document.querySelectorAll('main .badge')].every(el => {
            const style = getComputedStyle(el), a = luminance(style.color), b = luminance(style.backgroundColor);
            return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05) >= 4.5;
          });
        }
        """;
}
