using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed partial class Front013BrowserTests
{
    [Theory, Trait("Category", "FRONT_BROWSER")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyValidationAuthorizationAndDeepLinksDoNotLeakManualInputs(bool mobile)
    {
        using var playwright = await Playwright.CreateAsync();
        var fixture = new BrowserFixture();
        try
        {
            await fixture.StartAsync();
            var tickets = new List<string>();
            foreach (var account in fixture.Accounts) tickets.Add(await fixture.AuthenticateAsync(account));
            await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium).LaunchAsync(new() { Headless = true });
            await using var context = await ContextAsync(browser, mobile);
            await SessionAsync(context, fixture, tickets[0]);
            var page = await context.NewPageAsync();
            Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress, "/planificacion#alta-manual").AbsoluteUri))?.Status);
            Assert.Contains("No hay tareas disponibles", await page.Locator("#manual-catalog").InnerTextAsync());
            await CaptureAsync(page, mobile, "empty-catalog");
            await fixture.SeedPublishedEvidenceValidationAsync(fixture.Accounts[0].UserId);
            Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress, "/planificacion?taskCode=TAR-0093#alta-manual").AbsoluteUri))?.Status);
            Assert.Contains("No hay recepciones disponibles", await page.Locator("#manual-receipts").InnerTextAsync());
            Assert.True(await page.GetByRole(AriaRole.Button, new() { Name = "Revisar solicitud" }).IsDisabledAsync());
            await CaptureAsync(page, mobile, "empty-receipts");
            await page.GotoAsync(new Uri(fixture.BaseAddress, "/planificacion?taskCode=TAR-0018#alta-manual").AbsoluteUri);
            await page.Locator("#manual-eventReference").FillAsync("https://example.invalid/not-an-origin");
            await page.Locator("#manual-zoneReference").FillAsync("SYN-Z");
            await page.Locator("#manual-planogramReference").FillAsync("SYN-P");
            await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Revisar solicitud" }), "PrepareManual", 422);
            Assert.Equal("true", await page.Locator("#manual-eventReference").GetAttributeAsync("aria-invalid"));
            Assert.True(await page.Locator("#manual-error").EvaluateAsync<bool>("e => document.activeElement === e"));
            await CaptureAsync(page, mobile, "validation-and-focus");
            await page.Locator("#manual-eventReference").FillAsync("SYN-E");
            await page.Locator("#manual-zoneReference").FillAsync("SYN-Z");
            await page.Locator("#manual-planogramReference").FillAsync("SYN-P");
            await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Revisar solicitud" }), "PrepareManual", 200);
            await SubmitAsync(page, page.Locator("#manual-confirm").GetByRole(AriaRole.Button, new() { Name = "Crear solicitud", Exact = true }), "CreateManual", 200);
            var id = await page.Locator("#manual-result-id").InnerTextAsync();
            await VerifyApiContractAsync(context, fixture, page);
            var originalHash = await fixture.SetManualReplayHashAsync(Guid.Parse(id), new string('0', 64));
            await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Recuperar resultado" }), "CreateManual", 409);
            Assert.Contains("Esta solicitud ya se envió con otros datos", await page.Locator("#manual-error").InnerTextAsync());
            Assert.Equal(0, await page.Locator("#manual-result-id").CountAsync());
            await CaptureAsync(page, mobile, "conflict-explicit-decision");
            await fixture.SetManualReplayHashAsync(Guid.Parse(id), originalHash);
            await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Recuperar resultado" }), "CreateManual", 200);
            Assert.Equal(id, await page.Locator("#manual-result-id").InnerTextAsync());
            for (var index = 1; index < tickets.Count; index++)
            {
                await using var profile = await ContextAsync(browser, mobile);
                await SessionAsync(profile, fixture, tickets[index]);
                if (index == 3) await VerifyDeniedApiAsync(profile, fixture);
                var other = await profile.NewPageAsync();
                var address = new Uri(fixture.BaseAddress, "/planificacion?taskCode=TAR-0018#alta-manual").AbsoluteUri;
                Assert.Equal(index == 3 ? 404 : 200, (await other.GotoAsync(address))?.Status);
                Assert.Equal(index == 3 ? 0 : 1, await other.Locator("#manual-form").CountAsync());
                await CaptureAsync(other, mobile, index == 3 ? "denied-profile" : "allowed-profile-" + index);
                var hidden = new Uri(fixture.BaseAddress, $"/planificacion?generationRequestId={id}#alta-manual").AbsoluteUri;
                Assert.Equal(404, (await other.GotoAsync(hidden))?.Status);
                Assert.Equal(0, await other.Locator("#manual-result-id").CountAsync());
                Assert.DoesNotContain("SYN-E", await other.Locator("main").InnerTextAsync());
                await CaptureAsync(other, mobile, "hidden-deep-link-" + index);
            }
        }
        finally { await fixture.DisposeAsync(); Assert.True(fixture.CleanupComplete); }
    }
}
