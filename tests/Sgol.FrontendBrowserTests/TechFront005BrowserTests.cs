using System.Diagnostics;
using System.Globalization;
using Microsoft.Playwright;
using Sgol.Cv05Demo;
using Xunit;
using Xunit.Abstractions;

namespace Sgol.FrontendBrowserTests;

[Collection("FRONT_BROWSER")]
public sealed partial class TechFront005BrowserTests(ITestOutputHelper output) : IAsyncDisposable
{
    private BrowserFixture fixture = null!;
    private Cv05Infrastructure owner = null!;
    private IBrowser browser = null!;
    private IPlaywright playwright = null!;
    private readonly List<IBrowserContext> contexts = [];
    private readonly List<IPage> pages = [];
    private readonly List<Front005Step> steps = [];
    private readonly Dictionary<string, Guid> obligations = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, string> secrets = [];
    private string directory = null!;
    private bool mobile;
    private Guid draft;
    private Guid auxiliaryPerson;
    private DateOnly day;
    private int year;
    private int week;
    private bool resourcesCleaned = true;
    private bool disposed;
    private string Period => $"isoYear={year}&isoWeek={week}";
    private IPage Direction => pages[0];
    private static string Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value,
        TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    [Theory, Trait("Category", "TECH_FRONT005")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NineJourneysUseRealInterfaceAndPreserveTheBackendChain(bool mobileProfile)
    {
        mobile = mobileProfile;
        var cycle = int.Parse(Environment.GetEnvironmentVariable("SGOL_TECH_FRONT005_CYCLE") ?? "1", CultureInfo.InvariantCulture);
        var root = BrowserFixture.RepositoryRoot();
        var sha = (await NativeProcess.RunAsync("git", ["rev-parse", "HEAD"], root, TimeSpan.FromSeconds(30), CancellationToken.None)).Stdout.Trim();
        var runRoot = Environment.GetEnvironmentVariable("SGOL_TECH_FRONT005_OUTPUT") ?? Path.Combine(root, ".artifacts", "tech-front005", Guid.CreateVersion7().ToString("N"));
        if (!Path.GetFullPath(runRoot).StartsWith(Path.GetFullPath(Path.Combine(root, ".artifacts", "tech-front005")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid owned output directory.");
        directory = Path.Combine(runRoot, "cycle-" + cycle, mobile ? "mobile" : "desktop");
        var image = new Cv05Image(root, sha);
        var failure = "HARNESS_FAILED";
        var cleaned = true;
        var version = "Chromium";
        var priorCommit = Environment.GetEnvironmentVariable("CV05_COMMIT");
        Environment.SetEnvironmentVariable("CV05_COMMIT", sha);
        try
        {
            output.WriteLine("TECH_FRONT005 IMAGE");
            await image.BuildAsync(CancellationToken.None);
            owner = new(image.ImageId);
            output.WriteLine("TECH_FRONT005 INFRASTRUCTURE");
            await owner.StartAsync(CancellationToken.None);
            owner.TrustFrontendCertificate();
            fixture = new();
            await fixture.AttachTechFront005Async(owner);
            day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).DateTime);
            year = ISOWeek.GetYear(day.ToDateTime(TimeOnly.MinValue));
            week = ISOWeek.GetWeekOfYear(day.ToDateTime(TimeOnly.MinValue));
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            version = "Chromium " + browser.Version;
            await Run("R1", AccessAsync, TechFront005Report.Roles);
            await Run("R2", IdentityAsync, ["DIRECCION"]);
            await Run("R3", CalendarAsync, TechFront005Report.Roles);
            await Run("R4", ConfigurationAsync, ["DIRECCION"]);
            await Run("R5", GenerationAsync, ["DIRECCION", "ADMINISTRACION", "SUBCOORDINACION"]);
            await Run("R6", PlanAsync, TechFront005Report.Roles);
            await Run("R7", WorkAsync, TechFront005Report.Roles);
            await Run("R8", ValidationAsync, ["DIRECCION", "ADMINISTRACION", "SUBCOORDINACION"]);
            await Run("R9", ControlAsync, TechFront005Report.Roles);
            failure = "NONE";
        }
        catch (Exception exception)
        {
            // Browser call logs can contain filled credentials or a signed PUT URL. Do not publish them.
            output.WriteLine("TECH_FRONT005 PRIMARY_FAILURE " + exception.GetType().Name);
            if (exception is DemoFailureException demo)
                output.WriteLine("TECH_FRONT005 CONTRACT_FAILURE " + demo.Message);
            throw new InvalidOperationException("TECH_FRONT005 failed at " + (steps.LastOrDefault()?.Route ?? "INFRASTRUCTURE") +
                " (" + exception.GetType().Name + "). Sensitive browser/process logs were not exported.");
        }
        finally
        {
            await DisposeAsync();
            cleaned &= resourcesCleaned;
            try { cleaned &= await image.CleanupAsync(); } catch { cleaned = false; }
            Environment.SetEnvironmentVariable("CV05_COMMIT", priorCommit);
            if (!cleaned && failure == "NONE") failure = "CLEANUP_FAILED";
            await TechFront005Report.WriteAsync(directory, new("TECH-FRONT-005", sha, cycle, mobile ? "mobile" : "desktop",
                version, "Windows AMD64 / Docker Linux AMD64", true, steps.ToArray(), cleaned, failure));
            output.WriteLine("TECH_FRONT005 CLEANUP " + cleaned);
            if (failure == "CLEANUP_FAILED") Assert.True(cleaned, "Owned frontend resources were not fully cleaned.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        foreach (var context in contexts) { try { await context.DisposeAsync(); } catch { resourcesCleaned = false; } }
        try { if (browser is not null) await browser.DisposeAsync(); } catch { resourcesCleaned = false; }
        try { playwright?.Dispose(); } catch { resourcesCleaned = false; }
        try { if (fixture is not null) { await fixture.DisposeAsync(); resourcesCleaned &= fixture.CleanupComplete; } } catch { resourcesCleaned = false; }
        try { if (owner is not null) resourcesCleaned &= owner.CleanupFrontendCertificate(); } catch { resourcesCleaned = false; }
        try { if (owner is not null) resourcesCleaned &= await owner.CleanupAsync(); } catch { resourcesCleaned = false; }
        GC.SuppressFinalize(this);
    }

    private async Task Run(string id, Func<Task> action, string[] roles)
    {
        output.WriteLine("TECH_FRONT005 " + id + " START");
        var timer = Stopwatch.StartNew();
        try { await action(); steps.Add(new(id, "PASS", timer.ElapsedMilliseconds, roles)); }
        catch { steps.Add(new(id, "FAIL", timer.ElapsedMilliseconds, roles)); throw; }
        output.WriteLine("TECH_FRONT005 " + id + " PASS");
    }

    private async Task<IBrowserContext> ContextAsync() => await browser.NewContextAsync(new()
    {
        BaseURL = fixture.BaseAddress.AbsoluteUri,
        ViewportSize = new() { Width = mobile ? 390 : 1440, Height = mobile ? 844 : 900 },
        IsMobile = mobile,
        HasTouch = mobile,
        IgnoreHTTPSErrors = true,
        Locale = "es-MX",
        ReducedMotion = ReducedMotion.Reduce,
        ServiceWorkers = ServiceWorkerPolicy.Block
    });

    private async Task Submit(IPage page, ILocator button, string handler, int status = 200)
    {
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Loaded(object? _, IPage __) => loaded.TrySetResult();
        page.DOMContentLoaded += Loaded;
        try
        {
            var response = page.WaitForResponseAsync(r => r.Request.Method == "POST" &&
                (handler.Length == 0 ? !r.Url.Contains("handler=", StringComparison.Ordinal) : r.Url.Contains("handler=" + handler, StringComparison.Ordinal)));
            await button.ClickAsync();
            var observed = (await response).Status;
            output.WriteLine("TECH_FRONT005 HTTP " + (handler.Length == 0 ? "Default" : handler) + " " + observed);
            Assert.Equal(status, observed);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { page.DOMContentLoaded -= Loaded; }
    }

    private async Task Capture(IPage page, string name)
    {
        await Front018BrowserTests.CheckAccessibilityAsync(page);
        Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(directory, name + ".png"),
            FullPage = true,
            Mask = [page.Locator("[data-sensitive-activation], .acceso__codigos, input[type=password]")]
        });
    }

    private static void Same(string before, string after) => Assert.True(before == after, "Unexpected persistent effect.");
}
