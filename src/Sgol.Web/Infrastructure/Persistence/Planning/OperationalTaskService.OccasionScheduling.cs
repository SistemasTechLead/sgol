using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Identity.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record OccasionSchedulePreview(string Fingerprint, DateTimeOffset NotBefore, OperationalAssignmentDetails[] Assignments);

public sealed partial class OperationalTaskService
{
    public async Task<OccasionSchedulePreview> PreviewOccasionAsync(Guid actor, OccasionInput input, CancellationToken token)
    {
        var role = await AuthorizeOccasionalAsync(actor, token);
        if (role != CanonicalRole.Direction && (input.PlanId != null || input.Automatic)) throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token);
        var task = await db.OperationalTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == input.TaskId && t.Active && !t.Deleted && t.PeriodType == "OCASIONAL" && t.ProcessId == OperationalSequence.GeneralId, token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        if (task.Revision != input.TaskRevision) throw new DirectionPlanException("TASK_CONFLICT", 409);
        if (input.PersonIds.Length > 1000 || input.PersonIds.Distinct().Count() != input.PersonIds.Length || input.Automatic && input.PersonIds.Length != 0 || !input.Automatic && input.PersonIds.Length == 0) throw new DirectionPlanException("OCCASION_INVALID", 422);
        var now = clock.UtcNow;
        input = input with { Name = (input.Name ?? "").Trim(), Instructions = (input.Instructions ?? "").Trim(), PublicationText = (input.PublicationText ?? "").Trim(), Channels = (input.Channels ?? "").Trim(), StartsAt = input.StartsAt.ToUniversalTime(), DueAt = input.DueAt.ToUniversalTime(), PersonIds = input.PersonIds.Order().ToArray(), SchedulingNotBefore = now.AddMinutes(1) };
        var plan = input.PlanId is {} id ? await db.DirectionPreparedPlans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.Status == "ASIGNADO" && p.BranchId == BranchScope.LorettaId && p.PeriodType != "OCASIONAL", token)
            ?? throw new DirectionPlanException("DIRECTION_PLAN_NOT_FOUND", 404) : null;
        var policy = plan == null ? await OperationalSchedulingPolicyService.CurrentAsync(db, token) : plan.SchedulingPolicyId is {} pid ? await db.Set<OperationalSchedulingPolicy>().AsNoTracking().SingleAsync(p => p.Id == pid, token) : null;
        if (input.Name.Length is < 1 or > 120 || input.Instructions.Length is < 1 or > 4000 || input.PublicationText.Length > 4000 || input.Channels.Length > 500 || input.StartsAt < OperationalSchedule.Instant(WeekContract.LocalToday(now)) || input.StartsAt > now.AddDays(365)) throw new DirectionPlanException("OCCASION_INVALID", 422);
        if (plan != null && (WeekContract.LocalToday(input.StartsAt) < plan.StartsOn || WeekContract.LocalToday(input.StartsAt) > plan.EndsOn)) throw new DirectionPlanException("OCCASION_INVALID", 422);
        if (policy == null && (input.DueAt <= input.StartsAt || input.DueAt <= now || input.DueAt - input.StartsAt > TimeSpan.FromDays(365))) throw new DirectionPlanException("OCCASION_INVALID", 422);
        var ledger = await OperationalCapacityService.LedgerAsync(db, now, token);
        var assignments = OccasionAssignments(task, plan?.Id ?? Guid.Empty, Guid.Empty, policy, role, input, ledger, now, null);
        var fingerprint = OccasionFingerprint(task, policy, input, ledger, assignments);
        await tx.CommitAsync(token);
        return new(fingerprint, input.SchedulingNotBefore.Value, assignments.Select(a => a.Details()).ToArray());
    }

    private static string OccasionFingerprint(OperationalTask task, OperationalSchedulingPolicy? policy, OccasionInput input, CapacityLedger ledger, OperationalAssignment[] assignments) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { task, policy,
            input = input with { Name = input.Name.Trim(), Instructions = input.Instructions.Trim(), PublicationText = input.PublicationText.Trim(), Channels = input.Channels.Trim(), SchedulingFingerprint = null },
            ledger.Workers, ledger.Tasks, ledger.Capacities, ledger.Temporal,
            Unavailable = ledger.Unavailable.OrderBy(x => x.Item1).ThenBy(x => x.Item2), Legacy = ledger.Legacy.OrderBy(x => x.Key),
            rows = assignments.Select(a => new { a.PersonId, a.StartsAt, a.DueAt, a.NominalStartsAt, a.SchedulingPolicyId }) })));

    private static OperationalAssignment[] OccasionAssignments(OperationalTask task, Guid planId, Guid occasionId, OperationalSchedulingPolicy? policy, string actorRole, OccasionInput input, CapacityLedger ledger, DateTimeOffset now, string? specialty)
    {
        var candidates = ledger.Workers.Where(w => EligibleOccasionalRecipient(actorRole, task, w)).ToArray();
        if (!input.Automatic && input.PersonIds.Any(id => candidates.All(w => w.Id != id))) throw new DirectionPlanException("OCCASION_RECIPIENT_INVALID", 422);
        var assignments = new List<OperationalAssignment>();
        foreach (var recipient in input.Automatic ? new Guid?[] { null } : input.PersonIds.Select(id => (Guid?)id))
        {
            var assignment = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = planId, OccasionId = occasionId,
                TaskId = task.Id, TaskRevision = task.Revision, DefinitionSnapshotJson = AssignmentDefinition.Capture(task), TaskCode = task.TaskCode, Name = input.Name, Description = task.Description, Requirement = task.Requirement, ChecklistJson = task.ChecklistJson,
                AssignedRole = task.AssignedRole, SpecializationId = task.SpecializationId, SpecializationName = specialty,
                StartsAt = input.StartsAt, DueAt = input.DueAt, LoadMinutes = task.LoadMinutes,
                MinimumPhotos = task.MinimumPhotos, MaximumPhotos = task.MaximumPhotos, MaximumPhotoBytes = task.MaximumPhotoBytes,
                PeriodType = "OCASIONAL", Repetitions = 1 };
            if (policy != null) OperationalAgenda.Capture(assignment, policy, task.OperationalCriticality, input.StartsAt);
            var nominalDue = assignment.DueAt;
            var options = new List<(CapacityWorker Worker, SchedulingPlacement Placement)>();
            foreach (var candidate in candidates.Where(w => recipient == null || w.Id == recipient))
            {
                assignment.StartsAt = input.StartsAt; assignment.DueAt = nominalDue;
                var placement = policy == null ? new SchedulingPlacement(true, assignment.StartsAt, assignment.DueAt, "LEGACY_CONTRACT") : OperationalAgenda.Find(ledger, candidate.Id, assignment, input.SchedulingNotBefore ?? now);
                if (!placement.Eligible) continue;
                OperationalAgenda.Apply(assignment, placement);
                if (ledger.CanAssign(candidate.Id, assignment, now)) options.Add((candidate, placement));
            }
            var day = WeekContract.LocalToday(input.StartsAt);
            var selected = options.OrderBy(o => o.Placement.StartsAt).ThenBy(o => ledger.Occupancy(o.Worker.Id, day, day)).ThenBy(o => o.Worker.Code, StringComparer.Ordinal).FirstOrDefault();
            if (selected.Worker == null) throw new DirectionPlanException("PLAN_NO_AVAILABLE_WORKER", 422);
            OperationalAgenda.Apply(assignment, selected.Placement);
            assignment.PersonId = selected.Worker.Id; assignment.PersonName = selected.Worker.Name; assignment.AssignedRole = selected.Worker.Role;
            assignment.PreviousLoadMinutes = ledger.Tasks.Where(a => a.PersonId == assignment.PersonId).Sum(a => (long)a.RemainingMinutes) + ledger.Legacy.GetValueOrDefault(assignment.PersonId);
            ledger.CaptureTime(assignment, now); assignments.Add(assignment);
            ledger = ledger.WithAdded(assignment);
        }
        return assignments.ToArray();
    }
}
