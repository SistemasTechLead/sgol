using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Pages.Planning;

public sealed partial class ProcessesModel
{
    public DirectionPlanDeletionPreview? Deletion { get; private set; }
    public string? DeleteIntent { get; private set; }
    private sealed record Intention(Guid Actor, Guid Key, Guid PlanId, string Fingerprint, DateTimeOffset ExpiresAt);
    private IDataProtector Protector => protection.CreateProtector("SGOL.process-plan.delete.v1");
    private static string ArchiveCookie(Guid id) => $"sgol-plan-archive-{id:N}";

    public async Task<IActionResult> OnPostPrepareDeletionAsync(Guid planId, CancellationToken token)
    {
        var actor = await Actor(token, true); if (actor is null) return StatusCode(403);
        try
        {
            Plan = await plans.ReadAsync(actor.Value, planId, token);
            Deletion = await direction.PreviewDeletionAsync(actor.Value, planId, token);
            DeleteIntent = Protector.Protect(JsonSerializer.Serialize(new Intention(actor.Value, Guid.CreateVersion7(), planId,
                Deletion.Fingerprint, DateTimeOffset.UtcNow.AddMinutes(15))));
        }
        catch (DirectionPlanException e) { DeletionError(e); }
        return Page();
    }

    public async Task<IActionResult> OnPostDownloadArchiveAsync(string deleteIntent, CancellationToken token)
    {
        var actor = await Actor(token, true); if (actor is null) return StatusCode(403);
        var intent = ReadIntention(deleteIntent, actor.Value); if (intent is null) return BadRequest();
        try
        {
            var archive = await direction.DownloadArchiveAsync(actor.Value, intent.PlanId, intent.Fingerprint, token);
            // A signed receipt links the generated archive to the exact reviewed state.
            Response.Cookies.Append(ArchiveCookie(intent.PlanId), Protector.Protect(JsonSerializer.Serialize(intent)),
                new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
                    Path = "/planificacion/procesos", Expires = intent.ExpiresAt, IsEssential = true });
            return File(archive.Content, "application/zip", archive.FileName);
        }
        catch (DirectionPlanException e)
        {
            DeletionError(e);
            if (e.Status != 404) Plan = await plans.ReadAsync(actor.Value, intent.PlanId, token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string deleteIntent, string confirmation, string archiveChoice, CancellationToken token)
    {
        var actor = await Actor(token, true); if (actor is null) return StatusCode(403);
        var intent = ReadIntention(deleteIntent, actor.Value); if (intent is null) return BadRequest();
        if (confirmation != "ELIMINAR" || archiveChoice is not ("download" or "skip")) return BadRequest();
        if (archiveChoice == "download")
        {
            var receipt = ReadIntention(Request.Cookies[ArchiveCookie(intent.PlanId)], actor.Value);
            if (receipt is null || receipt.PlanId != intent.PlanId || receipt.Fingerprint != intent.Fingerprint)
            {
                Plan = await plans.ReadAsync(actor.Value, intent.PlanId, token);
                Deletion = await direction.PreviewDeletionAsync(actor.Value, intent.PlanId, token);
                if (Deletion.Fingerprint != intent.Fingerprint)
                {
                    Deletion = null;
                    DeletionError(new DirectionPlanException("PLAN_DELETE_STALE", 409));
                    return Page();
                }
                DeleteIntent = deleteIntent;
                Response.StatusCode = 409;
                Error = "Primero pulsa Descargar todos los archivos y verifica que el ZIP se haya guardado. Después confirma la eliminación.";
                return Page();
            }
        }
        try
        {
            await direction.DeleteAsync(actor.Value, intent.PlanId, intent.Key, Guid.CreateVersion7(), intent.Fingerprint, token);
            TempData["ProcessPlanDeleted"] = "Plan de proceso eliminado junto con sus etapas, asignaciones y archivos.";
            return Redirect("/planificacion");
        }
        catch (DirectionPlanException e)
        {
            DeletionError(e);
            if (e.Status != 404) Plan = await plans.ReadAsync(actor.Value, intent.PlanId, token);
            return Page();
        }
    }

    private Intention? ReadIntention(string? value, Guid actor)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 8192) return null;
        try
        {
            var intent = JsonSerializer.Deserialize<Intention>(Protector.Unprotect(value));
            return intent is not null && intent.Actor == actor && intent.PlanId != Guid.Empty && intent.Key != Guid.Empty &&
                intent.Fingerprint.Length == 64 && intent.ExpiresAt > DateTimeOffset.UtcNow ? intent : null;
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { return null; }
    }

    private void DeletionError(DirectionPlanException e)
    {
        Response.StatusCode = e.Status;
        Error = e.Code switch {
            "PLAN_DELETE_STALE" => "El plan cambió. Vuelve a revisar la eliminación y descarga nuevamente sus archivos.",
            "PLAN_DELETE_CAPACITY_CHANGED" => "La capacidad fue modificada después. Resuelve esos ajustes antes de eliminar el plan.",
            "PLAN_DELETE_DEPENDENCIES" => "Otro plan depende de estas asignaciones. Resuelve esa relación antes de eliminarlo.",
            "DIRECTION_PLAN_NOT_FOUND" => "El plan ya no existe. Vuelve a la lista de planes.",
            "ACCESO_DENEGADO" => "No tienes permiso para eliminar este plan.",
            _ => "No se pudo completar la operación. Recarga el plan y vuelve a intentarlo."
        };
    }
}
