using Sgol.Web.Presentation.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Tasks;

public sealed class ProcessesModel(IRazorSessionState sessions, OperationalProcessCatalogService processes, OperationalTaskService tasks) : PageModel
{
    [BindProperty] public ProcessDraftInput Input { get; set; } = new();
    [BindProperty] public Guid CreateKey { get; set; } = Guid.CreateVersion7();
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    public OperationalSequence[] Processes { get; private set; } = [];
    public ProcessEditor? Editor { get; private set; }
    public ProcessItemEntry[] Items { get; private set; } = [];
    public OperationalTask[] Candidates { get; private set; } = [];
    public bool CanManage { get; private set; }
    public bool Creating { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public string SavedInputJson { get; private set; } = "{}";
    private async Task<Guid?> Actor(CancellationToken token, bool write)
    {
        Response.Headers.CacheControl = "no-store";
        var session = await sessions.GetAsync(token); CanManage = session?.RoleCode == CanonicalRole.Direction;
        return session != null && (CanManage || !write && session.RoleCode == CanonicalRole.Administration) ? session.UserId : null;
    }
    private async Task Load(Guid actor, Guid? id, CancellationToken token, bool preserveInput = false)
    {
        Processes = await processes.ListAsync(actor, token);
        Items = await processes.ItemsAsync(actor, token);
        if (id == null) return;
        Editor = await processes.ReadAsync(actor, id.Value, token);
        if (Editor.Process.IsGeneral) return;
        var submitted = Input;
        {
            var v = Editor.Configuration;
            Input = new() { Id = Editor.Process.Id, Revision = Editor.Process.Revision, Name = Editor.Process.Name,
                Description = Editor.Process.Description, StartKind = v?.StartKind ?? "OCASIONAL", FirstMonth = v?.FirstMonth ?? 2,
                WindowFromDay = v?.WindowFromDay ?? 20, WindowThroughDay = v?.WindowThroughDay ?? 25,
                PreferredWeekDay = v?.PreferredWeekDay ?? 1, StartHour = v?.StartHourConfirmed == true ? v.StartHour : null, Steps = Editor.Steps.ToList() };
        }
        SavedInputJson = System.Text.Json.JsonSerializer.Serialize(new {
            Input.Name, Input.Description, Input.StartKind, Input.FirstMonth, Input.WindowFromDay,
            Input.WindowThroughDay, Input.PreferredWeekDay, Input.StartHour,
            Steps = Input.Steps.Select(s => new { s.TaskId, s.Position, s.Action, s.ReleaseCondition,
                s.MinimumStartOffsetHours, s.ReturnToPosition, s.Instructions, s.OutputItemId, s.OutputTargetTaskId, s.IsFinalAudit })
        });
        if (preserveInput) Input = submitted;
        Candidates = (await tasks.ListAsync(actor, token)).Where(t =>
            (t.ProcessId == OperationalSequence.GeneralId || t.ProcessId == id) && !Editor.Steps.Any(s => s.TaskId == t.Id) &&
            (string.IsNullOrWhiteSpace(Search) || SearchText.Contains(t.Name, Search.Trim()) || SearchText.Contains(t.TaskCode, Search.Trim()))).ToArray();
    }
    public async Task<IActionResult> OnGetAsync(Guid? id, bool create, string? notice, CancellationToken token)
    {
        var actor = await Actor(token, false); if (actor == null || create && !CanManage) return StatusCode(403);
        try { Creating = create; Notice = notice switch { "saved" => "Borrador guardado. No se generó trabajo.", "published" => "Versión publicada. La activación es una acción distinta.", "disabled" => "Nuevos inicios desactivados. La historia se conserva.", "enabled" => "Versión publicada activa para nuevos inicios.", _ => null }; await Load(actor.Value, id, token); return Page(); }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public async Task<IActionResult> OnPostCreateAsync(CancellationToken token)
    {
        var actor = await Actor(token, true); if (actor == null) return StatusCode(403);
        try { var id = await processes.CreateAsync(actor.Value, CreateKey, Input.Name, Input.Description, token); return RedirectToPage(new { id }); }
        catch (DirectionPlanException e) { Response.StatusCode = e.Status; Error = IndexModel.Message(e.Code); Creating = true; await Load(actor.Value, null, token); return Page(); }
    }
    public Task<IActionResult> OnPostSaveAsync(CancellationToken token) => Write(token, () => processes.SaveAsync(UserId, Input, token), "saved", true);
    public Task<IActionResult> OnPostAddAsync(Guid taskId, long taskRevision, CancellationToken token) => Write(token, () => processes.AddTaskAsync(UserId, Input.Id, Input.Revision, taskId, taskRevision, token));
    public Task<IActionResult> OnPostPublishAsync(CancellationToken token) => Write(token, () => processes.PublishAsync(UserId, Input.Id, Input.Revision, token), "published");
    public Task<IActionResult> OnPostEnableAsync(bool enabled, CancellationToken token) => Write(token, () => processes.SetEnabledAsync(UserId, Input.Id, Input.Revision, enabled, token), enabled ? "enabled" : "disabled");
    public Task<IActionResult> OnPostAbandonAsync(bool confirmed, CancellationToken token) => Write(token, () => confirmed ? processes.AbandonAsync(UserId, Input.Id, Input.Revision, token) : Task.FromException(new DirectionPlanException("PROCESS_INVALID", 422)));
    private Guid UserId;
    private async Task<IActionResult> Write(CancellationToken token, Func<Task> action, string? notice = null, bool preserveInput = false)
    {
        var actor = await Actor(token, true); if (actor == null) return StatusCode(403); UserId = actor.Value;
        try {
            ModelState.Remove("Input.Description");
            for (var i=0; i<Input.Steps.Count; i++) ModelState.Remove($"Input.Steps[{i}].Instructions");
            if (preserveInput && !ModelState.IsValid) throw new DirectionPlanException("PROCESS_RULES_INVALID", 422);
            await action(); return RedirectToPage(new { id = Input.Id, notice }); }
        catch (DirectionPlanException e) {
            Response.StatusCode = e.Status; Error = IndexModel.Message(e.Code);
            await Load(actor.Value, Input.Id, token, preserveInput);
            if (e.Code == "PROCESS_RULES_INVALID")
            {
                var details = ModelState.Where(pair => pair.Value?.Errors.Count > 0)
                    .Select(pair => {
                        var stage = System.Text.RegularExpressions.Regex.Match(pair.Key, @"Steps\[(\d+)\]");
                        var location = stage.Success && int.TryParse(stage.Groups[1].Value, out var index) && index < Input.Steps.Count
                            ? $"Etapa {Input.Steps[index].Position}" : "Proceso";
                        var field = pair.Key.Split('.').Last() switch {
                            "Position" => "la posición", "ReturnToPosition" => "la posición de devolución",
                            "MinimumStartOffsetHours" => "las horas de inicio mínimo", "StartHour" => "la hora de inicio",
                            "FirstMonth" => "el primer mes", "WindowFromDay" or "WindowThroughDay" => "los días de la ventana",
                            "PreferredWeekDay" => "el día preferido", "Action" => "la acción",
                            "ReleaseCondition" => "la condición de entrada", "Name" => "el nombre", _ => "los valores capturados" };
                        return $"{location}: revisa {field}.";
                    });
                var rules = Input.Steps.OrderBy(s => s.Position).ToArray();
                var entries = rules.Where(s => s.Position == 1 ? s.ReleaseCondition != "INICIO" :
                    s.ReleaseCondition != (rules.FirstOrDefault(p => p.Position == s.Position - 1)?.Action == "ENTREGAR" ? "ENTREGA" : "APROBACION"))
                    .Select(s => $"Etapa {s.Position} ({Editor?.Tasks.FirstOrDefault(t => t.Id == s.TaskId)?.Name}): revisa la condición de entrada según la acción de la etapa anterior.");
                Error += " " + string.Join(" ", details.Concat(entries));
            }
            return Page();
        }
    }
}
