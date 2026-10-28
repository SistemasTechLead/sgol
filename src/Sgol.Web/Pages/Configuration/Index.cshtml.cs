using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Web.Presentation.Navigation;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence.Identity;

namespace Sgol.Web.Pages.Configuration;

public sealed class IndexModel(IRazorSessionState sessionState, PersonAvatarService avatars) : PageModel
{
    public SessionSnapshot CurrentSession { get; private set; } = null!;
    [BindProperty] public string? AvatarVariant { get; set; }
    [BindProperty] public int AvatarPalette { get; set; }
    [BindProperty] public int AvatarSeed { get; set; }
    [TempData] public string? AvatarMessage { get; set; }

    private async Task<bool> LoadAsync(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var session = await sessionState.GetAsync(token);
        if (session is null) return false;
        CurrentSession = session;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Redirect("/acceso");
        var avatar = await avatars.FindAsync(CurrentSession.PersonId, cancellationToken);
        AvatarVariant = avatar?.Variant; AvatarPalette = avatar?.Palette ?? 0; AvatarSeed = avatar?.Seed ?? 0;
        return Page();
    }

    public async Task<IActionResult> OnPostAvatarAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return Redirect("/acceso");
        if (!ModelState.IsValid) return Page();
        try { await avatars.SaveAsync(CurrentSession, AvatarVariant, AvatarPalette, AvatarSeed, cancellationToken); }
        catch (ArgumentException e) { ModelState.AddModelError("", e.Message); return Page(); }
        AvatarMessage = AvatarVariant is null ? "Se guardó tu letra de usuario." : "Tu avatar se guardó y se mostrará también en el tablero.";
        return RedirectToPage();
    }
}
