using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.IntegrationTests;

internal static class InternalNoticeTestData
{
    internal static void AddAssignmentWithNotice(SgolDbContext context, AssignmentVersion assignment)
    {
        var recipient = context.AppUsers.Local.SingleOrDefault(user => user.PersonId == assignment.PersonId)
            ?? context.AppUsers.AsNoTracking().SingleOrDefault(user => user.PersonId == assignment.PersonId);
        if (recipient is null)
        {
            recipient = new AppUser
            {
                Id = Guid.CreateVersion7(),
                PersonId = assignment.PersonId,
                Status = AccountStatus.Active,
                MustChangePassword = false,
                MfaEnrolledAt = assignment.AssignedAt,
                SecurityStamp = $"synthetic-notice-{Guid.CreateVersion7():N}",
            };
            context.AppUsers.Add(recipient);
        }

        if (recipient.Status != AccountStatus.Active)
            throw new InvalidOperationException("Synthetic assignments require one active recipient account.");

        context.AssignmentVersions.Add(assignment);
        context.InternalNotices.Add(new InternalNotice(
            Guid.CreateVersion7(), recipient.Id, assignment.Id, assignment.AssignedAt));
    }
}
