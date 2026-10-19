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
        try { if(planId!=null) Plan=await plans.ReadAsync(actor.Value,planId.Value,token); else { if(!CanManage) return StatusCode(403); Input.ProcessId=processId??Guid.Empty; await Load(actor.Value,token); } return Page(); }
        catch(DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken token)
    {
        var actor=await Actor(token,true); if(actor==null) return StatusCode(403);
        try { Preview=await plans.PreviewAsync(actor.Value,Input,token); Input.Fingerprint=Preview.Fingerprint; ModelState.Remove("Input.Fingerprint"); }
        catch(DirectionPlanException e) { Response.StatusCode=e.Status; Error=Sgol.Web.Pages.Tasks.IndexModel.Message(e.Code); }
        await Load(actor.Value,token); return Page();
    }
    public async Task<IActionResult> OnPostPrepareAsync(CancellationToken token)
    {
        var actor=await Actor(token,true); if(actor==null) return StatusCode(403);
        try { var id=await plans.PrepareAsync(actor.Value,Input,token); return RedirectToPage(new { planId=id }); }
        catch(DirectionPlanException e) { Response.StatusCode=e.Status; Error=Sgol.Web.Pages.Tasks.IndexModel.Message(e.Code); await Load(actor.Value,token); return Page(); }
    }
    public async Task<IActionResult> OnPostActivateAsync(Guid planId,CancellationToken token)
    {
        var actor=await Actor(token,true); if(actor==null) return StatusCode(403);
        try { await plans.ActivateAsync(actor.Value,planId,token); return RedirectToPage(new { planId }); }
        catch(DirectionPlanException e) { Response.StatusCode=e.Status; Error=Sgol.Web.Pages.Tasks.IndexModel.Message(e.Code); Plan=await plans.ReadAsync(actor.Value,planId,token); return Page(); }
    }
}
