using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.MyWork;

public sealed partial class IndexModel
{
    public async Task<IActionResult> OnPostChecklistDraftAsync(Guid id, string answer, CancellationToken token)
    {
        NoStore();
        var session = await sessionState.GetAsync(token);
        if (session is null) return Unauthorized();
        try { await HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return BadRequest(); }
        try {
            await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().SaveChecklistDraftAsync(session.UserId, id, answer, token);
            return new JsonResult(new { saved = true });
        } catch (DirectionPlanException e) { return new JsonResult(new { error = Pages.Tasks.IndexModel.Message(e.Code) }) { StatusCode = e.Status }; }
    }
    public IReadOnlyList<OperationalAssignmentDetails> OperationalTasks { get; private set; } = [];
    public IReadOnlyList<OperationalLifecycleEntry> OperationalLifecycle { get; private set; } = [];
    public IReadOnlyList<OperationalTransfer> OperationalTransfers { get; private set; } = [];
    public IReadOnlyList<OperationalContribution> OperationalContributions { get; private set; } = [];
    public IReadOnlyList<OperationalWorkPlan> OperationalPlans => OperationalWorkPlan.Group(OperationalTasks, OperationalDay, OperationalNow);
    public IReadOnlyList<OperationalAssignmentDetails> OperationalDayTasks =>
        OperationalWorkPlan.TodayTasks(OperationalTasks, OperationalDay, OperationalNow);
    public IReadOnlyList<OperationalAssignmentDetails> OperationalOrderedTasks =>
        OperationalWorkPlan.OrderedTasks(OperationalTasks, OperationalDay, OperationalNow);
    public string? OperationalError { get; private set; }
    public OperationalAssignmentDetails? OperationalSelectedTask { get; private set; }
    public Sgol.Web.Presentation.MyWork.ProcessActionForm? OperationalProcessForm { get; private set; }
    public OccasionDetails? OperationalOccasion { get; private set; }
    public IReadOnlyList<OperationalProgressEntry> OperationalHistory { get; private set; } = [];
    public IReadOnlyList<OperationalPhotographDetails> OperationalPhotographs { get; private set; } = [];
    public Guid? OperationalAttemptedTaskId { get; private set; }
    public string? OperationalAttemptedComment { get; private set; }
    public string? OperationalAttemptedChecklist { get; private set; }
    public string? OperationalAttemptedConclusion { get; private set; }
    public DateTimeOffset OperationalNow { get; private set; }
    public DateOnly OperationalDay => WeekContract.LocalToday(OperationalNow);
    public bool OperationalToday(OperationalAssignmentDetails task) => OperationalWorkPlan.IsForDay(task, OperationalDay);
    public string OperationalColor(OperationalAssignmentDetails task) => task.ReviewStatus switch {
        "NO_SATISFECHA" => "urgent", "REASIGNADA" => "scheduled",
        _ => LiveBoardTask.FromAssignment(task).Color(OperationalNow)
    };
    public string OperationalTiming(OperationalAssignmentDetails task) => OperationalColor(task) switch {
        "delivered" => "Entregada", "scheduled" => "Por activar", "active" => "Activa",
        "warning" => "Último 30% del tiempo", _ => task.DueAt <= OperationalNow ? "Vencida" : "Último 10% del tiempo"
    };
    public string OperationalTaskHref(Guid id) => QueryHelpers.AddQueryString("/mi-trabajo", "assignedTask", id.ToString("D")) + "#plan-tasks";
    public static bool OperationalSubmitted(OperationalAssignmentDetails task) =>
        task.CompletedAt is not null || task.ReviewStatus is "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA";
    private async Task LoadOperationalAsync(Guid actor, CancellationToken token)
    {
        OperationalNow = HttpContext.RequestServices.GetRequiredService<IClock>().UtcNow;
        try {
            OperationalTasks = await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().MineAsync(actor, token);
            OperationalTransfers = await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().MyTransfersAsync(actor, token);
            var selected = OperationalAttemptedTaskId?.ToString("D") ?? Request.Query["assignedTask"].ToString();
            if (!string.IsNullOrEmpty(selected)) {
                if (!Guid.TryParseExact(selected, "D", out var id)) throw new DirectionPlanException("TASK_NOT_FOUND", 404);
                OperationalSelectedTask = OperationalTasks.SingleOrDefault(task => task.Id == id)
                    ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
                if (OperationalAttemptedChecklist != null && OperationalSelectedTask.Requirement == "CHECKLIST") {
                    try { var marks = OperationalChecklist.ValidateAnswer(OperationalSelectedTask.ChecklistJson, OperationalAttemptedChecklist, false);
                        OperationalSelectedTask = OperationalSelectedTask with { ChecklistDraftJson = System.Text.Json.JsonSerializer.Serialize(new ChecklistDraft(actor, OperationalNow, marks)) };
                    } catch (DirectionPlanException) { }
                }
                var process = await HttpContext.RequestServices.GetRequiredService<OperationalProcessService>().ReadAssignmentAsync(actor, id, token);
                if (process is not null)
                    OperationalProcessForm = new(process, OperationalSelectedTask.PersonId, "CompleteProcess", id,
                        OperationalAttemptedConclusion, OperationalAttemptedTaskId is not null ? Request.Form["Differences"].ToString() : null,
                        OperationalAttemptedTaskId is not null && Guid.TryParse(Request.Form["Key"], out var key) ? key : null,
                        OperationalAttemptedTaskId is not null && Request.Form["Declared"].Contains("true"), OperationalSelectedTask.ChecklistDraftJson);
                OperationalHistory = await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().ProgressHistoryAsync(actor, id, token);
                OperationalContributions = await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().ContributionsAsync(actor, id, token);
                OperationalLifecycle = await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().LifecycleHistoryAsync(actor, id, token);
                OperationalPhotographs = await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().PhotographsAsync(actor, id, token);
                if (OperationalSelectedTask.OccasionId != null)
                    OperationalOccasion = await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().OccasionAsync(actor, id, token);
            }
        }
        catch (DirectionPlanException e) { Response.StatusCode = e.Status; OperationalError = Pages.Tasks.IndexModel.Message(e.Code); }
    }
    public async Task<IActionResult> OnPostCompleteOperationalAsync(Guid id, string? comment, string? conclusion, IFormFile? file,
        Guid? captureSessionId, string? photographIds, List<IFormFile>? screenshots, CancellationToken token, string? checklistAnswer)
    {
        NoStore();
        var session = await sessionState.GetAsync(token); if (session is null) return Redirect("/acceso");
        try { await HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return BadRequest(); }
        try {
            Guid[]? photoIds = null;
            try { if (photographIds is not null && photographIds.Length <= 1000) photoIds = System.Text.Json.JsonSerializer.Deserialize<Guid[]>(photographIds); }
            catch (System.Text.Json.JsonException) { throw new DirectionPlanException("TASK_PHOTO_LIMIT", 422); }
            if (Request.Form.Files.Any(f => f.Name is not ("file" or "screenshots")) ||
                Request.Form.Files.Count(f => f.Name == "file") > 1 || Request.Form.Files.Count > 20 || !ModelState.IsValid)
                throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().CompleteAsync(session.UserId, id, comment, file, token, conclusion, captureSessionId, photoIds,
                screenshots?.Count > 0 ? screenshots : null, checklistAnswer);
            TempData["OperationalSuccess"] = "Evidencia enviada a revisión.";
            return Redirect("/mi-trabajo#plan-tasks");
        } catch (DirectionPlanException e) {
            Response.StatusCode = e.Status; OperationalError = Pages.Tasks.IndexModel.Message(e.Code);
            OperationalAttemptedChecklist = checklistAnswer;
            OperationalAttemptedTaskId = id; OperationalAttemptedComment = comment; OperationalAttemptedConclusion = conclusion;
        }
        return await LoadWorkAsync(session, token, true);
    }
    public async Task<IActionResult> OnPostCompleteProcessAsync(Guid id, Guid attemptId, long revision, Guid key,
        string decision, string conclusion, string? differences, IFormFile? document, bool declared, CancellationToken token,
        Guid? captureSessionId, string? photographIds, List<IFormFile>? screenshots, string? checklistAnswer)
    {
        NoStore();
        var session = await sessionState.GetAsync(token); if (session is null) return Redirect("/acceso");
        try { await HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return BadRequest(); }
        try {
            Guid[]? photoIds = null;
            try { if (photographIds is not null && photographIds.Length <= 1000) photoIds = System.Text.Json.JsonSerializer.Deserialize<Guid[]>(photographIds); }
            catch (System.Text.Json.JsonException) { throw new DirectionPlanException("TASK_PHOTO_LIMIT", 422); }
            if (!ModelState.IsValid || Request.Form.Files.Count > 21 || Request.Form.Files.Count(f => f.Name == "Document") > 1 || Request.Form.Files.Any(f => f.Name is not ("Document" or "screenshots")))
                throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            var service = HttpContext.RequestServices.GetRequiredService<OperationalProcessService>();
            var process = await service.ReadAssignmentAsync(session.UserId, id, token)
                ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
            if (!process.Attempts.Any(a => a.Id == attemptId && a.AssignmentId == id))
                throw new DirectionPlanException("PROCESS_CONFLICT", 409);
            await service.DecideAsync(session.UserId, process.Run.Id, attemptId, revision, key,
                decision, conclusion, differences, document, declared, token, captureSessionId, photoIds, screenshots?.Count > 0 ? screenshots : null, checklistAnswer);
            return Redirect(OperationalTaskHref(id));
        } catch (DirectionPlanException e) {
            Response.StatusCode = e.Status; OperationalError = Pages.Tasks.IndexModel.Message(e.Code);
            OperationalAttemptedChecklist = checklistAnswer;
            OperationalAttemptedTaskId = id; OperationalAttemptedConclusion = conclusion;
        }
        return await LoadWorkAsync(session, token, true);
    }

