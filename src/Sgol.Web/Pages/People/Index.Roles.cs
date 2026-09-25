using Microsoft.AspNetCore.Mvc;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.People;

public sealed partial class IndexModel
{
    public bool CanManageRoles { get; private set; }
    public Dictionary<Guid, RoleAssignmentDetails> RolesByUser { get; } = [];
    public Dictionary<Guid, string> RoleEtags { get; } = [];
    public ProblemDetailsPresentation? RoleError { get; private set; }
    public Guid? RoleConflictUserId { get; private set; }

    private async Task LoadRolesAsync(CancellationToken cancellationToken)
    {
        var session = await sessionState.GetAsync(cancellationToken);
        CanManageRoles = session?.Permissions?.Contains("PER-ROL-ADMIN", StringComparer.Ordinal) == true;
        if (!CanManageRoles) return;

        foreach (var account in Accounts.Where(item => item.Status == AccountStatus.Active))
        {
            try
            {
                var response = await apiClient.SendAsync<RoleAssignmentDetails>(
                    new ApiRequest(HttpMethod.Get, $"/api/v1/users/{account.Id:D}/role-assignments",
                        ApiResponseShape.Item), cancellationToken);
                if (response.Status == 401) { sessionState.Invalidate(); return; }
                if (!response.IsSuccess || response.Data is null)
                {
                    RoleError = response.Error ?? new("No se pudieron consultar los roles", "Recarga la sección antes de continuar.", response.CorrelationId);
                    continue;
                }
                RolesByUser[account.Id] = response.Data;
                if (!string.IsNullOrEmpty(response.ETag)) RoleEtags[account.Id] = response.ETag;
            }
            catch (ApiProtocolException)
            {
                RoleError = new("No se pudieron consultar los roles", "Recarga la sección antes de continuar.", null);
            }
        }
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        if (!Guid.TryParseExact(form["userId"].ToString(), "D", out var userId) || userId == Guid.Empty ||
            !Guid.TryParseExact(form["roleIntentKey"].ToString(), "D", out var key) || key == Guid.Empty)
            return await RoleFormErrorAsync("No se pudo verificar la solicitud", "Recarga los roles antes de continuar.", 400, cancellationToken);

        var roleCode = form["roleCode"].ToString();
        var reason = form["reason"].ToString().Trim();
        var etag = form["roleEtag"].ToString();
        if (string.IsNullOrWhiteSpace(reason) || roleCode.Length > 0 && !CanonicalRole.IsDefined(roleCode))
            return await RoleFormErrorAsync("Revisa el rol y el motivo", "Selecciona un rol canónico e ingresa el motivo.", 400, cancellationToken);

        try
        {
            var response = await antiforgery.SendValidatedAsync<RoleAssignmentDetails>(HttpContext,
                new ApiRequest(HttpMethod.Post, $"/api/v1/users/{userId:D}/role-assignments",
                    ApiResponseShape.Item, new { roleCode = roleCode.Length == 0 ? null : roleCode, reason },
                    IfMatch: string.IsNullOrEmpty(etag) ? null : etag,
                    Intent: ApiMutationIntent.FromKey(key)), cancellationToken);
            if (response is null)
                return await RoleFormErrorAsync("No se pudo verificar la solicitud", "Recarga los roles antes de continuar.", 400, cancellationToken);
            if (response.Status == 401) return Redirect("/acceso");
            if (response.IsSuccess)
                AccountSuccess = response.Replayed ? "El cambio de rol ya se había procesado" :
                    roleCode.Length == 0 ? "Rol revocado" : string.IsNullOrEmpty(etag) ? "Rol asignado" : "Rol cambiado";
            else
            {
                RoleError = MapRoleError(response.ErrorCode, response.CorrelationId, response.Error);
                RoleConflictUserId = response.Status == 412 || response.ErrorCode == "IF_MATCH_INVALIDO" ? userId : null;
                Response.StatusCode = response.Status;
            }
        }
        catch (ApiProtocolException)
        {
            RoleError = new("No se pudo confirmar el cambio de rol", "Consulta el rol vigente antes de otra solicitud.", null);
            Response.StatusCode = 503;
        }
        return await LoadPeopleAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostResetMfaAsync(CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        if (!Guid.TryParseExact(form["userId"].ToString(), "D", out var userId) || userId == Guid.Empty ||
            !Guid.TryParseExact(form["resetIntentKey"].ToString(), "D", out var key) || key == Guid.Empty)
            return await RoleFormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de continuar.", 400, cancellationToken);
        var reason = form["reason"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return await RoleFormErrorAsync("Revisa el motivo", "Ingresa el motivo antes de continuar.", 400, cancellationToken);

        try
        {
            var response = await antiforgery.SendValidatedAsync<AccountActivationResponse>(HttpContext,
                new ApiRequest(HttpMethod.Post, $"/api/v1/users/{userId:D}/mfa-reset",
                    ApiResponseShape.Item, new { reason }, Intent: ApiMutationIntent.FromKey(key)), cancellationToken);
            if (response is null)
                return await RoleFormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de continuar.", 400, cancellationToken);
            if (response.Status == 401) return Redirect("/acceso");
            if (response.IsSuccess)
            {
                OneTimePassword = response.Replayed ? null : response.Data?.TemporaryPassword;
                AccountSuccess = OneTimePassword is null
                    ? "El reset ya se había procesado; la contraseña temporal no puede volver a mostrarse"
                    : "MFA restablecido; las sesiones anteriores quedaron invalidadas";
            }
            else
            {
                RoleError = response.ErrorCode == "MFA_RECIENTE_REQUERIDO"
                    ? new("Se requiere MFA reciente", "Cierra sesión e inicia nuevamente con MFA antes de restablecerlo.", response.CorrelationId)
                    : MapRoleError(response.ErrorCode, response.CorrelationId, response.Error);
                Response.StatusCode = response.Status;
            }
        }
        catch (ApiProtocolException)
        {
            RoleError = new("No se pudo confirmar el reset MFA", "Consulta el estado antes de otra solicitud.", null);
            Response.StatusCode = 503;
        }
        return await LoadPeopleAsync(cancellationToken);
    }

    private async Task<IActionResult> RoleFormErrorAsync(string title, string message, int status,
        CancellationToken cancellationToken)
    {
        RoleError = new(title, message, null);
        var result = await LoadPeopleAsync(cancellationToken);
        Response.StatusCode = status;
        return result;
    }

    private static ProblemDetailsPresentation MapRoleError(string? code, string correlationId,
        ProblemDetailsPresentation? fallback) => code switch
        {
            "CUENTA_NO_DISPONIBLE" or "CUENTA_NO_ENCONTRADA" or "CUENTA_FUERA_DE_ALCANCE" or "CUENTA_INACTIVA" or "PERSONA_FUERA_DE_ALCANCE" =>
                new("No existe o no está disponible en tu alcance", "Recarga la lista de cuentas.", correlationId),
            "VERSION_CONFLICT" or "IF_MATCH_INVALIDO" =>
                new("Este rol cambió mientras lo editabas", "Recarga los roles antes de guardar otra intención.", correlationId),
            "ROL_ACTIVO_DUPLICADO" => new("La cuenta ya tiene un rol vigente", "Recarga los roles antes de continuar.", correlationId),
            "ROL_SIN_CAMBIO" => new("Ese rol ya está vigente", "Selecciona un cambio distinto.", correlationId),
            "DATOS_ROL_INVALIDOS" => new("Revisa el rol y el motivo", "Corrige los campos antes de continuar.", correlationId),
            "ACCESO_DENEGADO" => new("No tienes permiso para administrar roles", "La sección no está disponible para tu sesión.", correlationId),
            _ => fallback ?? new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", correlationId),
        };
}
