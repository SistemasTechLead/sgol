using Microsoft.AspNetCore.WebUtilities;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Presentation.Navigation;

public sealed record SectionNavigationLink(string Label, string Href, bool Active, string? Icon = null);
public sealed record SectionNavigationModel(string Label, IReadOnlyList<SectionNavigationLink> Links);

public static class SectionNavigation
{
    public static SectionNavigationModel Create(string section, HttpRequest request, SessionSnapshot? session)
    {
        if (session is null) return new("", []);
        var path = request.Path;
        bool Flag(string key) => bool.TryParse(request.Query[key], out var value) && value;
        var director = session.RoleCode == CanonicalRole.Direction;
        var links = new List<SectionNavigationLink>();
        string label;
        switch (section)
        {
            case "my-work":
                label = "Apartados de Mi trabajo";
                links.Add(new("Mis tareas", "/mi-trabajo", !path.StartsWithSegments("/mi-trabajo/procesos") && !path.StartsWithSegments("/mi-trabajo/arrastre"), "briefcase"));
                links.Add(new("Mis procesos", "/mi-trabajo/procesos", path.StartsWithSegments("/mi-trabajo/procesos"), "list"));
                links.Add(new("Petición de arrastre", "/mi-trabajo/arrastre", path.StartsWithSegments("/mi-trabajo/arrastre"), "clock"));
                break;
            case "tasks":
                label = "Apartados de tareas";
                var assigning = path.StartsWithSegments("/asignar-tarea") || path.StartsWithSegments("/tareas/asignar");
                var processes = path.StartsWithSegments("/tareas/procesos");
                var canBrowse = session.RoleCode is CanonicalRole.Direction or CanonicalRole.Administration;
                if (canBrowse)
                {
                    links.Add(new("Tareas", "/tareas", !assigning && !processes && !Flag("create") && !Flag("AdjustClosure"), "checklist"));
                    if (director) links.Add(new("Crear tarea", "/tareas?create=true", !assigning && !processes && Flag("create")));
                    links.Add(new("Procesos", "/tareas/procesos", processes, "list"));
                }
                if (session.RoleCode is CanonicalRole.Direction or CanonicalRole.Administration or CanonicalRole.Subcoordination)
                    links.Add(new("Asignar tarea", "/asignar-tarea", assigning, "users"));
                if (director) links.Add(new("Porcentaje para el cierre", "/tareas?AdjustClosure=true", !assigning && !processes && Flag("AdjustClosure"), "settings"));
                break;
            case "planning":
                label = "Apartados de planificación";
                var mode = request.Query["mode"].ToString();
                var processPlan = path.StartsWithSegments("/planificacion/procesos");
                var viewing = session.Permissions.Contains(WeekAuthorization.View);
                links.Add(new("Planes", "/planificacion", !processPlan && mode is not ("create" or "operations"), "calendar"));
                if (director && viewing) links.Add(new("Crear nuevo plan", "/planificacion?mode=create", !processPlan && mode == "create"));
                if (viewing && session.RoleCode is CanonicalRole.Direction or CanonicalRole.Subcoordination)
                    links.Add(new("Operaciones semanales", "/planificacion?mode=operations", !processPlan && mode == "operations", "checklist"));
                if (director && viewing || processPlan)
                    links.Add(new("Plan de proceso", director ? "/planificacion/procesos" : request.Path + request.QueryString, processPlan, "list"));
                break;
            case "people":
                label = "Apartados de Personas y accesos";
                if (!director) break;
                var specialties = path.StartsWithSegments("/personas-y-accesos/especializaciones");
                links.Add(new("Personas y accesos", "/personas-y-accesos", !specialties && !Flag("create"), "users"));
                links.Add(new("Crear usuario", "/personas-y-accesos?create=true", !specialties && Flag("create")));
                links.Add(new("Especializaciones", "/personas-y-accesos/especializaciones", specialties, "list"));
                break;
            case "supervision":
                label = "Vista de supervisión";
                if (session.RoleCode == CanonicalRole.SalesFloor) break;
                var validation = Flag("Validation");
                var workload = path.StartsWithSegments("/supervision/carga");
                string View(bool value) => QueryHelpers.AddQueryString("/validaciones", request.Query
                    .Where(q => !new[] { "task", "handler", "Validation" }.Contains(q.Key, StringComparer.OrdinalIgnoreCase))
                    .ToDictionary(q => q.Key, q => (string?)q.Value.ToString())
                    .Append(new KeyValuePair<string, string?>("Validation", value.ToString())).ToDictionary());
                links.Add(new("Supervisión", View(false), !validation && !workload, "eye"));
                links.Add(new("Validación", View(true), validation, "check"));
                links.Add(new("Carga y reasignación", "/supervision/carga", workload, "users"));
                break;
            case "board":
                label = "Vistas del tablero";
                var weekly = path.StartsWithSegments("/tablero/rol-de-tareas");
                links.Add(new("Tablero", "/tablero", !weekly, "calendar"));
                links.Add(new("Rol de tareas", "/tablero/rol-de-tareas", weekly, "list"));
                break;
            default: return new("", []);
        }
        return new(label, links);
    }
}
