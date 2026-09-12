using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class WorkPlanApiEndpoints
{
    public static IEndpointRouteBuilder MapWorkPlanApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/plans/{isoYear}/{isoWeek}/ensure", HandleRequestAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleRequestAsync(
        string isoYear,
        string isoWeek,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa");
        }

        JsonElement? body = null;
        using var reader = new StreamReader(
            context.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var bodyText = await reader.ReadToEndAsync(cancellationToken);
        if (bodyText.Length > 0)
        {
            try
            {
                using var document = JsonDocument.Parse(bodyText);
                body = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return Problem(context, 400, "SOLICITUD_PLAN_INVALIDA", "El cuerpo debe ser un objeto JSON vacío");
            }
        }

        var service = context.RequestServices.GetRequiredService<IWorkPlanService>();
        return await HandleEnsureAsync(isoYear, isoWeek, body, context, service, cancellationToken);
    }

    public static async Task<IResult> HandleEnsureAsync(
        string isoYear,
        string isoWeek,
        JsonElement? body,
        HttpContext context,
        IWorkPlanService service,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Problem(context, 401, "AUTENTICACION_REQUERIDA", "Se requiere una sesión activa");
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            return Problem(context, 403, "ACCESO_DENEGADO", "La sesión no identifica un actor autorizado");
        }

        var parsedIdempotency = IdempotencyKeyHeader.Parse(context.Request);
        if (!parsedIdempotency.IsValid)
        {
            return Problem(context, 400, parsedIdempotency.ErrorCode!, parsedIdempotency.Detail!);
        }
        var idempotencyKey = parsedIdempotency.Key;

        if (!int.TryParse(isoYear, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear) ||
            !int.TryParse(isoWeek, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedWeek) ||
            body is { } value && (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any()))
        {
            return Problem(context, 400, "SOLICITUD_PLAN_INVALIDA", "La ruta y el cuerpo del plan son inválidos");
        }

        try
        {
            var result = await service.EnsureAsync(new EnsureWorkPlanCommand(
                actorUserId,
                idempotencyKey,
                CorrelationId(context),
                BranchScope.LorettaId,
                parsedYear,
                parsedWeek), cancellationToken);
            context.Response.Headers.ETag = VersionEtag.Format(result.RowVersion);
            return Results.Json(
                new { data = result, meta = new { correlationId = context.GetCorrelationId() } },
                statusCode: result.Result == WorkPlanResults.Created ? 201 : 200);
        }
        catch (WorkPlanException exception)
        {
            return Problem(context, exception.ResponseCode, exception.ErrorCode, Title(exception));
        }
    }

    private static string Title(WorkPlanException exception) => exception switch
    {
        WorkPlanAccessDeniedException => "Acceso denegado",
        WorkPlanIsoWeekInvalidException => "La semana ISO no es válida",
        WorkPlanPeriodNotFoundException => "El período no existe en LOR-001",
        WorkPlanPeriodIncompatibleException => "El período no coincide con la semana solicitada",
        WorkPlanIdempotencyConflictException => "Idempotency-Key ya fue usada con otro contenido",
        WorkPlanConcurrencyConflictException => "No fue posible serializar la creación del plan",
        _ => "El plan entra en conflicto con la integridad persistida",
    };

    private static Guid CorrelationId(HttpContext context) =>
        Guid.TryParse(context.GetCorrelationId(), out var id) ? id : Guid.CreateVersion7();

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
