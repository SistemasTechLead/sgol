using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Continuity.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Continuity;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class ContinuityPersistenceTests
{
    [Fact]
    public async Task Front020AuditedQueryFailsClosedAndApprovalRollbackPreservesOriginalIntention()
    {
        await using var context = CreateContext(); await context.Database.EnsureDeletedAsync(); await context.Database.MigrateAsync();
        var direction = AddActor(context, "FRONT020-DIR", CanonicalRole.Direction); await context.SaveChangesAsync();
        var clock = new FixedClock(Now); var generator = new Uuid7Generator(clock);
        var service = new EfRecoveryReconciliationService(context, new AuditTransaction(context), new TestOutboxWriter(context, generator), clock, generator);
        var createKey = Guid.CreateVersion7(); var command = new CreateRecoveryReconciliationCommand(direction, createKey, Guid.CreateVersion7(), "Simulacro sintético");
        var created = await service.CreateAsync(command); var before = await CountContinuityEffectsAsync(context);
        var recovered = await service.CreateAsync(command with { CorrelationId = Guid.CreateVersion7() }); Assert.True(recovered.Replayed); Assert.Equal(created.ReconciliationId, recovered.ReconciliationId); Assert.Equal(before, await CountContinuityEffectsAsync(context));
        context.RecoveryReconciliationEvents.Add(new() { Id = Guid.CreateVersion7(), ReconciliationId = created.ReconciliationId, Sequence = 2, EventType = RecoveryReconciliationEvents.Completed, Status = "MATCHED", TechnicalActor = "SGOL_WORKER", OccurredAt = Now, CorrelationId = Guid.CreateVersion7(), DifferenceCount = 0, DifferencesTruncated = false, ObservedRpoSeconds = 10, ObservedRtoSeconds = 20 }); await context.SaveChangesAsync(); context.ChangeTracker.Clear();
        var viewed = await service.GetAsync(new(direction, Guid.CreateVersion7(), created.ReconciliationId)); Assert.Equal("MATCHED", viewed.Status);
        Assert.Equal(before.Audit + 1, await context.AuditEvents.CountAsync());
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event ADD CONSTRAINT front020_reject_view CHECK (action <> 'RECOVERY_RECONCILIATION_VIEWED') NOT VALID"); before = await CountContinuityEffectsAsync(context);
        await Assert.ThrowsAsync<RecoveryAuditFailedException>(() => service.GetAsync(new(direction, Guid.CreateVersion7(), created.ReconciliationId))); Assert.Equal(before, await CountContinuityEffectsAsync(context));
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event DROP CONSTRAINT front020_reject_view");
        var approval = new ApproveRecoveryReconciliationCommand(direction, Guid.CreateVersion7(), Guid.CreateVersion7(), created.ReconciliationId, 2, "Aceptación sintética");
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event ADD CONSTRAINT front020_reject_approval CHECK (action <> 'RECOVERY_RECONCILIATION_APPROVED') NOT VALID"); var keys = await context.IdempotencyRecords.CountAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => service.ApproveAsync(approval)); context.ChangeTracker.Clear(); Assert.Equal(before, await CountContinuityEffectsAsync(context)); Assert.Equal(keys, await context.IdempotencyRecords.CountAsync());
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event DROP CONSTRAINT front020_reject_approval");
        var approved = await service.ApproveAsync(approval); Assert.Equal("APPROVED", approved.Status); before = await CountContinuityEffectsAsync(context);
        Assert.True((await service.ApproveAsync(approval with { CorrelationId = Guid.CreateVersion7() })).Replayed); Assert.Equal(before, await CountContinuityEffectsAsync(context));
        await Assert.ThrowsAsync<RecoveryReconciliationVersionConflictException>(() => service.ApproveAsync(approval with { IdempotencyKey = Guid.CreateVersion7() })); Assert.Equal(before, await CountContinuityEffectsAsync(context));
    }
}
