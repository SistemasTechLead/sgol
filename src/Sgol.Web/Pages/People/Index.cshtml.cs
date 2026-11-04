using Sgol.Web.Presentation.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Pages.People;

public sealed class IndexModel(IRazorSessionState sessions, UserProfileService profiles, SpecializationService specializations, PersonalScheduleService schedules) : PageModel
{
    [BindProperty] public PersonalScheduleInput Schedule { get; set; } = new();
    public SchedulePreview? SchedulePreview { get; private set; }
    public bool ShowSchedule => Editing && Input.Id != Guid.Empty;
    public static readonly string[] DayNames = ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? State { get; set; }
    [BindProperty] public UserProfileInput Input { get; set; } = new();
    public IReadOnlyList<UserProfile> Profiles { get; private set; } = [];
    public IReadOnlyList<UserProfile> RotationPeople { get; private set; } = [];
    public Guid CurrentUserId { get; private set; }
    public bool IsDirector { get; private set; }
    public bool TemporalPolicyEnabled { get; private set; }
    public List<Specialization> Specializations { get; private set; } = [];
    public int Total { get; private set; }
    public int Active { get; private set; }
    public bool Editing { get; private set; }
    public string? Error { get; private set; }
    public string? Success { get; private set; }
    public string? TemporaryPassword { get; private set; }
    public string? CreatedUserName { get; private set; }
    public static string RoleName(string code) => SessionPresentation.RoleName(code) ?? "Sin rol asignado";
    public static readonly string[] Roles = [CanonicalRole.Direction, CanonicalRole.Administration,
        CanonicalRole.Subcoordination, CanonicalRole.SalesFloor];
    private async Task<IActionResult?> AuthorizeAsync(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var session = await sessions.GetAsync(ct);
        if (session is null) return Redirect("/acceso");
        if (session.RoleCode != CanonicalRole.Direction) return StatusCode(403);
        CurrentUserId = session.UserId;
        IsDirector = session.RoleCode == CanonicalRole.Direction;
        return null;
    }
    public async Task<IActionResult> OnGetAsync(Guid? edit, bool create, CancellationToken ct)
    {
        var denied = await AuthorizeAsync(ct); if (denied is not null) return denied;
        try
        {
            var all = await LoadAsync(ct);
            if (edit.HasValue)
            {
                var profile = all.SingleOrDefault(p => p.Id == edit.Value);
                if (profile is null) return NotFound();
                Input = new() { Id = profile.Id, DisplayName = profile.DisplayName, UserName = profile.UserName,
                    RoleCode = profile.RoleCode, Revision = profile.Revision, IsOnTrial = profile.IsOnTrial,
                    SpecializationIds = profile.SpecializationIds ?? [], ManageSpecializations = IsDirector };
                Editing = true;
                Schedule = await schedules.ReadAsync(CurrentUserId, profile.Id, ct);
            }
            else if (create) Editing = true;
            return Page();
        }
        catch (UserProfileException e) { return StatusCode(e.Status); }
    }
    // Unknown/retired form actions must not fall back to rendering a successful POST.
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var denied = await AuthorizeAsync(ct);
        return denied ?? NotFound();
    }
    public Task<IActionResult> OnPostSaveAsync(CancellationToken ct) => MutateAsync("save", ct);
    public Task<IActionResult> OnPostSchedulePreviewAsync(CancellationToken ct) => ScheduleAsync(false, ct);
    public Task<IActionResult> OnPostScheduleSaveAsync(CancellationToken ct) => ScheduleAsync(true, ct);
    private async Task<IActionResult> ScheduleAsync(bool save, CancellationToken ct)
    {
        var denied = await AuthorizeAsync(ct); if (denied != null) return denied;
        var user = Schedule.UserId;
        var all = await LoadAsync(ct); var profile = all.SingleOrDefault(p => p.Id == user);
        if (profile == null) return NotFound();
        Input = new() { Id = profile.Id, DisplayName = profile.DisplayName, UserName = profile.UserName, RoleCode = profile.RoleCode,
            Revision = profile.Revision, SpecializationIds = profile.SpecializationIds ?? [], IsOnTrial = profile.IsOnTrial };
        Editing = true;
        while(Schedule.Days.Count<7) Schedule.Days.Add(new());
        foreach(var day in Schedule.Days) while(day.Intervals.Count<4) day.Intervals.Add(new());
        foreach (var key in ModelState.Keys.Where(k => !k.StartsWith("Schedule.", StringComparison.Ordinal)).ToArray()) ModelState.Remove(key);
        try
        {
            if (Enumerable.Range(0,7).Any(d => Request.Form[$"Schedule.Days[{d}].Mode"].Count != 1)) throw new DirectionPlanException("SCHEDULE_INTERVAL_INVALID",422);
            if (!ModelState.IsValid) throw new DirectionPlanException("SCHEDULE_INTERVAL_INVALID", 422);
            if (save) await schedules.SaveAsync(CurrentUserId, Schedule, ct);
            else { SchedulePreview = await schedules.PreviewAsync(CurrentUserId, Schedule, ct); Schedule.Fingerprint = SchedulePreview.Fingerprint; ModelState.Remove("Schedule.Fingerprint"); }
            if (save) return RedirectToPage(new { edit = user });
        }
        catch (DirectionPlanException e) { Error = ScheduleMessage(e.Code); Response.StatusCode = e.Status; }
        return Page();
    }
    public static string ScheduleMessage(string code) => TemporalEligibilityContext.Message(code);
    public Task<IActionResult> OnPostDeactivateAsync(CancellationToken ct) => MutateAsync("deactivate", ct);
    public Task<IActionResult> OnPostReactivateAsync(CancellationToken ct) => MutateAsync("reactivate", ct);
    public Task<IActionResult> OnPostDeleteAsync(CancellationToken ct) => MutateAsync("delete", ct);
    private async Task<IActionResult> MutateAsync(string action, CancellationToken ct)
    {
        var denied = await AuthorizeAsync(ct); if (denied is not null) return denied;
        try
        {
            if (action != "save") { ModelState.Remove("Input.DisplayName"); ModelState.Remove("Input.UserName"); ModelState.Remove("Input.RoleCode"); }
            if (!ModelState.IsValid) throw new UserProfileException("Revisa los datos del formulario.");
            var result = await profiles.MutateAsync(CurrentUserId, Input, action, Guid.CreateVersion7(), ct);
            Success = result.Replayed ? "Esta solicitud ya se había completado. La contraseña temporal no vuelve a mostrarse." :
                action switch { "save" => Input.Id == Guid.Empty ? "Usuario creado." : "Perfil actualizado.",
                    "deactivate" => "Usuario desactivado.", "reactivate" => "Usuario reactivado.", _ => "Usuario eliminado." };
            TemporaryPassword = result.TemporaryPassword;
            CreatedUserName = (await LoadAsync(ct)).SingleOrDefault(p => p.Id == result.Id)?.UserName;
            Input = new(); ModelState.Clear();
            return Page();
        }
        catch (UserProfileException e)
        {
            if (e.Status == 403) return StatusCode(403);
            Error = e.Message; Editing = action == "save";
            await LoadAsync(ct);
            if (Input.Id != Guid.Empty) { Schedule=await schedules.ReadAsync(CurrentUserId,Input.Id,ct); }
            Response.StatusCode = e.Status; return Page();
        }
    }
    private async Task<IReadOnlyList<UserProfile>> LoadAsync(CancellationToken ct)
    {
        TemporalPolicyEnabled=await schedules.EnabledAsync(CurrentUserId,ct);
        if (IsDirector) Specializations = (await specializations.ListAsync(CurrentUserId, ct)).Select(s => s.Item).ToList();
        var all = await profiles.ListAsync(CurrentUserId, ct);
        RotationPeople = all;
        Total = all.Count; Active = all.Count(p => p.Status == AccountStatus.Active);
        Profiles = all.Where(p => (string.IsNullOrWhiteSpace(Search) ||
            SearchText.Contains(p.DisplayName, Search.Trim()) ||
            SearchText.Contains(p.UserName, Search.Trim())) &&
            (string.IsNullOrEmpty(State) || p.Status == State)).OrderByDescending(p => p.Id == CurrentUserId).ToArray();
        return all;
    }
    public string SpecializationNames(UserProfile profile) => string.Join(", ", Specializations.Where(s => (profile.SpecializationIds ?? []).Contains(s.Id)).Select(s => s.Name));
}


