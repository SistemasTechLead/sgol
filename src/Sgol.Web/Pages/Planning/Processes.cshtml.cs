using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.Planning;

public sealed partial class ProcessesModel(IRazorSessionState sessions,OperationalProcessCatalogService catalog,ProcessPlanningService plans,
    OperationalTaskService tasks, IDirectionPreparedPlanService direction, Microsoft.AspNetCore.DataProtection.IDataProtectionProvider protection) : PageModel
{
    [BindProperty] public ProcessPlanInput Input { get; set; }=new() { From=WeekContract.LocalToday(DateTimeOffset.UtcNow),Through=WeekContract.LocalToday(DateTimeOffset.UtcNow) };
    public OperationalSequence[] Options { get; private set; }=[];
    public ProcessEditor? Editor { get; private set; }
    public CapacityWorker[] Workers { get; private set; }=[];
    public ProcessPlanPreview? Preview { get; private set; }
    public ProcessPlanView? Plan { get; private set; }
    public bool CanManage { get; private set; }
    public string? Error { get; private set; }
    public int[] AutomaticPositions { get; private set; }=[];
    private async Task<Guid?> Actor(CancellationToken token,bool write)
    {
        Response.Headers.CacheControl="no-store"; var session=await sessions.GetAsync(token); CanManage=session?.RoleCode==CanonicalRole.Direction;
        return session!=null && (CanManage || !write && session.RoleCode==CanonicalRole.Administration)?session.UserId:null;
    }
    private async Task Load(Guid actor,CancellationToken token)
    {
        Options=(await catalog.ListAsync(actor,token)).Where(p=>!p.IsGeneral && p.PublishedVersionId!=null && p.Enabled).ToArray();
        if(Input.ProcessId!=Guid.Empty) { Editor=await catalog.ReadPublishedAsync(actor,Input.ProcessId,token); if(CanManage) Workers=await tasks.OccasionWorkersAsync(actor,token); }
    }
    public async Task<IActionResult> OnGetAsync(Guid? processId,Guid? planId,CancellationToken token)
    {
        var actor=await Actor(token,false); if(actor==null) return StatusCode(403);
        try { if(planId!=null) Plan=await plans.ReadAsync(actor.Value,planId.Value,token); else { if(!CanManage) return StatusCode(403); Input.ProcessId=processId??Guid.Empty; await Load(actor.Value,token); if(Editor != null) { Input.Mode=Editor.Configuration?.StartKind == "OCASIONAL" ? "OCASION" : "CALENDARIO"; Input.Name=("Plan de " + Editor.Process.Name); if(Input.Name.Length>120) Input.Name=Input.Name[..120]; } } return Page(); }
        catch(DirectionPlanException e) { return StatusCode(e.Status); }
    }
    private void ValidateInput()
    {
        if (!ModelState.IsValid) throw new DirectionPlanException(ModelState.TryGetValue("Input.OccasionTime", out var time) && time.Errors.Count > 0 ? "PROCESS_PLAN_TIME_REQUIRED" : "PROCESS_INVALID", 422);
    }
    public static string PlanError(DirectionPlanException error)
    {
        if (error.Data["CandidateFailures"] is DirectionPlanException[] failures)
            return string.Join(" ", failures.Select(PlanError).Distinct());
        var reason = error.Data["ConflictReason"] as string ?? error.Code;
        var message = Sgol.Web.Pages.Validations.IndexModel.Message(reason);
        if (reason.StartsWith("SCHEDUL", StringComparison.Ordinal)) message += " Elige otra hora o responsable y vuelve a revisar fechas y capacidad.";
        if (error.Data["StagePosition"] is not {} position) return message;
        var prefix = $"Etapa {position} ({error.Data["StageName"]})";
        if (error.Data["PersonName"] is string person) prefix += $" · {person}";
        if (error.Data["ConflictDay"] is DateOnly day) message += $" Fecha: {day:dd/MM/yyyy}.";
        if (error.Data["EvaluatedTime"] is string evaluated) message += $" Hora evaluada: {evaluated}.";
        if (error.Data["CurrentMinutes"] is decimal current && error.Data["CapacityMinutes"] is decimal capacity)
            message += $" Carga computada: {current:0.##} min; capacidad: {capacity:0.##} min; esta etapa añade {error.Data["RequiredMinutes"]} min.";
        if (error.Data["WorkHours"] is string hours && hours.Length > 0) message += $" Horario: {hours} (Ciudad de México).";
        return prefix + ": " + message;
    }
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken token)
    {
        var actor=await Actor(token,true); if(actor==null) return StatusCode(403);
        try { ValidateInput(); AutomaticPositions=Input.People.Where(p=>p.Value==Guid.Empty).Select(p=>p.Key).ToArray(); Preview=await plans.PreviewAsync(actor.Value,Input,token); Input.Fingerprint=Preview.Fingerprint; ModelState.Remove("Input.Fingerprint"); }
        catch(DirectionPlanException e) { Response.StatusCode=e.Status; Error=PlanError(e); }
        await Load(actor.Value,token); return Page();
    }
    public async Task<IActionResult> OnPostPrepareAsync(CancellationToken token)
    {
        var actor=await Actor(token,true); if(actor==null) return StatusCode(403);
        try { ValidateInput(); var id=await plans.PrepareAsync(actor.Value,Input,token); return RedirectToPage(new { planId=id }); }
        catch(DirectionPlanException e) { Response.StatusCode=e.Status; Error=PlanError(e); await Load(actor.Value,token); return Page(); }
    }
    public async Task<IActionResult> OnPostActivateAsync(Guid planId,CancellationToken token)
    {
        var actor=await Actor(token,true); if(actor==null) return StatusCode(403);
        try { await plans.ActivateAsync(actor.Value,planId,token); return RedirectToPage(new { planId }); }
        catch(DirectionPlanException e) { Response.StatusCode=e.Status; Error=Sgol.Web.Pages.Tasks.IndexModel.Message(e.Code); Plan=await plans.ReadAsync(actor.Value,planId,token); return Page(); }
    }
}
