using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Continuity.Contracts;
using Sgol.Identity.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Continuity;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class ContinuityPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 18, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => postgres.StartAsync();
    public async Task DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task ReconciliationTablesRejectUpdateAndDeleteWithoutChangingRows()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var reconciliationId = Guid.CreateVersion7();
        context.RecoveryReconciliations.Add(new RecoveryReconciliation
        {
            Id = reconciliationId,
            BranchId = BranchScope.LorettaId,
            RequestedBy = Guid.CreateVersion7(),
            Reason = "Simulacro sintético",
            RequestedAt = DateTimeOffset.UtcNow
        });
        context.RecoveryReconciliationEvents.Add(new RecoveryReconciliationEvent
        {
            Id = Guid.CreateVersion7(),
            ReconciliationId = reconciliationId,
            Sequence = 1,
            EventType = "RECOVERY_RECONCILIATION_REQUESTED",
            Status = "REQUESTED",
            ActorUserId = Guid.CreateVersion7(),
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.CreateVersion7()
        });
        context.RecoveryReconciliationDifferences.Add(new RecoveryReconciliationDifference
        {
            Id = Guid.CreateVersion7(),
            ReconciliationId = reconciliationId,
            Ordinal = 1,
            Group = "identity",
            ResourceType = "person",
            StableKey = "synthetic-person",
            Kind = "IDENTITY_MISSING"
        });
        await context.SaveChangesAsync();

        foreach (var table in new[]
                 {
                     "recovery_reconciliation", "recovery_reconciliation_event",
                     "recovery_reconciliation_difference"
                 })
        {
            var updateSql = table switch
            {
                "recovery_reconciliation" => "UPDATE recovery_reconciliation SET id = id",
                "recovery_reconciliation_event" => "UPDATE recovery_reconciliation_event SET id = id",
                _ => "UPDATE recovery_reconciliation_difference SET id = id"
            };
            var deleteSql = table switch
            {
                "recovery_reconciliation" => "DELETE FROM recovery_reconciliation",
                "recovery_reconciliation_event" => "DELETE FROM recovery_reconciliation_event",
                _ => "DELETE FROM recovery_reconciliation_difference"
            };
            var update = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
                updateSql));
            Assert.Equal("55000", update.SqlState);
            var delete = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
                deleteSql));
            Assert.Equal("55000", delete.SqlState);
        }

        Assert.Equal(1, await context.RecoveryReconciliations.AsNoTracking().CountAsync());
        Assert.Equal(1, await context.RecoveryReconciliationEvents.AsNoTracking().CountAsync());
        Assert.Equal(1, await context.RecoveryReconciliationDifferences.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task SequenceTerminalAndDifferenceUniquenessAreDatabaseAuthoritative()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var reconciliationId = Guid.CreateVersion7();
        context.RecoveryReconciliations.Add(new RecoveryReconciliation
        {
            Id = reconciliationId,
            BranchId = BranchScope.LorettaId,
            RequestedBy = Guid.CreateVersion7(),
            Reason = "Simulacro sintético",
            RequestedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO recovery_reconciliation_event
                (id,reconciliation_id,sequence,event_type,status,technical_actor,occurred_at,correlation_id)
            VALUES ({{Guid.CreateVersion7()}},{{reconciliationId}},1,'RECOVERY_RECONCILIATION_FAILED','FAILED',
                'SGOL_OPERATIONS',{{DateTimeOffset.UtcNow}},{{Guid.CreateVersion7()}})
            """);
        var terminal = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO recovery_reconciliation_event
                (id,reconciliation_id,sequence,event_type,status,technical_actor,occurred_at,correlation_id)
            VALUES ({{Guid.CreateVersion7()}},{{reconciliationId}},2,'RECOVERY_RECONCILIATION_FAILED','FAILED',
                'SGOL_OPERATIONS',{{DateTimeOffset.UtcNow}},{{Guid.CreateVersion7()}})
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, terminal.SqlState);
    }

    [Fact]
    public async Task DirectionCreatesAtomicallyAndOtherRoleIsDeniedWithoutEffect()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var direction = AddActor(context, "HU035-DIR", CanonicalRole.Direction);
        var administration = AddActor(context, "HU035-ADM", CanonicalRole.Administration);
        await context.SaveChangesAsync();
        var clock = new FixedClock(Now);
        var generator = new Uuid7Generator(clock);
        var service = new EfRecoveryReconciliationService(context, new AuditTransaction(context),
            new TestOutboxWriter(context, generator), clock, generator);
        var correlationId = Guid.CreateVersion7();

        var created = await service.CreateAsync(new(direction, Guid.CreateVersion7(), correlationId,
            "Simulacro sintético"));

        Assert.Equal(RecoveryReconciliationStatuses.Requested, created.Status);
        Assert.Single(await context.RecoveryReconciliations.AsNoTracking().ToListAsync());
        Assert.Single(await context.RecoveryReconciliationEvents.AsNoTracking().ToListAsync());
        Assert.Single(await context.OutboxEvents.AsNoTracking().ToListAsync());
        Assert.Single(await context.AuditEvents.AsNoTracking().Where(item =>
            item.Action == RecoveryReconciliationEvents.Requested).ToListAsync());
        var before = await CountContinuityEffectsAsync(context);
        await Assert.ThrowsAsync<RecoveryAccessDeniedException>(() => service.CreateAsync(new(administration,
            Guid.CreateVersion7(), Guid.CreateVersion7(), "No autorizado")));
        Assert.Equal(before, await CountContinuityEffectsAsync(context));
    }

    [Fact]
    public async Task RestoreStartReplayUsesPostgreSqlTimestampPrecisionWithoutRelaxingImmutability()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var reconciliationId = Guid.CreateVersion7();
        context.RecoveryReconciliations.Add(new RecoveryReconciliation
        {
            Id = reconciliationId,
            BranchId = BranchScope.LorettaId,
            RequestedBy = Guid.CreateVersion7(),
            Reason = "Simulacro sintético",
            RequestedAt = Now
        });
        context.RecoveryReconciliationEvents.Add(new RecoveryReconciliationEvent
        {
            Id = Guid.CreateVersion7(),
            ReconciliationId = reconciliationId,
            Sequence = 1,
            EventType = RecoveryReconciliationEvents.ReferenceReady,
            Status = RecoveryReconciliationStatuses.ReferenceReady,
            OccurredAt = Now,
            CorrelationId = Guid.CreateVersion7(),
            TechnicalActor = "SGOL_OPERATIONS"
        });
        await context.SaveChangesAsync();
        var clock = new FixedClock(Now);
        var generator = new Uuid7Generator(clock);
        var service = new EfRecoveryReconciliationService(context, new AuditTransaction(context),
            new TestOutboxWriter(context, generator), clock, generator);
        var startedAt = Now.AddTicks(9);
        var evidenceHash = new string('a', 64);

        await service.MarkRestoreStartedAsync(new(reconciliationId, Guid.CreateVersion7(), startedAt, evidenceHash));
        context.ChangeTracker.Clear();
        await service.MarkRestoreStartedAsync(new(reconciliationId, Guid.CreateVersion7(), startedAt, evidenceHash));

        Assert.Single(await context.RecoveryReconciliationEvents.AsNoTracking().Where(item =>
            item.ReconciliationId == reconciliationId &&
            item.EventType == RecoveryReconciliationEvents.RestoreStarted).ToListAsync());
        var conflict = await Assert.ThrowsAsync<RecoveryContractException>(() => service.MarkRestoreStartedAsync(
            new(reconciliationId, Guid.CreateVersion7(), startedAt.AddTicks(10), evidenceHash)));
        Assert.Equal("RECONCILIATION_IMMUTABLE_CONFLICT", conflict.ErrorCode);
    }

    private SgolDbContext CreateContext() => new(new DbContextOptionsBuilder<SgolDbContext>()
        .UseNpgsql(postgres.GetConnectionString()).Options);

    private static Guid AddActor(SgolDbContext context, string code, string role)
    {
        var personId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        context.People.Add(new Person
        {
            Id = personId,
            StableCode = code,
            DisplayName = $"Persona {code}",
            CreatedAt = Now.AddDays(-30)
        });
        context.EmploymentVersions.Add(new EmploymentVersion(Guid.CreateVersion7(), personId,
            BranchScope.LorettaId, EmploymentStatus.Active, Now.AddDays(-30)));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now.AddDays(-30),
            SecurityStamp = $"synthetic-{userId:N}"
        });
        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BranchId = BranchScope.LorettaId,
            RoleCode = role,
            Status = RoleAssignmentStatus.Active,
            ValidFrom = Now.AddDays(-30)
        });
        return userId;
    }

    private static async Task<(int Headers, int Events, int Outbox, int Audit)> CountContinuityEffectsAsync(
        SgolDbContext context) => (await context.RecoveryReconciliations.CountAsync(),
        await context.RecoveryReconciliationEvents.CountAsync(), await context.OutboxEvents.CountAsync(),
        await context.AuditEvents.CountAsync());

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }

    private sealed class TestOutboxWriter(SgolDbContext context, IUuidGenerator generator) : IOutboxWriter
    {
        public OutboxEvent Enqueue(string eventType, Guid? aggregateId, System.Text.Json.JsonElement data,
            Guid correlationId, DateTimeOffset? availableAt = null)
        {
            var item = new OutboxEvent
            {
                Id = generator.NewUuid(),
                EventType = eventType,
                AggregateId = aggregateId,
                Payload = System.Text.Json.JsonSerializer.Serialize(new { schemaVersion = 1, correlationId, data }),
                CreatedAt = Now,
                AvailableAt = availableAt ?? Now
            };
            context.OutboxEvents.Add(item);
            return item;
        }
    }
}
