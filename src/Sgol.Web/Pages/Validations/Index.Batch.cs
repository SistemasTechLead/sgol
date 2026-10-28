using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.Validations;

[RequestFormLimits(ValueCountLimit = 6000)]
public sealed partial class IndexModel
{
    [BindProperty(SupportsGet = true)] public bool Validation { get; set; }
    [BindProperty(SupportsGet = true)] public string Section { get; set; } = "pending";
    [BindProperty] public List<ReviewCardForm> Reviews { get; set; } = [];
    [TempData] public string? ReviewSuccess { get; set; }
    public Dictionary<Guid, Guid[]> CardPhotos { get; private set; } = [];
    public Guid? FailedReviewId { get; private set; }
    public SupervisionSection ValidationSection => Sections.FirstOrDefault(s => s.Key == Section) ?? Sections[0];
    public Dictionary<string, string> ValidationRoutes => Request.Query
        .Where(q => q.Key is not ("handler" or "task"))
        .ToDictionary(q => q.Key, q => q.Value.ToString());
    public string ViewHref(bool validation) => QueryHelpers.AddQueryString("/validaciones", Request.Query
        .Where(q => q.Key is not ("task" or "handler" or "Validation"))
        .ToDictionary(q => q.Key, q => (string?)q.Value.ToString())
        .Append(new KeyValuePair<string, string?>("Validation", validation.ToString())).ToDictionary());

    public async Task<IActionResult> OnPostBatchReviewAsync(CancellationToken token)
    {
        Guid? actor = null;
        Validation = true;
        try
        {
            actor = await ActorAsync(token);
            if (actor is null) return Redirect("/acceso");
            if (!ModelState.IsValid) throw new DirectionPlanException("REVIEW_BATCH_INVALID", 422);
            var selected = Reviews.Where(r => !string.IsNullOrEmpty(r.Result)).ToArray();
            if (selected.Any(r => r.Result is not ("yes" or "no") || r.Result == "no" &&
                r.Action is not ("MISMA_PERSONA" or "ROL" or "NO_SATISFECHA"))) throw new DirectionPlanException("REVIEW_INVALID", 422);
            await supervision.ReviewBatchAsync(actor.Value, selected.Select(r => new SupervisionReview(r.Id, r.Revision,
                r.Result == "yes" ? "CONCLUIDA" : r.Action, r.Comment)).ToArray(), token);
            ReviewSuccess = $"Se guardó la revisión de {selected.Length} tareas.";
            return Redirect(ViewHref(true));
        }
        catch (DirectionPlanException e)
        {
            if (actor is null || e.Status == 403) return StatusCode(e.Status);
            Response.StatusCode = e.Status;
            FailedReviewId = e.Data["ReviewAssignmentId"] as Guid?;
            await LoadAsync(actor.Value, null, token);
            var failed = Sections.SelectMany(s => s.All).FirstOrDefault(t => t.Assignment.Id == FailedReviewId);
            Error = "No se guardó ninguna revisión del grupo. " +
                (failed is null ? "" : failed.Assignment.Name + ": ") + Message(e.Code);
            return Page();
        }
    }
}

public sealed class ReviewCardForm
{
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public string? Result { get; set; }
    public string Action { get; set; } = "MISMA_PERSONA";
    public string? Comment { get; set; }
}
