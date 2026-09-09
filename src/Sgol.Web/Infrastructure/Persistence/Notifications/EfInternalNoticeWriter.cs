using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Notifications;

public sealed class EfInternalNoticeWriter(SgolDbContext dbContext, IUuidGenerator uuidGenerator) : IInternalNoticeWriter
{
    public async Task AddAssignmentNoticeAsync(Guid assignmentVersionId, Guid personId, DateTimeOffset createdAt, CancellationToken cancellationToken = default)
    {
        try
        {
            var recipients = await dbContext.AppUsers.AsNoTracking()
                .Where(user => user.PersonId == personId && user.Status == BootstrapContract.ActiveAccountStatus)
                .Select(user => user.Id)
                .Take(2)
                .ToListAsync(cancellationToken);
            if (recipients.Count != 1)
                throw new InvalidOperationException("INTERNAL_NOTICE_RECIPIENT_INCONSISTENT");

            dbContext.InternalNotices.Add(new InternalNotice(uuidGenerator.NewUuid(), recipients[0], assignmentVersionId, createdAt));
        }
        catch
        {
            InboxTelemetry.Notices.Add(1, new("operation", "create"), new("result", "failure"));
            throw;
        }
    }

    public void RecordAssignmentNoticeCommitted() =>
        InboxTelemetry.Notices.Add(1, new("operation", "create"), new("result", "success"));
}