    public async Task<IActionResult> OnGetOperationalFileAsync(Guid id, Guid? photoId, bool preview, CancellationToken token)
    {
        NoStore(); var session = await sessionState.GetAsync(token); if (session is null) return Redirect("/acceso");
        try {
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            var service = HttpContext.RequestServices.GetRequiredService<OperationalTaskService>();
            if (photoId is not null)
            {
                var photograph = await service.PhotographAsync(session.UserId, id, photoId.Value, token);
                return File(photograph.Content, photograph.ContentType);
            }
            var photos = await service.PhotographsAsync(session.UserId, id, token);
            if (photos.Count > 0) return Redirect($"/mi-trabajo/fotografias/{id:D}");
            var assignment = await service.FileAsync(session.UserId, id, token);
            if (preview && assignment.ContentType is "image/jpeg" or "image/png")
                return File(assignment.FileContent!, assignment.ContentType);
            return File(assignment.FileContent!, assignment.ContentType!, assignment.FileName!);
        } catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }

    public async Task<IActionResult> OnGetReferenceFileAsync(Guid id, Guid fileId, bool preview, CancellationToken token)
    {
        NoStore(); var session = await sessionState.GetAsync(token); if (session is null) return Redirect("/acceso");
        try {
            var file = await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().ReferenceFileAsync(session.UserId, id, fileId, token);
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            return preview && file.ContentType is "image/jpeg" or "image/png" ? File(file.Content, file.ContentType) : File(file.Content, file.ContentType, file.FileName);
        } catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }

