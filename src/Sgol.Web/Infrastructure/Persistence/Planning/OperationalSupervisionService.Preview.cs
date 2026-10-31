using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record CorrectionProposal(Guid Id, string Name, Guid? PersonId, string? PersonName,
    DateTimeOffset? StartsAt, DateTimeOffset? DueAt, string? Error);
public sealed record ReviewProposal(DateTimeOffset At, string Fingerprint, CorrectionProposal[] Items);

public sealed partial class OperationalSupervisionService
{
    private async Task<string> ReviewFingerprintAsync(Guid actor, IReadOnlyList<SupervisionReview> reviews, DateTimeOffset at, CapacityLedger ledger, CancellationToken token)
    {
        await AuthorizeAsync(actor, token);
        if (reviews.Count is < 1 or > 500 || reviews.Select(r => r.Id).Distinct().Count() != reviews.Count) throw Error("REVIEW_BATCH_INVALID", 422);
        var ids = reviews.Select(r => r.Id).ToArray();
        var sources = await db.OperationalAssignments.AsNoTracking().Where(a => ids.Contains(a.Id)).OrderBy(a => a.Id).ToArrayAsync(token);
        var taskIds = sources.Select(a => a.TaskId).ToArray();
        var definitions = await db.OperationalTasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).OrderBy(t => t.Id).ToArrayAsync(token);
        var peers = await db.OperationalAssignments.AsNoTracking().Where(a => taskIds.Contains(a.TaskId)).OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.Revision, a.PersonId, a.StartsAt, a.DueAt, a.ReviewStatus, a.OccasionId }).ToArrayAsync(token);
        var state = JsonSerializer.Serialize(new { actor, reviews, at, sources, definitions, peers, ledger.Workers, ledger.Tasks, ledger.Temporal,
            Absent = ledger.Unavailable.OrderBy(x => x.Item1).ThenBy(x => x.Item2).Select(x => new { Person = x.Item1, Day = x.Item2 }), ledger.Legacy,
            Policy = await OperationalSchedulingPolicyService.CurrentAsync(db, token) });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
    }

    public async Task<ReviewProposal> PreviewReviewsAsync(Guid actor, IReadOnlyList<SupervisionReview> reviews, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        try
        {
            var at = clock.UtcNow;
            var ledger = await OperationalCapacityService.LedgerAsync(db, at, token);
            var hash = await ReviewFingerprintAsync(actor, reviews, at, ledger, token);
            var items = new List<CorrectionProposal>();
            foreach (var review in reviews)
            {
                try { items.Add(await StageReviewAsync(actor, review, at, ledger, token)); }
                catch (DirectionPlanException e) when (e.Status != 403)
                { items.Add(new(review.Id, (await db.OperationalAssignments.AsNoTracking().Where(a => a.Id == review.Id).Select(a => a.Name).FirstOrDefaultAsync(token)) ?? "Tarea", null, null, null, null, e.Code)); }
            }
            return new(at, hash, items.ToArray());
        }
        finally { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
    }

    private async Task<CorrectionProposal> StageReviewAsync(Guid actor, SupervisionReview review, DateTimeOffset at, CapacityLedger ledger, CancellationToken token)
    {
        Guid? id;
        if (review.Decision == "REPROGRAMAR")
        {
            await AuthorizeAssignmentAsync(actor, review.Id, token);
            await OperationalTaskService.RequireIndependentActionAsync(db, review.Id, token);
            var task = await db.OperationalAssignments.SingleAsync(a => a.Id == review.Id, token);
            if (task.Revision != review.Revision || task.ReassignedFromId == null || task.CompletedAt != null || task.RetiredAt != null) throw Error("TASK_CONFLICT", 409);
            if (string.IsNullOrWhiteSpace(review.Comment) || review.Comment.Length > 4000) throw Error("REVIEW_COMMENT_REQUIRED", 422);
            ledger = new CapacityLedger(ledger.Workers, ledger.Tasks.Where(t => !db.OperationalAssignments.Local.Any(a => a.Id == t.Id)).Concat(db.OperationalAssignments.Local).ToArray(), ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today) { Temporal = ledger.Temporal };
            var placement = CorrectionScheduling.Find(ledger, task.PersonId, task, at, review.Schedule ?? new());
            if (!placement.Eligible) throw Error(placement.Reason, 422);
            var before = new { task.StartsAt, task.DueAt, task.CorrectionScheduleJson };
            OperationalAgenda.Apply(task, placement);
            task.NominalStartsAt = task.StartsAt;
            task.CorrectionScheduleJson = JsonSerializer.Serialize(new { Request = review.Schedule ?? new(), Version = 1, Actor = actor, ScheduledAt = clock.UtcNow });
            ledger.CaptureTime(task); task.Revision++;
            Audit(actor, task.Id, "OPERATIONAL_CORRECTION_RESCHEDULED", new { Before = before, task.StartsAt, task.DueAt, review.Comment, task.Revision });
            id = task.Id;
        }
        else id = await ReviewCoreAsync(actor, review.Id, review.Revision, review.Decision, review.Comment, token, review.Schedule, true, at, ledger);
        var source = db.OperationalAssignments.Local.Single(a => a.Id == review.Id);
        var result = id is {} target ? db.OperationalAssignments.Local.Single(a => a.Id == target) : null;
        return new(review.Id, source.Name, result?.PersonId, result?.PersonName, result?.StartsAt, result?.DueAt, null);
    }

    public async Task ApplyReviewsAsync(Guid actor, IReadOnlyList<SupervisionReview> reviews, DateTimeOffset at, string fingerprint, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        try
        {
            db.ChangeTracker.Clear();
            var ledger = await OperationalCapacityService.LedgerAsync(db, at, token);
            if (at > clock.UtcNow || clock.UtcNow - at > TimeSpan.FromMinutes(2) ||
                fingerprint != await ReviewFingerprintAsync(actor, reviews, at, ledger, token)) throw Error("CORRECTION_PREVIEW_STALE", 409);
            // Compare the original proposal with the effective instant. An immediate start may
            // advance within the same uninterrupted slot; losing that slot requires a new preview.
            var expected = new List<CorrectionProposal>();
            foreach (var review in reviews) expected.Add(await StageReviewAsync(actor, review, at, ledger, token));
            db.ChangeTracker.Clear();
            var now = clock.UtcNow;
            ledger = await OperationalCapacityService.LedgerAsync(db, now, token);
            foreach (var review in reviews)
            {
                try
                {
                    var actual = await StageReviewAsync(actor, review, now, ledger, token);
                    var prior = expected.Single(p => p.Id == review.Id);
                    var immediate = prior.StartsAt == at && actual.StartsAt == now;
                    if (prior.PersonId != actual.PersonId || !immediate && (prior.StartsAt != actual.StartsAt || prior.DueAt != actual.DueAt))
                        throw Error("CORRECTION_PREVIEW_STALE", 409);
                }
                catch (DirectionPlanException e) { e.Data["ReviewAssignmentId"] = review.Id; throw; }
            }
            await db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }

    public async Task<Dictionary<Guid, string>> CorrectionConflictsAsync(Guid actor, CancellationToken token)
    {
        var tasks = await Query(await AuthorizeAsync(actor, token), actor).Where(x => x.Assignment.ReassignedFromId != null && x.Assignment.CompletedAt == null)
            .Select(x => x.Assignment).ToArrayAsync(token);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, token);
        return tasks.Select(t => (t.Id, Reason: CorrectionScheduling.Conflict(ledger, t))).Where(t => t.Reason != null).ToDictionary(t => t.Id, t => t.Reason!);
    }
}
