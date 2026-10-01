using Microsoft.Playwright;
using Xunit;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed class RenewedDesignBrowserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "FRONT_BROWSER")]
    public async Task ExistingSurfacesKeepTablesAndAccessibleRenewedComposition(bool mobile)
    {
        var fixture = new BrowserFixture();
        var evidenceDirectory = Environment.GetEnvironmentVariable("SGOL_RENEWED_DESIGN_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidenceDirectory))
        {
            var path = Path.GetFullPath(evidenceDirectory);
            if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Contains("Fuentes", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Protected evidence path.");
            Directory.CreateDirectory(path);
            fixture.Progress = phase => File.AppendAllText(Path.Combine(path, $"{(mobile ? "mobile" : "desktop")}-stages.log"), phase + Environment.NewLine);
        }
        try
        {
            await fixture.StartAsync();
            var direction = fixture.Accounts[0];
            var ticket = await fixture.AuthenticateAsync(direction);
            var tasks = await fixture.SeedMyWorkAsync(enrolledUsers: true);
            var ownTask = tasks.First(t => t.UserId == direction.UserId && t.State == "DISPONIBLE");
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await (mobile ? playwright.Webkit : playwright.Chromium)
                .LaunchAsync(new() { Headless = true });
            await using var context = await browser.NewContextAsync(new()
            {
                ViewportSize = mobile ? new() { Width = 390, Height = 844 } : new() { Width = 1440, Height = 900 },
                IsMobile = mobile,
                HasTouch = mobile,
                IgnoreHTTPSErrors = true,
                Locale = "es-MX",
                ServiceWorkers = ServiceWorkerPolicy.Block,
                ReducedMotion = ReducedMotion.Reduce,
            });
            var page = await context.NewPageAsync();
            await page.GotoAsync(new Uri(fixture.BaseAddress, "/acceso").AbsoluteUri);
            await VerifyAsync(page, "acceso", mobile);
            await context.AddCookiesAsync([new Cookie
            {
                Name = "__Host-SGOL-Session", Value = ticket, Url = fixture.BaseAddress.AbsoluteUri,
                Secure = true, HttpOnly = true, SameSite = SameSiteAttribute.Strict,
            }]);
            var before = await fixture.MyWorkRowsAsync();
            var routes = new (string Name, string Path)[]
            {
                ("personas", "/personas-y-accesos"),
                ("persona-detalle", $"/personas-y-accesos/personas/{direction.PersonId:D}"),
                ("configuracion", "/configuracion?taskCode=TAR-0008"),
                ("planificacion", "/planificacion"),
                ("mi-trabajo", "/mi-trabajo"),
                ("tarea-detalle", $"/mi-trabajo/tareas/{ownTask.Id:D}"),
                ("sucursal", "/branches/LOR-001"),
            };
            foreach (var route in routes)
            {
                var routeResponse = await page.GotoAsync(new Uri(fixture.BaseAddress, route.Path).AbsoluteUri);
                if (routeResponse?.Status != 200) await CaptureAsync(page, route.Name + "-http-diagnostico", mobile);
                Assert.True(routeResponse?.Status == 200, route.Name + " HTTP " + routeResponse?.Status + ": " + string.Join("; ", await page.Locator(".alerta__contenido").AllTextContentsAsync()));
                await VerifyAsync(page, route.Name, mobile);
                // These pages only read existing data; opening the renewed composition must not mutate business rows.
                Assert.Equal(before, await fixture.MyWorkRowsAsync());
            }
            // This fixture has assignments but no confirmed eligibility evaluation.
            // Preserve the server rejection and verify its visual error state explicitly.
            var missingEvaluation = await page.GotoAsync(new Uri(fixture.BaseAddress,
                $"/planificacion?obligationId={ownTask.Id:D}").AbsoluteUri);
            Assert.Equal(404, missingEvaluation?.Status);
            await VerifyAsync(page, "asignacion-sin-evaluacion", mobile);
            Assert.Equal(before, await fixture.MyWorkRowsAsync());
            await page.GotoAsync(new Uri(fixture.BaseAddress, "/planificacion").AbsoluteUri);
            await page.GetByRole(AriaRole.Button, new() { Name = "Crear o recuperar plan", Exact = true }).ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Contains("Plan confirmado", await page.Locator("#plan-semanal").InnerTextAsync());
            await CaptureAsync(page, "plan-confirmado", mobile);
            await page.GetByRole(AriaRole.Button, new() { Name = "Preparar publicación", Exact = true }).ClickAsync();
            var publication = page.Locator("#plan-confirm");
            await Assertions.Expect(publication.GetByRole(AriaRole.Button, new() { Name = "Cancelar", Exact = true })).ToBeFocusedAsync();
            await CaptureAsync(page, "publicacion-confirmacion", mobile);
            await page.Keyboard.PressAsync("Escape");
            await page.GotoAsync(new Uri(fixture.BaseAddress, "/personas-y-accesos").AbsoluteUri);
            var opener = page.GetByRole(AriaRole.Button, new() { Name = "Desactivar cuenta", Exact = true }).First;
            await opener.ClickAsync();
            var dialog = page.Locator("dialog[open]");
            await Assertions.Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar", Exact = true })).ToBeFocusedAsync();
            await CaptureAsync(page, "confirmacion-cuenta", mobile);
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(opener).ToBeFocusedAsync();
            if (mobile)
            {
                var navigation = page.GetByRole(AriaRole.Button, new() { Name = "Navegación principal", Exact = true });
                await navigation.ClickAsync();
                await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Cerrar navegación" })).ToBeFocusedAsync();
                await page.Keyboard.PressAsync("Tab");
                Assert.True(await page.EvaluateAsync<bool>("() => document.activeElement.closest('#navegacion-movil') !== null"));
                await CaptureAsync(page, "navegacion-modal", mobile);
                await page.Keyboard.PressAsync("Escape");
                await Assertions.Expect(navigation).ToBeFocusedAsync();
            }
            // Do not include audit: reauthorization denials can legitimately append sensitive-denial events.
            var businessBefore = await fixture.MyWorkRowsAsync(includeNoticeAndAudit: false);
            Assert.Equal(404, (await page.GotoAsync(new Uri(fixture.BaseAddress,
                $"/mi-trabajo/tareas/{Guid.CreateVersion7():D}").AbsoluteUri))?.Status);
            await VerifyAsync(page, "tarea-no-disponible", mobile);
            Assert.Equal(businessBefore, await fixture.MyWorkRowsAsync(includeNoticeAndAudit: false));
        }
        finally
        {
            await fixture.DisposeAsync();
            Assert.True(fixture.CleanupComplete);
        }
    }

    private static async Task VerifyAsync(IPage page, string screen, bool mobile)
    {
        Assert.Equal(1, await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).CountAsync());
        await page.EvaluateAsync("() => document.fonts.ready");
        var accessibilityFailures = await page.EvaluateAsync<string[]>(AccessibilityCheck);
        if (accessibilityFailures.Length > 0) await CaptureAsync(page, screen + "-diagnostico", mobile);
        Assert.True(accessibilityFailures.Length == 0, screen + ": " + string.Join("; ", accessibilityFailures));
        await page.Locator(".salto-contenido").FocusAsync();
        Assert.True(await page.Locator(".salto-contenido").EvaluateAsync<bool>(
            "el => getComputedStyle(el).outlineStyle !== 'none' && parseFloat(getComputedStyle(el).outlineWidth) >= 2"));
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("main")).ToBeFocusedAsync();
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
        Assert.True(await page.Locator(".esqueleto").EvaluateAllAsync<bool>(
            "els => els.every(el => getComputedStyle(el).animationName === 'none')"));
        await CaptureAsync(page, screen, mobile);
        var initialText = await page.Locator("main").TextContentAsync();
        await page.SetViewportSizeAsync(320, 844);
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth"), screen + " reflow at 320px");
        // Text-only enlargement measures fonts before changing any ancestor, so each element is enlarged once.
        await page.EvaluateAsync("""
            () => {
              const values = [...document.querySelectorAll('main *, .encabezado-aplicacion *')]
                .map(el => [el, parseFloat(getComputedStyle(el).fontSize), parseFloat(getComputedStyle(el).lineHeight)]);
              for (const [el, size, line] of values) {
                el.style.fontSize = (size * 2) + 'px';
                if (Number.isFinite(line)) el.style.lineHeight = (line * 2) + 'px';
              }
            }
            """);
        Assert.Equal(initialText, await page.Locator("main").TextContentAsync());
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth"), screen + " enlarged text");
        await CaptureAsync(page, screen + "-texto-ampliado", mobile);
        await page.SetViewportSizeAsync(mobile ? 390 : 1440, mobile ? 844 : 900);
    }

    private static async Task CaptureAsync(IPage page, string screen, bool mobile)
    {
        var directory = Environment.GetEnvironmentVariable("SGOL_RENEWED_DESIGN_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) return;
        var path = Path.GetFullPath(directory);
        if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains("Fuentes", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Protected evidence path.");
        Directory.CreateDirectory(path);
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(path, $"{(mobile ? "mobile" : "desktop")}-{screen}.png"),
            FullPage = true,
            Mask = [page.Locator("[data-sensitive-activation], .acceso__codigos, input[type=password], #totp-key, [name=__RequestVerificationToken]")],
        });
    }

    private const string AccessibilityCheck = """
        () => {
          const failures = [];
          const visible = el => !el.closest('dialog:not([open]), [hidden]') && [...el.getClientRects()].some(r => r.width > 0 && r.height > 0) && getComputedStyle(el).visibility !== 'hidden';
          const ids = [...document.querySelectorAll('[id]')].map(el => el.id);
          if (new Set(ids).size !== ids.length) failures.push('duplicate id');
          for (const table of document.querySelectorAll('main table')) {
            if (!table.querySelector('caption')?.textContent.trim()) failures.push('table caption');
            if ([...table.querySelectorAll('thead th')].some(el => el.scope !== 'col')) failures.push('table scope');
            if (getComputedStyle(table).display !== 'table') failures.push('table semantics');
          }
          for (const control of document.querySelectorAll('button, a, summary, input:not([type=hidden]), select, textarea')) {
            if (!visible(control)) continue;
            const target = control.closest('.casilla, .grupo-radio__opcion') || control;
            const box = target.getBoundingClientRect();
            if (box.width < 43.9 || box.height < 43.9) failures.push('target area: ' + control.tagName + '.' + control.className);
          }
          const luminance = color => {
            const c = color.match(/[\d.]+/g).slice(0,3).map(Number).map(v => {
              v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4;
            });
            return c[0] * .2126 + c[1] * .7152 + c[2] * .0722;
          };
          for (const el of document.querySelectorAll('.badge, .alerta, .boton:not(:disabled), .campo__control:not(:disabled), .encabezado-pagina__titulo')) {
            if (!visible(el)) continue;
            const s = getComputedStyle(el);
            let ancestor = el, background = s.backgroundColor;
            while (background === 'rgba(0, 0, 0, 0)' && ancestor.parentElement) {
              ancestor = ancestor.parentElement; background = getComputedStyle(ancestor).backgroundColor;
            }
            // A transparent root is composited over the browser's white canvas, never black.
            if (background === 'rgba(0, 0, 0, 0)') background = 'rgb(255, 255, 255)';
            const a = luminance(s.color), b = luminance(background);
            if ((Math.max(a,b)+.05)/(Math.min(a,b)+.05) < 4.5) failures.push('text contrast: .' + el.className + ' fg=' + s.color + ' bg=' + background + ' ratio=' + ((Math.max(a,b)+.05)/(Math.min(a,b)+.05)));
          }
          return failures;
        }
        """;
}
