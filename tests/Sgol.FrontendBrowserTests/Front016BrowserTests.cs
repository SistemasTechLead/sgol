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
            var tasks = await fixture.SeedMyWorkAsync();
            var tickets = new Dictionary<Guid, string>();
            foreach (var account in fixture.Accounts) tickets.Add(account.UserId, await fixture.AuthenticateAsync(account));
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
                var future = tasks.Single(t => t.UserId == account.UserId && t.State == "FUTURA");
                await page.Locator($"[data-visible-task='{future.Id}']").GetByRole(AriaRole.Link, new() { Name = "Ver este período en mi bandeja" }).ClickAsync();
                Assert.Equal(1, await page.Locator("[data-own-task]").CountAsync());
                Assert.Contains("Futura", await page.Locator("#mis-tareas").InnerTextAsync());
                await page.Locator("#mis-tareas").GetByRole(AriaRole.Link, new() { Name = "Ver tarea", Exact = true }).ClickAsync();
                Assert.Contains("Manual", await page.Locator("main").InnerTextAsync());
                Assert.Contains("Generación solicitada", await page.Locator("#historia").InnerTextAsync());
                Assert.Contains("Asignación automática", await page.Locator("#historia").InnerTextAsync());
                Assert.Equal(before, await fixture.MyWorkRowsAsync());
                await page.GetByRole(AriaRole.Link, new() { Name = "Volver a Mi trabajo" }).ClickAsync();
                await page.Locator("#avisos select").SelectOptionAsync("UNREAD");
                await page.Locator("#avisos").GetByRole(AriaRole.Button, new() { Name = "Aplicar filtros" }).ClickAsync();
                var beforeTasks = await fixture.MyWorkRowsAsync(includeNoticeAndAudit: false);
                var notice = page.Locator($"[data-own-notice='{future.NoticeId}']");
                await notice.GetByRole(AriaRole.Button, new() { Name = "Marcar como leído" }).ClickAsync();
                await page.WaitForLoadStateAsync();
                Assert.Contains("Aviso marcado como leído. La tarea no cambió", await page.Locator("main").InnerTextAsync());
                Assert.Equal(0, await page.Locator($"[data-own-notice='{future.NoticeId}']").CountAsync());
                Assert.True(await page.Locator("#avisos-title").EvaluateAsync<bool>("e => e === document.activeElement"));
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
}
