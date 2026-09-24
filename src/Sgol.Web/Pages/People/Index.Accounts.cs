using Microsoft.AspNetCore.Mvc;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.People;

public sealed partial class IndexModel
{
    public IReadOnlyList<AccountSummary> Accounts { get; private set; } = [];
    public bool CanManageAccounts { get; private set; }
    public ProblemDetailsPresentation? AccountError { get; private set; }
    public string? AccountSuccess { get; private set; }
    public string? OneTimePassword { get; private set; }
    public string? AccountUserName { get; private set; }
    public Guid AccountPersonId { get; private set; }
    public Guid AccountIntentKey { get; private set; } = Guid.CreateVersion7();

    private async Task LoadAccountsAsync(CancellationToken cancellationToken)
    {
        var session = await sessionState.GetAsync(cancellationToken);
        if (session?.Permissions?.Contains("PER-USUARIO-ADMIN", StringComparer.Ordinal) != true) return;
        try
        {
            var response = await apiClient.SendAsync<AccountSummary>(
                new ApiRequest(HttpMethod.Get, "/api/v1/users", ApiResponseShape.Collection), cancellationToken);
            if (response.Status == StatusCodes.Status401Unauthorized) { sessionState.Invalidate(); return; }
            if (response.Status == StatusCodes.Status403Forbidden)
            {
                AccountError = new("No tienes permiso para administrar cuentas", "La sección no está disponible para tu sesión actual.", response.CorrelationId);
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (!response.IsSuccess)
            {
                AccountError = response.Error;
                Response.StatusCode = response.Status;
                return;
            }
            Accounts = response.Items ?? [];
            CanManageAccounts = true;
        }
        catch (ApiProtocolException)
        {
            AccountError = new("No se pudo cargar la lista de cuentas", "Vuelve a consultar más tarde.", null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
    }

    public async Task<IActionResult> OnPostCreateAccountAsync(CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        AccountUserName = form["userName"].ToString().Trim();
        Guid.TryParseExact(form["personId"].ToString(), "D", out var personId);
        AccountPersonId = personId;
        if (!Guid.TryParseExact(form["accountIntentKey"].ToString(), "D", out var key) || key == Guid.Empty)
            return await AccountFormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", 400, cancellationToken);
        AccountIntentKey = key;
        if (personId == Guid.Empty || string.IsNullOrWhiteSpace(AccountUserName))
            return await AccountFormErrorAsync("Revisa los datos de la cuenta", "Selecciona una persona e ingresa un usuario.", 400, cancellationToken);
        try
        {
            var response = await antiforgery.SendValidatedAsync<AccountActivationResponse>(HttpContext,
                new ApiRequest(HttpMethod.Post, "/api/v1/users", ApiResponseShape.Item,
                    new { personId, userName = AccountUserName }, Intent: ApiMutationIntent.FromKey(key)), cancellationToken);
            if (response is null)
                return await AccountFormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", 400, cancellationToken);
            if (response.Status == 401) return Redirect("/acceso");
            if (response.IsSuccess && response.Data is { } created)
            {
                AccountSuccess = response.Replayed || created.TemporaryPassword is null
                    ? "La operación ya se había procesado; la contraseña temporal no puede volver a mostrarse"
                    : "Cuenta creada";
                OneTimePassword = response.Replayed ? null : created.TemporaryPassword;
                AccountUserName = null;
                AccountPersonId = Guid.Empty;
                AccountIntentKey = Guid.CreateVersion7();
            }
            else SetAccountError(response.Status, response.ErrorCode, response.CorrelationId, response.Error);
        }
        catch (ApiProtocolException)
        {
            AccountError = new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return await LoadPeopleAsync(cancellationToken);
    }

    public Task<IActionResult> OnPostDeactivateAccountAsync(CancellationToken cancellationToken) =>
        ChangeAccountStatusAsync(reactivate: false, cancellationToken);

    public Task<IActionResult> OnPostReactivateAccountAsync(CancellationToken cancellationToken) =>
        ChangeAccountStatusAsync(reactivate: true, cancellationToken);

    private async Task<IActionResult> ChangeAccountStatusAsync(bool reactivate, CancellationToken cancellationToken)
    {
        NoStore();
        if (await sessionState.GetAsync(cancellationToken) is null) return Redirect("/acceso");
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        if (!Guid.TryParseExact(form["userId"].ToString(), "D", out var userId) || userId == Guid.Empty ||
            !Guid.TryParseExact(form["accountIntentKey"].ToString(), "D", out var key) || key == Guid.Empty)
            return await AccountFormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", 400, cancellationToken);
        var reason = form["reason"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return await AccountFormErrorAsync("Revisa el motivo", "Ingresa el motivo antes de continuar.", 400, cancellationToken);
        var path = $"/api/v1/users/{userId:D}/{(reactivate ? "reactivate" : "deactivate")}";
        try
        {
            var request = new ApiRequest(HttpMethod.Post, path, ApiResponseShape.Item, new { reason },
                Intent: ApiMutationIntent.FromKey(key));
            if (reactivate)
            {
                var response = await antiforgery.SendValidatedAsync<AccountActivationResponse>(HttpContext, request, cancellationToken);
                if (response is null)
                    return await AccountFormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", 400, cancellationToken);
                if (response.Status == 401) return Redirect("/acceso");
                if (response.IsSuccess)
                {
                    AccountSuccess = response.Replayed || response.Data?.TemporaryPassword is null
                        ? "La operación ya se había procesado; la contraseña temporal no puede volver a mostrarse"
                        : "Cuenta reactivada";
                    OneTimePassword = response.Replayed ? null : response.Data?.TemporaryPassword;
                }
                else SetAccountError(response.Status, response.ErrorCode, response.CorrelationId, response.Error);
            }
            else
            {
                var response = await antiforgery.SendValidatedAsync<AccountSummary>(HttpContext, request, cancellationToken);
                if (response is null)
                    return await AccountFormErrorAsync("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla.", 400, cancellationToken);
                if (response.Status == 401) return Redirect("/acceso");
                if (response.IsSuccess) AccountSuccess = "Cuenta desactivada";
                else SetAccountError(response.Status, response.ErrorCode, response.CorrelationId, response.Error);
            }
        }
        catch (ApiProtocolException)
        {
            AccountError = new("No se pudo completar la operación", "Consulta el estado antes de volver a enviarla.", null);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return await LoadPeopleAsync(cancellationToken);
    }

    private async Task<IActionResult> AccountFormErrorAsync(string title, string message, int status,
        CancellationToken cancellationToken)
    {
        AccountError = new(title, message, null);
        var result = await LoadPeopleAsync(cancellationToken);
        Response.StatusCode = status;
        return result;
    }

    private void SetAccountError(int status, string? code, string correlationId, ProblemDetailsPresentation? fallback)
    {
        AccountError = code switch
        {
            "CUENTA_DUPLICADA" => new("La persona o el usuario ya tiene una cuenta", "Revisa la selección antes de continuar.", correlationId),
            "PERSONA_NO_ENCONTRADA" => new("No existe o no está disponible en tu alcance", "Selecciona otra persona.", correlationId),
            "PERSONA_FUERA_DE_ALCANCE" => new("La persona debe estar activa en LOR-001", "Revisa su vigencia laboral.", correlationId),
            "DATOS_CUENTA_INVALIDOS" => new("Revisa los datos de la cuenta", "Corrige los campos señalados.", correlationId),
            "ESTADO_CUENTA_SIN_CAMBIO" => new("El estado de la cuenta ya cambió", "Recarga la lista antes de otra acción.", correlationId),
            _ => fallback,
        };
        Response.StatusCode = status;
    }
}
