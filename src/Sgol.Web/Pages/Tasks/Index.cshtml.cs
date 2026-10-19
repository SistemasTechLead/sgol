using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Infrastructure.Persistence.Organization;

namespace Sgol.Web.Pages.Tasks;

public sealed class IndexModel(IRazorSessionState sessions, OperationalTaskService tasks, SpecializationService specializations, OperationalProcessCatalogService processes) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ProcessFilter { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ReturnProcess { get; set; }
    public OperationalSequence[] Processes { get; private set; } = [];
    public string ProcessName(Guid id) => Processes.SingleOrDefault(p => p.Id == id)?.Name ?? "Proceso no disponible";
    public string CancelUrl => ReturnProcess is { } id ? $"/tareas/procesos?id={id}" : "/tareas";
    [BindProperty] public OperationalTask Input { get; set; } = new() { Revision = 0 };
    [BindProperty] public int[] InputDays { get; set; } = [];
    [BindProperty] public bool RequiresSpecialization { get; set; }
    public List<Specialization> Specializations { get; private set; } = [];
    public IReadOnlyList<OperationalTask> Items { get; private set; } = [];
    public bool Editing { get; private set; }
    public bool CanManage { get; private set; }
    public string? Error { get; private set; }
    public static string Label(string code) => code switch {
        "SEMANAL" => "Semanal", "MENSUAL" => "Mensual", "TRIMESTRAL" => "Trimestral", "ANUAL" => "Anual", "OCASIONAL" => "Ocasional",
        "CAPTURA_PANTALLA" => "Captura de pantalla",
        "FOTOGRAFIA" => "Fotografía", "COMENTARIO" => "Comentario", "DOCUMENTO" => "Documento PDF", "EXCEL" => "Documento Excel (.xlsx)", _ => "Ninguno"
    };
    public static string RoleLabel(string? code) => code is null ? "—" : SessionPresentation.RoleName(code) ?? "—";
    private async Task<Guid?> ActorAsync(CancellationToken token, bool requireManagement = true)
    {
        Response.Headers.CacheControl = "no-store";
        var session = await sessions.GetAsync(token);
        CanManage = session?.RoleCode == CanonicalRole.Direction;
        return session is not null && (CanManage || !requireManagement && session.RoleCode == CanonicalRole.Administration) ? session.UserId : null;
    }
    public async Task<IActionResult> OnGetAsync(Guid? edit, bool create, CancellationToken token)
    {
        var actor = await ActorAsync(token, requireManagement: false); if (actor is null) return StatusCode(403);
        if (!CanManage && (edit is not null || create)) return StatusCode(403);
        Specializations = await specializations.OptionsAsync(actor.Value, token);
        Processes = await processes.ListAsync(actor.Value, token);
        try {
            var all = await tasks.ListAsync(actor.Value, token);
            if (edit is not null) { Input = all.SingleOrDefault(t => t.Id == edit) ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404); Editing = true; }
            if (create) { Editing = true; Input.ProcessId = ReturnProcess ?? OperationalSequence.GeneralId; }
            RequiresSpecialization = Input.SpecializationId != null;
            InputDays = Enumerable.Range(0, 7).Where(d => (Input.WeekDays & (1 << d)) != 0).ToArray();
            Items = Filter(all); return Page();
        } catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken token)
    {
        var actor = await ActorAsync(token); if (actor is null) return StatusCode(403);
        Specializations = await specializations.OptionsAsync(actor.Value, token);
        Processes = await processes.ListAsync(actor.Value, token);
        try {
            if (!RequiresSpecialization) { Input.SpecializationId = null; ModelState.Remove("Input.SpecializationId"); }
            else if (Input.SpecializationId == null) throw new DirectionPlanException("TASK_SPECIALIZATION_INVALID", 422);
            ModelState.Remove("Input.TaskCode");
            if (!Input.Active && Input.ScheduleMode == "PROGRESO" && string.IsNullOrWhiteSpace(Input.ProgressUnit))
            {
                Input.ProgressUnit = "";
                ModelState.Remove("Input.ProgressUnit");
            }
            if (InputDays.Any(d => d is < 0 or > 6)) throw new DirectionPlanException("TASK_PARAMETERS_INVALID", 422);
            Input.WeekDays = InputDays.Distinct().Aggregate(0, (mask, day) => mask | (1 << day));
            if (!ModelState.IsValid) throw new DirectionPlanException("TASK_PARAMETERS_INVALID", 422);
            if (ReturnProcess != null && ReturnProcess != Input.ProcessId) throw new DirectionPlanException("PROCESS_MEMBERSHIP_INVALID", 422);
            await tasks.SaveAsync(actor.Value, Input, token);
            return ReturnProcess is { } id ? RedirectToPage("Processes", new { id }) : RedirectToPage();
        } catch (DirectionPlanException e) { Response.StatusCode = e.Status; Error = Message(e.Code); Editing = true; Items = Filter(await tasks.ListAsync(actor.Value, token)); return Page(); }
    }
    public async Task<IActionResult> OnPostChangeAsync(Guid id, long revision, bool delete, CancellationToken token)
    {
        var actor = await ActorAsync(token); if (actor is null) return StatusCode(403);
        Specializations = await specializations.OptionsAsync(actor.Value, token);
        Processes = await processes.ListAsync(actor.Value, token);
        try { await tasks.ChangeAsync(actor.Value, id, revision, delete, token); return RedirectToPage(); }
        catch (DirectionPlanException e) { Response.StatusCode = e.Status; Error = Message(e.Code); Items = Filter(await tasks.ListAsync(actor.Value, token)); return Page(); }
    }
    private IReadOnlyList<OperationalTask> Filter(List<OperationalTask> all) => all.Where(t =>
        (ProcessFilter == null || t.ProcessId == ProcessFilter) && (string.IsNullOrWhiteSpace(Search) ||
        t.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase) || t.TaskCode.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray();
    public static string Message(string code) => code switch {
        "TASK_MONTH_DAYS_INVALID" => "Indica días del mes entre 1 y 31 separados por comas, solo para tareas mensuales distribuidas.",
        "TASK_DESCRIPTION_INVALID" => "La descripción admite hasta 4,000 caracteres, con saltos de línea.",
        "WEEKLY_OPERATIONS_TASK_INVALID" => "El plan de operaciones semanal solo admite tareas periódicas del proceso General.",
        "PROCESS_USE_ACTION" => "Esta asignación pertenece a un proceso. Abre el proceso para entregar, revisar o devolver su intento vigente.",
        "PROCESS_DECISION_INVALID" => "Revisa la declaración, conclusión, decisión y diferencias pendientes de la etapa vigente.",
        "PROCESS_PLAN_PEOPLE_REQUIRED" => "Selecciona un responsable elegible para cada etapa y un periodo desde hoy.",
        "PROCESS_PLAN_NO_DATES" => "El calendario de este proceso no tiene inicios dentro del periodo elegido. Amplía el periodo o crea una ocasión explícita.",
        "PROCESS_CAPACITY_ADJUSTMENT_REQUIRED" => "La redistribución afecta una etapa o reserva de proceso. No se aplicó el cambio: debe conservarse la identidad de su intento y responsable.",
        "PLAN_CAPACITY_STALE" => "La versión o capacidad cambió desde la vista previa. Calcula otra vista previa antes de guardar.",
        "PROCESS_NOT_FOUND" => "El proceso ya no está disponible.",
        "PROCESS_GENERAL_RESERVED" => "General conserva las tareas independientes y no se edita como flujo.",
        "PROCESS_INVALID" => "Revisa el nombre, descripción y cantidad de etapas.",
        "PROCESS_CONFLICT" => "El proceso cambió. Recarga antes de guardar; no se aplicó este envío.",
        "PROCESS_RULES_INVALID" => "Revisa orden, entrada, acción, plazos, devoluciones y hora del calendario. Las posiciones deben ser consecutivas y las tareas únicas.",
        "PROCESS_MEMBERSHIP_INVALID" => "Cada tarea debe pertenecer al proceso, estar activa y aparecer una vez en la versión publicada.",
        "PROCESS_DOCUMENT_FLOW_REQUIRED" => "El motor documental de esta primera integración requiere tareas con evidencia PDF. Los demás requisitos conservan su recorrido en General.",
        "PROCESS_DOCUMENT_BOUNDARIES_REQUIRED" => "El flujo documental requiere al menos una entrega inicial y una aceptación final. Configura Entregar en la primera etapa y Aceptar resultado en la última.",
        "PROCESS_DRAFT_REQUIRED" => "Guarda un borrador antes de publicar o abandonar.",
        "PROCESS_USE_EDITOR" => "Cambia la pertenencia desde el editor del proceso para revisar sus efectos.",
        "PROCESS_TRANSFER_REQUIRES_PUBLICATION" => "La tarea pertenece a otro proceso. Requiere una publicación coherente en origen y destino; no se movió.",
        "PROCESS_TASK_COMMITTED" => "La tarea forma parte de una versión vigente. Publica una sustitución coherente antes de retirarla o desactivarla.",
        "PROCESS_OPERATION_NOT_READY" => "La activación requiere versión publicada e integración operativa verificada. El proceso continúa sin generar trabajo.",
        "OCCASION_INVALID" => "Revisa el nombre, las instrucciones, destinatarios y fechas. Adjunta hasta 5 archivos JPG, PNG o PDF (15 MB por archivo, 30 MB en total).",
        "OCCASION_RECIPIENT_INVALID" => "Selecciona personas activas con la especialización requerida y fuera del periodo de prueba; si la tarea no requiere especialización, deben pertenecer al rol indicado.",
        "OCCASION_CONFLICT" => "La solicitud ya fue usada con otros datos. Abre una nueva asignación para crear otra ocasión.",
        "PLAN_NO_AVAILABLE_WORKER" => "No hay horario, cualificación, disponibilidad o capacidad suficiente para algún destinatario. Revisa los conflictos en Asignar cargas de trabajo y Supervisión; no se crearon asignaciones parciales.",
        "TASK_CAMERA_REQUIRED" => "Toma las fotografías con la cámara y súbelas antes de enviar la tarea.",
        "TASK_CAMERA_EXPIRED" => "La sesión de captura venció. Recarga la tarea y vuelve a tomar las fotografías.",
        "TASK_CAMERA_DISABLED" => "La captura está temporalmente desactivada. La tarea permanece pendiente.",
        "TASK_CAMERA_RATE_LIMIT" => "Se alcanzó el límite de cargas de fotografías. Intenta más tarde.",
        "TASK_PHOTO_LIMIT" => "Revisa el mínimo y máximo configurados de fotografías o capturas: hasta 20 imágenes, 15 MiB por imagen y 30 MiB en total.",
        "TASK_PHOTO_NOT_FOUND" => "La fotografía todavía no tiene una carga confirmada. Reintenta la carga.",
        "TASK_SPECIALIZATION_INVALID" => "Selecciona una especialización activa.",
        "TASK_CONFLICT" => "La tarea cambió mientras la editabas. Recarga la página antes de guardar.",
        "TASK_NOT_FOUND" => "Esta tarea ya no está disponible.",
        "TASK_PARAMETERS_INVALID" => "Revisa los parámetros. Para activar una tarea de progreso completa meta y unidad y desmarca Personas distintas.",
        "TASK_NOT_STARTED" => "Esta tarea todavía no inicia.",
        "TASK_GOAL_PENDING" => "Registra el avance hasta completar la meta antes de enviar la tarea a revisión.",
        "TASK_PROGRESS_INVALID" => "Registra un avance positivo que no exceda la meta y describe lo realizado (máximo 1,000 caracteres).",
        "TASK_COMMENT_REQUIRED" => "Escribe un comentario de hasta 4,000 caracteres para terminar.",
        "TASK_CONCLUSION_TOO_LONG" => "La conclusión admite un máximo de 4,000 caracteres.",
        "TASK_FILE_REQUIRED" => "Adjunta el archivo requerido (máximo 15 MB).",
        "TASK_FILE_INVALID" => "Adjunta una fotografía JPG/PNG o un documento PDF/Excel (.xlsx), según el requisito.",
        "TASK_FILE_SCAN_FAILED" => "El archivo no pasó la revisión. Comprueba el archivo y vuelve a intentar.",
        _ => "No se pudo completar la operación."
    };
}
