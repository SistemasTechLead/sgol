using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sgol.Web.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class PostgreSqlPersistenceTests : IAsyncLifetime
{
    private const string MigrationId = "20260827000000_InitializePersistence";
    private readonly PostgreSqlContainer _postgres = CreateContainer();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task EmptyMigration_ConnectsToPostgreSql_WithoutFunctionalTables()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTests");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Sgol"] = _postgres.GetConnectionString(),
                    }));
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                    services.AddDataProtection().UseEphemeralDataProtectionProvider());
            });

        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Equal([MigrationId], appliedMigrations);

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'public' ORDER BY table_name";

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Equal(["__EFMigrationsHistory"], tables);
    }

    private static PostgreSqlContainer CreateContainer()
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        return new PostgreSqlBuilder("postgres:18.6-alpine3.23")
            .WithDatabase("sgol_integration")
            .WithUsername("sgol_integration")
            .WithPassword(password)
            .Build();
    }
}
