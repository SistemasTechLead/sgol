using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence;
using Xunit;

namespace Sgol.Cv02Demo;

public sealed partial class NoDockerContractTests
{
    [Fact]
    [Trait("Category", "NoDocker")]
    public void ParserAcceptsOnlyClosedModes()
    {
        Assert.True(DemoOptions.TryParse(["--mode", "Automated"], out var automated));
        Assert.Equal(DemoMode.Automated, automated!.Mode);
        Assert.True(DemoOptions.TryParse(["--mode", "Interactive"], out var interactive));
        Assert.Equal(DemoMode.Interactive, interactive!.Mode);
        Assert.False(DemoOptions.TryParse([], out _));
        Assert.False(DemoOptions.TryParse(["--mode", "automated"], out _));
        Assert.False(DemoOptions.TryParse(["--mode", "Automated", "--connection"], out _));
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public void RoutesAndScenarioMatrixAreClosed()
    {
        Assert.Equal(3, DemoContract.FunctionalRoutes.Count);
        Assert.Equal(
            ["/cv02/calendario", "/cv02/plan-semanal", "/cv02/recurrencias"],
            DemoContract.FunctionalRoutes.OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(13, ScenarioCatalog.All.Count);
        Assert.Equal(ScenarioCatalog.All.Count, ScenarioCatalog.All.Select(item => item.Id).Distinct().Count());
        Assert.All(ScenarioCatalog.All, item => Assert.Matches("^S(0[1-9]|1[0-3])$", item.Id));
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public void ExternalDatabaseConfigurationIsRejected()
    {
        Assert.Throws<DemoSafetyException>(() =>
            DemoSafety.RejectExternalDatabaseConfiguration(name =>
                name == "ConnectionStrings__Sgol" ? "Host=production" : null));
        DemoSafety.RejectExternalDatabaseConfiguration(_ => null);
    }

    [Theory]
    [InlineData("database.example", "sgol_cv02_a", "sgol_cv02_a", 5432)]
    [InlineData("127.0.0.1", "production", "production", 5432)]
    [InlineData("127.0.0.1", "sgol_cv02_a", "sgol_cv02_b", 5432)]
    [InlineData("127.0.0.1", "sgol_cv02_a", "sgol_cv02_a", 0)]
    [Trait("Category", "NoDocker")]
    public void NonDisposableDatabaseTargetsAreRejected(string host, string database, string expected, int port)
    {
        Assert.Throws<DemoSafetyException>(() =>
            DemoSafety.ValidateDisposableDatabase(host, database, expected, port));
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public void SyntheticSeedIdentityIsDeterministic()
    {
        Assert.Equal(EvidenceWriter.SeedHash, EvidenceWriter.SeedHash);
        Assert.Equal(64, EvidenceWriter.SeedHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", EvidenceWriter.SeedHash);
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public void ReaderIsExplicitlyReadOnly()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "Cv02DemoReader.cs"));
        Assert.Contains("SET TRANSACTION READ ONLY", source, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking()", source, StringComparison.Ordinal);
        Assert.Contains("DemoReadOnlyCommandInterceptor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges", source, StringComparison.Ordinal);

        var snapshot = new Cv02Snapshot(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            ["RECUPERADA"], ["VIGENTE"], ["BORRADOR"]);
        Assert.Equal(Cv02DemoReader.Fingerprint(snapshot), Cv02DemoReader.Fingerprint(snapshot));
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public void HostHasNoProductiveRouteOrMigration()
    {
        var projectRoot = ProjectRoot();
        Assert.False(Directory.Exists(Path.Combine(projectRoot, "Migrations")));
        var project = File.ReadAllText(Path.Combine(projectRoot, "Sgol.Cv02Demo.csproj"));
        Assert.Contains("src\\Sgol.Web\\Sgol.Web.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectReference Include=\"..\\..\\tests", project, StringComparison.Ordinal);
        var application = File.ReadAllText(Path.Combine(projectRoot, "Cv02DemoApplication.cs"));
        Assert.Contains("DemoContract.LoopbackAddress", application, StringComparison.Ordinal);
        Assert.Equal("127.0.0.1", DemoContract.LoopbackAddress);
        Assert.DoesNotContain("MapGet", application, StringComparison.Ordinal);
        Assert.DoesNotContain("MapPost", application, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public void DemoCssUsesExistingColorTokensOnly()
    {
        var css = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "css", "demo.css"));
        Assert.DoesNotMatch(ColorLiteral(), css);
        Assert.DoesNotContain("!important", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(":focus-visible", css, StringComparison.Ordinal);
        Assert.Contains(":disabled", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", css, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public void EvidenceContractExcludesSensitivePayloads()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "EvidenceWriter.cs"));
        foreach (var prohibited in new[]
        {
            "password =", "cookie =", "totp =", "connectionString =", "sql =", "payload =", "afterData =",
        })
            Assert.DoesNotContain(prohibited, source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("relatedDefect", source, StringComparison.Ordinal);
        Assert.Contains("coverage", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public void BrowserVideoUsesSystemTempAndAvoidsRecursiveArtifactDeletion()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "PlaywrightDemoRunner.cs"));
        Assert.Contains("Directory.CreateTempSubdirectory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("recursive: true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("video-temp", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public void PersistenceCompositionValidatesWithoutOpeningPostgreSql()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sgol"] =
                    "Host=127.0.0.1;Port=1;Database=sgol_cv02_composition;Username=cv02;Password=synthetic",
            }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        var clock = new DemoClock(Cv02Timeline.ConfigurationNow);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IUuidGenerator>(new DeterministicUuid7Generator(clock));
        services.AddSgolPersistence(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        Assert.NotNull(provider);
    }

    [Fact]
    [Trait("Category", "NoDocker")]
    public async Task RazorHostStartsOnlyOnEphemeralLoopbackAndStopsCleanly()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(Cv02DemoApplication).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.ClearProviders();
        builder.WebHost.UseStaticWebAssets();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorPages();
        await using var application = builder.Build();
        application.UseStaticFiles();
        application.MapStaticAssets();
        application.MapRazorPages();

        await application.StartAsync();
        var address = application.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        Assert.Equal("127.0.0.1", new Uri(address).Host);
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        using var stylesheet = await client.GetAsync("/css/demo.css");
        Assert.Equal(System.Net.HttpStatusCode.OK, stylesheet.StatusCode);
        Assert.Contains("var(--", await stylesheet.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await application.StopAsync();
    }

    private static string ProjectRoot() => Path.Combine(
        Cv02DemoApplication.FindRepositoryRoot(), "tests", "Sgol.Cv02Demo");

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\s*\(|\bhsla?\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex ColorLiteral();
}
