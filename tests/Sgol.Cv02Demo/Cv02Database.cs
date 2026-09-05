using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Sgol.Cv02Demo;

internal sealed class Cv02Database : IAsyncDisposable
{
    private readonly PostgreSqlContainer container;
    private readonly string databaseName;

    private Cv02Database(
        PostgreSqlContainer container,
        string databaseName,
        IConfiguration configuration,
        DemoClock clock,
        DeterministicUuid7Generator uuidGenerator,
        ServiceProvider services)
    {
        this.container = container;
        this.databaseName = databaseName;
        Configuration = configuration;
        Clock = clock;
        UuidGenerator = uuidGenerator;
        Services = services;
    }

    public DemoClock Clock { get; }

    public IConfiguration Configuration { get; }

    public DeterministicUuid7Generator UuidGenerator { get; }

    public ServiceProvider Services { get; }

    public string ConnectionString => container.GetConnectionString();

    public static async Task<Cv02Database> StartAsync(CancellationToken cancellationToken)
    {
        DemoSafety.RejectExternalDatabaseConfiguration(Environment.GetEnvironmentVariable);
        var suffix = RandomNumberGenerator.GetHexString(12).ToLowerInvariant();
        var database = $"{DemoContract.DatabasePrefix}{suffix}";
        var username = $"cv02_{RandomNumberGenerator.GetHexString(10).ToLowerInvariant()}";
        var password = RandomNumberGenerator.GetHexString(32);
        var container = new PostgreSqlBuilder(DemoContract.PostgreSqlImage)
            .WithDatabase(database)
            .WithUsername(username)
            .WithPassword(password)
            .Build();

        try
        {
            await container.StartAsync(cancellationToken);
            var connection = new NpgsqlConnectionStringBuilder(container.GetConnectionString());
            DemoSafety.ValidateDisposableDatabase(
                connection.Host ?? string.Empty,
                connection.Database ?? string.Empty,
                database,
                connection.Port);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Sgol"] = container.GetConnectionString(),
                })
                .Build();
            var clock = new DemoClock(Cv02Timeline.ConfigurationNow);
            var generator = new DeterministicUuid7Generator(clock);
            var collection = new ServiceCollection();
            collection.AddLogging(builder => builder.ClearProviders());
            collection.AddSingleton<IClock>(clock);
            collection.AddSingleton<IUuidGenerator>(generator);
            collection.AddSgolPersistence(configuration);
            var services = collection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            return new Cv02Database(container, database, configuration, clock, generator, services);
        }
        catch
        {
            await container.DisposeAsync();
            throw;
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.EnsureDeletedAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        context.ChangeTracker.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await container.DisposeAsync();
    }
}

internal static class Cv02Timeline
{
    public static readonly DateTimeOffset ConfigurationNow =
        new(2026, 12, 20, 18, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset EffectiveFrom =
        new(2026, 12, 21, 6, 0, 0, TimeSpan.Zero);

    public static readonly DateOnly WorkingDate = new(2026, 12, 31);
    public static readonly DateOnly NonWorkingDate = new(2027, 1, 1);

    public static readonly DateTimeOffset WorkingCutoff =
        new(2026, 12, 31, 23, 15, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset NonWorkingCutoff =
        new(2027, 1, 1, 23, 15, 0, TimeSpan.Zero);
}
