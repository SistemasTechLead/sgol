using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.JobInfrastructure;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Presentation.Endpoints;
using Sgol.Worker;
using Testcontainers.PostgreSql;

namespace Sgol.Cv03Demo;

internal sealed class DemoActorContext
{
    public Guid? CurrentUserId { get; set; }
}

internal sealed class Cv03Infrastructure : IAsyncDisposable
{
    private readonly string temporaryDirectory;
    private readonly INetwork network;
    private readonly PostgreSqlContainer postgres;
    private readonly IContainer storage;
    private readonly IContainer scanner;
    private readonly string databaseName;
    private readonly string adminAccessKey;
    private readonly string adminSecretKey;
    private readonly string applicationAccessKey;
    private readonly string applicationSecretKey;
    private WebApplication? application;
    private AmazonS3Client? adminClient;

    private Cv03Infrastructure(
        string temporaryDirectory,
        INetwork network,
        PostgreSqlContainer postgres,
        IContainer storage,
        IContainer scanner,
        string databaseName,
        string adminAccessKey,
        string adminSecretKey,
        string applicationAccessKey,
        string applicationSecretKey)
    {
        this.temporaryDirectory = temporaryDirectory;
        this.network = network;
        this.postgres = postgres;
        this.storage = storage;
        this.scanner = scanner;
        this.databaseName = databaseName;
        this.adminAccessKey = adminAccessKey;
        this.adminSecretKey = adminSecretKey;
        this.applicationAccessKey = applicationAccessKey;
        this.applicationSecretKey = applicationSecretKey;
        Clock = new(Cv03Timeline.Now);
        Uuids = new(Clock);
        Actor = new();
    }

