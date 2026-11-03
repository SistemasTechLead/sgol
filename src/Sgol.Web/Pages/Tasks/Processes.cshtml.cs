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
    [BindProperty] public int[] ProcessDays { get; set; } = [];
    [BindProperty] public Guid CreateKey { get; set; } = Guid.CreateVersion7();
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    public OperationalSequence[] Processes { get; private set; } = [];
    public ProcessEditor? Editor { get; private set; }
    public ProcessItemEntry[] Items { get; private set; } = [];
    public OperationalTask[] Candidates { get; private set; } = [];
    public bool CanManage { get; private set; }
    public bool Creating { get; private set; }
    public string? Error { get; private set; }
    public ProcessMembershipConflict[] TaskConflicts { get; private set; } = [];
    public List<(string Field, string Message)> FieldConflicts { get; } = [];
    public string? Notice { get; private set; }
    public Guid? BlockingPlanId { get; private set; }
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
                Schedule = ProcessStartSchedule.Read(v?.StartScheduleJson), Description = Editor.Process.Description, StartKind = v?.StartKind ?? "OCASIONAL", FirstMonth = v?.FirstMonth ?? 2,
                WindowFromDay = v?.WindowFromDay ?? 20, WindowThroughDay = v?.WindowThroughDay ?? 25,
                PreferredWeekDay = v?.PreferredWeekDay ?? 1, StartHour = v?.StartHourConfirmed == true ? v.StartHour : null, Steps = Editor.Steps.ToList() };
        }
        SavedInputJson = System.Text.Json.JsonSerializer.Serialize(new {
            Input.Schedule, Input.Name, Input.Description, Input.StartKind, Input.FirstMonth, Input.WindowFromDay,
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
        try { Creating = create; Notice = notice switch { "deleted" => "Proceso eliminado del catálogo. Sus tareas volvieron a General; las asignaciones y el historial se conservan.", "saved" => "Borrador guardado. Las tareas retiradas vuelven a General. No se generó trabajo.", "published" => "Versión publicada. La activación es una acción distinta.", "disabled" => "Nuevos inicios desactivados. La historia se conserva.", "enabled" => "Versión publicada activa para nuevos inicios.", _ => null }; await Load(actor.Value, id, token); return Page(); }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public async Task<IActionResult> OnPostCreateAsync(CancellationToken token)
    {
        var actor = await Actor(token, true); if (actor == null) return StatusCode(403);
        try { var id = await processes.CreateAsync(actor.Value, CreateKey, Input.Name, Input.Description, token); return RedirectToPage(new { id }); }
        catch (DirectionPlanException e) { Response.StatusCode = e.Status; Error = IndexModel.Message(e.Code); Creating = true; await Load(actor.Value, null, token); ApplyIssues(e, true); return Page(); }
    }
    public Task<IActionResult> OnPostSaveAsync(CancellationToken token) {
        if (ProcessDays.Any(d => d < 0 || d > 6)) return Task.FromResult<IActionResult>(BadRequest());
        Input.Schedule.WeekDays = ProcessDays.Aggregate(0, (mask, day) => mask | (1 << day));
        return Write(token, () => processes.SaveAsync(UserId, Input, token), "saved", true);
    }
    public Task<IActionResult> OnPostAddAsync(Guid taskId, long taskRevision, CancellationToken token) => Write(token, () => processes.AddTaskAsync(UserId, Input.Id, Input.Revision, taskId, taskRevision, token));
    public Task<IActionResult> OnPostPublishAsync(CancellationToken token) => Write(token, () => processes.PublishAsync(UserId, Input.Id, Input.Revision, token), "published");
    public Task<IActionResult> OnPostEnableAsync(bool enabled, CancellationToken token) => Write(token, () => processes.SetEnabledAsync(UserId, Input.Id, Input.Revision, enabled, token), enabled ? "enabled" : "disabled");
    public Task<IActionResult> OnPostAbandonAsync(bool confirmed, CancellationToken token) => Write(token, () => confirmed ? processes.AbandonAsync(UserId, Input.Id, Input.Revision, token) : Task.FromException(new DirectionPlanException("PROCESS_INVALID", 422)));
    public Task<IActionResult> OnPostDeleteAsync(bool confirmed, CancellationToken token) => Write(token,
        () => confirmed ? processes.DeleteAsync(UserId, Input.Id, Input.Revision, token) : Task.FromException(new DirectionPlanException("PROCESS_DELETE_CONFIRMATION_REQUIRED", 422)), "deleted");
    private Guid UserId;
    private async Task<IActionResult> Write(CancellationToken token, Func<Task> action, string? notice = null, bool preserveInput = false)
    {
        var actor = await Actor(token, true); if (actor == null) return StatusCode(403); UserId = actor.Value;
        try {
            ModelState.Remove("Input.Description");
            for (var i=0; i<Input.Steps.Count; i++) ModelState.Remove($"Input.Steps[{i}].Instructions");
            if (preserveInput && !ModelState.IsValid) throw new DirectionPlanException("PROCESS_RULES_INVALID", 422);
            await action(); return notice == "deleted" ? RedirectToPage(new { notice }) : RedirectToPage(new { id = Input.Id, notice }); }
        catch (DirectionPlanException e) {
            Response.StatusCode = e.Status; Error = IndexModel.Message(e.Code);
            if (e.Data["TaskConflicts"] is ProcessMembershipConflict[] conflicts)
            {
                TaskConflicts = conflicts;
                Error = "No se pudo completar la operación. Revisa los conflictos indicados; no se aplicó ningún cambio.";
            }
            if (e.Code == "PROCESS_PREPARED_PLANS_EXIST" && e.Data["PlanId"] is Guid planId)
            {
                BlockingPlanId = planId;
                Error = $"El plan «{e.Data["PlanName"]}» reserva carga de este proceso. Revisa y elimina ese plan en Planificación antes de quitar tareas o eliminar el proceso. No se aplicó ningún cambio.";
            }
            await Load(actor.Value, e.Code == "PROCESS_NOT_FOUND" ? null : Input.Id, token, preserveInput);
            ApplyIssues(e, preserveInput);
            return Page();
        }
    }
    private void ApplyIssues(DirectionPlanException error, bool includeBindingErrors)
    {
        var issues = (error.Data["ProcessIssues"] as ProcessValidationIssue[] ?? []).ToList();
        foreach (var pair in ModelState.Where(p => includeBindingErrors && p.Value?.Errors.Count > 0).ToArray())
        {
            var match = System.Text.RegularExpressions.Regex.Match(pair.Key, @"Steps\[(\d+)\]");
            Guid? id = match.Success && int.TryParse(match.Groups[1].Value, out var index) && index < Input.Steps.Count ? Input.Steps[index].TaskId : null;
            issues.Add(new(id, pair.Key.Split('.').Last(), "El valor capturado no es válido. Revisa el formato y los límites del campo."));
        }
        foreach (var issue in issues.Distinct())
        {
            var message = string.IsNullOrEmpty(issue.Message) ? IndexModel.Message(error.Code) : issue.Message;
            if (issue.TaskId is { } id)
            {
                var task = Editor?.Tasks.FirstOrDefault(t => t.Id == id) ?? Candidates.FirstOrDefault(t => t.Id == id);
                TaskConflicts = [.. TaskConflicts, new(id, task?.Name ?? "Tarea no disponible en este borrador", task?.TaskCode ?? id.ToString(), message)];
            }
            if (issue.Field.Length > 0)
            {
                var index = issue.TaskId == null ? -1 : Input.Steps.FindIndex(s => s.TaskId == issue.TaskId);
                if (issue.TaskId == null || index >= 0)
                    FieldConflicts.Add((index < 0 ? $"Input.{issue.Field}" : $"Input.Steps[{index}].{issue.Field}", message));
            }
        }
        TaskConflicts = TaskConflicts.Distinct().ToArray();
    }

}
