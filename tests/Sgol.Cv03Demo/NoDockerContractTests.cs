using System.Net;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Evidence.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.Cv03Demo;

public sealed class NoDockerContractTests
{
    [Fact]
    public void Catalog_is_closed_ordered_and_traceable()
    {
        var scenarios = ScenarioCatalog.All;

        Assert.Equal(24, scenarios.Count);
        Assert.Equal(Enumerable.Range(1, 24).Select(value => $"S{value:00}"), scenarios.Select(item => item.Id));
        Assert.All(scenarios, item =>
        {
            Assert.Contains("HU-", item.Coverage, StringComparison.Ordinal);
            Assert.Contains("CA-", item.Coverage, StringComparison.Ordinal);
            Assert.Contains("CP-", item.Coverage, StringComparison.Ordinal);
            Assert.NotEmpty(item.Rule);
        });
    }

    [Fact]
    public void Contract_uses_only_the_approved_external_infrastructure()
    {
        Assert.Contains("postgres", DemoContract.PostgreSqlImage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("seaweedfs", DemoContract.SeaweedImage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clamav", DemoContract.ClamAvImage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latest", DemoContract.PostgreSqlImage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latest", DemoContract.SeaweedImage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latest", DemoContract.ClamAvImage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parser_accepts_only_automated_mode()
    {
        Assert.False(DemoOptions.TryParse([], out _));
        Assert.True(DemoOptions.TryParse(["--mode", "Automated"], out var options));
        Assert.Equal(DemoMode.Automated, options!.Mode);
    }

    [Theory]
    [InlineData("Interactive")]
    [InlineData("Browser")]
    [InlineData("Unknown")]
    public void Parser_rejects_unapproved_modes(string mode)
    {
        Assert.False(DemoOptions.TryParse(["--mode", mode], out _));
    }

    [Fact]
    public void Demo_primitives_override_production_defaults_once()
    {
        var services = new ServiceCollection();
        var clock = new DemoClock(Cv03Timeline.Now);
        var uuidGenerator = new DeterministicUuid7Generator(clock);
        services.AddSgolHttpPrimitives();

        Cv03Infrastructure.ApplyDeterministicPrimitives(services, clock, uuidGenerator);

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IClock));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUuidGenerator));
        using var provider = services.BuildServiceProvider();
        Assert.Same(clock, provider.GetRequiredService<IClock>());
        Assert.Same(uuidGenerator, provider.GetRequiredService<IUuidGenerator>());
    }

    [Fact]
    public void Timeline_uses_one_utc_minute_anchor_for_the_whole_execution()
    {
        var observed = new DateTimeOffset(2030, 4, 5, 6, 7, 59, TimeSpan.Zero);

        var anchor = Cv03Timeline.Anchor(observed);

        Assert.Equal(new DateTimeOffset(2030, 4, 5, 6, 7, 0, TimeSpan.Zero), anchor);
        Assert.Equal(TimeSpan.Zero, Cv03Timeline.Now.Offset);
        Assert.Equal(Cv03Timeline.Now.AddHours(-2), Cv03Timeline.Before);
        Assert.Equal(Cv03Timeline.Now.AddHours(2), Cv03Timeline.After);
    }

    [Fact]
    public void Demo_clock_advances_past_a_future_domain_event_without_going_backwards()
    {
        var infrastructureClock = new DemoClock(Cv03Timeline.Now);
        var futureEvent = Cv03Timeline.Now.AddMinutes(4);

        infrastructureClock.AdvancePast(futureEvent);

        Assert.True(infrastructureClock.UtcNow > futureEvent);
        var advanced = infrastructureClock.UtcNow;
        infrastructureClock.AdvancePast(Cv03Timeline.Now);
        Assert.Equal(advanced, infrastructureClock.UtcNow);
    }

    [Fact]
    public void Embedded_worker_uses_ci_environment_and_real_private_adapters()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Evidence:Storage:Endpoint"] = "http://127.0.0.1:12345",
            ["Evidence:Storage:Region"] = "us-east-1",
            ["Evidence:Storage:QuarantineBucket"] = DemoContract.QuarantineBucket,
            ["Evidence:Storage:CleanBucket"] = DemoContract.CleanBucket,
            ["Evidence:Storage:AccessKey"] = "synthetic-access",
            ["Evidence:Storage:SecretKey"] = "synthetic-secret",
            ["Evidence:Storage:AllowedUploadOrigins:0"] = "http://127.0.0.1:5000",
            ["Evidence:Storage:AllowInsecureTransport"] = "true",
            ["Evidence:Scanner:Host"] = DemoContract.LoopbackAddress,
            ["Evidence:Scanner:Port"] = "3310",
            ["Evidence:Scanner:ConnectTimeoutSeconds"] = "3",
            ["Evidence:Scanner:ScanTimeoutSeconds"] = "30",
        }).Build();
        var environment = new TestHostEnvironment();

        Cv03Infrastructure.ConfigureEvidenceWorkerServices(services, configuration, environment);

        Assert.Equal("CI", environment.EnvironmentName);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IPrivateObjectStorage) &&
            descriptor.ImplementationType == typeof(S3PrivateObjectStorage));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IFileMalwareScanner) &&
            descriptor.ImplementationType == typeof(ClamAvScanner));
    }

    [Fact]
    public async Task Notice_read_http_endpoint_rejects_invalid_and_accepts_valid_csrf()
    {
        var actor = Guid.CreateVersion7();
        var notice = Guid.CreateVersion7();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(Cv03DemoApplication).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "CI",
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSgolHttpPrimitives();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-SGOL-CSRF";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });
        var noticeService = new RecordingNoticeService(new(
            notice, InternalNoticeStatuses.Read, Cv03Timeline.Now, InternalNoticeReadResults.MarkedRead));
        builder.Services.AddSingleton<IInboxReader, EmptyInboxReader>();
        builder.Services.AddSingleton<IInternalNoticeService>(noticeService);
        await using var application = builder.Build();
        application.UseSgolHttpPrimitives();
        application.Use(async (context, next) =>
        {
            context.Request.Scheme = Uri.UriSchemeHttps;
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.ToString("D"))], "cv03-test"));
            await next(context);
        });
        application.MapInboxApi();
        await application.StartAsync();
        try
        {
            var address = application.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var issuance = new DefaultHttpContext { RequestServices = application.Services };
            issuance.Request.Scheme = Uri.UriSchemeHttps;
            issuance.Request.Host = new HostString(DemoContract.LoopbackAddress);
            issuance.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.ToString("D"))], "cv03-test"));
            var tokens = application.Services.GetRequiredService<IAntiforgery>().GetAndStoreTokens(issuance);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/me/notices/{notice:D}/read");
            request.Headers.TryAddWithoutValidation("Cookie", Assert.Single(issuance.Response.Headers.SetCookie)!.Split(';', 2)[0]);
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", tokens.RequestToken + "-invalid");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(0, noticeService.CallCount);

            var validIssuance = new DefaultHttpContext { RequestServices = application.Services };
            validIssuance.Request.Scheme = Uri.UriSchemeHttps;
            validIssuance.Request.Host = new HostString(DemoContract.LoopbackAddress);
            validIssuance.User = issuance.User;
            var validTokens = application.Services.GetRequiredService<IAntiforgery>().GetAndStoreTokens(validIssuance);
            using var validRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/me/notices/{notice:D}/read");
            validRequest.Headers.TryAddWithoutValidation("Cookie", Assert.Single(validIssuance.Response.Headers.SetCookie)!.Split(';', 2)[0]);
            validRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", validTokens.RequestToken);

            using var validResponse = await client.SendAsync(validRequest);

            Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
            Assert.Equal(1, noticeService.CallCount);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task Report_writer_sanitizes_failures_and_rotates_latest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sgol-cv03-contract-{Guid.NewGuid():N}");
        try
        {
            var latest = Path.Combine(root, ".artifacts", "cv03", "latest");
            Directory.CreateDirectory(latest);
            File.WriteAllText(Path.Combine(latest, "stale.txt"), "stale");
            var state = new DemoState();
            state.MarkFailure("Host=private;Password=secret;token=abc");
            EvidenceWriter.Prepare(root);
            await EvidenceWriter.WriteAsync(root, state, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, true);

            Assert.False(File.Exists(Path.Combine(latest, "stale.txt")));
            var json = File.ReadAllText(Path.Combine(latest, $"{DemoContract.TaskId}-report.json"));
            using var document = JsonDocument.Parse(json);
            Assert.Equal(DemoContract.ReportSchemaVersion, document.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CV03_UNEXPECTED_FAILURE", json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Safety_guard_rejects_real_or_shared_targets()
    {
        Assert.Throws<DemoSafetyException>(() => DemoSafety.RejectExternalConfiguration(name => name == "PGHOST" ? "shared" : null));
        DemoSafety.RejectExternalConfiguration(_ => null);
        Assert.Throws<DemoSafetyException>(() => DemoSafety.ValidateDisposableDatabase("shared", "sgol", "sgol", 5432));
        DemoSafety.ValidateDisposableDatabase("127.0.0.1", "sgol_cv03_test", "sgol_cv03_test", 5432);
    }

    [Theory]
    [InlineData("CV03_S20_CSRF_REJECTION_FAILED")]
    [InlineData("CV03_S20_FIRST_STATUS_FAILED")]
    [InlineData("CV03_S20_REPLAY_STATUS_FAILED")]
    [InlineData("CV03_S20_FIRST_RESULT_FAILED")]
    [InlineData("CV03_S20_REPLAY_RESULT_FAILED")]
    [InlineData("CV03_S20_AUDIT_COUNT_FAILED")]
    public void S20_diagnostics_are_allowlisted_without_raw_responses(string code)
    {
        Assert.Equal(code, DemoSafety.Sanitize(code));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Sgol.Cv03Demo.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingNoticeService(ReadInternalNoticeResult result) : IInternalNoticeService
    {
        public int CallCount { get; private set; }

        public Task<ReadInternalNoticeResult> MarkReadAsync(
            ReadInternalNoticeCommand command,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class EmptyInboxReader : IInboxReader
    {
        public Task<InboxPage> ReadAsync(InboxQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new InboxPage(
                null,
                new InboxSection<InboxTask>([], null),
                new InboxSection<InboxNotice>([], null),
                Cv03Timeline.Now));
    }
}
