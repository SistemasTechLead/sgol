using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.MyWork;

[RequestSizeLimit(16777216)]
public sealed class ProcessesModel(IRazorSessionState sessions,OperationalProcessService processes,OperationalTaskService tasks) : PageModel
{
    public ProcessRunView? View { get; private set; }
    public OperationalSequenceRun[] Runs { get; private set; }=[];
    public Guid PersonId { get; private set; }
    public string? Error { get; private set; }
    [BindProperty] public Guid RunId { get; set; }
    [BindProperty] public Guid AttemptId { get; set; }
    [BindProperty] public long Revision { get; set; }
    [BindProperty] public Guid Key { get; set; }=Guid.CreateVersion7();
    [BindProperty] public string Decision { get; set; }="";
    [BindProperty] public string Conclusion { get; set; }="";
    [BindProperty] public string? Differences { get; set; }
    [BindProperty] public bool Declared { get; set; }
    [BindProperty] public int? ItemQuantity { get; set; }
    [BindProperty] public string? CapacityResolution { get; set; }
    public string? ItemConflict { get; private set; }
    public bool CanForce { get; private set; } = true;
    public bool CanSearch { get; private set; } = true;
    [BindProperty] public bool UsePreservedWork { get; set; }
    [BindProperty] public string? CapacityFingerprint { get; set; }
    [BindProperty] public Guid? CaptureSessionId { get; set; }
    [BindProperty] public string? PhotographIds { get; set; }
    [BindProperty] public List<IFormFile>? Screenshots { get; set; }
    [BindProperty] public string? ChecklistAnswer { get; set; }
    [BindProperty] public IFormFile? Document { get; set; }
    private async Task<SessionSnapshot?> Session(CancellationToken token)
    { Response.Headers.CacheControl="no-store"; var session=await sessions.GetAsync(token); PersonId=session?.PersonId??Guid.Empty; return session; }
    public async Task<IActionResult> OnGetAsync(Guid? id,CancellationToken token)
    {
        var session=await Session(token); if(session==null) return Redirect("/acceso");
        try { if(id==null) Runs=await processes.MineAsync(session.UserId,token); else { View=await processes.ReadAsync(session.UserId,id.Value,token); RunId=id.Value;
            var stage=View.Stages.Single(s=>s.Position==View.Run.ActivePosition); Differences=View.Attempts.SingleOrDefault(a=>a.Id==stage.CurrentAttemptId)?.UnresolvedDifferences;
        } return Page(); }
        catch(DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public async Task<IActionResult> OnPostDecideAsync(CancellationToken token)
    {
        var session=await Session(token); if(session==null) return Redirect("/acceso");
        try {
            if (!ModelState.IsValid) throw new DirectionPlanException("PROCESS_ITEM_QUANTITY_INVALID",422);
            Guid[]? photoIds = null;
            try { if (PhotographIds != null && PhotographIds.Length <= 1000) photoIds=System.Text.Json.JsonSerializer.Deserialize<Guid[]>(PhotographIds); }
            catch (System.Text.Json.JsonException) { throw new DirectionPlanException("TASK_PHOTO_LIMIT",422); }
            if (Request.Form.Files.Count>21 || Request.Form.Files.Count(f=>f.Name=="Document")>1 || Request.Form.Files.Any(f=>f.Name is not ("Document" or "screenshots"))) throw new DirectionPlanException("TASK_FILE_INVALID",422);
            await processes.DecideAsync(session.UserId,RunId,AttemptId,Revision,Key,Decision,Conclusion,Differences,Document,Declared,token,CaptureSessionId,photoIds,Screenshots?.Count>0?Screenshots:null,ChecklistAnswer,itemQuantity:ItemQuantity,capacityResolution:CapacityResolution,capacityFingerprint:CapacityFingerprint,usePreservedWork:UsePreservedWork); return RedirectToPage(new { id=RunId, notice="saved" }); }
        catch(DirectionPlanException e) { ItemConflict=e.Data["ItemConflict"] as string; CapacityFingerprint=e.Data["CapacityFingerprint"] as string; CanForce=e.Data["CanForce"] is not false; CanSearch=e.Data["CanSearch"] is not false; Response.StatusCode=e.Status; Error=Sgol.Web.Pages.Tasks.IndexModel.Message(e.Code); View=await processes.ReadAsync(session.UserId,RunId,token); return Page(); }
    }
    public async Task<IActionResult> OnPostReturnPendingCountAsync(Guid stageId,long stageRevision,string reason,CancellationToken token)
    {
        var session=await Session(token);if(session==null)return Redirect("/acceso");
        try { if(new[]{"RunId","stageId","stageRevision","Key","reason"}.Any(k=>ModelState.TryGetValue(k,out var value)&&value.Errors.Count>0))throw new DirectionPlanException("PROCESS_ITEM_RETURN_REASON_REQUIRED",422);
            await processes.ReturnPendingCountAsync(session.UserId,RunId,stageId,stageRevision,Key,reason,token);
            return RedirectToPage(new{id=RunId,notice="returned-count"});
        } catch(DirectionPlanException e) { Response.StatusCode=e.Status;Error=Sgol.Web.Pages.Tasks.IndexModel.Message(e.Code);View=await processes.ReadAsync(session.UserId,RunId,token);return Page(); }
    }
    public async Task<IActionResult> OnGetPreservedPhotoAsync(Guid currentAttemptId, Guid photoId, CancellationToken token)
    {
        var session=await Session(token); if(session==null) return Redirect("/acceso");
        try { var photo=await processes.PreservedPhotoAsync(session.UserId,currentAttemptId,photoId,token); return File(photo.Content,photo.ContentType); }
        catch(DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public async Task<IActionResult> OnGetDocumentAsync(Guid id,Guid documentId,CancellationToken token)
    {
        var session=await Session(token); if(session==null) return Redirect("/acceso");
        try { var view=await processes.ReadAsync(session.UserId,id,token); var doc=view.Documents.SingleOrDefault(d=>d.Id==documentId); if(doc==null) return NotFound(); var file=await tasks.FileAsync(session.UserId,doc.AssignmentId,token); return File(file.FileContent!,file.ContentType!,file.FileName); }
        catch(DirectionPlanException e) { return StatusCode(e.Status); }
    }
}
