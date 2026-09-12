using Microsoft.EntityFrameworkCore;
using Sgol.Auditing.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Auditing;

public sealed class EfAuditSecurityEventWriter(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IAuditSecurityEventWriter
{
    public async Task WriteDeleteAttemptAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid? resourceId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty || correlationId == Guid.Empty)
            throw new ArgumentException("Audit security-event identifiers are required.");
        var occurredAt = clock.UtcNow;
        var branchId = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            where user.Id == actorUserId && user.Status == BootstrapContract.ActiveAccountStatus &&
                employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= occurredAt && (employment.ValidTo == null || occurredAt < employment.ValidTo)
            select (Guid?)employment.BranchId).SingleOrDefaultAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        await auditTransaction.ExecuteAsync(_ => Task.FromResult(new AuditEvent
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = occurredAt,
            ActorUserId = actorUserId,
            ActorType = "USER",
            Action = "AUDIT_EVENT_DELETE_ATTEMPTED",
            ResourceType = "AUDIT_EVENT",
            ResourceId = resourceId,
            BranchId = branchId,
            CorrelationId = correlationId,
            RequestId = null,
            BeforeData = null,
            AfterData = null,
            Reason = "METHOD_NOT_ALLOWED",
            Outcome = "REJECTED",
            SourceIpHash = null,
        }), cancellationToken);
        dbContext.ChangeTracker.Clear();
    }
}
