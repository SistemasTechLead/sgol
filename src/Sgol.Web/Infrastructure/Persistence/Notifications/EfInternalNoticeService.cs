using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Notifications;

public sealed class EfInternalNoticeService(SgolDbContext dbContext, IClock clock, IUuidGenerator uuidGenerator) : IInternalNoticeService
{
    private const int MaximumAttempts = 3;

    public async Task<ReadInternalNoticeResult> MarkReadAsync(ReadInternalNoticeCommand command, CancellationToken cancellationToken = default)
    {
        var readAt = clock.UtcNow;
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await EnsureActorAsync(command.ActorUserId, readAt, cancellationToken);
                var notice = await dbContext.InternalNotices
                    .FromSqlInterpolated($"SELECT * FROM internal_notice WHERE id = {command.NoticeId} AND recipient_user_id = {command.ActorUserId} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InternalNoticeNotFoundException();

                if (notice.ReadAt is { } existingReadAt)
                {
                    await transaction.CommitAsync(cancellationToken);
                    InboxTelemetry.Notices.Add(1, new("operation", "read"), new("result", "replayed"));
                    return new(notice.Id, InternalNoticeStatuses.Read, existingReadAt, InternalNoticeReadResults.AlreadyRead);
                }

                notice.MarkRead(readAt);
                dbContext.AuditEvents.Add(new AuditEvent
                {
                    Id = uuidGenerator.NewUuid(),
                    OccurredAt = readAt,
                    ActorUserId = command.ActorUserId,
                    ActorType = "USER",
                    Action = "INTERNAL_NOTICE_READ",
                    ResourceType = "INTERNAL_NOTICE",
                    ResourceId = notice.Id,
                    BranchId = BranchScope.LorettaId,
                    CorrelationId = command.CorrelationId,
                    RequestId = command.CorrelationId.ToString("D"),
                    BeforeData = JsonSerializer.SerializeToDocument(new { schemaVersion = 1, status = InternalNoticeStatuses.Unread }),
                    AfterData = JsonSerializer.SerializeToDocument(new { schemaVersion = 1, status = InternalNoticeStatuses.Read, readAt }),
                    Outcome = "READ",
                });
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                InboxTelemetry.Notices.Add(1, new("operation", "read"), new("result", "success"));
                return new(notice.Id, InternalNoticeStatuses.Read, readAt, InternalNoticeReadResults.MarkedRead);
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < MaximumAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                dbContext.ChangeTracker.Clear();
                InboxTelemetry.Notices.Add(1, new("operation", "read"), new("result", "conflict"));
                throw new InternalNoticeConcurrencyException();
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                InboxTelemetry.Notices.Add(1, new("operation", "read"), new("result", "failure"));
                throw;
            }
        }

        throw new InternalNoticeConcurrencyException();
    }

    private async Task EnsureActorAsync(Guid actorUserId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var actors = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where user.Id == actorUserId && user.Status == BootstrapContract.ActiveAccountStatus && user.MfaEnrolledAt != null &&
                employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= at && (employment.ValidTo == null || at < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= at && (role.ValidTo == null || at < role.ValidTo)
            select role.RoleCode).Take(2).ToListAsync(cancellationToken);
        if (actors.Count != 1 || !RoleHierarchy.GrantsOwnInbox(actors[0])) throw new InboxAccessDeniedException();
    }

    private static bool IsRetryable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: "40001" or "40P01" })
            {
                return true;
            }
        }

        return false;
    }
}
