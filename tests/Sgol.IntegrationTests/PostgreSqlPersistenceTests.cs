using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class PostgreSqlPersistenceTests : IAsyncLifetime
{
    private const string InitialMigrationId = "20260827000000_InitializePersistence";
    private const string AuditMigrationId = "20260831192942_AddAuditEvent";
    private const string BootstrapMigrationId = "20260901190333_AddDirectionBootstrap";
    private const string BranchScopeMigrationId = "20260901223021_EnforceLorettaBranchScope";
    private const string PersonAdministrationMigrationId = "20260901233438_AddPersonAdministration";
    private const string RoleAdministrationMigrationId = "20260902190915_AddRoleAdministration";
    private const string AvailabilityAdministrationMigrationId = "20260902200533_AddAvailabilityAdministration";
    private const string ConfigurationReleaseMigrationId = "20260902231629_AddConfigurationReleases";
    private const string CalendarAdministrationMigrationId = "20260903001027_AddCalendarAdministration";
    private const string WeekPeriodsMigrationId = "20260903170116_AddWeekPeriods";
    private const string TaskDefinitionsMigrationId = "20260903175354_AddTaskDefinitions";
    private readonly PostgreSqlContainer _postgres = CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Migrations_CreateOnlyTheApprovedTables()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Equal(
            [
                InitialMigrationId,
                AuditMigrationId,
                BootstrapMigrationId,
                BranchScopeMigrationId,
                PersonAdministrationMigrationId,
                RoleAdministrationMigrationId,
                AvailabilityAdministrationMigrationId,
                ConfigurationReleaseMigrationId,
                CalendarAdministrationMigrationId,
                WeekPeriodsMigrationId,
                TaskDefinitionsMigrationId,
            ],
            appliedMigrations);

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

        Assert.Equal(
            [
                "__EFMigrationsHistory",
                "app_user",
                "audit_event",
                "availability_day_version",
                "branch",
                "calendar_day_version",
                "configuration_release",
                "direction_bootstrap",
                "employment_version",
                "idempotency_record",
                "identity_credential",
                "person",
                "role_assignment_version",
                "task_definition",
                "task_definition_version",
                "week_period",
            ],
            tables);
    }

    [Fact]
    public async Task AuditTransaction_CommitsCriticalWriteAndAuditEventTogether()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var auditTransaction = scope.ServiceProvider.GetRequiredService<AuditTransaction>();
        await PrepareDatabaseAsync(context);

        var auditId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();
        var resourceId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();
        var correlationId = Guid.CreateVersion7();
        var probeId = Guid.CreateVersion7();
        using var beforeData = JsonDocument.Parse("""{"schemaVersion":1,"value":"before"}""");
        using var afterData = JsonDocument.Parse("""{"schemaVersion":1,"value":"after"}""");
        var auditEvent = new AuditEvent
        {
            Id = auditId,
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserId = actorId,
            ActorType = "USER",
            Action = "TECHNICAL_TEST_WRITE",
            ResourceType = "TECHNICAL_TEST_PROBE",
            ResourceId = resourceId,
            BranchId = branchId,
            CorrelationId = correlationId,
            RequestId = "technical-test-request",
            BeforeData = beforeData,
            AfterData = afterData,
            Reason = "Verify transactional audit core",
            Outcome = "SUCCESS",
            SourceIpHash = new string('a', 64),
        };

        await auditTransaction.ExecuteAsync(
            auditEvent,
            cancellationToken => context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO audit_write_probe (id, payload) VALUES ({probeId}, {"committed"})",
                cancellationToken));

        Assert.True(await ProbeExistsAsync(context, probeId));
        var stored = await context.AuditEvents.AsNoTracking().SingleAsync(item => item.Id == auditId);
        Assert.Equal(actorId, stored.ActorUserId);
        Assert.Equal(resourceId, stored.ResourceId);
        Assert.Equal(branchId, stored.BranchId);
        Assert.Equal(correlationId, stored.CorrelationId);
        Assert.Equal("after", stored.AfterData?.RootElement.GetProperty("value").GetString());
        Assert.Equal(new string('a', 64), stored.SourceIpHash);
    }

    [Fact]
    public async Task AuditTransaction_WhenCriticalWriteFails_RollsBackWithoutPartialEffects()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var auditTransaction = scope.ServiceProvider.GetRequiredService<AuditTransaction>();
        await PrepareDatabaseAsync(context);

        var auditId = Guid.CreateVersion7();
        var probeId = Guid.CreateVersion7();

        await Assert.ThrowsAsync<InvalidOperationException>(() => auditTransaction.ExecuteAsync(
            CreateAuditEvent(auditId),
            async cancellationToken =>
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO audit_write_probe (id, payload) VALUES ({probeId}, {"rolled-back"})",
                    cancellationToken);
                throw new InvalidOperationException("Injected failure before audit insertion.");
            }));

        Assert.False(await ProbeExistsAsync(context, probeId));
        Assert.False(await context.AuditEvents.AsNoTracking().AnyAsync(item => item.Id == auditId));
    }

    [Fact]
    public async Task AuditTransaction_WhenAuditInsertFails_RollsBackCriticalWrite()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var auditTransaction = scope.ServiceProvider.GetRequiredService<AuditTransaction>();
        await PrepareDatabaseAsync(context);

        var duplicateAuditId = Guid.CreateVersion7();
        var probeId = Guid.CreateVersion7();
        context.AuditEvents.Add(CreateAuditEvent(duplicateAuditId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DbUpdateException>(() => auditTransaction.ExecuteAsync(
            CreateAuditEvent(duplicateAuditId),
            cancellationToken => context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO audit_write_probe (id, payload) VALUES ({probeId}, {"rolled-back"})",
                cancellationToken)));

        Assert.False(await ProbeExistsAsync(context, probeId));
        Assert.Equal(
            1,
            await context.AuditEvents.AsNoTracking().CountAsync(item => item.Id == duplicateAuditId));
    }

    [Fact]
    public async Task AuditEvent_UpdateAndDeleteAreRejectedByPostgreSql_WithoutEffect()
    {
        await using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await PrepareDatabaseAsync(context);

        var auditId = Guid.CreateVersion7();
        context.AuditEvents.Add(CreateAuditEvent(auditId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var updateException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE audit_event SET outcome = {"TAMPERED"} WHERE id = {auditId}"));
        Assert.Equal("55000", updateException.SqlState);

        var deleteException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM audit_event WHERE id = {auditId}"));
        Assert.Equal("55000", deleteException.SqlState);

        var preserved = await context.AuditEvents.AsNoTracking().SingleAsync(item => item.Id == auditId);
        Assert.Equal("SUCCESS", preserved.Outcome);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
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

    private static async Task PrepareDatabaseAsync(SgolDbContext context)
    {
        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TEMP TABLE audit_write_probe
            (
                id uuid PRIMARY KEY,
                payload text NOT NULL
            )
            """);
    }

    private static AuditEvent CreateAuditEvent(Guid id) => new()
    {
        Id = id,
        OccurredAt = DateTimeOffset.UtcNow,
        ActorType = "SYSTEM",
        Action = "TECHNICAL_TEST_WRITE",
        ResourceType = "TECHNICAL_TEST_PROBE",
        CorrelationId = Guid.CreateVersion7(),
        Outcome = "SUCCESS",
    };

    private static async Task<bool> ProbeExistsAsync(SgolDbContext context, Guid id)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM audit_write_probe WHERE id = @id)";
        command.Parameters.Add(new NpgsqlParameter<Guid>("id", id));
        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync();
        }

        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    internal static PostgreSqlContainer CreateContainerForTests()
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        return new PostgreSqlBuilder("postgres:18.6-alpine3.23")
            .WithDatabase("sgol_integration")
            .WithUsername("sgol_integration")
            .WithPassword(password)
            .Build();
    }
}
