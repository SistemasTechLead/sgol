using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Sgol.Configuration.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.BuildingBlocks.Time;
using Microsoft.EntityFrameworkCore;

namespace Sgol.Web.Pages.Planning;

public sealed partial class IndexModel
{
    private static readonly string[] DirectionFormFields = ["name", "area", "periodType", "year", "periodNumber"];
    public bool CanViewDirectionPlans { get; private set; }
    public bool CanPrepareDirectionPlan { get; private set; }
    public bool CanPrepareWeeklyOperationsPlan { get; private set; }
    private static bool CanPreparePlan(string role, bool operations) => role == CanonicalRole.Direction ||
        operations && role == CanonicalRole.Subcoordination;
    public bool DirectionCreating { get; private set; }
    public bool DirectionOperations { get; private set; }
    public bool DirectionConfirming { get; private set; }
    public bool DirectionUncertain { get; private set; }
    public string DirectionName { get; private set; } = "";
    public string DirectionArea { get; private set; } = "";
    public string DirectionPeriodType { get; private set; } = "SEMANAL";
    public string DirectionYear { get; private set; } = "";
    public string DirectionNumber { get; private set; } = "";
    public IReadOnlyList<string> DirectionSelectedCodes { get; private set; } = [];
    public IReadOnlyList<DirectionPlanningTaskOption> DirectionTaskOptions { get; private set; } = [];
    public IReadOnlyList<DirectionPlanDetails> DirectionPlans { get; private set; } = [];
    public DirectionPlanDetails? DirectionDetail { get; private set; }
    public WeekRange? DirectionRange { get; private set; }
    public ProblemDetailsPresentation? DirectionError { get; private set; }
    public string? DirectionSuccess { get; private set; }
    public string? DirectionNextHref { get; private set; }
    public string? DirectionPreviousHref { get; private set; }
    public string? DirectionPlanIntent { get; private set; }
    public string DirectionCapacityJson { get; private set; } = "[]";
    public string DirectionCapacityFingerprint { get; private set; } = "";
    public IReadOnlyList<string> DirectionCapacitySummary { get; private set; } = [];
    public TemporalPlanRow[] DirectionTemporalRows { get; private set; } = [];
    public MonthlyPlanPending[] DirectionMonthlyPending { get; private set; } = [];
    public DateOnly DirectionToday => WeekContract.LocalToday(HttpContext.RequestServices.GetRequiredService<IClock>().UtcNow);
    public PlanDailyLoad[] DirectionDailyLoad { get; private set; } = [];
    public PlanningSpecialistGap[] DirectionSpecialistGaps { get; private set; } = [];
    public PlanningAssignmentConflict? DirectionAssignmentConflict { get; private set; }
    public static string DirectionHours(decimal minutes)
    {
        var rounded = (long)Math.Ceiling(minutes);
        return rounded % 60 == 0 ? $"{rounded / 60} h" : $"{rounded / 60} h {rounded % 60:00} min";
    }
    private sealed record DirectionIntention(Guid Actor, Guid Key, DirectionPlanInput Input, DateTimeOffset ExpiresAt);
    private IDataProtector DirectionProtector => protection.CreateProtector("SGOL.direction-plans.intention.v1");
    public static string DirectionPeriodLabel(string code) => code switch
    { "SEMANAL" => "Semanal", "MENSUAL" => "Mensual", "TRIMESTRAL" => "Trimestral", "SEMESTRAL" => "Semestral", "ANUAL" => "Anual", "OCASIONAL" => "Ocasional", _ => "" };
    public static string DirectionIsoLabel(DateOnly date) => $"Semana {ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue))}, año ISO {ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue))}";

