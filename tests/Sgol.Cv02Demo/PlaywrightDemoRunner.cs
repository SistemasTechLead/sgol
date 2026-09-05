using Microsoft.Playwright;

namespace Sgol.Cv02Demo;

internal static class PlaywrightDemoRunner
{
    public static async Task RunAsync(
        Uri baseAddress,
        string repositoryRoot,
        DemoState state,
        CancellationToken cancellationToken)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await RunBrowserAsync(playwright.Chromium, "chromium-desktop", DemoContract.ChromiumVersion,
                baseAddress, repositoryRoot, state, runScenarios: false, width: 1440, height: 900,
                isMobile: false, cancellationToken);
            await RunBrowserAsync(playwright.Chromium, "chromium-phone", DemoContract.ChromiumVersion,
                baseAddress, repositoryRoot, state, runScenarios: true, width: 390, height: 844,
                isMobile: true, cancellationToken);
            await RunBrowserAsync(playwright.Webkit, "webkit-phone", DemoContract.WebKitVersion,
                baseAddress, repositoryRoot, state, runScenarios: false, width: 390, height: 844,
                isMobile: true, cancellationToken);
        }
        catch (PlaywrightException exception) when (
            exception.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("playwright install", StringComparison.OrdinalIgnoreCase))
        {
            throw new DemoSafetyException("CV02_BROWSER_NOT_INSTALLED");
        }
        catch (PlaywrightException)
        {
            throw new DemoSafetyException("CV02_BROWSER_NOT_INSTALLED");
        }

        if (state.Results.Count != ScenarioCatalog.All.Count || state.Results.Any(item => item.Status != "PASSED"))
        {
            throw new DemoSafetyException("CV02_SCENARIO_FAILED");
        }
    }

    private static async Task RunBrowserAsync(
        IBrowserType browserType,
        string browserName,
        string expectedVersion,
        Uri baseAddress,
        string repositoryRoot,
        DemoState state,
        bool runScenarios,
        int width,
        int height,
        bool isMobile,
        CancellationToken cancellationToken)
    {
        await using var browser = await browserType.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        if (!string.Equals(browser.Version, expectedVersion, StringComparison.Ordinal))
            throw new DemoSafetyException("CV02_BROWSER_NOT_INSTALLED");
        state.RecordBrowser(browserName, browser.Version);
        var videoRoot = Directory.CreateTempSubdirectory($"sgol-cv02-{browserName}-").FullName;
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = height },
            Locale = "es-MX",
            IsMobile = isMobile,
            HasTouch = isMobile,
            RecordVideoDir = videoRoot,
        });
        await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });
        var failed = false;
        try
        {
            var page = await context.NewPageAsync();
            foreach (var route in DemoContract.FunctionalRoutes.OrderBy(item => item, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await page.GotoAsync(new Uri(baseAddress, route).ToString());
                await AssertAccessibleShellAsync(page);
            }

            var unknown = await page.GotoAsync(new Uri(baseAddress, "/cv02/no-existe").ToString());
            if (unknown?.Status != 404) throw new DemoSafetyException("CV02_SCENARIO_FAILED");
            var withoutAntiforgery = await page.APIRequest.PostAsync(
                new Uri(baseAddress, "/cv02/calendario?handler=Run").ToString());
            if (withoutAntiforgery.Status != 400) throw new DemoSafetyException("CV02_SCENARIO_FAILED");

            if (runScenarios)
            {
                await page.GotoAsync(new Uri(baseAddress, "/cv02/calendario").ToString());
                var loadingButton = page.GetByRole(AriaRole.Button, new() { Name = "Ejecutar S01" });
                var loadingState = await loadingButton.EvaluateAsync<bool>(
                    "button => { button.form.dispatchEvent(new Event('submit')); return button.disabled && button.getAttribute('aria-busy') === 'true'; }");
                if (!loadingState) throw new DemoSafetyException("CV02_SCENARIO_FAILED");
                await page.ReloadAsync();
                foreach (var scenario in ScenarioCatalog.All)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var route = $"/cv02/{scenario.Page}";
                    await page.GotoAsync(new Uri(baseAddress, route).ToString());
                    var card = page.Locator($"[data-scenario='{scenario.Id}']");
                    var button = card.GetByRole(AriaRole.Button, new() { Name = $"Ejecutar {scenario.Id}" });
                    await button.FocusAsync();
                    var outline = await button.EvaluateAsync<string>(
                        "element => getComputedStyle(element).outlineStyle");
                    if (string.Equals(outline, "none", StringComparison.Ordinal))
                        throw new DemoSafetyException("CV02_SCENARIO_FAILED");
                    await button.PressAsync("Enter");
                    await card.WaitForAsync();
                    if (!string.Equals(await card.GetAttributeAsync("data-status"), "PASSED", StringComparison.Ordinal))
                    {
                        throw new DemoSafetyException("CV02_SCENARIO_FAILED");
                    }
                }

                var replayScenario = ScenarioCatalog.Require("S09");
                await page.GotoAsync(new Uri(baseAddress, $"/cv02/{replayScenario.Page}").ToString());
                var replayCard = page.Locator("[data-scenario='S09']");
                await replayCard.GetByRole(AriaRole.Button, new() { Name = "Ejecutar S09" }).ClickAsync();
                if (!string.Equals(await replayCard.GetAttributeAsync("data-status"), "PASSED", StringComparison.Ordinal))
                    throw new DemoSafetyException("CV02_SCENARIO_FAILED");
            }
        }
        catch
        {
            failed = true;
            var failures = Path.Combine(repositoryRoot, ".artifacts", "cv02", "latest", "failures");
            Directory.CreateDirectory(failures);
            var pages = context.Pages;
            if (pages.Count > 0)
            {
                var screenshotName = $"{browserName}.png";
                await pages[^1].ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(failures, screenshotName),
                    FullPage = true,
                });
                state.RecordFailureArtifact(Path.Combine("failures", screenshotName));
            }
            var traceName = $"{browserName}-trace.zip";
            await context.Tracing.StopAsync(new TracingStopOptions
            {
                Path = Path.Combine(failures, traceName),
            });
            state.RecordFailureArtifact(Path.Combine("failures", traceName));
            throw;
        }
        finally
        {
            if (!failed) await context.Tracing.StopAsync();
            await context.CloseAsync();
            if (Directory.Exists(videoRoot))
            {
                foreach (var video in Directory.EnumerateFiles(videoRoot))
                {
                    if (failed)
                    {
                        var failures = Path.Combine(repositoryRoot, ".artifacts", "cv02", "latest", "failures");
                        Directory.CreateDirectory(failures);
                        var videoName = $"{browserName}-{Path.GetFileName(video)}";
                        File.Move(video, Path.Combine(failures, videoName), overwrite: true);
                        state.RecordFailureArtifact(Path.Combine("failures", videoName));
                    }
                    else
                    {
                        File.Delete(video);
                    }
                }
                Directory.Delete(videoRoot);
            }
        }
    }

    private static async Task AssertAccessibleShellAsync(IPage page)
    {
        if (!string.Equals(await page.Locator("html").GetAttributeAsync("lang"), "es-MX", StringComparison.Ordinal))
            throw new DemoSafetyException("CV02_SCENARIO_FAILED");
        if (await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).CountAsync() != 1)
            throw new DemoSafetyException("CV02_SCENARIO_FAILED");
        if (await page.GetByRole(AriaRole.Navigation).CountAsync() != 1)
            throw new DemoSafetyException("CV02_SCENARIO_FAILED");
        if (await page.Locator("input:not([type='hidden'])").CountAsync() != 0)
            throw new DemoSafetyException("CV02_SCENARIO_FAILED");
        if (!await page.EvaluateAsync<bool>(ContrastScript))
            throw new DemoSafetyException("CV02_SCENARIO_FAILED");
        var overflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        if (overflow) throw new DemoSafetyException("CV02_SCENARIO_FAILED");
        await page.Keyboard.PressAsync("Tab");
        if (!await page.EvaluateAsync<bool>("() => document.activeElement !== document.body"))
            throw new DemoSafetyException("CV02_SCENARIO_FAILED");
    }

    private const string ContrastScript = """
        () => {
          const rgb = value => value.match(/\d+/g).slice(0, 3).map(Number);
          const luminance = value => {
            const channels = rgb(value).map(channel => {
              channel /= 255;
              return channel <= 0.04045 ? channel / 12.92 : Math.pow((channel + 0.055) / 1.055, 2.4);
            });
            return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
          };
          const ratio = (foreground, background) => {
            const values = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
            return (values[0] + 0.05) / (values[1] + 0.05);
          };
          const body = getComputedStyle(document.body);
          const button = getComputedStyle(document.querySelector('button'));
          return ratio(body.color, body.backgroundColor) >= 4.5 &&
                 ratio(button.color, button.backgroundColor) >= 4.5;
        }
        """;
}
