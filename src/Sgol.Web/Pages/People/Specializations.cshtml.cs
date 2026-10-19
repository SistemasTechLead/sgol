using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Pages.People;

public sealed class SpecializationsModel(IRazorSessionState sessions, SpecializationService specializations) : PageModel
{
    [BindProperty] public Specialization Input { get; set; } = new();
    public List<SpecializationView> Items { get; private set; } = [];
    public bool Editing { get; private set; }
    public string? Error { get; private set; }
    private async Task<Guid?> ActorAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var session = await sessions.GetAsync(ct);
        return session?.RoleCode == CanonicalRole.Direction ? session.UserId : null;
    }
    public async Task<IActionResult> OnGetAsync(Guid? edit, bool create, CancellationToken ct)
    {
        var actor = await ActorAsync(ct); if (actor == null) return StatusCode(403);
        try {
            Items = await specializations.ListAsync(actor.Value, ct);
            if (edit != null) { Input = Items.SingleOrDefault(s => s.Item.Id == edit)?.Item ?? throw new DirectionPlanException("SPECIALIZATION_NOT_FOUND", 404); Editing = true; }
            else Editing = create;
            return Page();
        } catch (DirectionPlanException e) { return StatusCode(e.Status); }
    }
    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        var actor = await ActorAsync(ct); if (actor == null) return StatusCode(403);
        try {
            ModelState.Remove("Input.NormalizedName");
            if (!ModelState.IsValid) throw new DirectionPlanException("SPECIALIZATION_INVALID", 422);
            await specializations.SaveAsync(actor.Value, Input, ct); return RedirectToPage();
        } catch (DirectionPlanException e) {
            if (e.Status == 403) return StatusCode(403);
            Response.StatusCode = e.Status; Error = SpecializationService.Message(e.Code); Editing = true;
            Items = await specializations.ListAsync(actor.Value, ct); return Page();
        }
    }
}
