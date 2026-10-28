using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record TransferRequest(Guid AssignmentId, long Revision, Guid RecipientId, int RemainingMinutes,
    DateTimeOffset DueAt, string Reason);
public sealed record TransferPreview(TransferRequest Request, string FromName, string ToName, string TaskName,
    int ProgressUnits, int GoalUnits, long LoadBefore, long LoadAfter, decimal Available,
    string[] Overlaps, double ShiftMinutesBeforeDue, string Fingerprint);
public sealed record SupervisionWorkload(CapacityRow[] People, SupervisedTask[] Tasks);

public sealed partial class OperationalSupervisionService
{
    public static bool Transferable(OperationalAssignment a) => a.CompletedAt == null &&
        a.ReviewStatus is not ("CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA" or "PENDIENTE_REVISION");

    public async Task<SupervisionWorkload> WorkloadAsync(Guid actor, DateOnly day, bool week, CancellationToken token)
    {
        var role = await AuthorizeAsync(actor, token);
        var from = week ? day.AddDays(-(((int)day.DayOfWeek + 6) % 7)) : day;
        var through = week ? from.AddDays(6) : day;
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, token);
        var own = await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync(token);
        var people = ledger.Workers.Where(w => RolesFor(role).Contains(w.Role) && (role == CanonicalRole.Direction || w.Id != own))
            .OrderBy(w => w.Name).Select(w => {
                var c = ledger.Setting(w.Id, from);
                return new CapacityRow(w.Id, w.Name, w.Role, c?.Percent ?? 100, c?.DailyMinutes ?? OperationalCapacity.DefaultDailyMinutes,
                    ledger.Load(w.Id, from, through), ledger.Available(w.Id, from, through), 0);
            }).ToArray();
        var all = await ListAsync(actor, token);
        var items = all.Where(t => Transferable(t.Assignment) && people.Any(p => p.PersonId == t.Assignment.PersonId) &&
            ledger.Footprint(t.Assignment).Any(f => f.Day >= from && f.Day <= through)).ToArray();
        return new(people, items);
    }

    public async Task<CapacityWorker[]> TransferRecipientsAsync(Guid actor, Guid id, CancellationToken token)
    {
        var role = await AuthorizeAsync(actor, token);
        await AuthorizeAssignmentAsync(actor, id, token);
        var task = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == id, token);
        var requiredRole = task.AssignedRole ?? await db.OperationalTasks.Where(t => t.Id == task.TaskId).Select(t => t.AssignedRole).SingleAsync(token);
        var own = await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync(token);
        return (await OperationalCapacityService.WorkersAsync(db, clock.UtcNow, token))
            .GroupBy(w => w.Id).Where(g => g.Count() == 1).Select(g => g.Single())
            .Where(w => w.Id != task.PersonId && RolesFor(role).Contains(w.Role) &&
                (role == CanonicalRole.Direction || w.Id != own) && w.EligibleFor(requiredRole, task.SpecializationId))
            .OrderBy(w => w.Name).ToArray();
    }

    public async Task<TransferPreview> PreviewTransferAsync(Guid actor, TransferRequest request, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        var preview = await BuildTransferAsync(actor, request, token);
        await tx.CommitAsync(token);
        return preview;
    }

    public async Task TransferAsync(Guid actor, TransferRequest request, string fingerprint, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        var preview = await BuildTransferAsync(actor, request, token);
        if (string.IsNullOrEmpty(fingerprint) || fingerprint != preview.Fingerprint) throw Error("TRANSFER_STALE", 409);
        var task = await db.OperationalAssignments.SingleAsync(a => a.Id == request.AssignmentId, token);
        var now = clock.UtcNow;
        var receipt = new OperationalTransfer {
            Id = Guid.CreateVersion7(), AssignmentId = task.Id, TaskName = task.Name,
            FromPersonId = task.PersonId, FromName = task.PersonName, ToPersonId = request.RecipientId, ToName = preview.ToName,
            ActorId = actor, At = now, PreviousStartsAt = task.StartsAt, PreviousDueAt = task.DueAt, NewDueAt = request.DueAt,
            ProgressUnits = task.ProgressUnits, RemainingMinutes = request.RemainingMinutes, Reason = request.Reason.Trim(),
            SnapshotJson = JsonSerializer.Serialize(task)
        };
        var recipient = (await TransferRecipientsAsync(actor, task.Id, token)).Single(w => w.Id == request.RecipientId);
        task.PersonId = recipient.Id; task.PersonName = recipient.Name; task.AssignedRole = recipient.Role;
        task.StartsAt = now; task.DueAt = request.DueAt;
        if (task.SchedulingPolicyId != null) task.NominalStartsAt = now;
        task.TransferRemainingMinutes = request.RemainingMinutes; task.TransferProgressUnits = task.ProgressUnits;
        task.PreviousLoadMinutes = preview.LoadBefore; task.Revision++;
        var ledger = await OperationalCapacityService.LedgerAsync(db, now, token);
        ledger.CaptureTime(task, now);
        var attempt = await db.Set<OperationalSequenceAttempt>().SingleOrDefaultAsync(a => a.AssignmentId == task.Id, token);
        if (attempt is not null)
        {
            var stage = await db.Set<OperationalSequenceStage>().SingleAsync(s => s.Id == attempt.StageId, token);
            stage.ReservedPersonId = recipient.Id; stage.ReservedPersonName = recipient.Name;
            stage.EffectiveDueAt = task.DueAt; stage.Revision++; attempt.Revision++;
            var run = await db.Set<OperationalSequenceRun>().SingleAsync(r => r.Id == attempt.RunId, token);
            run.Revision++;
            db.Add(new OperationalSequenceEvent { Id = Guid.CreateVersion7(), RunId = run.Id, StageId = stage.Id,
                AttemptId = attempt.Id, Kind = "REASIGNACION", EventKey = $"transfer:{receipt.Id:N}", OccurredAt = now,
                ActorId = actor, Comment = $"{receipt.FromName} → {receipt.ToName}: {receipt.Reason}" });
        }
        db.Add(receipt);
        Audit(actor, task.Id, "OPERATIONAL_TASK_TRANSFERRED", new { receipt.Id, receipt.FromPersonId, receipt.FromName,
            receipt.ToPersonId, receipt.ToName, receipt.PreviousStartsAt, receipt.PreviousDueAt, task.StartsAt, task.DueAt,
            receipt.ProgressUnits, receipt.RemainingMinutes, receipt.Reason, task.Revision, preview.LoadBefore, preview.LoadAfter,
            preview.Available, preview.Overlaps });
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
    }

    private async Task<TransferPreview> BuildTransferAsync(Guid actor, TransferRequest request, CancellationToken token)
    {
        if (request.RemainingMinutes is < 1 or > 4800 || request.DueAt.Offset != TimeSpan.Zero ||
            request.DueAt <= clock.UtcNow || request.DueAt > clock.UtcNow.AddDays(366) || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            throw Error("TRANSFER_INVALID", 422);
        await AuthorizeAssignmentAsync(actor, request.AssignmentId, token);
        var task = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == request.AssignmentId, token);
        if (task.Revision != request.Revision) throw Error("TRANSFER_STALE", 409);
        if (!Transferable(task)) throw Error("TRANSFER_STATE", 409);
        var recipient = (await TransferRecipientsAsync(actor, task.Id, token)).SingleOrDefault(w => w.Id == request.RecipientId)
            ?? throw Error("TRANSFER_RECIPIENT", 422);
        var attempt = await db.Set<OperationalSequenceAttempt>().AsNoTracking().SingleOrDefaultAsync(a => a.AssignmentId == task.Id, token);
        OperationalSequenceStage? stage = null; OperationalSequenceRun? run = null;
        if (attempt != null)
        {
            stage = await db.Set<OperationalSequenceStage>().AsNoTracking().SingleAsync(s => s.Id == attempt.StageId, token);
            run = await db.Set<OperationalSequenceRun>().AsNoTracking().SingleAsync(r => r.Id == attempt.RunId, token);
            if (attempt.Status != "ACTIVO" || stage.CurrentAttemptId != attempt.Id || stage.Status != "DISPONIBLE" ||
                run.Status != "EN_CURSO" || run.ActivePosition != stage.Position || stage.DependencySatisfiedAt == null ||
                stage.MinimumStartsAt > clock.UtcNow || stage.EligibleAt > clock.UtcNow)
                throw Error("TRANSFER_PROCESS_STATE", 409);
        }
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, token);
        // Validate real presence, without treating coverage as a longer base shift.
        var projected = JsonSerializer.Deserialize<OperationalAssignment>(JsonSerializer.Serialize(task))!;
        projected.PersonId = recipient.Id; projected.StartsAt = clock.UtcNow; projected.DueAt = request.DueAt;
        var time = ledger.CheckTime(recipient.Id, projected, clock.UtcNow);
        if (!time.Eligible) throw Error(time.Reason, 422);
        if (!ledger.Temporal.Enabled) throw Error("TRANSFER_SCHEDULE_REQUIRED", 422);
        var day = WeekContract.LocalToday(clock.UtcNow);
        var interval = ledger.Temporal.Day(recipient.Id, day, false).Intervals?
            .Where(i => TemporalEligibilityContext.At(day, i.Entry) <= clock.UtcNow && clock.UtcNow < TemporalEligibilityContext.At(day, i.Exit))
            .Select(i => (DateTimeOffset?)TemporalEligibilityContext.At(day, i.Exit)).FirstOrDefault();
        var end = time.Windows?.Where(w => w.Entry <= clock.UtcNow && clock.UtcNow < w.Exit).Select(w => (DateTimeOffset?)w.Exit).FirstOrDefault();
        if (interval == null || end == null) throw Error("SCHEDULE_NOT_PRESENT_AT_RECEPTION", 422);
        var deadlineDay = WeekContract.LocalToday(request.DueAt.AddTicks(-1));
        var deadlineInShift = (ledger.Temporal.Day(recipient.Id, deadlineDay, false).Intervals ?? [])
            .Any(i => TemporalEligibilityContext.At(deadlineDay, i.Entry) <= request.DueAt && request.DueAt <= TemporalEligibilityContext.At(deadlineDay, i.Exit));
        if (!deadlineInShift || ledger.Unavailable.Contains((recipient.Id, deadlineDay)) ||
            recipient.EmploymentEnd < request.DueAt || recipient.RoleEnd < request.DueAt ||
            task.TaskId == ledger.Temporal.ClosingTaskId && request.DueAt > interval)
            throw Error("TRANSFER_DEADLINE", 422);
        projected.TransferRemainingMinutes = request.RemainingMinutes; projected.TransferProgressUnits = task.ProgressUnits;
        var load = ledger.Load(recipient.Id, day, day);
        var available = ledger.Available(recipient.Id, day, day);
        var projectedLedger = new CapacityLedger(ledger.Workers, ledger.Tasks.Where(t => t.Id != task.Id).Append(projected).ToArray(),
            ledger.Capacities, ledger.Unavailable, ledger.Legacy, ledger.Today) { Temporal = ledger.Temporal };
        var loadAfter = projectedLedger.Load(recipient.Id, day, day);
        var shiftMinutes = CapacityLedger.Days(day, deadlineDay).Where(d => !ledger.Unavailable.Contains((recipient.Id, d)))
            .SelectMany(d => (ledger.Temporal.Day(recipient.Id, d, false).Intervals ?? []).Select(i =>
                Math.Max(0, (new[] { request.DueAt, TemporalEligibilityContext.At(d, i.Exit) }.Min() -
                    new[] { clock.UtcNow, TemporalEligibilityContext.At(d, i.Entry) }.Max()).TotalMinutes))).Sum();
        var overlaps = ledger.Tasks.Where(t => t.Id != task.Id && t.PersonId == recipient.Id && t.CompletedAt == null &&
            t.StartsAt < request.DueAt && t.DueAt > clock.UtcNow).OrderBy(t => t.Id).Select(t => t.Name).ToArray();
        var photos = await db.OperationalPhotographs.AsNoTracking().Where(p => p.AssignmentId == task.Id)
            .OrderBy(p => p.Id).Select(p => new { p.Id, p.AttachedAt }).ToArrayAsync(token);
        var attachment = await db.OperationalAttachments.AsNoTracking().Where(a => a.AssignmentId == task.Id)
            .Select(a => new { a.FileName, a.ContentType }).SingleOrDefaultAsync(token);
        var state = JsonSerializer.Serialize(new { request, task, ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Temporal,
            Unavailable = ledger.Unavailable.OrderBy(x => x.Item1).ThenBy(x => x.Item2), ledger.Legacy, attempt, stage, run, photos, attachment });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
        return new(request, task.PersonName, recipient.Name, task.Name, task.ProgressUnits, task.GoalUnits,
            load, loadAfter, available, overlaps, shiftMinutes, hash);
    }
}
