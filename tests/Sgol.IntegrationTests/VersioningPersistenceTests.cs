using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Versioning;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class VersioningPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset September =
        new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] CurrentConstraintNames =
        ["IX_technical_version_probe_one_current", "EX_technical_version_probe_validity"];
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = CreateProbeContext();
        await context.Database.ExecuteSqlRawAsync(VersioningPostgreSql.RequiredExtensionSql);
        await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
        await context.Database.ExecuteSqlRawAsync(
            VersioningPostgreSql.AddNoOverlapConstraintSql(
                "technical_version_probe",
                "object_id",
                "scope_id"));

        await using var application = CreateApplicationContext();
        await application.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task PostgreSqlRejectsSecondCurrentOverlapAndInvalidLifecycle()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        var scopeId = Guid.CreateVersion7();
        var currentObject = Guid.CreateVersion7();
        await InsertAsync(connection, Guid.CreateVersion7(), currentObject, scopeId, "VIGENTE", September, null, "initial");

        var currentConflict = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            connection,
            Guid.CreateVersion7(),
            currentObject,
            scopeId,
            "VIGENTE",
            September.AddDays(1),
            null,
            "second"));
        Assert.Contains(
            currentConflict.ConstraintName,
            CurrentConstraintNames);

        var historicalObject = Guid.CreateVersion7();
        await InsertAsync(
            connection,
            Guid.CreateVersion7(),
            historicalObject,
            scopeId,
            "SUSTITUIDA",
            September,
            September.AddDays(10),
            "first interval");
        var overlap = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            connection,
            Guid.CreateVersion7(),
            historicalObject,
            scopeId,
            "SUSTITUIDA",
            September.AddDays(5),
            September.AddDays(15),
            "overlap"));
        Assert.Equal("EX_technical_version_probe_validity", overlap.ConstraintName);

        var invalidDraft = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            connection,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            scopeId,
            "BORRADOR",
            effectiveFrom: null,
            effectiveTo: null,
            reason: "draft must not have reason"));
        Assert.Equal("CK_technical_version_probe_lifecycle", invalidDraft.ConstraintName);
    }

    [Fact]
    public async Task SameEtagConcurrency_AllowsOneWinnerAndLeavesTrackerClean()
    {
        var probe = NewDraft();
        await using (var seed = CreateProbeContext())
        {
            seed.Add(probe);
            await seed.SaveChangesAsync();
            seed.ChangeTracker.Clear();
            Assert.Empty(seed.ChangeTracker.Entries());
        }

        await using var first = CreateProbeContext();
        await using var second = CreateProbeContext();
        var firstCopy = await first.Set<VersionProbe>().SingleAsync(item => item.Id == probe.Id);
        var secondCopy = await second.Set<VersionProbe>().SingleAsync(item => item.Id == probe.Id);
        firstCopy.RowVersion++;
        secondCopy.RowVersion++;

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        first.ChangeTracker.Clear();
        second.ChangeTracker.Clear();

        Assert.Empty(first.ChangeTracker.Entries());
        Assert.Empty(second.ChangeTracker.Entries());
        await using var verification = CreateProbeContext();
        Assert.Equal(2, (await verification.Set<VersionProbe>().SingleAsync(item => item.Id == probe.Id)).RowVersion);
    }

    [Fact]
    public async Task VersioningTransaction_AuditsSuccessAndDenialAndRollsBackAuditFailure()
    {
        await using var context = CreateApplicationContext();
        var transaction = new VersioningTransaction(context, new AuditTransaction(context));
        var successId = Guid.CreateVersion7();
        var successAuditId = Guid.CreateVersion7();

        var result = await transaction.ExecuteAsync(
            _ => Task.FromResult(true),
            () => NewAudit(Guid.CreateVersion7(), "DENIED"),
            "approved technical reason",
            async token =>
            {
                await InsertDraftRawAsync(context, successId, token);
                return (successId, NewAudit(successAuditId, "SUCCESS", "approved technical reason"));
            });

        Assert.Equal(successId, result);
        Assert.True(await ProbeExistsAsync(context, successId));
        Assert.True(await context.AuditEvents.AsNoTracking().AnyAsync(item => item.Id == successAuditId));
        Assert.Empty(context.ChangeTracker.Entries());

        var deniedId = Guid.CreateVersion7();
        var deniedAuditId = Guid.CreateVersion7();
        await Assert.ThrowsAsync<VersioningAccessDeniedException>(() => transaction.ExecuteAsync(
            _ => Task.FromResult(false),
            () => NewAudit(deniedAuditId, "DENIED"),
            "reason not evaluated before authorization",
            async token =>
            {
                await InsertDraftRawAsync(context, deniedId, token);
                return (deniedId, NewAudit(Guid.CreateVersion7(), "SUCCESS"));
            }));
        Assert.False(await ProbeExistsAsync(context, deniedId));
        Assert.True(await context.AuditEvents.AsNoTracking().AnyAsync(item => item.Id == deniedAuditId));
        Assert.Empty(context.ChangeTracker.Entries());

        var rollbackId = Guid.CreateVersion7();
        await Assert.ThrowsAsync<DbUpdateException>(() => transaction.ExecuteAsync(
            _ => Task.FromResult(true),
            () => NewAudit(Guid.CreateVersion7(), "DENIED"),
            "approved technical reason",
            async token =>
            {
                await InsertDraftRawAsync(context, rollbackId, token);
                return (rollbackId, NewInvalidAudit(Guid.CreateVersion7(), "approved technical reason"));
            }));
        Assert.False(await ProbeExistsAsync(context, rollbackId));
        Assert.Empty(context.ChangeTracker.Entries());

        var functionalFailureId = Guid.CreateVersion7();
        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.ExecuteAsync<Guid>(
            _ => Task.FromResult(true),
            () => NewAudit(Guid.CreateVersion7(), "DENIED"),
            "approved technical reason",
            async token =>
            {
                await InsertDraftRawAsync(context, functionalFailureId, token);
                throw new InvalidOperationException("Synthetic functional failure.");
            }));
        Assert.False(await ProbeExistsAsync(context, functionalFailureId));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public void ApplicationModel_ContainsOnlyApprovedFunctionalVersionConsumers()
    {
        using var context = CreateApplicationContext();

        var consumers = context.Model.GetEntityTypes()
            .Where(entity => typeof(IVersionedEntity).IsAssignableFrom(entity.ClrType))
            .Select(entity => entity.ClrType)
            .ToHashSet();

        Assert.True(consumers.SetEquals(
            [typeof(ConfigurationRelease), typeof(CalendarDayVersion), typeof(TaskDefinitionVersion), typeof(EligibilityPolicyVersion), typeof(ActivationRuleVersion)]));
        Assert.DoesNotContain(consumers, type => type.Assembly == typeof(IVersionedEntity).Assembly);
    }

    private ProbeDbContext CreateProbeContext() => new(
        new DbContextOptionsBuilder<ProbeDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private SgolDbContext CreateApplicationContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private static VersionProbe NewDraft() => new()
    {
        Id = Guid.CreateVersion7(),
        ObjectId = Guid.CreateVersion7(),
        ScopeId = Guid.CreateVersion7(),
        Status = VersionStatuses.Draft,
        RowVersion = 1,
    };

    private static AuditEvent NewAudit(Guid id, string outcome, string? reason = null) => new()
    {
        Id = id,
        OccurredAt = September,
        ActorType = "SYSTEM",
        Action = "TECHNICAL_VERSION_TEST",
        ResourceType = "TECHNICAL_TEST_PROBE",
        CorrelationId = Guid.CreateVersion7(),
        BeforeData = JsonSerializer.SerializeToDocument(new { schemaVersion = 1, status = "BORRADOR" }),
        AfterData = JsonSerializer.SerializeToDocument(new { schemaVersion = 1, status = "VIGENTE" }),
        Reason = reason,
        Outcome = outcome,
    };

    private static AuditEvent NewInvalidAudit(Guid id, string reason) => new()
    {
        Id = id,
        OccurredAt = September,
        ActorType = null!,
        Action = "TECHNICAL_VERSION_TEST",
        ResourceType = "TECHNICAL_TEST_PROBE",
        CorrelationId = Guid.CreateVersion7(),
        BeforeData = JsonSerializer.SerializeToDocument(new { schemaVersion = 1, status = "BORRADOR" }),
        AfterData = JsonSerializer.SerializeToDocument(new { schemaVersion = 1, status = "VIGENTE" }),
        Reason = reason,
        Outcome = "SUCCESS",
    };

    private static async Task InsertDraftRawAsync(
        SgolDbContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO technical_version_probe
                (id, object_id, scope_id, status, row_version)
            VALUES
                ({id}, {Guid.CreateVersion7()}, {Guid.CreateVersion7()}, {VersionStatuses.Draft}, {1L})
            """,
            cancellationToken);
    }

    private static async Task<bool> ProbeExistsAsync(SgolDbContext context, Guid id)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM technical_version_probe WHERE id = @id)";
        command.Parameters.Add(new NpgsqlParameter<Guid>("id", id));
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task InsertAsync(
        NpgsqlConnection connection,
        Guid id,
        Guid objectId,
        Guid scopeId,
        string status,
        DateTimeOffset? effectiveFrom,
        DateTimeOffset? effectiveTo,
        string? reason)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO technical_version_probe " +
            "(id, object_id, scope_id, status, effective_from, effective_to, reason, row_version) " +
            "VALUES (@id, @object_id, @scope_id, @status, @effective_from, @effective_to, @reason, 1)";
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("object_id", objectId);
        command.Parameters.AddWithValue("scope_id", scopeId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("effective_from", (object?)effectiveFrom ?? DBNull.Value);
        command.Parameters.AddWithValue("effective_to", (object?)effectiveTo ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var probe = modelBuilder.Entity<VersionProbe>();
            probe.HasKey(entity => entity.Id);
            probe.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
            probe.Property(entity => entity.ObjectId).HasColumnName("object_id");
            probe.Property(entity => entity.ScopeId).HasColumnName("scope_id");
            probe.ConfigureVersioning(
                "technical_version_probe",
                nameof(VersionProbe.ObjectId),
                nameof(VersionProbe.ScopeId));
        }
    }

    private sealed class VersionProbe : IVersionedEntity
    {
        public Guid Id { get; set; }

        public Guid ObjectId { get; set; }

        public Guid ScopeId { get; set; }

        public string Status { get; set; } = null!;

        public DateTimeOffset? EffectiveFrom { get; set; }

        public DateTimeOffset? EffectiveTo { get; set; }

        public string? Reason { get; set; }

        public Guid? SupersedesId { get; set; }

        public long RowVersion { get; set; }
    }
}
