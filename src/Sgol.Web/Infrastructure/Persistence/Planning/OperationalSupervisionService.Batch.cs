using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record SupervisionReview(Guid Id, long Revision, string Decision, string? Comment);

public sealed partial class OperationalSupervisionService
{
    public async Task ReviewBatchAsync(Guid actor, IReadOnlyList<SupervisionReview> reviews, CancellationToken token)
    {
        if (reviews.Count is < 1 or > 500 || reviews.Select(x => x.Id).Distinct().Count() != reviews.Count)
            throw Error("REVIEW_BATCH_INVALID", 422);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        try
        {
            foreach (var review in reviews)
            {
                try { await ReviewCoreAsync(actor, review.Id, review.Revision, review.Decision, review.Comment, token); }
                catch (DirectionPlanException e) { e.Data["ReviewAssignmentId"] = review.Id; throw; }
            }
            await tx.CommitAsync(token);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<Dictionary<Guid, Guid[]>> PhotoIdsAsync(Guid actor, Guid[] ids, CancellationToken token)
    {
        var role = await AuthorizeAsync(actor, token);
        var allowed = Query(role, actor).Where(x => ids.Contains(x.Assignment.Id)).Select(x => x.Assignment.Id);
        var photos = await db.OperationalPhotographs.AsNoTracking()
            .Where(p => allowed.Contains(p.AssignmentId) && p.AttachedAt != null)
            .OrderBy(p => p.Position).Select(p => new { p.AssignmentId, p.Id }).ToArrayAsync(token);
        return photos.GroupBy(p => p.AssignmentId).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToArray());
    }
}
