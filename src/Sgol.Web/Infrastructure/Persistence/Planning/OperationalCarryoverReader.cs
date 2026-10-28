using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence.Reporting;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record CarryoverTiming(DateTimeOffset? ReferenceDue, DateTimeOffset? RecognizedDue, int ApprovedCount, bool Pending, string References, int RefreshCount = 0)
{
    public string Label(DateTimeOffset due, DateTimeOffset? delivered) => delivered is {} at
        ? RefreshCount > 0 && at > (ReferenceDue ?? due)
            ? "Entrega posterior al plazo original · " + (at <= due ? "dentro del plazo actualizado" : "fuera del plazo actualizado")
            : at <= due ? "A tiempo" : ApprovedCount > 0
            ? "Entrega tardía con arrastre autorizado · " + (at <= (RecognizedDue ?? due) ? "dentro del plazo reconocido" : "fuera del plazo reconocido")
            : "Entrega fuera de plazo"
        : ApprovedCount > 0 ? "Cierre con arrastre autorizado" : Pending ? "Arrastre solicitado, pendiente de autorización" : RefreshCount > 0 ? "Plazo actualizado; se conserva el vencimiento original" : "Sin arrastre";
}
// Scoped cache: one metadata query for all cards, never evidence bytes or recalculation of deadlines.
public sealed class OperationalCarryoverReader(SgolDbContext db)
{
    private Task<OperationalCarryoverRequest[]>? requests;
    private Task<Dictionary<Guid, HashSet<Guid>>>? restorations;
    private Task<Dictionary<Guid, DateTimeOffset[]>>? refreshes;
    private async Task<Dictionary<Guid, DateTimeOffset[]>> Refreshes()
    {
        var events = await db.AuditEvents.AsNoTracking().Where(e => e.Action == "OPERATIONAL_ASSIGNMENT_REFRESHED" && e.BeforeData != null).OrderBy(e => e.OccurredAt)
            .Select(e => new { e.ResourceId, e.BeforeData, e.AfterData }).ToArrayAsync();
        return events.Where(e => e.BeforeData!.RootElement.GetProperty("DueAt").GetDateTimeOffset() != e.AfterData!.RootElement.GetProperty("Assignment").GetProperty("DueAt").GetDateTimeOffset())
            .GroupBy(e => e.ResourceId!.Value).ToDictionary(g => g.Key, g => g.Select(e => e.BeforeData!.RootElement.GetProperty("DueAt").GetDateTimeOffset()).ToArray());
    }
    private async Task<Dictionary<Guid, HashSet<Guid>>> Restorations()
    {
        var events = await db.AuditEvents.AsNoTracking().Where(e => e.BranchId == Sgol.Organization.Contracts.BranchScope.LorettaId && e.Action == OperationalCarryoverService.RestorationAction)
            .Select(e => new { e.ResourceId, e.AfterData }).ToArrayAsync();
        return events.GroupBy(e => e.ResourceId!.Value).ToDictionary(g => g.Key, g => g.Select(e => e.AfterData!.RootElement.GetProperty("RequestId").GetGuid()).ToHashSet());
    }
    public async Task<bool> IsRestoredAsync(Guid request, Guid assignment)
    {
        restorations ??= Restorations();
        return (await restorations).TryGetValue(assignment, out var ids) && ids.Contains(request);
    }
    public async Task<CarryoverTiming> ForAsync(Guid id)
    {
        requests ??= db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r => r.BranchId == Sgol.Organization.Contracts.BranchScope.LorettaId && r.State != "RECHAZADA").OrderBy(r => r.SubmittedAt).ToArrayAsync();
        var matching = (await requests).Where(r => r.Items().Any(i => i.AssignmentId == id)).ToArray();
        restorations ??= Restorations();
        var reversed = (await restorations).GetValueOrDefault(id) ?? [];
        var approved = matching.Where(r => r.State == "APROBADA" && !reversed.Contains(r.Id) && !r.Results().Any(x => x.AssignmentId == id && x.Kind == "ENTREGADA_SIN_CAMBIOS")).ToArray();
        var results = approved.SelectMany(r => r.Results()).Where(r => r.AssignmentId == id).ToArray();
        refreshes ??= Refreshes();
        var updated = (await refreshes).GetValueOrDefault(id) ?? [];
        var carryoverReference = approved.FirstOrDefault()?.Items().Single(i => i.AssignmentId == id).Due;
        var reference = updated.Length > 0 ? new[] { (DateTimeOffset?)updated[0], carryoverReference }.Where(d => d != null).Min() : carryoverReference;
        return new(reference,
            results.Length > 0 ? results.Max(r => r.RecognizedDue) : null, approved.Length, matching.Any(r => r.State == "PENDIENTE"), string.Join(", ", approved.Select(r => r.Id)), updated.Length);
    }
}

