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
    private Task<Dictionary<Guid, CarryoverTiming>>? timings;
    private Task<Dictionary<Guid, HashSet<Guid>>>? restorations;
    private Guid[]? scope;
    public Task PrepareAsync(IEnumerable<Guid> ids, CancellationToken token)
    {
        scope = ids.Distinct().ToArray();
        restorations = null;
        timings = BuildAsync(token);
        return timings;
    }
    private async Task<Dictionary<Guid, HashSet<Guid>>> Restorations(CancellationToken token)
    {
        var query = db.AuditEvents.AsNoTracking().Where(e => e.BranchId == Sgol.Organization.Contracts.BranchScope.LorettaId && e.Action == OperationalCarryoverService.RestorationAction);
        if (scope != null) query = query.Where(e => e.ResourceId != null && scope.Contains(e.ResourceId.Value));
        var events = await query.Select(e => new { e.ResourceId, e.AfterData }).ToArrayAsync(token);
        return events.GroupBy(e => e.ResourceId!.Value).ToDictionary(g => g.Key, g => g.Select(e => e.AfterData!.RootElement.GetProperty("RequestId").GetGuid()).ToHashSet());
    }
    public async Task<bool> IsRestoredAsync(Guid request, Guid assignment, CancellationToken token = default)
    {
        restorations ??= Restorations(token);
        return (await restorations).TryGetValue(assignment, out var ids) && ids.Contains(request);
    }
    private async Task<Dictionary<Guid, CarryoverTiming>> BuildAsync(CancellationToken token)
    {
        using var measurement = Sgol.Web.Infrastructure.Http.PerformanceWork.Measure("carryover.index");
        if (scope is { Length: 0 }) return [];
        var queryRequests = scope == null ? db.Set<OperationalCarryoverRequest>().AsNoTracking() : db.Set<OperationalCarryoverRequest>().FromSqlInterpolated($"""
            SELECT * FROM operational_carryover_request r
            WHERE EXISTS (SELECT 1 FROM jsonb_array_elements(r."SnapshotJson") i WHERE (i->>'AssignmentId')::uuid = ANY({scope}))
            """).AsNoTracking();
        var requests = await queryRequests
            .Where(r => r.BranchId == Sgol.Organization.Contracts.BranchScope.LorettaId && r.State != "RECHAZADA")
            .OrderBy(r => r.SubmittedAt).ThenBy(r => r.Id).ToArrayAsync(token);
        restorations ??= Restorations(token);
        var reversed = await restorations;
        var query = db.AuditEvents.AsNoTracking().Where(e => e.BranchId == Sgol.Organization.Contracts.BranchScope.LorettaId && e.Action == "OPERATIONAL_ASSIGNMENT_REFRESHED" && e.BeforeData != null);
        if (scope != null) query = query.Where(e => e.ResourceId != null && scope.Contains(e.ResourceId.Value));
        var events = await query.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).Select(e => new { e.ResourceId, e.BeforeData, e.AfterData }).ToArrayAsync(token);
        var refreshes = events.Where(e => e.BeforeData!.RootElement.GetProperty("DueAt").GetDateTimeOffset() != e.AfterData!.RootElement.GetProperty("Assignment").GetProperty("DueAt").GetDateTimeOffset())
            .GroupBy(e => e.ResourceId!.Value).ToDictionary(g => g.Key, g => g.Select(e => e.BeforeData!.RootElement.GetProperty("DueAt").GetDateTimeOffset()).ToArray());
        var requested = scope?.ToHashSet();
        var entries = new Dictionary<Guid, List<(OperationalCarryoverRequest Request, CarryoverItem Item, CarryoverResult[] Results)>>();
        foreach (var request in requests)
        {
            token.ThrowIfCancellationRequested();
            var results = request.Results().GroupBy(r => r.AssignmentId).ToDictionary(g => g.Key, g => g.ToArray());
            foreach (var item in request.Items())
            {
                if (requested != null && !requested.Contains(item.AssignmentId)) continue;
                if (!entries.TryGetValue(item.AssignmentId, out var list)) entries[item.AssignmentId] = list = [];
                list.Add((request, item, results.GetValueOrDefault(item.AssignmentId) ?? []));
            }
        }
        var output = new Dictionary<Guid, CarryoverTiming>();
        foreach (var id in entries.Keys.Concat(refreshes.Keys).Distinct())
        {
            token.ThrowIfCancellationRequested();
            var matching = entries.GetValueOrDefault(id) ?? [];
            var approved = matching.Where(x => x.Request.State == "APROBADA" && !(reversed.GetValueOrDefault(id)?.Contains(x.Request.Id) ?? false) && !x.Results.Any(r => r.Kind == "ENTREGADA_SIN_CAMBIOS")).ToArray();
            var results = approved.SelectMany(x => x.Results).ToArray();
            var updated = refreshes.GetValueOrDefault(id) ?? [];
            DateTimeOffset? reference = approved.Length > 0 ? approved[0].Item.Due : null;
            if (updated.Length > 0 && (reference == null || updated[0] < reference)) reference = updated[0];
            output[id] = new(reference, results.Length > 0 ? results.Max(r => r.RecognizedDue) : null,
                approved.Length, matching.Any(x => x.Request.State == "PENDIENTE"), string.Join(", ", approved.Select(x => x.Request.Id)), updated.Length);
        }
        return output;
    }
    public async Task<CarryoverTiming> ForAsync(Guid id, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (scope != null && !scope.Contains(id)) { scope = scope.Append(id).ToArray(); restorations = null; timings = null; }
        timings ??= BuildAsync(token);
        return (await timings).GetValueOrDefault(id) ?? new(null, null, 0, false, "");
    }
}