    public async Task<IActionResult> OnGetOperationalPhotosAsync(Guid id, CancellationToken token)
    {
        NoStore(); var session = await sessionState.GetAsync(token); if (session is null) return Redirect("/acceso");
        try
        {
            OperationalPhotographs = await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().PhotographsAsync(session.UserId, id, token);
            if (OperationalPhotographs.Count == 0) return NotFound();
            return Redirect($"/mi-trabajo/fotografias/{id:D}");
        }
        catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }

    public Task<IActionResult> OnPostBeginCameraAsync(Guid id, CancellationToken token) => CameraOperationAsync("begin", id, Guid.Empty, Guid.Empty, null, token);
    public Task<IActionResult> OnPostUploadCameraAsync(Guid id, Guid captureSessionId, Guid photographId, IFormFile? photo, CancellationToken token) =>
        CameraOperationAsync("upload", id, captureSessionId, photographId, photo, token);
    public Task<IActionResult> OnPostCameraStatusAsync(Guid id, Guid captureSessionId, Guid photographId, CancellationToken token) =>
        CameraOperationAsync("status", id, captureSessionId, photographId, null, token);
    public Task<IActionResult> OnPostDeleteCameraPhotoAsync(Guid id, Guid captureSessionId, Guid photographId, CancellationToken token) =>
        CameraOperationAsync("delete", id, captureSessionId, photographId, null, token);

