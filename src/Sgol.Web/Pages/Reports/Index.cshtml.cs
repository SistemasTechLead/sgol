using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Persistence.Reporting;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Reports;

public sealed class IndexModel(IRazorSessionState sessions, SgolDbContext db, IClock clock, OperationalCarryoverReader carryovers) : PageModel
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [BindProperty(SupportsGet = true)] public ReportFilter F { get; set; } = new();
    [BindProperty(SupportsGet = true)] public Guid? EditingTemplate { get; set; }
    [BindProperty(SupportsGet = true)] public string? AppliedTemplate { get; set; }
    [BindProperty] public string TemplateName { get; set; } = "";
    [BindProperty] public string Configuration { get; set; } = "";
    [BindProperty] public Guid? TemplateId { get; set; }
    [TempData] public string? Success { get; set; }
    public string? Error { get; private set; }
    public DateTimeOffset Cutoff { get; private set; }
    public DateOnly From { get; private set; }
    public DateOnly Through { get; private set; }
    public string Scope { get; private set; } = "";
    public ReportRow[] Options { get; private set; } = [];
    public ReportRow[] Rows { get; private set; } = [];
    public ReportMetric[] Metrics { get; private set; } = [];
    public ReportChart[] Charts { get; private set; } = [];
    public ReportTemplate[] Templates { get; private set; } = [];
    public string? EditingName => Templates.FirstOrDefault(t => t.Id == EditingTemplate)?.Name;
    public string[] SelectedColumns => F.Columns.Split(',');
    public ReportDefinition Definition => ReportCatalog.Definitions.FirstOrDefault(d => d.Id == F.Kind) ?? ReportCatalog.Definitions[0];
    public string FilterJson => JsonSerializer.Serialize(F, JsonOptions);
    public string ExportJson { get; private set; } = "{}";

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken token)
    {
        NoStore();
        var session = await sessions.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        if (!ModelState.IsValid) { Error = "Revisa las fechas y los valores numéricos de los filtros."; return Page(); }
        Cutoff = clock.UtcNow;
        try { (From, Through) = ReportAnalysis.Range(F, ReportCatalog.Day(Cutoff)); }
        catch (ArgumentException ex) { Error = ex.Message; return Page(); }
        var at = Cutoff;
        if (!await db.AppUsers.AnyAsync(u => u.Id == session.UserId && u.Status == AccountStatus.Active, token) ||
            !await db.EmploymentVersions.AnyAsync(e => e.PersonId == session.PersonId && e.BranchId == BranchScope.LorettaId &&
                e.Status == EmploymentStatus.Active && e.ValidFrom <= at && (e.ValidTo == null || e.ValidTo > at), token) ||
            !await db.RoleAssignmentVersions.AnyAsync(r => r.UserId == session.UserId && r.RoleCode == session.RoleCode &&
                r.BranchId == BranchScope.LorettaId && r.Status == RoleAssignmentStatus.Active &&
                r.ValidFrom <= at && (r.ValidTo == null || r.ValidTo > at), token)) return StatusCode(403);
        var allowed = session.Permissions.Contains("PER-SUPERVISION-VER") ? OperationalSupervisionService.RolesFor(session.RoleCode) : [];
        Scope = allowed.Length > 0 ? "Tus tareas y los niveles que puedes supervisar" : "Solo tus tareas";
        var scoped = from a in db.OperationalAssignments.AsNoTracking()
            join p in db.DirectionPreparedPlans.AsNoTracking() on a.PlanId equals p.Id
            join t in db.OperationalTasks.AsNoTracking() on a.TaskId equals t.Id
            where p.BranchId == BranchScope.LorettaId &&
                (a.PersonId == session.PersonId || allowed.Contains(a.AssignedRole ?? t.AssignedRole))
            select new { A = a, P = p, Role = a.AssignedRole ?? t.AssignedRole };
        var selections = await scoped.Select(x => new { x.A.PersonId, x.A.PersonName, x.A.PlanId, PlanName = x.P.Name,
            x.P.Area, x.A.TaskCode, x.A.Name, x.Role, x.A.SpecializationName }).Distinct().ToArrayAsync(token);
        Options = selections.Select(x => new ReportRow(Guid.Empty, x.PersonId, x.PlanId, x.Name, x.TaskCode,
            x.PersonName, x.Role, x.PlanName, x.Area, "", "", "", "", 0, 0, 0, 0, default, default, null, null, 0, 0, "", x.SpecializationName ?? "", "", null)).ToArray();
        var start = OperationalSchedule.Instant(From); var end = OperationalSchedule.Instant(Through.AddDays(1));
        scoped = F.DateBasis switch {
            "due" => scoped.Where(x => x.A.DueAt >= start && x.A.DueAt < end),
            "delivered" => scoped.Where(x => x.A.CompletedAt >= start && x.A.CompletedAt < end),
            "reviewed" => scoped.Where(x => x.A.ReviewedAt >= start && x.A.ReviewedAt < end),
            _ => scoped.Where(x => x.A.StartsAt >= start && x.A.StartsAt < end) };
        // Scalar projection intentionally excludes private evidence bytes.
        var raw = await scoped.Select(x => new ReportRow(x.A.Id, x.A.PersonId, x.A.PlanId, x.A.Name, x.A.TaskCode,
            x.A.PersonName, x.Role, x.P.Name, x.P.Area, x.A.ReviewStatus ?? "", x.A.Requirement, x.A.ScheduleMode,
            x.A.PeriodType, x.A.Repetitions, 0, x.A.LoadMinutes, 0, x.A.StartsAt, x.A.DueAt,
            x.A.CompletedAt, x.A.ReviewedAt, x.A.GoalUnits, x.A.ProgressUnits, x.A.ProgressUnit,
            x.A.SpecializationName ?? "", x.A.ReviewComment ?? "", x.A.ReassignedFromId) {
                TransferRemainingMinutes = x.A.TransferRemainingMinutes, TransferProgressUnits = x.A.TransferProgressUnits
            }).ToArrayAsync(token);
        var transferredIds = raw.Where(r => r.TransferRemainingMinutes != null).Select(r => r.Id).ToArray();
        var contributions = await (from e in db.OperationalProgressEntries.AsNoTracking()
            join u in db.AppUsers.AsNoTracking() on e.ActorId equals u.Id
            join p in db.People.AsNoTracking() on u.PersonId equals p.Id
            where transferredIds.Contains(e.AssignmentId)
            group e by new { e.AssignmentId, p.Id, p.DisplayName } into g
            select new { g.Key.AssignmentId, g.Key.DisplayName, Units = g.Sum(x => x.Units) }).ToArrayAsync(token);
        raw = raw.Select(r => r with { ProgressAuthors = string.Join(" · ", contributions.Where(c => c.AssignmentId == r.Id).OrderBy(c => c.DisplayName).Select(c => $"{c.DisplayName}: {c.Units}")) }).ToArray();
        for (var i = 0; i < raw.Length; i++) raw[i] = raw[i] with { Carryover = await carryovers.ForAsync(raw[i].Id) };
        raw = raw.Select(r => r with {
            Status = r.Status is "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA" ? r.Status : r.Delivered != null ? "PENDIENTE_REVISION" : r.Start > Cutoff ? "PROGRAMADA" : r.Due < Cutoff ? "VENCIDA" : "ASIGNADA",
            Hours = (decimal)(r.Due - r.Start).TotalHours,
            RemainingMinutes = r.Delivered != null ? 0 : r.TransferRemainingMinutes is {} pending
                ? r.Goal > r.TransferProgressUnits ? (int)Math.Ceiling(pending * Math.Max(0m, (decimal)(r.Goal - r.Units) / (r.Goal - r.TransferProgressUnits))) : pending
                : r.Goal > 0 ? (int)Math.Ceiling(r.Minutes * (1m - Math.Min(1m, (decimal)r.Units / r.Goal))) : r.Minutes }).ToArray();
        Rows = ReportAnalysis.Filter(raw, F, Cutoff);
        var capacity = new Dictionary<Guid, decimal>();
        if (F.Kind == "load") {
            var ledger = await OperationalCapacityService.LedgerAsync(db, Cutoff, token);
            foreach (var person in Rows.Select(r => r.PersonId).Distinct())
                if (ledger.Workers.Any(w => w.Id == person)) capacity[person] = ledger.Available(person, From, Through);
        }
        Metrics = ReportAnalysis.Metrics(Rows, F.Kind, Cutoff);
        Metrics = Metrics.Concat(new[] {
            new ReportMetric("Con arrastre autorizado", Rows.Count(r => r.Status != "REASIGNADA" && r.Delivered > r.Due && r.Carryover.ApprovedCount > 0).ToString(), "Entregas tardías; una vez por asignación", "info"),
            new ReportMetric("Cubiertas por arrastre", Rows.Count(r => r.Status != "REASIGNADA" && r.Delivered > r.Due && r.Carryover.ApprovedCount > 0 && r.Delivered <= r.Carryover.RecognizedDue).ToString(), "Dentro del plazo reconocido", "info") }).ToArray();
        Charts = ReportAnalysis.Charts(Rows, F, Cutoff, capacity);
        Templates = await db.ReportTemplates.AsNoTracking().Where(t => t.UserId == session.UserId).OrderBy(t => t.Name).ToArrayAsync(token);
        ReportFilter? applied = null;
        if (AppliedTemplate?.StartsWith("preset:") == true && ReportCatalog.Definitions.Any(d => "preset:" + d.Id == AppliedTemplate))
            applied = ReportCatalog.Preset(AppliedTemplate[7..]);
        else if (Guid.TryParse(AppliedTemplate, out var appliedId) && Templates.FirstOrDefault(t => t.Id == appliedId) is { } saved)
            applied = JsonSerializer.Deserialize<ReportFilter>(saved.Configuration, JsonOptions);
        if (applied == null || !ReportCatalog.SameConfiguration(F, applied)) AppliedTemplate = "";
        // A hidden field must render the verified value, rather than the original query value.
        ModelState.Remove(nameof(AppliedTemplate));
        ExportJson = JsonSerializer.Serialize(new { title = Definition.Name, description = Definition.Description,
            period = $"{From:dd/MM/yyyy} - {Through:dd/MM/yyyy}", cutoff = ReportCatalog.Time(Cutoff), scope = Scope,
            filters = FilterSummary(), metrics = Metrics, charts = Charts,
            columns = SelectedColumns.Select(c => new { key = c, label = ReportCatalog.Columns[c] }),
            rows = Rows.Select(r => SelectedColumns.Select(c => ReportCatalog.Cell(r, c)).ToArray()),
            note = "Estado actual al corte de consulta. Entrega y aprobación son momentos distintos. La carga es estimada. Los intentos reasignados se separan del total de tareas." }, JsonOptions);
        return Page();
    }

    public string FilterSummary()
    {
        var parts = new List<string> { "Fecha: " + (F.DateBasis switch { "due" => "vencimiento", "delivered" => "entrega", "reviewed" => "revisión", _ => "inicio" }) };
        if (F.Person.Length > 0) parts.Add("Responsable: " + (Options.FirstOrDefault(r => r.PersonId.ToString() == F.Person)?.Person ?? F.Person));
        if (F.Role.Length > 0) parts.Add("Rol: " + ReportCatalog.RoleName(F.Role));
        if (F.Plan.Length > 0) parts.Add("Plan: " + (Options.FirstOrDefault(r => r.PlanId.ToString() == F.Plan)?.Plan ?? F.Plan));
        if (F.Area.Length > 0) parts.Add("Área: " + F.Area);
        if (F.Task.Length > 0) parts.Add("Tarea: " + (Options.FirstOrDefault(r => r.Code == F.Task)?.Task ?? F.Task));
        if (F.Status.Length > 0) parts.Add("Estado: " + ReportCatalog.Statuses.GetValueOrDefault(F.Status, F.Status));
        if (F.Carryover.Length > 0) parts.Add("Arrastre / puntualidad: " + F.Carryover);
        if (F.Specialization.Length > 0) parts.Add("Especialización: " + F.Specialization);
        foreach (var (label, value) in new[] { ("Requisito", F.Requirement), ("Programación", F.Schedule), ("Frecuencia", F.Frequency) })
            if (value.Length > 0) parts.Add(label + ": " + value);
        foreach (var (label, value) in new[] { ("Repeticiones", F.Repetitions), ("Plazo (h)", F.Hours), ("Carga (min)", F.Minutes), ("Avance mínimo (%)", F.ProgressMin), ("Avance máximo (%)", F.ProgressMax) })
            if (value != null) parts.Add(label + ": " + value);
        parts.Add("Agrupación: " + (F.Group switch { "plan" => "plan", "role" => "rol", "task" => "tarea", _ => "responsable" }));
        parts.Add("Orden: " + (F.Order switch { "person" => "responsable", "progress" => "menor avance", "delay" => "mayor antigüedad", _ => "vencimiento" }));
        return string.Join(" · ", parts);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken token)
    {
        NoStore(); var session = await sessions.GetAsync(token); if (session is null) return Redirect("/acceso");
        ReportFilter config;
        try {
            if (Configuration.Length > 12000) throw new ArgumentException();
            config = JsonSerializer.Deserialize<ReportFilter>(Configuration, JsonOptions) ?? throw new ArgumentException();
            ReportAnalysis.Range(config, ReportCatalog.Day(clock.UtcNow));
        } catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException) { return BadRequest("Configuración de plantilla inválida."); }
        var name = TemplateName.Trim();
        if (name.Length is < 1 or > 100 || name.Any(char.IsControl)) return BadRequest("Usa un nombre de 1 a 100 caracteres.");
        ReportTemplate? template;
        if (TemplateId is { } id) {
            template = await db.ReportTemplates.SingleOrDefaultAsync(t => t.Id == id && t.UserId == session.UserId, token);
            if (template is null) return NotFound();
        } else {
            if (await db.ReportTemplates.CountAsync(t => t.UserId == session.UserId, token) >= 100) return BadRequest("Puedes guardar hasta 100 plantillas personales.");
            template = new() { Id = Guid.CreateVersion7(), UserId = session.UserId }; db.ReportTemplates.Add(template);
        }
        template.Name = name; template.Configuration = JsonSerializer.Serialize(config, JsonOptions); template.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(token); Success = "Plantilla guardada.";
        var query = typeof(ReportFilter).GetProperties().ToDictionary(p => "F." + p.Name, p => (string?)(p.GetValue(config) is DateOnly date ? date.ToString("yyyy-MM-dd") : p.GetValue(config)?.ToString()));
        query[nameof(AppliedTemplate)] = template.Id.ToString();
        return Redirect(QueryHelpers.AddQueryString("/reportes", query));
    }

    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken token)
    {
        NoStore(); var session = await sessions.GetAsync(token); if (session is null) return Redirect("/acceso");
        var template = await db.ReportTemplates.SingleOrDefaultAsync(t => t.Id == TemplateId && t.UserId == session.UserId, token);
        if (template is null) return NotFound();
        db.ReportTemplates.Remove(template); await db.SaveChangesAsync(token); Success = "Plantilla eliminada."; return Redirect("/reportes");
    }
}



