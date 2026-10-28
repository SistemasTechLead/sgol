using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Tasks;

[RequestSizeLimit(33554432)]
public sealed class AssignModel(IRazorSessionState sessions, OperationalTaskService tasks, SgolDbContext db, IClock clock) : PageModel
{
    [BindProperty] public Guid Key { get; set; }
    [BindProperty] public AssignmentForm Input { get; set; } = new();
    [BindProperty] public List<IFormFile> Material { get; set; } = [];
    public OperationalTask? TaskDefinition { get; private set; }
    public CapacityWorker[] Workers { get; private set; } = [];
    public DirectionPreparedPlan[] Plans { get; private set; } = [];
    public OccasionSchedulePreview? SchedulePreview { get; private set; }
    public bool LegacyPlan => Input.PlanId is {} id && Plans.Any(p => p.Id == id && p.SchedulingPolicyId == null);
    public string? Error { get; private set; }
    public bool IsScopedAssignment { get; private set; }
    public string CancelUrl => IsScopedAssignment ? "/asignar-tarea" : "/tareas";

    public sealed class AssignmentForm
    {
        public Guid TaskId { get; set; }
        public long TaskRevision { get; set; }
        public string Name { get; set; } = "";
        public string Instructions { get; set; } = "";
        public string PublicationText { get; set; } = "";
        public string Channels { get; set; } = "";
        public DateTime StartsAt { get; set; }
        public DateTime DueAt { get; set; }
        public Guid[] PersonIds { get; set; } = [];
        public bool Automatic { get; set; }
        public Guid? PlanId { get; set; }
        public string? SchedulingFingerprint { get; set; }
        public DateTimeOffset? SchedulingNotBefore { get; set; }
    }

    private async Task<Guid?> ActorAsync(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        var session = await sessions.GetAsync(token);
        IsScopedAssignment = session?.RoleCode is CanonicalRole.Subcoordination or CanonicalRole.Administration;
        return session is not null && (session.RoleCode == CanonicalRole.Direction ||
            IsScopedAssignment && Request.Path.StartsWithSegments("/asignar-tarea")) ? session.UserId : null;
    }

    private async Task LoadAsync(Guid actor, Guid taskId, CancellationToken token)
    {
        TaskDefinition = (await tasks.OccasionalTasksAsync(actor, token)).SingleOrDefault(t => t.Id == taskId)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        Workers = await tasks.OccasionalRecipientsAsync(actor, taskId, token);
        if (IsScopedAssignment) return;
        var today = WeekContract.LocalToday(clock.UtcNow);
        Plans = await db.DirectionPreparedPlans.AsNoTracking().Where(p => p.BranchId == BranchScope.LorettaId && p.Status == "ASIGNADO" && p.PeriodType != "OCASIONAL" && p.EndsOn >= today)
            .OrderByDescending(p => p.StartsOn).ToArrayAsync(token);
    }

    public async Task<IActionResult> OnGetAsync(Guid taskId, Guid? planId, CancellationToken token)
    {
        var actor = await ActorAsync(token); if (actor == null) return StatusCode(403);
        try {
            await LoadAsync(actor.Value, taskId, token);
            var start = TimeZoneInfo.ConvertTime(clock.UtcNow, OperationalSchedule.Zone).DateTime;
            start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0).AddMinutes(5);
            Input = new() { TaskId = taskId, TaskRevision = TaskDefinition!.Revision, Name = TaskDefinition.Name.Length > 120 ? TaskDefinition.Name[..120] : TaskDefinition.Name,
                StartsAt = start, DueAt = start.AddHours(TaskDefinition.DeadlineHours), PlanId = planId };
            Key = Guid.CreateVersion7(); return Page();
        } catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }

    private async Task<OccasionInput> ValueAsync(CancellationToken token)
    {
        var starts = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Input.StartsAt, DateTimeKind.Unspecified), OperationalSchedule.Zone));
        var policy = Input.PlanId is {} id ? (await db.DirectionPreparedPlans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, token))?.SchedulingPolicyId : (await OperationalSchedulingPolicyService.CurrentAsync(db, token)).Id;
        var margin = policy is {} pid ? (await db.Set<OperationalSchedulingPolicy>().AsNoTracking().SingleAsync(p => p.Id == pid, token)).MarginPercent : (int?)null;
        var due = margin is {} percent ? starts.AddMinutes(OperationalAgenda.Duration(TaskDefinition!.LoadMinutes, percent)) : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Input.DueAt, DateTimeKind.Unspecified), OperationalSchedule.Zone));
        return new(Input.TaskId, Input.TaskRevision, Input.Name, Input.Instructions, Input.PublicationText, Input.Channels, starts, due, Input.PersonIds, Input.Automatic, Input.PlanId, Input.SchedulingFingerprint, Input.SchedulingNotBefore);
    }
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken token)
    {
        var actor = await ActorAsync(token); if (actor == null) return StatusCode(403);
        ModelState.Remove("Input.PublicationText"); ModelState.Remove("Input.Channels"); ModelState.Remove("Input.SchedulingFingerprint");
        try {
            await LoadAsync(actor.Value, Input.TaskId, token);
            if (!ModelState.IsValid) throw new DirectionPlanException("OCCASION_INVALID", 422);
            SchedulePreview = await tasks.PreviewOccasionAsync(actor.Value, await ValueAsync(token), token);
            Input.SchedulingFingerprint = SchedulePreview.Fingerprint; Input.SchedulingNotBefore = SchedulePreview.NotBefore;
            ModelState.Remove("Input.SchedulingFingerprint"); ModelState.Remove("Input.SchedulingNotBefore");
            return Page();
        } catch (DirectionPlanException e) { Error = IndexModel.Message(e.Code); Response.StatusCode = e.Status; return Page(); }
        catch (ArgumentException) { Error = "Revisa la activación de Ciudad de México."; Response.StatusCode = 422; return Page(); }
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken token)
    {
        var actor = await ActorAsync(token); if (actor == null) return StatusCode(403);
        // Caption and channel are optional for generic occasional work.
        ModelState.Remove("Input.PublicationText"); ModelState.Remove("Input.Channels");
        try {
            if (!ModelState.IsValid || Request.Form.Files.Any(f => f.Name != "Material")) throw new DirectionPlanException("OCCASION_INVALID", 422);
            await LoadAsync(actor.Value, Input.TaskId, token);
            var plan = await tasks.AssignOccasionAsync(actor.Value, Key, await ValueAsync(token), Material, token);
            if (IsScopedAssignment) { TempData["OccasionalAssigned"] = "Tarea asignada. Cada destinatario la verá en Mi trabajo."; return Redirect("/asignar-tarea"); }
            return Redirect($"/planificacion?planId={plan:D}");
        } catch (DirectionPlanException e) {
            if (e.Status is 403 or 404) return StatusCode(e.Status);
            Error = IndexModel.Message(e.Code); Response.StatusCode = e.Status;
            await LoadAsync(actor.Value, Input.TaskId, token); return Page();
        } catch (ArgumentException) {
            Error = "Revisa las fechas y horas de Ciudad de México."; Response.StatusCode = 422;
            await LoadAsync(actor.Value, Input.TaskId, token); return Page();
        }
    }
}