    private async Task<IActionResult> LoadDirectionPlansAsync(CancellationToken token)
    {
        var session = await sessionState.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        CanViewDirectionPlans = (session.RoleCode is CanonicalRole.Direction or CanonicalRole.Administration or CanonicalRole.Subcoordination) && session.Permissions.Contains(WeekAuthorization.View);
        CanPrepareDirectionPlan = session.RoleCode == CanonicalRole.Direction && session.Permissions.Contains(WeekAuthorization.View);
        CanPrepareWeeklyOperationsPlan = CanPreparePlan(session.RoleCode, true) && session.Permissions.Contains(WeekAuthorization.View);
        if (!CanViewDirectionPlans) { Response.StatusCode = 403; DirectionError = new("No tienes permiso para consultar estos planes", "", null); return Page(); }
        if(Guid.TryParse(Request.Query["planId"],out var processPlanId) && await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
            HttpContext.RequestServices.GetRequiredService<Sgol.Web.Infrastructure.Persistence.SgolDbContext>().Set<Sgol.Web.Infrastructure.Persistence.Planning.PreparedProcessStart>().Where(s=>s.PlanId==processPlanId),token))
            return Redirect($"/planificacion/procesos?planId={processPlanId:D}");
        try
        {
            if (CanPrepareDirectionPlan) await LoadClosingAsync(session.UserId,token);
            if (Request.Method == "GET")
            {
                string[] allowed = ["mode", "planId", "directionCursor", "closingFrom"];
                if (Request.Query.Any(q => !allowed.Contains(q.Key) || q.Value.Count != 1)) { Response.StatusCode = 400; DirectionError = new("Revisa el nombre, el área y el periodo del plan", "", null); return Page(); }
                DirectionOperations = Request.Query["mode"] == "operations";
                DirectionCreating = Request.Query["mode"] == "create" || DirectionOperations;
                if (DirectionCreating && !(DirectionOperations ? CanPrepareWeeklyOperationsPlan : CanPrepareDirectionPlan)) return StatusCode(403);
                if (Request.Query.ContainsKey("mode") && !DirectionCreating) { Response.StatusCode = 400; DirectionError = new("Revisa el nombre, el área y el periodo del plan", "", null); return Page(); }
                if (DirectionCreating && Request.Query.ContainsKey("planId")) return BadRequest();
            }
            if (DirectionCreating)
            {
                if (DirectionYear.Length == 0)
                {
                    var today = DirectionOperations ? DirectionPlanContract.OperationsStart(DirectionToday) : DirectionToday;
                    DirectionYear = ISOWeek.GetYear(today.ToDateTime(TimeOnly.MinValue)).ToString(CultureInfo.InvariantCulture);
                    DirectionNumber = ISOWeek.GetWeekOfYear(today.ToDateTime(TimeOnly.MinValue)).ToString(CultureInfo.InvariantCulture);
                }
                var options = await apiClient.SendAsync<DirectionPlanningTaskOption>(new(HttpMethod.Get, "/api/v1/direction-plans/task-options", ApiResponseShape.Collection), token);
                if (options.Status == 401) return Redirect("/acceso");
                if (!options.IsSuccess) SetDirectionError(options); else DirectionTaskOptions = options.Items ?? [];
                if (DirectionOperations && Request.Method == "GET")
                {
                    var existing = await HttpContext.RequestServices.GetRequiredService<SgolDbContext>().DirectionPreparedPlans.AsNoTracking()
                        .Where(p => p.IsWeeklyOperationsPlan && p.BranchId == Sgol.Organization.Contracts.BranchScope.LorettaId &&
                            p.Year == int.Parse(DirectionYear) && p.PeriodNumber == int.Parse(DirectionNumber))
                        .Select(p => (Guid?)p.Id).SingleOrDefaultAsync(token);
                    if (existing is { } masterId) return Redirect($"/planificacion?planId={masterId:D}");
                    DirectionName = $"Semana {int.Parse(DirectionNumber):00}";
                    DirectionArea = "Plan de Operaciones Semanal";
                    DirectionSelectedCodes = DirectionTaskOptions.Where(t => t.IsWeeklyOperationsTask).Select(t => t.TaskCode).ToArray();
                }
                if (int.TryParse(DirectionYear, out var year) && (DirectionPeriodType == "ANUAL" || int.TryParse(DirectionNumber, out _)))
                {
                    try { DirectionRange = DirectionPlanContract.Calculate(DirectionPeriodType, year, DirectionPeriodType == "ANUAL" ? null : int.Parse(DirectionNumber, CultureInfo.InvariantCulture), DirectionOperations ? DirectionToday : null); }
                    catch (DirectionPlanException) { }
                }
                return Page();
            }
            if (DirectionDetail is not null) {
                if (CanPrepareDirectionPlan) DirectionTaskOptions = await HttpContext.RequestServices.GetRequiredService<Sgol.Configuration.Contracts.IDirectionPlanningCatalogReader>().ReadTaskOptionsAsync(token);
                return Page();
            }
            if (Request.Query.ContainsKey("planId"))
            {
                if (!Guid.TryParseExact(Request.Query["planId"], "D", out var id) || id == Guid.Empty) return BadRequest();
                var read = await apiClient.SendAsync<DirectionPlanDetails>(new(HttpMethod.Get, $"/api/v1/direction-plans/{id:D}", ApiResponseShape.Item), token);
                if (read.Status == 401) return Redirect("/acceso");
                if (!read.IsSuccess) SetDirectionError(read); else if (read.Data is { BranchCode: "LOR-001" } detail && detail.PlanId == id) DirectionDetail = detail; else throw new ApiProtocolException();
                if (CanPrepareDirectionPlan && DirectionDetail is not null) DirectionTaskOptions = await HttpContext.RequestServices.GetRequiredService<Sgol.Configuration.Contracts.IDirectionPlanningCatalogReader>().ReadTaskOptionsAsync(token);
            }
            else
            {
                var paging = new MyWorkCursor(protection, session.UserId, "direction-prepared-plans", "active:20");
                if (!paging.Read(Request.Query.ContainsKey("directionCursor") ? Request.Query["directionCursor"].ToString() : null))
                { Response.StatusCode = 400; DirectionError = new("Revisa el nombre, el área y el periodo del plan", "", null); return Page(); }
                var read = await apiClient.SendAsync<DirectionPlanDetails>(new(HttpMethod.Get, "/api/v1/direction-plans" + (paging.Cursor is null ? "" : "?cursor=" + Uri.EscapeDataString(paging.Cursor)), ApiResponseShape.Collection), token);
                if (read.Status == 401) return Redirect("/acceso");
                if (!read.IsSuccess) SetDirectionError(read); else
                {
                    DirectionPlans = read.Items ?? [];
                    var links = paging.Links(read.NextCursor, value => value is null ? "/planificacion" : "/planificacion?directionCursor=" + Uri.EscapeDataString(value));
                    DirectionPreviousHref = links.PreviousHref; DirectionNextHref = links.NextHref;
                }
            }
        }
        catch (ApiProtocolException) { Response.StatusCode = 503; DirectionError ??= new("No se pudieron consultar los planes. Inténtalo de nuevo", "", null); }
        return Page();
    }

    public async Task<IActionResult> OnPostPrepareDirectionPlanAsync(CancellationToken token)
    {
        NoStore(); DirectionCreating = true;
        var session = await sessionState.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        if (!CanPreparePlan(session.RoleCode, true) || !session.Permissions.Contains(WeekAuthorization.View)) return StatusCode(403);
        var form = await DirectionFormAsync(token); if (form is null) return BadRequest();
        if (form["isWeeklyOperationsPlan"].Count > 1) return BadRequest();
        DirectionOperations = form["isWeeklyOperationsPlan"] == "true";
        if (!CanPreparePlan(session.RoleCode, DirectionOperations)) return StatusCode(403);
        DirectionName = form["name"].ToString(); DirectionArea = form["area"].ToString(); DirectionPeriodType = form["periodType"].ToString();
        DirectionYear = form["year"].ToString(); DirectionNumber = form["periodNumber"].ToString();
        DirectionSelectedCodes = form["taskCodes"].Select(c => c ?? "").ToArray();
        DirectionCapacityJson = form["capacityAdjustments"].ToString();
        DirectionCapacityFingerprint = form["capacityFingerprint"].ToString();
        try
        {
            if (DirectionFormFields.Any(f => form[f].Count != 1) ||
                !int.TryParse(DirectionYear, NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
                DirectionPeriodType != "ANUAL" && !int.TryParse(DirectionNumber, NumberStyles.None, CultureInfo.InvariantCulture, out _)) throw new DirectionPlanException("DIRECTION_PLAN_REQUEST_INVALID", 400);
            if (DirectionCapacityJson.Length > 131072) throw new DirectionPlanException("PLAN_CAPACITY_INVALID", 422);
            var adjustments = string.IsNullOrEmpty(DirectionCapacityJson) ? [] : JsonSerializer.Deserialize<PlanCapacityAdjustment[]>(DirectionCapacityJson, JsonSerializerOptions.Web);
            var input = DirectionPlanContract.Normalize(new(DirectionName, DirectionArea, DirectionPeriodType, year,
                DirectionPeriodType == "ANUAL" ? null : int.Parse(DirectionNumber, CultureInfo.InvariantCulture), DirectionSelectedCodes,
                adjustments, DirectionCapacityFingerprint, DirectionOperations));
            DirectionRange = DirectionPlanContract.Calculate(input.PeriodType, input.Year, input.PeriodNumber, input.IsWeeklyOperationsPlan ? DirectionToday : null);
            await LoadDirectionPlansAsync(token);
            if (DirectionError is not null) return Page();
            if (input.TaskCodes.Any(code => DirectionTaskOptions.All(t => t.TaskCode != code))) throw new DirectionPlanException("DIRECTION_PLAN_TASK_INVALID", 422);
            DirectionName = input.Name; DirectionArea = input.Area;
            var temporalState = await PlanningCapacity.ReadAsync(HttpContext.RequestServices.GetRequiredService<SgolDbContext>(),input,HttpContext.RequestServices.GetRequiredService<IClock>().UtcNow,token);
            if(!string.IsNullOrWhiteSpace(input.CapacityFingerprint) && input.CapacityFingerprint!=temporalState.Preview.Fingerprint) throw new DirectionPlanException("PLAN_CAPACITY_STALE",409);
            DirectionTemporalRows=temporalState.Preview.TemporalRows;
            DirectionSpecialistGaps = temporalState.Preview.SpecialistGaps;
            DirectionDailyLoad = await PlanningCapacity.DailyLoadAsync(HttpContext.RequestServices.GetRequiredService<SgolDbContext>(),
                input, temporalState.Preview, temporalState.Ledger, token);
            DirectionMonthlyPending = temporalState.Preview.MonthlyPending;
            DirectionTemporalRows = temporalState.Preview.TemporalRows;
            input=input with { CapacityFingerprint=temporalState.Preview.Fingerprint };
            if (input.CapacityAdjustments is { Count: > 0 })
            {
                var state = temporalState;
                if (state.Preview.Fingerprint != input.CapacityFingerprint) throw new DirectionPlanException("PLAN_CAPACITY_STALE", 409);
                var changes = PlanningCapacity.Changes(input, state.Preview, state.Ledger);
                DirectionCapacitySummary = changes.Select(c => $"{state.Preview.Workers.Single(w => w.PersonId == c.PersonId).Name}: {c.Percent}% de {c.DailyMinutes} min diarios · {c.From:dd/MM/yyyy} — {c.Through:dd/MM/yyyy}").ToArray();
            }
            DirectionPlanIntent = DirectionProtector.Protect(JsonSerializer.Serialize(new DirectionIntention(session.UserId, Guid.CreateVersion7(), input, DateTimeOffset.UtcNow.AddMinutes(30))));
            DirectionConfirming = true;
        }
        catch (JsonException) { Response.StatusCode = 422; DirectionError = new("Revisa los ajustes de carga de trabajo", "", null); }
        catch (DirectionPlanException e)
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<IndexModel>>()
                .LogWarning("Plan preparation rejected: {Code}", e.Code);
            SetDirectionError(new ApiResponse<object>(e.Status, null, null, "", null, null, null, false, e.Code, null));
            DirectionAssignmentConflict = e.Data[nameof(PlanningAssignmentConflict)] as PlanningAssignmentConflict;
        }
        return DirectionConfirming ? Page() : await LoadDirectionPlansAsync(token);
    }

    public async Task<IActionResult> OnPostSaveDirectionPlanAsync(CancellationToken token)
    {
        NoStore();
        var session = await sessionState.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        if (!CanPreparePlan(session.RoleCode, true) || !session.Permissions.Contains(WeekAuthorization.View)) return StatusCode(403);
        var form = await DirectionFormAsync(token); if (form is null) return BadRequest();
        DirectionIntention? intention;
        try
        {
            if (form["directionIntent"].Count != 1 || form["directionIntent"].ToString().Length > 131072) return BadRequest();
            intention = JsonSerializer.Deserialize<DirectionIntention>(DirectionProtector.Unprotect(form["directionIntent"].ToString()));
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { return BadRequest(); }
        if (intention is null || intention.Actor != session.UserId || intention.Key == Guid.Empty || intention.ExpiresAt <= DateTimeOffset.UtcNow) return BadRequest();
        DirectionPlanIntent = form["directionIntent"];
        var input = intention.Input;
        if (!CanPreparePlan(session.RoleCode, input.IsWeeklyOperationsPlan)) return StatusCode(403);
        DirectionOperations = input.IsWeeklyOperationsPlan;
        DirectionName = input.Name; DirectionArea = input.Area; DirectionPeriodType = input.PeriodType;
        DirectionYear = input.Year.ToString(CultureInfo.InvariantCulture); DirectionNumber = input.PeriodNumber?.ToString(CultureInfo.InvariantCulture) ?? "";
        DirectionSelectedCodes = input.TaskCodes;
        DirectionCapacityJson = JsonSerializer.Serialize(input.CapacityAdjustments ?? [], JsonSerializerOptions.Web);
        DirectionCapacityFingerprint = input.CapacityFingerprint ?? "";
        try
        {
            var saved = await antiforgery.SendValidatedAsync<DirectionPlanSaved>(HttpContext,
                new(HttpMethod.Post, "/api/v1/direction-plans", ApiResponseShape.Item, input, Intent: ApiMutationIntent.FromKey(intention.Key)), token);
            if (saved is null) return BadRequest();
            if (saved.Status == 401) return Redirect("/acceso");
            if (!saved.IsSuccess) { SetDirectionError(saved); if (saved.Status == 403) return Page(); DirectionCreating = true; }
            else if (saved.Data is { } result && result.Plan.PlanId != Guid.Empty && result.Plan.BranchCode == "LOR-001" && result.Result is "CREATED" or "RECOVERED")
            {
                return Redirect($"/planificacion?planId={result.Plan.PlanId:D}");
            }
            else throw new ApiProtocolException();
        }
        catch (ApiProtocolException)
        {
            Response.StatusCode = 503; DirectionCreating = DirectionConfirming = DirectionUncertain = true;
            DirectionError = new("No se pudo confirmar el guardado. Reenvía la misma solicitud para comprobar el resultado", "", null);
        }
        if (DirectionCreating)
        {
            try
            {
                var db = HttpContext.RequestServices.GetRequiredService<SgolDbContext>();
                var state = await PlanningCapacity.ReadAsync(db, input, HttpContext.RequestServices.GetRequiredService<IClock>().UtcNow, token);
                DirectionDailyLoad = await PlanningCapacity.DailyLoadAsync(db, input, state.Preview, state.Ledger, token);
                DirectionMonthlyPending = state.Preview.MonthlyPending;
                DirectionTemporalRows = state.Preview.TemporalRows;
            }
            catch (DirectionPlanException) { /* Keep the save error if the catalogue or capacity changed. */ }
        }
        return await LoadDirectionPlansAsync(token);
    }

    private async Task<IFormCollection?> DirectionFormAsync(CancellationToken token)
    {
        if (!Request.HasFormContentType) return null;
        try { await formAntiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return null; }
        return await Request.ReadFormAsync(token);
    }

    private void SetDirectionError<T>(ApiResponse<T> response)
    {
        Response.StatusCode = response.Status;
        var text = response.Status switch
        {
            403 => "No tienes permiso para preparar o consultar estos planes",
            404 => "El plan no existe o no está disponible",
            409 when response.ErrorCode == "PLAN_CAPACITY_STALE" => "La carga cambió. Abre Asignar cargas de trabajo y revisa los ajustes antes de guardar",
            409 when response.ErrorCode == "WEEKLY_OPERATIONS_PLAN_EXISTS" => "Ya existe el Plan de Operaciones Semanal para esta semana. Vuelve a planes y abre el existente.",
            409 when response.ErrorCode == "WEEKLY_OPERATIONS_CATALOG_CHANGED" => "Las tareas del maestro cambiaron. Vuelve a planes y abre Plan de Operaciones Semanal para cargar el catálogo vigente.",
            422 when response.ErrorCode == "WEEKLY_OPERATIONS_TASKS_REQUIRED" => "Marca al menos una tarea activa como parte del plan de operaciones semanal en Tareas.",
            422 when response.ErrorCode == "PLAN_NO_WORKERS" => "Registra al menos un trabajador activo con cuenta y rol antes de crear el plan",
            422 when response.ErrorCode == "PLAN_NO_WORKER_FOR_ROLE" => "No hay una persona activa para el rol asignado de alguna tarea. Revisa los roles de las tareas y las personas antes de crear el plan",
            422 when response.ErrorCode == "PLAN_NO_SPECIALIST" => "No hay una persona con la especialización requerida que haya terminado su periodo de prueba. Revisa Personas y accesos",
            422 when response.ErrorCode == "PLAN_NO_AVAILABLE_WORKER" => "Hay tareas sin personal disponible. Revisa la carga, los roles y los horarios por fecha",
            422 when response.ErrorCode == "PLAN_DISTINCT_WORKERS_REQUIRED" => "No hay suficientes personas distintas del rol para las repeticiones de la semana",
            422 when response.ErrorCode == "PLAN_OCCURRENCES_RESERVED" => "Estas repeticiones ya pertenecen a otro plan. Abre el plan existente para agregar tareas",
            422 when response.ErrorCode == "PLAN_ENDED" => "Ese periodo ya terminó. De lunes a viernes el plan comienza hoy; sábado y domingo comienza el lunes siguiente.",
            422 when response.ErrorCode == "PLAN_TASKS_REQUIRED" => "Agrega al menos una tarea que tenga repeticiones dentro del periodo",
            400 or 422 => "Revisa el nombre, el área y el periodo del plan",
            _ => "No se pudieron consultar los planes. Inténtalo de nuevo"
        };
        DirectionError = new(text, "", response.CorrelationId);
        if (response.Status == 403) { CanViewDirectionPlans = CanPrepareDirectionPlan = CanPrepareWeeklyOperationsPlan = false; DirectionTaskOptions = []; DirectionPlans = []; DirectionDetail = null; DirectionSelectedCodes = []; DirectionPlanIntent = null; DirectionConfirming = false; }
    }
    public async Task<IActionResult> OnPostDirectionCapacityAsync(CancellationToken token)
    {
        NoStore();
        var session = await sessionState.GetAsync(token);
        if (session is null) return StatusCode(401);
        if (!CanPreparePlan(session.RoleCode, true) || !session.Permissions.Contains(WeekAuthorization.View)) return StatusCode(403);
        var form = await DirectionFormAsync(token); if (form is null) return BadRequest();
        var operations = form["isWeeklyOperationsPlan"] == "true";
        if (form["isWeeklyOperationsPlan"].Count > 1) return BadRequest();
        if (!CanPreparePlan(session.RoleCode, operations)) return StatusCode(403);
        try
        {
            var service = HttpContext.RequestServices.GetRequiredService<IDirectionPreparedPlanService>();
            if (operations) await service.AuthorizeWeeklyOperationsAsync(session.UserId, token);
            else await service.AuthorizeAsync(session.UserId, token);
            if (!int.TryParse(form["year"], out var year) || form["periodType"] != "ANUAL" && !int.TryParse(form["periodNumber"], out _)) return BadRequest();
            var input = new DirectionPlanInput("Consulta de cargas", "Planificación", form["periodType"].ToString(), year,
                form["periodType"] == "ANUAL" ? null : int.Parse(form["periodNumber"]!, CultureInfo.InvariantCulture), form["taskCodes"].Select(c => c ?? "").ToArray(), IsWeeklyOperationsPlan: operations);
            var db = HttpContext.RequestServices.GetRequiredService<SgolDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token);
            var state = await PlanningCapacity.ReadAsync(db, input, HttpContext.RequestServices.GetRequiredService<IClock>().UtcNow, token);
            await tx.CommitAsync(token);
            return new JsonResult(state.Preview);
        }
        catch (DirectionPlanException e) { return new JsonResult(new { message = "Revisa las tareas y el periodo del plan" }) { StatusCode = e.Status }; }
    }
    public async Task<IActionResult> OnPostAddDirectionTasksAsync(Guid planId, string[] taskCodes, CancellationToken token)
    {
        NoStore(); var session = await sessionState.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        if (session.RoleCode != CanonicalRole.Direction) return StatusCode(403);
        if (await DirectionFormAsync(token) is null) return BadRequest();
        try {
            await HttpContext.RequestServices.GetRequiredService<Sgol.Web.Infrastructure.Persistence.Planning.OperationalSupervisionService>().AddTasksAsync(session.UserId, planId, taskCodes, token);
            return Redirect($"/planificacion?planId={planId:D}");
        } catch (DirectionPlanException e) {
            if (e.Status == 403) return StatusCode(403);
            DirectionDetail = await HttpContext.RequestServices.GetRequiredService<IDirectionPreparedPlanService>().ReadAsync(session.UserId, planId, token);
            Response.StatusCode = e.Status;
            DirectionError = new(e.Code == "PLAN_ENDED" ? "Este plan ya terminó." : Pages.Validations.IndexModel.Message(e.Code), "", null);
            return await LoadDirectionPlansAsync(token);
        }
    }
}
