using System.Globalization;

namespace Sgol.Web.Infrastructure.Persistence.Reporting;

public static class ReportAnalysis
{
    public static (DateOnly From, DateOnly To) Range(ReportFilter f, DateOnly today)
    {
        if (!ReportCatalog.Definitions.Any(d => d.Id == f.Kind) ||
            f.DateBasis is not ("start" or "due" or "delivered" or "reviewed") ||
            f.Group is not ("person" or "plan" or "role" or "task") ||
            f.Order is not ("due" or "person" or "progress" or "delay") ||
            f.Columns.Split(',').Any(c => !ReportCatalog.Columns.ContainsKey(c)) ||
            f.Columns.Split(',').Distinct().Count() != f.Columns.Split(',').Length ||
            f.ProgressMin is < 0 or > 100 || f.ProgressMax is < 0 or > 100 ||
            f.ProgressMin > f.ProgressMax || f.Minutes is < 1 or > 4800 ||
            f.Hours is < 1 or > 8760 || f.Repetitions is < 1 or > 100 ||
            f.Carryover is not ("" or "approved" or "covered" or "pending" or "late") ||
            f.Status.Length > 0 && !ReportCatalog.Statuses.ContainsKey(f.Status))
            throw new ArgumentException("Revisa los parámetros del reporte.");
        DateOnly from, to;
        switch (f.Range)
        {
            case "week": from = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); to = from.AddDays(6); break;
            case "last-week": to = today.AddDays(-(((int)today.DayOfWeek + 6) % 7) - 1); from = to.AddDays(-6); break;
            case "month": from = new(today.Year, today.Month, 1); to = from.AddMonths(1).AddDays(-1); break;
            case "last-month": from = new DateOnly(today.Year, today.Month, 1).AddMonths(-1); to = from.AddMonths(1).AddDays(-1); break;
            case "quarter": from = new(today.Year, (today.Month - 1) / 3 * 3 + 1, 1); to = from.AddMonths(3).AddDays(-1); break;
            case "semester": from = new(today.Year, (today.Month - 1) / 6 * 6 + 1, 1); to = from.AddMonths(6).AddDays(-1); break;
            case "year": from = new(today.Year, 1, 1); to = new(today.Year, 12, 31); break;
            case "custom": from = f.From ?? throw new ArgumentException("Selecciona la fecha inicial."); to = f.To ?? throw new ArgumentException("Selecciona la fecha final."); break;
            default: throw new ArgumentException("Selecciona un periodo válido.");
        }
        if (to < from || to.DayNumber - from.DayNumber > 365 || from.Year < 2 || to.Year > 9998)
            throw new ArgumentException("El periodo debe tener entre 1 y 366 días.");
        return (from, to);
    }

    public static ReportRow[] Filter(IEnumerable<ReportRow> rows, ReportFilter f, DateTimeOffset now)
    {
        rows = rows.Where(r => f.Carryover switch { "approved" => r.Carryover.ApprovedCount > 0, "pending" => r.Carryover.Pending, "covered" => r.Delivered > r.TimelinessDue && r.Carryover.ApprovedCount > 0 && r.Delivered <= r.Carryover.RecognizedDue, "late" => r.Delivered > r.TimelinessDue, _ => true });
        var selected = rows.Where(r =>
            (f.Person.Length == 0 || r.PersonId.ToString() == f.Person) &&
            (f.Role.Length == 0 || r.Role == f.Role) && (f.Plan.Length == 0 || r.PlanId.ToString() == f.Plan) &&
            (f.Area.Length == 0 || r.Area == f.Area) && (f.Task.Length == 0 || r.Code == f.Task) &&
            (f.Specialization.Length == 0 || r.Specialization == f.Specialization) &&
            (f.Status.Length == 0 || r.Status == f.Status) &&
            (f.Requirement.Length == 0 || r.Requirement == f.Requirement) &&
            (f.Schedule.Length == 0 || r.Schedule == f.Schedule) && (f.Frequency.Length == 0 || r.Frequency == f.Frequency) &&
            (f.Repetitions == null || r.Repetitions == f.Repetitions) && (f.Hours == null || r.Hours == f.Hours) &&
            (f.Minutes == null || r.Minutes == f.Minutes) &&
            (f.ProgressMin == null || r.Goal > 0 && r.Progress >= f.ProgressMin) &&
            (f.ProgressMax == null || r.Goal > 0 && r.Progress <= f.ProgressMax));
        selected = f.Kind switch {
            "overdue" => selected.Where(r => r.Status == "VENCIDA"),
            "goals" => selected.Where(r => r.Goal > 0 && r.Status != "REASIGNADA"),
            "review" => selected.Where(r => r.Status == "PENDIENTE_REVISION"),
            "results" => selected.Where(r => r.Terminal),
            "load" => selected.Where(r => !r.Terminal && r.Delivered == null), _ => selected };
        return (f.Order switch {
            "person" => selected.OrderBy(r => r.Person).ThenBy(r => r.Due),
            "progress" => selected.OrderBy(r => r.Progress).ThenBy(r => r.Due),
            "delay" => selected.OrderBy(r => r.Status == "PENDIENTE_REVISION" ? r.Delivered : r.Due),
            _ => selected.OrderBy(r => r.Due) }).ThenBy(r => r.Id).ToArray();
    }

    public static ReportMetric[] Metrics(ReportRow[] rows, string kind, DateTimeOffset now)
    {
        var active = rows.Where(r => r.Status != "REASIGNADA").ToArray();
        var done = active.Count(r => r.Status == "CONCLUIDA");
        var total = active.Length;
        var percent = total == 0 ? "Sin datos" : $"{Math.Round(100m * done / total, 1):0.#} %";
        if (kind == "goals") return [new("Metas", total.ToString(), "Tareas con meta definida", "info"),
            new("Avance promedio", total == 0 ? "Sin datos" : $"{active.Average(r => r.Progress):0.#} %", "Promedio por tarea; no suma unidades distintas", "success"),
            new("Meta alcanzada", active.Count(r => r.Units >= r.Goal).ToString(), "Todavía puede requerir revisión", "info"),
            new("Metas vencidas", active.Count(r => r.Status == "VENCIDA").ToString(), "Pendientes fuera de plazo", "danger", "VENCIDA")];
        if (kind == "load") return [new("Asignaciones pendientes", total.ToString(), "Sin entrega ni revisión final", "info"),
            new("Carga restante", active.Sum(r => (long)r.RemainingMinutes) + " min", "Estimación según avance registrado", "warning"),
            new("Responsables", active.Select(r => r.PersonId).Distinct().Count().ToString(), "Con trabajo en este resultado", "info"),
            new("Vencidas", active.Count(r => r.Status == "VENCIDA").ToString(), "Pendientes fuera de plazo", "danger", "VENCIDA")];
        if (kind == "review") return [new("Por revisar", total.ToString(), "Entregas pendientes de decisión", "warning"),
            new("Espera máxima", total == 0 ? "Sin datos" : $"{active.Max(r => (now - r.Delivered!.Value).TotalHours):0.#} h", "Desde la entrega registrada", "warning"),
            new("Entregas fuera de plazo", active.Count(r => r.Delivered > r.TimelinessDue).ToString(), "Entrega posterior al vencimiento", "danger"),
            new("Responsables", active.Select(r => r.PersonId).Distinct().Count().ToString(), "Con entregas por revisar", "info")];
        if (kind == "results") return [new("Concluidas", done.ToString(), "Resultado satisfactorio", "success", "CONCLUIDA"),
            new("No satisfechas", active.Count(r => r.Status == "NO_SATISFECHA").ToString(), "Resultado no satisfactorio", "danger", "NO_SATISFECHA"),
            new("Reasignaciones", rows.Count(r => r.Status == "REASIGNADA").ToString(), "Intentos sustituidos; separados del total", "info", "REASIGNADA"),
            new("Resultados finales", total.ToString(), "Concluidas y no satisfechas", "info")];
        return [new("Tareas", total.ToString(), $"{rows.Count(r => r.Status == "REASIGNADA")} intentos reasignados, separados del total", "info"),
            new("Cumplimiento", percent, $"{done} de {total} con resultado satisfactorio", "success", "CONCLUIDA"),
            new("Vencidas", active.Count(r => r.Status == "VENCIDA").ToString(), "Pendientes fuera de plazo", "danger", "VENCIDA"),
            new("Por revisar", active.Count(r => r.Status == "PENDIENTE_REVISION").ToString(), "Entregadas, pendientes de aprobación", "warning", "PENDIENTE_REVISION")];
    }

    public static ReportChart[] Charts(ReportRow[] rows, ReportFilter f, DateTimeOffset now,
        IReadOnlyDictionary<Guid, decimal> capacity)
    {
        var state = new ReportChart("Distribución de estados", "asignaciones", "info", ReportCatalog.Statuses
            .Select(s => new ReportPoint(s.Value, rows.Count(r => r.Status == s.Key), $"{rows.Count(r => r.Status == s.Key)} asignaciones", s.Key)).ToArray());
        var groups = rows.GroupBy(r => f.Group switch { "plan" => r.PlanId.ToString(), "role" => r.Role, "task" => r.Code, _ => r.PersonId.ToString() });
        var grouped = groups.Select(g => {
            var current = g.Where(r => r.Status != "REASIGNADA").ToArray();
            var value = f.Kind switch {
                "compliance" => current.Length == 0 ? 0 : 100m * current.Count(r => r.Status == "CONCLUIDA") / current.Length,
                "load" => current.Sum(r => (decimal)r.RemainingMinutes),
                "goals" => current.Length == 0 ? 0 : current.Average(r => r.Progress),
                _ => current.Length };
            decimal? cap = f.Kind == "load" ? g.Select(r => r.PersonId).Distinct().Sum(id => capacity.GetValueOrDefault(id)) : null;
            var caption = f.Kind switch {
                "compliance" => $"{current.Count(r => r.Status == "CONCLUIDA")} de {current.Length} · {value:0.#} %",
                "load" => $"{value:0} min pendientes / {cap:0} min de capacidad",
                "goals" => $"{value:0.#} % de avance promedio", _ => $"{value:0} tareas" };
            var segments = f.Kind == "compliance" ? ReportCatalog.Statuses.Where(s => s.Key != "REASIGNADA")
                .Select(s => new ReportSegment(s.Value, current.Length == 0 ? 0 : 100m * current.Count(r => r.Status == s.Key) / current.Length,
                    ReportCatalog.Tone(s.Key))).ToArray() : null;
            if (segments != null) caption += " · " + string.Join(" · ", segments.Where(s => s.Value > 0).Select(s => $"{s.Label}: {s.Value:0.#} %"));
            var first = g.First();
            var label = f.Group switch { "plan" => first.Plan, "role" => ReportCatalog.RoleName(first.Role),
                "task" => $"{first.Task} · {first.Code}", _ => first.Person };
            return new ReportPoint(label, Math.Round(value, 1), caption, g.Key, cap, segments);
        }).OrderByDescending(p => p.Value).ThenBy(p => p.Label).ToArray();
        var comparison = new ReportChart(f.Kind switch {
            "compliance" => "Cumplimiento por grupo", "load" => "Carga pendiente y capacidad",
            "goals" => "Avance promedio por grupo", _ => "Tareas por grupo" },
            f.Kind is "compliance" or "goals" ? "%" : f.Kind == "load" ? "minutos estimados" : "tareas", "info", grouped);
        if (f.Kind == "overdue" || f.Kind == "review") {
            var labels = new[] { "Menos de 1 día", "De 1 a 3 días", "De 3 a 7 días", "7 días o más" };
            var ages = rows.Select(r => (now - (f.Kind == "review" ? r.Delivered!.Value : r.Due)).TotalDays).ToArray();
            state = new(f.Kind == "review" ? "Antigüedad de la espera" : "Antigüedad del atraso", "tareas", "warning",
                labels.Select((label, i) => { var count = ages.Count(d => i switch { 0 => d < 1, 1 => d >= 1 && d < 3, 2 => d >= 3 && d < 7, _ => d >= 7 }); return new ReportPoint(label, count, $"{count} tareas"); }).ToArray());
        }
        if (f.Kind == "goals") state = new("Progreso de cada meta", "%", "success", rows.Select(r =>
            new ReportPoint($"{r.Task} · {r.Person}", r.Progress, $"{r.Units} / {r.Goal} {r.Unit} · {r.Progress:0.#} %", r.Id.ToString())).ToArray());
        if (f.Kind == "overview") comparison = new("Entregas registradas por día", "entregas", "success", rows
            .Where(r => r.Delivered != null).GroupBy(r => ReportCatalog.Day(r.Delivered!.Value)).OrderBy(g => g.Key)
            .Select(g => new ReportPoint(g.Key.ToString("dd/MM/yyyy"), g.Count(), $"{g.Count()} entregas")).ToArray());
        return [state, comparison];
    }
}


