using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Presentation.ApiClient;

namespace Sgol.Web.Pages.Planning;

public sealed partial class IndexModel
{
    public DirectionPlanDeletionPreview? DirectionDeletion { get; private set; }
    public string? DirectionDeleteIntent { get; private set; }
    private sealed record DeletePlanIntention(Guid Actor, Guid Key, Guid PlanId, string Fingerprint, DateTimeOffset ExpiresAt);
    private IDataProtector DeletePlanProtector => protection.CreateProtector("SGOL.direction-plans.delete.v1");

    public async Task<IActionResult> OnPostPrepareDirectionPlanDeletionAsync(CancellationToken token)
    {
        NoStore();
        var session = await sessionState.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        if (session.RoleCode != CanonicalRole.Direction) return StatusCode(403);
        var form = await DirectionFormAsync(token);
        if (form is null || form["planId"].Count != 1 || !Guid.TryParseExact(form["planId"], "D", out var id) || id == Guid.Empty) return BadRequest();
        try
        {
            var service = HttpContext.RequestServices.GetRequiredService<IDirectionPreparedPlanService>();
            DirectionDeletion = await service.PreviewDeletionAsync(session.UserId, id, token);
            DirectionDeleteIntent = DeletePlanProtector.Protect(JsonSerializer.Serialize(new DeletePlanIntention(session.UserId,
                Guid.CreateVersion7(), id, DirectionDeletion.Fingerprint, DateTimeOffset.UtcNow.AddMinutes(15))));
            DirectionDetail = await service.ReadAsync(session.UserId, id, token);
        }
        catch (DirectionPlanException e) { DeletionError(e.Code, e.Status); }
        return await LoadDirectionPlansAsync(token);
    }

    public async Task<IActionResult> OnPostDeleteDirectionPlanAsync(CancellationToken token)
    {
        NoStore();
        var session = await sessionState.GetAsync(token);
        if (session is null) return Redirect("/acceso");
        if (session.RoleCode != CanonicalRole.Direction) return StatusCode(403);
        var form = await DirectionFormAsync(token);
        if (form is null || form["deleteIntent"].Count != 1 || form["deleteIntent"].ToString().Length > 8192) return BadRequest();
        DeletePlanIntention? intent;
        try { intent = JsonSerializer.Deserialize<DeletePlanIntention>(DeletePlanProtector.Unprotect(form["deleteIntent"].ToString())); }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { return BadRequest(); }
        if (intent is null || intent.Actor != session.UserId || intent.PlanId == Guid.Empty || intent.Key == Guid.Empty || intent.ExpiresAt <= DateTimeOffset.UtcNow) return BadRequest();
        if (form["confirmation"].Count != 1 || form["confirmation"] != "ELIMINAR") return BadRequest("Escribe ELIMINAR para confirmar.");
        try
        {
            var result = await antiforgery.SendValidatedAsync<DirectionPlanDeleted>(HttpContext,
                new(HttpMethod.Delete, $"/api/v1/direction-plans/{intent.PlanId:D}", ApiResponseShape.Item,
                    new { fingerprint = intent.Fingerprint }, Intent: ApiMutationIntent.FromKey(intent.Key)), token);
            if (result is null) return BadRequest();
            if (result.Status == 401) return Redirect("/acceso");
            if (!result.IsSuccess)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<IndexModel>>()
                    .LogWarning("Plan deletion rejected: {PlanId}, {Status}, {Code}", intent.PlanId, result.Status, result.ErrorCode);
                DeletionError(result.ErrorCode ?? "", result.Status);
            }
            else if (result.Data is { Result: "DELETED" } deleted && deleted.PlanId == intent.PlanId)
                DirectionSuccess = "Plan eliminado junto con sus asignaciones, avances, evidencias y parámetros generados.";
            else throw new ApiProtocolException();
        }
        catch (ApiProtocolException)
        {
            DirectionDeleteIntent = form["deleteIntent"];
            Response.StatusCode = 503;
            DirectionError = new("No se pudo confirmar la eliminación", "Reenvía la misma solicitud para comprobar el resultado.", null);
        }
        return await LoadDirectionPlansAsync(token);
    }

    private void DeletionError(string code, int status)
    {
        Response.StatusCode = status;
        DirectionError = new(code switch {
            "PLAN_DELETE_STALE" => "El plan o sus tareas cambiaron. Recarga y revisa de nuevo la eliminación.",
            "PLAN_DELETE_CAPACITY_CHANGED" => "La capacidad de este plan fue modificada después. Resuelve los ajustes posteriores antes de eliminarlo para no afectar otros planes.",
            "PLAN_DELETE_DEPENDENCIES" => "Otro plan depende de estas asignaciones. Resuelve esa relación antes de eliminarlo.",
            "DIRECTION_PLAN_NOT_FOUND" => "El plan ya no existe. Recarga la lista de planes.",
            "ACCESO_DENEGADO" => "No tienes permiso para eliminar este plan.",
            _ => "No se pudo eliminar el plan. Recarga y vuelve a intentarlo."
        }, "", null);
    }
}
