using System.Globalization;
using System.Security.Claims;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class RoleApiEndpoints
{
    public static async Task<IResult> HandleChangeAsync(
        Guid userId,
        ChangeRoleAssignmentRequest request,
        HttpContext context,
        IRoleAssignmentService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId, out var denied))
        {
            return denied;
        }

        var parsedIdempotency = IdempotencyKeyHeader.Parse(context.Request);
        if (!parsedIdempotency.IsValid)
        {
            return Problem(
                context,
                StatusCodes.Status400BadRequest,
                parsedIdempotency.ErrorCode!,
                parsedIdempotency.Detail!);
        }
        var idempotencyKey = parsedIdempotency.Key;

        if (!TryGetOptionalRowVersion(context, out var rowVersion, out var invalidVersion))
        {
            return invalidVersion;
        }

        try
        {
            var result = await service.ChangeAsync(
                new ChangeRoleAssignmentCommand(
                    actorUserId,
                    idempotencyKey,
                    GetCorrelationId(context),
                    userId,
                    request.RoleCode,
                    request.Reason,
                    rowVersion),
                cancellationToken);
            var latest = result.Assignment.History
                .OrderByDescending(item => item.RowVersion)
                .ThenByDescending(item => item.ValidFrom)
                .ThenByDescending(item => item.Id)
                .First();
            context.Response.Headers.ETag = $"\"{latest.RowVersion.ToString(CultureInfo.InvariantCulture)}\"";
            return Results.Ok(Envelope(context, result.Assignment));
        }
        catch (Exception exception)
        {
            return MapException(context, exception);
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId, out IResult denied)
    {
        actorUserId = default;
        if (context.User.Identity?.IsAuthenticated != true)
        {
            denied = Problem(
                context,
                StatusCodes.Status401Unauthorized,
                "AUTENTICACION_REQUERIDA",
                "Se requiere una sesión activa");
            return false;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId))
        {
            denied = Problem(
                context,
                StatusCodes.Status403Forbidden,
                "ACCESO_DENEGADO",
                "La sesión no identifica un actor autorizado");
            return false;
        }

        denied = null!;
        return true;
    }

    private static bool TryGetOptionalRowVersion(
        HttpContext context,
        out long? rowVersion,
        out IResult invalid)
    {
        var value = context.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            rowVersion = null;
            invalid = null!;
            return true;
        }

        if (long.TryParse(
                value.Trim('"'),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsed) && parsed >= 1)
        {
            rowVersion = parsed;
            invalid = null!;
            return true;
        }

        rowVersion = null;
        invalid = Problem(
            context,
            StatusCodes.Status400BadRequest,
            "IF_MATCH_INVALIDO",
            "If-Match debe contener el ETag vigente");
        return false;
    }

    private static IResult MapException(HttpContext context, Exception exception) => exception switch
    {
        RoleValidationException => Problem(context, 400, "DATOS_ROL_INVALIDOS", exception.Message),
        RoleAccessDeniedException => Problem(context, 403, "ACCESO_DENEGADO", "Se requiere PER-ROL-ADMIN vigente en LOR-001"),
        RoleTargetNotFoundException => Problem(context, 404, "CUENTA_NO_ENCONTRADA", "No se encontró la cuenta"),
        RoleTargetInactiveException => Problem(context, 409, "CUENTA_INACTIVA", "La cuenta objetivo no está activa"),
        RoleTargetOutOfScopeException => Problem(context, 409, "CUENTA_FUERA_DE_ALCANCE", "La cuenta objetivo no está activa en LOR-001"),
        RoleAssignmentNotFoundException => Problem(context, 409, "ROL_ACTIVO_NO_ENCONTRADO", "La cuenta no tiene un rol activo que revocar"),
        RoleAssignmentNoChangeException => Problem(context, 409, "ROL_SIN_CAMBIO", "El rol solicitado ya está activo"),
        RoleIfMatchRequiredException => Problem(context, 400, "IF_MATCH_INVALIDO", "If-Match es obligatorio para cambiar o revocar el rol vigente"),
        RoleVersionConflictException => Problem(context, 412, "VERSION_CONFLICT", "La versión cambió; vuelve a cargar el recurso"),
        RoleIdempotencyConflictException => Problem(context, 409, "IDEMPOTENCY_KEY_CONFLICT", "La clave ya fue usada con otro contenido"),
        RoleAssignmentConflictException => Problem(context, 409, "ROL_ACTIVO_DUPLICADO", "La cuenta ya tiene un rol activo en LOR-001"),
        _ => throw exception,
    };

    private static Guid GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var correlationId)
            ? correlationId
            : Guid.CreateVersion7();

    private static object Envelope(HttpContext context, object data) => new
    {
        data,
        meta = new { correlationId = context.GetCorrelationId() },
    };

    private static IResult Problem(HttpContext context, int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.GetCorrelationId(),
            });
}

public sealed record ChangeRoleAssignmentRequest(string? RoleCode, string Reason);
