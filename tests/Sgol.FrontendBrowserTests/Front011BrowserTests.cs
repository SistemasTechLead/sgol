using System.Buffers.Binary;
using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class Front011BrowserTests
{
    [Fact]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task PublishedPoliciesShowCurrentVersionAndHistoryWithoutEditingPastObligations()
    {
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-011-evidence");
        Directory.CreateDirectory(output);
        using var playwright = await Playwright.CreateAsync();
        foreach (var mobile in new[] { false, true })
        {
            var fixture = new BrowserFixture();
            try
            {
                await fixture.StartAsync();
                await fixture.SeedPublishedPoliciesAsync(fixture.Accounts[0].UserId);
                var ticket = await fixture.AuthenticateAsync(fixture.Accounts[0]);
                await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                    .LaunchAsync(new() { Headless = true });
                await using var context = await NewContextAsync(browser, mobile);
                await SetSessionAsync(context, fixture, ticket);
                var page = await context.NewPageAsync();
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    "/configuracion?taskCode=TAR-0005").AbsoluteUri))?.Status);
                Assert.Contains("Versión vigente 1: RECURRENTE", await page.Locator("#activation-policy").InnerTextAsync());
                Assert.Contains("Versión vigente 1: rol SUBCOORDINACION", await page.Locator("#eligibility-policy").InnerTextAsync());
                Assert.Contains("Vigente", await page.Locator("#activation-policy-history").InnerTextAsync());
                Assert.Contains("Vigente", await page.Locator("#eligibility-policy-history").InnerTextAsync());
                await CaptureAsync(page, output, mobile, "published-policies");
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
    public async Task DirectionEditsClosedPoliciesAndOtherProfilesCannotReadThem()
    {
        var output = Path.Combine(Directory.GetParent(BrowserFixture.RepositoryRoot())!.FullName,
            "front-011-evidence");
        Directory.CreateDirectory(output);
        using var playwright = await Playwright.CreateAsync();
        foreach (var mobile in new[] { false, true })
        {
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
                Assert.Equal(200, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                    "/configuracion?taskCode=TAR-0005").AbsoluteUri))?.Status);
                Assert.Equal(8, await page.Locator("#task-definition-catalog tbody tr").CountAsync());
                Assert.Contains("Aún no hay versiones de esta política", await page.Locator("#activation-policy").InnerTextAsync());
                await CaptureAsync(page, output, mobile, "catalog-and-empty");

                await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear borrador", Exact = true }), "handler=Create", 200);
                foreach (var code in new[] { "TAR-0005", "TAR-0007", "TAR-0026" })
                {
                    await page.Locator("#task-code-select").SelectOptionAsync(code);
                    var release = await page.Locator("#task-release-select option:nth-child(2)").GetAttributeAsync("value");
                    Assert.False(string.IsNullOrWhiteSpace(release));
                    await page.Locator("#task-release-select").SelectOptionAsync(release!);
                    await SubmitAsync(page, page.GetByRole(AriaRole.Button, new() { Name = "Crear versión TAR" }),
                        "handler=CreateTaskVersion", 200);
                }

                await page.GetByRole(AriaRole.Link, new() { Name = "Ver detalle de TAR-0005" }).ClickAsync();
                Assert.Contains("RECURRENTE", await page.Locator("#activation-policy").InnerTextAsync());
                await page.Locator("#activation-policy button[data-dialog-open]").First.ClickAsync();
                var dialog = page.Locator("dialog[open]");
                Assert.True(await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" })
                    .EvaluateAsync<bool>("el => document.activeElement === el"));
                Assert.Contains("12:00 y 17:00", await dialog.InnerTextAsync());
                await CaptureAsync(page, output, mobile, "recurring-confirmation");
                await page.Keyboard.PressAsync("Escape");
                await page.Locator("#activation-policy button[data-dialog-open]").First.ClickAsync();
                var activationIntent = await page.Locator("dialog[open] input[name=intentKey]").InputValueAsync();
                await SubmitAsync(page, page.Locator("dialog[open]").GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de activación" }), "handler=ActivationPolicy", 200);
                Assert.Contains("Política guardada en borrador", await page.Locator("#activation-policy").InnerTextAsync());
                Assert.Contains("Borrador", await page.Locator("#activation-policy-history").InnerTextAsync());
                await CaptureAsync(page, output, mobile, "activation-draft");

                await page.Locator("#activation-policy button[data-dialog-open]").First.ClickAsync();
                await page.Locator("dialog[open] input[name=intentKey]").EvaluateAsync(
                    "(el, key) => el.value = key", activationIntent);
                await SubmitAsync(page, page.Locator("dialog[open]").GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de activación" }), "handler=ActivationPolicy", 200);
                Assert.Contains("ya se había procesado", await page.Locator("#activation-policy").InnerTextAsync());
                Assert.Equal(1, await page.Locator("#activation-policy-history tbody tr").CountAsync());
                await CaptureAsync(page, output, mobile, "activation-replay");

                await page.Locator("#eligibility-policy button[data-dialog-open]").First.ClickAsync();
                dialog = page.Locator("dialog[open]");
                Assert.Contains("SUBCOORDINACION", await dialog.InnerTextAsync());
                Assert.Contains("disponibilidad positiva obligatoria", await dialog.InnerTextAsync());
                Assert.Contains("turno sin restricción", await dialog.InnerTextAsync());
                await CaptureAsync(page, output, mobile, "eligibility-confirmation");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de elegibilidad" }), "handler=EligibilityPolicy", 200);
                Assert.Contains("Borrador", await page.Locator("#eligibility-policy-history").InnerTextAsync());
                await CaptureAsync(page, output, mobile, "eligibility-draft");

                await page.Locator("#activation-policy button[data-dialog-open]").First.ClickAsync();
                dialog = page.Locator("dialog[open]");
                await dialog.Locator("input[name=policyEtag]").EvaluateAsync("el => el.value = '\"999\"'");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de activación" }), "handler=ActivationPolicy", 412);
                Assert.Contains("Esta política cambió", await page.Locator("#activation-policy-error").InnerTextAsync());
                Assert.Equal(0, await page.Locator("#activation-policy button[data-dialog-open]").CountAsync());
                Assert.Equal(1, await page.Locator("#activation-policy-history tbody tr").CountAsync());
                await CaptureAsync(page, output, mobile, "conflict-412");
                await page.GetByRole(AriaRole.Link, new() { Name = "Recargar política de activación" }).ClickAsync();
                Assert.Equal(1, await page.Locator("#activation-policy button[data-dialog-open]").CountAsync());

                await page.GetByRole(AriaRole.Link, new() { Name = "Ver detalle de TAR-0007" }).ClickAsync();
                Assert.Contains("MANUAL", await page.Locator("#activation-policy").InnerTextAsync());
                await page.Locator("#activation-policy button[data-dialog-open]").First.ClickAsync();
                Assert.DoesNotContain("Hora local", await page.Locator("dialog[open]").InnerTextAsync());
                await CaptureAsync(page, output, mobile, "manual-confirmation");
                await page.Keyboard.PressAsync("Escape");

                await page.GetByRole(AriaRole.Link, new() { Name = "Ver detalle de TAR-0026" }).ClickAsync();
                await page.Locator("#activation-policy button[data-dialog-open]").First.ClickAsync();
                dialog = page.Locator("dialog[open]");
                Assert.Contains("tres días hábiles antes", (await dialog.InnerTextAsync()).ToLowerInvariant());
                await dialog.GetByLabel("Hora local (America/Mexico_City)").EvaluateAsync(
                    "el => { el.type = 'text'; el.value = '24:00'; }");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de activación" }), "handler=ActivationPolicy", 400);
                dialog = page.Locator("dialog[open]");
                Assert.Contains("Revisa la hora local", await dialog.InnerTextAsync());
                Assert.Equal("true", await dialog.GetByLabel("Hora local (America/Mexico_City)").GetAttributeAsync("aria-invalid"));
                await CaptureAsync(page, output, mobile, "validation-error");
                await dialog.GetByLabel("Hora local (America/Mexico_City)").FillAsync("08:30");
                await CaptureAsync(page, output, mobile, "service-schedule");
                await SubmitAsync(page, dialog.GetByRole(AriaRole.Button,
                    new() { Name = "Guardar política de activación" }), "handler=ActivationPolicy", 200);
                Assert.Contains("Borrador", await page.Locator("#activation-policy-history").InnerTextAsync());

                for (var index = 1; index < tickets.Count; index++)
                {
                    await using var deniedContext = await NewContextAsync(browser, mobile);
                    await SetSessionAsync(deniedContext, fixture, tickets[index]);
                    var denied = await deniedContext.NewPageAsync();
                    Assert.Equal(200, (await denied.GotoAsync(new Uri(fixture.BaseAddress,
                        "/configuracion?taskCode=TAR-0005").AbsoluteUri))?.Status);
                    Assert.Equal(0, await denied.Locator("#activation-policy, #eligibility-policy").CountAsync());
                    if (index == 1)
                    {
                        await CaptureAsync(denied, output, mobile, "denied-profile");
                        var deniedPost = denied.WaitForResponseAsync(response => response.Request.Method == "POST" &&
                            response.Url.Contains("handler=ActivationPolicy", StringComparison.Ordinal));
                        await denied.EvaluateAsync("""
                            ({ releaseId, versionId, intentKey }) => {
                              const token = document.querySelector('.encabezado-aplicacion__logout input[name="__RequestVerificationToken"]').value;
                              const form = document.createElement('form');
                              form.method = 'post'; form.action = '/configuracion?handler=ActivationPolicy';
                              for (const [name, value] of Object.entries({
                                __RequestVerificationToken: token, taskCode: 'TAR-0005', releaseId,
                                taskDefinitionVersionId: versionId, intentKey
                              })) {
                                const input = document.createElement('input');
                                input.type = 'hidden'; input.name = name; input.value = value; form.append(input);
                              }
                              document.body.append(form); form.submit();
                            }
                            """, new
                        {
                            releaseId = Guid.CreateVersion7().ToString("D"),
                            versionId = Guid.CreateVersion7().ToString("D"),
                            intentKey = Guid.CreateVersion7().ToString("D"),
                        });
                        Assert.Equal(403, (await deniedPost).Status);
                        await denied.Locator("#activation-policy-error").WaitForAsync();
                        Assert.Equal(0, await denied.Locator("#activation-policy-history, #eligibility-policy-history").CountAsync());
                        await CaptureAsync(denied, output, mobile, "authorization-error");
                    }
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
        finally
        {
            page.DOMContentLoaded -= OnLoaded;
        }
    }

    private static async Task CaptureAsync(IPage page, string output, bool mobile, string state)
    {
        var widths = await page.EvaluateAsync<int[]>(
            "() => [window.innerWidth, document.documentElement.scrollWidth, document.body.scrollWidth]");
        Assert.True(widths[1] <= widths[0] && widths[2] <= widths[0], string.Join('/', widths));
        var bytes = await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(output, $"{(mobile ? "mobile" : "desktop")}-{state}.png"),
            FullPage = true,
            Mask = [page.Locator(".encabezado-aplicacion")],
        });
        Assert.Equal(mobile ? 390 : 1440, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
    }
}
