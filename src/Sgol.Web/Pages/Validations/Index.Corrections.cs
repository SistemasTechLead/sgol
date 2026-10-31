using Microsoft.AspNetCore.Mvc;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Pages.Validations;

public sealed record ReassignmentScheduleModel(string Prefix, CorrectionScheduleForm Value, DateOnly Today);
public sealed class CorrectionScheduleForm
{
    public string Mode { get; set; } = "ASAP";
    public DateOnly? From { get; set; }
    public DateOnly? Through { get; set; }
    public CorrectionSchedule Value() => new(Mode, Mode == "CUSTOM" ? From : null, Mode == "CUSTOM" ? Through ?? From : null);
}

public sealed partial class IndexModel
{
    [BindProperty] public CorrectionScheduleForm Schedule { get; set; } = new();
    [BindProperty] public DateTimeOffset PreviewAt { get; set; }
    [BindProperty] public string? ReviewFingerprint { get; set; }
    public ReviewProposal? Proposal { get; private set; }
    public Dictionary<Guid, string> CorrectionConflicts { get; private set; } = [];
    public Dictionary<Guid, string> CorrectionNames { get; private set; } = [];

    private bool HasReviewBindingErrors() => ModelState.Any(e =>
    {
        if (e.Value!.Errors.Count == 0) return false;
        var schedule = e.Key.StartsWith("Schedule.", StringComparison.OrdinalIgnoreCase) || e.Key.Contains(".Schedule.", StringComparison.OrdinalIgnoreCase);
        if (schedule && (e.Key.EndsWith(".From", StringComparison.OrdinalIgnoreCase) || e.Key.EndsWith(".Through", StringComparison.OrdinalIgnoreCase)) &&
            Request.Form[e.Key[..e.Key.LastIndexOf('.')] + ".Mode"] != "CUSTOM") return false;
        return schedule || e.Key.StartsWith("Reviews[", StringComparison.OrdinalIgnoreCase) || e.Key is "id" or "revision" or "PreviewAt";
    });

    private async Task ReviewActionAsync(Guid actor, SupervisionReview[] reviews, bool apply, CancellationToken token)
    {

        if (HasReviewBindingErrors()) throw new DirectionPlanException("CORRECTION_RANGE_INVALID", 422);
        if (apply) await supervision.ApplyReviewsAsync(actor, reviews, PreviewAt, ReviewFingerprint ?? "", token);
        else
        {
            Proposal = await supervision.PreviewReviewsAsync(actor, reviews, token);
            PreviewAt = Proposal.At; ReviewFingerprint = Proposal.Fingerprint;
            ModelState.Remove(nameof(PreviewAt)); ModelState.Remove(nameof(ReviewFingerprint));
            if (Proposal.Items.Any(i => i.Error != null)) Error = "No se guardó ninguna revisión. Revisa los avisos junto a cada tarea.";
        }
    }

    public async Task<IActionResult> OnPostRescheduleAsync(Guid id, long revision, string? comment, string? stage, CancellationToken token)
    {
        Guid? actor = null;
        try
        {
            actor = await ActorAsync(token); if (actor == null) return Redirect("/acceso");
            AttemptedComment = comment; AttemptedDecision = "REPROGRAMAR";
            await ReviewActionAsync(actor.Value, [new(id, revision, "REPROGRAMAR", comment, Schedule.Value())], stage == "apply", token);
            if (stage == "apply") return Redirect(TaskHref(id));
            await LoadAsync(actor.Value, id, token); return Page();
        }
        catch (DirectionPlanException e)
        {
            if (actor == null || e.Status == 403) return StatusCode(e.Status);
            Error = Message(e.Code); Response.StatusCode = e.Status;
            await LoadAsync(actor.Value, id, token); return Page();
        }
    }
}