    private async Task<IActionResult> CameraOperationAsync(string operation, Guid id, Guid captureSessionId, Guid photographId, IFormFile? photo, CancellationToken token)
    {
        NoStore(); var session = await sessionState.GetAsync(token);
        if (session is null) return new JsonResult(new { message = "Tu sesión terminó. Vuelve a iniciar sesión." }) { StatusCode = 401 };
        try { await HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return new JsonResult(new { message = "Recarga la página antes de continuar." }) { StatusCode = 400 }; }
        try
        {
            if (!ModelState.IsValid || Request.Form.Files.Count != (operation == "upload" ? 1 : 0) || operation == "upload" && Request.Form.Files[0].Name != "photo")
                throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            if (Request.Form.Any(f => f.Value.Count != 1) || Request.Form.Keys.Any(k => k is not ("__RequestVerificationToken" or "id" or "captureSessionId" or "photographId")))
                throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            var service = HttpContext.RequestServices.GetRequiredService<OperationalTaskService>();
            if (operation == "begin")
            {
                var capture = await service.BeginCaptureAsync(session.UserId, id, token);
                return new JsonResult(new { captureSessionId = capture.Id, expiresAt = capture.ExpiresAt });
            }
            if (operation == "delete")
            {
                await service.DeleteStagedPhotographAsync(session.UserId, id, captureSessionId, photographId, token);
                return new JsonResult(new { deleted = true });
            }
            var result = operation == "upload" ? await service.UploadPhotographAsync(session.UserId, id, captureSessionId, photographId,
                photo ?? throw new DirectionPlanException("TASK_FILE_REQUIRED", 422), token) :
                await service.PhotographStatusAsync(session.UserId, id, captureSessionId, photographId, token);
            return new JsonResult(new { photographId = result.Id, status = "LIMPIO", sizeBytes = result.SizeBytes });
        }
        catch (DirectionPlanException e) { return new JsonResult(new { message = Pages.Tasks.IndexModel.Message(e.Code) }) { StatusCode = e.Status }; }
        catch (Exception) when (!token.IsCancellationRequested)
        { return new JsonResult(new { message = "No se pudo verificar la fotografía. Reintenta la carga." }) { StatusCode = 503 }; }
    }
    public async Task<IActionResult> OnPostOperationalProgressAsync(Guid id, long revision, int units, string? note, CancellationToken token)
    {
        NoStore(); var session = await sessionState.GetAsync(token); if (session is null) return Redirect("/acceso");
        try { await HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return BadRequest(); }
        try {
            await HttpContext.RequestServices.GetRequiredService<OperationalTaskService>().ProgressAsync(session.UserId, id, revision, units, note, token);
            return Redirect(OperationalTaskHref(id));
        } catch (DirectionPlanException e) {
            Response.StatusCode = e.Status; OperationalError = Pages.Tasks.IndexModel.Message(e.Code); OperationalAttemptedTaskId = id;
            return await LoadWorkAsync(session, token, true);
        }
    }
}