    public DemoClock Clock { get; }
    public DeterministicUuid7Generator Uuids { get; }
    public DemoActorContext Actor { get; }
    public IServiceProvider Services => application?.Services ?? throw new InvalidOperationException();
    public IConfiguration Configuration { get; private set; } = null!;
    public Uri BaseAddress { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public HttpRequestMessage CreateCsrfRequest(HttpMethod method, string path, Guid actorUserId)
    {
        var context = new DefaultHttpContext { RequestServices = Services, User = CreatePrincipal(actorUserId) };
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Request.Host = new HostString(DemoContract.LoopbackAddress);
        var tokens = Services.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        var request = new HttpRequestMessage(method, path);
        var cookie = context.Response.Headers.SetCookie.Single()!.Split(';', 2)[0];
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", tokens.RequestToken);
        return request;
    }

    public static async Task<Cv03Infrastructure> StartAsync(CancellationToken cancellationToken)
    {
        DemoSafety.RejectExternalConfiguration(Environment.GetEnvironmentVariable);
        var temporaryDirectory = Directory.CreateTempSubdirectory("sgol-cv03-").FullName;
        var suffix = RandomNumberGenerator.GetHexString(12).ToLowerInvariant();
        var database = DemoContract.DatabasePrefix + suffix;
        var username = "cv03_" + RandomNumberGenerator.GetHexString(10).ToLowerInvariant();
        var password = RandomNumberGenerator.GetHexString(32);
        var adminAccessKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var adminSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var applicationAccessKey = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var applicationSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var network = new NetworkBuilder().WithName("sgol-cv03-" + suffix).Build();
        var configPath = Path.Combine(temporaryDirectory, "s3.json");
        var storageConfiguration = new
        {
            identities = new object[]
            {
                new { name = "provisioner", credentials = new[] { new { accessKey = adminAccessKey, secretKey = adminSecretKey } }, actions = new[] { "Admin", "Read", "Write", "List" } },
                new
                {
                    name = "sgol-evidence",
                    credentials = new[] { new { accessKey = applicationAccessKey, secretKey = applicationSecretKey } },
                    actions = new[]
                    {
                        $"Read:{DemoContract.QuarantineBucket}", $"Write:{DemoContract.QuarantineBucket}", $"List:{DemoContract.QuarantineBucket}",
                        $"Read:{DemoContract.CleanBucket}", $"Write:{DemoContract.CleanBucket}", $"List:{DemoContract.CleanBucket}",
                    },
                },
            },
        };
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(storageConfiguration), cancellationToken);

        var postgres = new PostgreSqlBuilder(DemoContract.PostgreSqlImage)
            .WithDatabase(database).WithUsername(username).WithPassword(password).WithNetwork(network).Build();
        var storage = new ContainerBuilder(DemoContract.SeaweedImage)
            .WithNetwork(network).WithPortBinding(8333, true)
            .WithBindMount(configPath, "/run/sgol/s3.json", AccessMode.ReadOnly)
            .WithCommand("mini", "-dir=/data", "-s3.config=/run/sgol/s3.json")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8333)).Build();
        var scanner = new ContainerBuilder(DemoContract.ClamAvImage)
            .WithNetwork(network).WithPortBinding(3310, true)
            .WithEnvironment("CLAMAV_NO_FRESHCLAMD", "true")
            .WithEnvironment("CLAMD_CONF_StreamMaxLength", "16M")
            .WithEnvironment("CLAMD_CONF_MaxFileSize", "16M")
            .WithEnvironment("CLAMD_CONF_MaxScanSize", "32M")
            .WithEnvironment("CLAMD_CONF_MaxRecursion", "4")
            .WithEnvironment("CLAMD_CONF_MaxFiles", "64")
            .WithEnvironment("CLAMD_CONF_MaxScanTime", "30000")
            .WithEnvironment("CLAMD_CONF_MaxThreads", "2")
            .WithEnvironment("CLAMD_CONF_MaxQueue", "4")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310)).Build();
        var result = new Cv03Infrastructure(temporaryDirectory, network, postgres, storage, scanner, database,
            adminAccessKey, adminSecretKey, applicationAccessKey, applicationSecretKey);
        try
        {
            await network.CreateAsync(cancellationToken);
            await postgres.StartAsync(cancellationToken);
            var parsed = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString());
            DemoSafety.ValidateDisposableDatabase(parsed.Host ?? string.Empty, parsed.Database ?? string.Empty, database, parsed.Port);
            await storage.StartAsync(cancellationToken);
            await result.ProvisionStorageAsync(cancellationToken);
            await scanner.StartAsync(cancellationToken);
            await result.StartHostAsync(cancellationToken);
            await result.ResetAsync(cancellationToken);
            return result;
        }
        catch
        {
            await result.DisposeAsync();
            throw;
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.EnsureDeletedAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }

    public async Task RunWorkerUntilProcessedAsync(Guid aggregateId, CancellationToken cancellationToken)
    {
        Clock.Set(Clock.UtcNow.AddSeconds(1));
        await using (var preflightScope = Services.CreateAsyncScope())
        {
            var preflightContext = preflightScope.ServiceProvider.GetRequiredService<SgolDbContext>();
            var pending = await preflightContext.OutboxEvents.AsNoTracking()
                .SingleOrDefaultAsync(item => item.AggregateId == aggregateId, cancellationToken)
                ?? throw new DemoScenarioAssertionException("CV03_S05_OUTBOX_EVENT_MISSING");
            if (pending.AvailableAt > Clock.UtcNow)
                throw new DemoScenarioAssertionException("CV03_S05_OUTBOX_EVENT_FUTURE");
        }
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var output = new List<string>();
        var worker = WorkerApplication.RunAsync(
            ["outbox"],
            services =>
            {
                services.RemoveAll<IClock>();
                services.RemoveAll<IUuidGenerator>();
                services.AddSingleton<IClock>(Clock);
                services.AddSingleton<IUuidGenerator>(Uuids);
            },
            output.Add,
            Configuration,
            ConfigureEvidenceWorkerServices,
            stop.Token);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (worker.IsCompleted)
            {
                _ = await worker;
                throw new DemoScenarioAssertionException("CV03_S05_WORKER_EXITED");
            }
            await using var scope = Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
            var processed = await context.OutboxEvents.AsNoTracking()
                .AnyAsync(item => item.AggregateId == aggregateId && item.ProcessedAt != null, cancellationToken);
            if (processed)
            {
                stop.Cancel();
                _ = await worker;
                return;
            }
            await Task.Delay(100, cancellationToken);
        }

        stop.Cancel();
        _ = await worker;
        await using var diagnosticScope = Services.CreateAsyncScope();
        var diagnosticContext = diagnosticScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var diagnosticEvent = await diagnosticContext.OutboxEvents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.AggregateId == aggregateId, cancellationToken)
            ?? throw new DemoScenarioAssertionException("CV03_S05_OUTBOX_EVENT_MISSING");
        throw new DemoScenarioAssertionException(diagnosticEvent.AttemptCount > 0
            ? "CV03_S05_WORKER_RETRYING"
            : "CV03_S05_WORKER_UNCLAIMED");
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (application is not null)
        {
            await application.StopAsync();
            await application.DisposeAsync();
        }
        adminClient?.Dispose();
        await scanner.DisposeAsync();
        await storage.DisposeAsync();
        await postgres.DisposeAsync();
        await network.DeleteAsync();
        await network.DisposeAsync();
        if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
    }

    private async Task ProvisionStorageAsync(CancellationToken cancellationToken)
    {
        var endpoint = $"http://{DemoContract.LoopbackAddress}:{storage.GetMappedPublicPort(8333)}";
        adminClient = CreateS3(endpoint, adminAccessKey, adminSecretKey);
        await adminClient.PutBucketAsync(DemoContract.QuarantineBucket, cancellationToken);
        await adminClient.PutBucketAsync(DemoContract.CleanBucket, cancellationToken);
        await adminClient.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
        {
            BucketName = DemoContract.QuarantineBucket,
            Configuration = new CORSConfiguration
            {
                Rules =
                [
                    new CORSRule
                    {
                        Id = "sgol-evidence-upload",
                        AllowedOrigins = ["http://127.0.0.1:5000"],
                        AllowedMethods = ["PUT"],
                        AllowedHeaders = ["Content-Type", "Content-Length", "If-None-Match", "x-amz-meta-sgol-sha256", "x-amz-meta-sgol-media-type", "x-amz-meta-sgol-size-bytes"],
                        MaxAgeSeconds = 600,
                    },
                ],
            },
        }, cancellationToken);
    }

    private async Task StartHostAsync(CancellationToken cancellationToken)
    {
        var endpoint = $"http://{DemoContract.LoopbackAddress}:{storage.GetMappedPublicPort(8333)}";
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sgol"] = postgres.GetConnectionString(),
            ["Evidence:Storage:Endpoint"] = endpoint,
            ["Evidence:Storage:Region"] = "us-east-1",
            ["Evidence:Storage:QuarantineBucket"] = DemoContract.QuarantineBucket,
            ["Evidence:Storage:CleanBucket"] = DemoContract.CleanBucket,
            ["Evidence:Storage:AccessKey"] = applicationAccessKey,
            ["Evidence:Storage:SecretKey"] = applicationSecretKey,
            ["Evidence:Storage:AllowInsecureTransport"] = "true",
            ["Evidence:Storage:AllowedUploadOrigins:0"] = "http://127.0.0.1:5000",
            ["Evidence:Scanner:Host"] = DemoContract.LoopbackAddress,
            ["Evidence:Scanner:Port"] = scanner.GetMappedPublicPort(3310).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Evidence:Scanner:ConnectTimeoutSeconds"] = "3",
            ["Evidence:Scanner:ScanTimeoutSeconds"] = "30",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command"] = "Warning",
        };
        Configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(Cv03DemoApplication).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "CI",
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddConfiguration(Configuration);
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{DemoContract.LoopbackAddress}:0");
        builder.Services.AddSingleton(Actor);
        builder.Services.AddSgolHttpPrimitives();
        builder.Services.AddSgolEvidenceInfrastructure(builder.Configuration, builder.Environment);
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-SGOL-CSRF";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });
        builder.Services.AddSgolPersistence(builder.Configuration);
        ApplyDeterministicPrimitives(builder.Services, Clock, Uuids);
        application = builder.Build();
        application.UseSgolHttpPrimitives();
        application.Use(async (context, next) =>
        {
            context.Request.Scheme = Uri.UriSchemeHttps;
            if (Actor.CurrentUserId is { } id)
            {
                context.User = CreatePrincipal(id);
            }
            await next(context);
        });
        application.MapObligationQueryApi();
        application.MapObligationConclusionApi();
        application.MapInboxApi();
        application.MapEvidenceApi();
        await application.StartAsync(cancellationToken);
        var address = application.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()?.Addresses.SingleOrDefault()
            ?? throw new DemoSafetyException("CV03_HOST_START_FAILED");
        BaseAddress = new(address);
        if (BaseAddress.Host != DemoContract.LoopbackAddress) throw new DemoSafetyException("CV03_HOST_START_FAILED");
        Client = new HttpClient { BaseAddress = BaseAddress };
    }

    internal static void ApplyDeterministicPrimitives(
        IServiceCollection services,
        IClock clock,
        IUuidGenerator uuidGenerator)
    {
        services.RemoveAll<IClock>();
        services.RemoveAll<IUuidGenerator>();
        services.AddSingleton(clock);
        services.AddSingleton(uuidGenerator);
    }

    internal static void ConfigureEvidenceWorkerServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        environment.EnvironmentName = "CI";
        services.AddSgolEvidenceWorkerInfrastructure(configuration, environment);
    }

    private static AmazonS3Client CreateS3(string endpoint, string accessKey, string secretKey) =>
        new(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "us-east-1",
            ForcePathStyle = true,
            UseHttp = true,
            MaxErrorRetry = 0,
        });

    private static ClaimsPrincipal CreatePrincipal(Guid actorUserId) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString("D")), new Claim("amr", "mfa")], "cv03"));
}
