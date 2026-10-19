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
        try { await processes.DecideAsync(session.UserId,RunId,AttemptId,Revision,Key,Decision,Conclusion,Differences,Document,Declared,token); return RedirectToPage(new { id=RunId }); }
        catch(DirectionPlanException e) { Response.StatusCode=e.Status; Error=Sgol.Web.Pages.Tasks.IndexModel.Message(e.Code); View=await processes.ReadAsync(session.UserId,RunId,token); return Page(); }
    }
    public async Task<IActionResult> OnGetDocumentAsync(Guid id,Guid documentId,CancellationToken token)
    {
        var session=await Session(token); if(session==null) return Redirect("/acceso");
        try { var view=await processes.ReadAsync(session.UserId,id,token); var doc=view.Documents.SingleOrDefault(d=>d.Id==documentId); if(doc==null) return NotFound(); var file=await tasks.FileAsync(session.UserId,doc.AssignmentId,token); return File(file.FileContent!,file.ContentType!,file.FileName); }
        catch(DirectionPlanException e) { return StatusCode(e.Status); }
    }
}
