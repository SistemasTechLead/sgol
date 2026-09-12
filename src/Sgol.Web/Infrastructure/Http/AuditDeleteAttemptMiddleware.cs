using System.Security.Claims;
using Sgol.Auditing.Contracts;
using Sgol.Web.Presentation.Endpoints;

namespace Sgol.Web.Infrastructure.Http;

public sealed class AuditDeleteAttemptMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsDelete(context.Request.Method) || !TryResourceId(context.Request.Path, out var resourceId))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await AuditApiEndpoints.Problem(context, 401, "AUTENTICACION_REQUERIDA",
                "Se requiere una sesión activa con MFA").ExecuteAsync(context);
            return;
        }
        if (!Guid.TryParseExact(context.User.FindFirstValue(ClaimTypes.NameIdentifier), "D", out var actorUserId) ||
            actorUserId == Guid.Empty || !Guid.TryParse(context.GetCorrelationId(), out var correlationId) ||
            correlationId == Guid.Empty)
        {
            await AuditApiEndpoints.Problem(context, 403, "ACCESO_DENEGADO",
                "La sesión no identifica un actor autorizado").ExecuteAsync(context);
            return;
        }

        try
        {
            var writer = context.RequestServices.GetRequiredService<IAuditSecurityEventWriter>();
            await writer.WriteDeleteAttemptAsync(actorUserId, correlationId, resourceId, context.RequestAborted);
        }
        catch
        {
            await AuditApiEndpoints.Problem(context, 500, "AUDIT_SECURITY_EVENT_FAILED",
                "No fue posible registrar el intento de eliminación").ExecuteAsync(context);
            return;
        }

        context.Response.Headers.Allow = HttpMethods.Get;
        await AuditApiEndpoints.Problem(context, 405, "AUDIT_NOT_DELETABLE",
            "La auditoría no puede eliminarse").ExecuteAsync(context);
    }

    private static bool TryResourceId(PathString path, out Guid? resourceId)
    {
        resourceId = null;
        if (path == "/api/v1/audit-events") return true;
        const string prefix = "/api/v1/audit-events/";
        var value = path.Value;
        if (value is null || !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var id = value[prefix.Length..];
        if (!Guid.TryParseExact(id, "D", out var parsed) || parsed == Guid.Empty || id != parsed.ToString("D"))
            return false;
        resourceId = parsed;
        return true;
    }
}
