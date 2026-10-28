using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalCarryoverService
{
    public const string RestorationAction = "OPERATIONAL_POSTPONEMENT_RESTORED";

    // Explicit compensating correction. The approved request and its decision remain immutable.
    public async Task RestorePostponementAsync(Guid actor, Guid requestId, Guid[] selected, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 4000 || selected.Length == 0 || selected.Distinct().Count() != selected.Length) throw Error("ARRASTRE_DATOS_INVALIDOS", 422);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await OperationalTaskService.LockAsync(db, ct);
        if (await supervision.AuthorizeAsync(actor, ct) != CanonicalRole.Direction) throw Error("ACCESO_DENEGADO", 403);
        var request = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == requestId && r.BranchId == BranchScope.LorettaId && r.Kind == "POSPONER" && r.State == "APROBADA", ct)
            ?? throw Error("ARRASTRE_CONFLICTO");
        var snapshot = request.Items().Where(i => selected.Contains(i.AssignmentId)).ToArray();
        if (snapshot.Length != selected.Length || snapshot.Any(i => !request.Results().Any(r => r.AssignmentId == i.AssignmentId && r.Kind == "POSPOSICION"))) throw Error("ARRASTRE_CONFLICTO");
        var reader = new OperationalCarryoverReader(db);
        var restored = new List<Guid>();
        foreach (var item in snapshot) if (await reader.IsRestoredAsync(requestId, item.AssignmentId)) restored.Add(item.AssignmentId);
        if (restored.Count == snapshot.Length) { await tx.CommitAsync(ct); return; }
        if (restored.Count != 0) throw Error("ARRASTRE_CONFLICTO");
        var rows = await db.OperationalAssignments.Where(a => selected.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var changes = new List<(OperationalAssignment Row, OperationalAssignment Before, OperationalAssignment Restored)>();
        foreach (var item in snapshot) {
            await supervision.AuthorizeAssignmentAsync(actor, item.AssignmentId, ct);
            if (!rows.TryGetValue(item.AssignmentId, out var row) || row.CompletedAt != null || row.ReviewedAt != null || row.ReviewStatus != item.ReviewStatus ||
                row.StartsAt != item.ProposedStart || row.DueAt != item.Proposed || row.Revision != item.Revision + 1) throw Error("ARRASTRE_CONFLICTO");
            using var structure = JsonDocument.Parse(item.Structure);
            using var original = JsonDocument.Parse(structure.RootElement.GetProperty("Original").GetString()!);
            var prior = PostponementAllocator.Copy(row);
            prior.StartsAt = item.Start; prior.DueAt = item.Due;
            prior.NominalStartsAt = structure.RootElement.GetProperty("NominalStartsAt").ValueKind == JsonValueKind.Null ? null : structure.RootElement.GetProperty("NominalStartsAt").GetDateTimeOffset();
            prior.TemporalDecisionJson = original.RootElement.GetProperty("TemporalDecisionJson").GetString();
            if (PostponementAllocator.Structure(prior) != item.Structure) throw Error("ARRASTRE_CONFLICTO");
            changes.Add((row, PostponementAllocator.Copy(row), prior));
        }
        foreach (var (row, before, prior) in changes) {
            row.StartsAt = prior.StartsAt; row.DueAt = prior.DueAt; row.NominalStartsAt = prior.NominalStartsAt;
            row.TemporalDecisionJson = prior.TemporalDecisionJson; row.Revision++;
            db.AuditEvents.Add(new AuditEvent { Id = Guid.CreateVersion7(), OccurredAt = clock.UtcNow, ActorUserId = actor, ActorType = "USER", Action = RestorationAction,
                ResourceType = "OPERATIONAL_ASSIGNMENT", ResourceId = row.Id, BranchId = BranchScope.LorettaId, CorrelationId = requestId, Outcome = "SUCCESS",
                BeforeData = JsonSerializer.SerializeToDocument(before), AfterData = JsonSerializer.SerializeToDocument(new { RequestId = requestId, Assignment = row, Reason = reason.Trim() }) });
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
