namespace Sgol.Web.Infrastructure.Persistence.Reporting;

public sealed record ReportPerson(Guid Id, string Name, string Roles, int Total, int Delivered,
    int Completed, int Pending, int Overdue, int Review, long Load, decimal? Capacity)
{
    public decimal? Fulfillment => Total == 0 ? null : Math.Round(100m * Completed / Total, 1);
    public decimal? Progress => Total == 0 ? null : Math.Round(100m * Delivered / Total, 1);
    public decimal? Occupancy => Capacity > 0 ? Math.Round(100m * Load / Capacity.Value, 1) : null;
    public bool OverCapacity => Capacity.HasValue && Load > Capacity.Value;
    public string State => OverCapacity ? "Sobre capacidad" : Overdue > 0 ? "Con vencidas" :
        Review > 0 ? "Por revisar" : Load > 0 && Capacity == null ? "Capacidad no disponible" :
        Load == 0 ? "Sin carga pendiente" : "En rango";
    public string Tone => OverCapacity ? "danger" : Overdue > 0 || Review > 0 ? "warning" : "info";

    public static ReportPerson[] Summarize(IEnumerable<ReportRow> rows, IReadOnlyDictionary<Guid, decimal> capacity) =>
        rows.GroupBy(r => r.PersonId).Select(g => {
            var current = g.Where(r => r.Status != "REASIGNADA").ToArray();
            var pending = current.Where(r => !r.Terminal && r.Delivered == null).ToArray();
            return new ReportPerson(g.Key, g.First().Person,
                string.Join(" · ", g.Select(r => ReportCatalog.RoleName(r.Role)).Distinct().Order()),
                current.Length, current.Count(r => r.Delivered != null), current.Count(r => r.Status == "CONCLUIDA"),
                pending.Length, pending.Count(r => r.Status == "VENCIDA"), current.Count(r => r.Status == "PENDIENTE_REVISION"),
                pending.Sum(r => (long)r.RemainingMinutes), capacity.TryGetValue(g.Key, out var value) ? value : null);
        }).OrderByDescending(p => p.OverCapacity).ThenByDescending(p => p.Overdue).ThenBy(p => p.Name).ThenBy(p => p.Id).ToArray();
}
