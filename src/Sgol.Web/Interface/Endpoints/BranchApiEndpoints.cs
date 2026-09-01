using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Http;

namespace Sgol.Web.Presentation.Endpoints;

public static class BranchApiEndpoints
{
    public static IEndpointRouteBuilder MapBranchApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/branches/{branchCode}", HandleRequestAsync);
        return endpoints;
    }

    private static Task<IResult> HandleRequestAsync(
        string branchCode,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(Problem(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "AUTENTICACION_REQUERIDA",
                "Se requiere una sesión activa"));
        }

        var reader = httpContext.RequestServices.GetRequiredService<IBranchCatalogReader>();
        return HandleGetAsync(branchCode, httpContext, reader, cancellationToken);
    }

    public static async Task<IResult> HandleGetAsync(
        string branchCode,
        HttpContext httpContext,
        IBranchCatalogReader reader,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Problem(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "AUTENTICACION_REQUERIDA",
                "Se requiere una sesión activa");
        }

        try
        {
            var canonicalCode = BranchScope.RequireLoretta(branchCode);
            var branch = await reader.FindAsync(canonicalCode, cancellationToken);
            if (branch is null)
            {
                return Problem(
                    httpContext,
                    StatusCodes.Status404NotFound,
                    "SUCURSAL_NO_ENCONTRADA",
                    "No se encontró la sucursal Loretta");
            }

            return Results.Ok(new
            {
                data = branch,
                meta = new { correlationId = httpContext.GetCorrelationId() },
            });
        }
        catch (UnsupportedBranchCodeException)
        {
            return Problem(
                httpContext,
                StatusCodes.Status422UnprocessableEntity,
                "ALCANCE_INVALIDO",
                "El MVP sólo reconoce la sucursal LOR-001");
        }
    }

    private static IResult Problem(
        HttpContext context,
        int status,
        string code,
        string title) => Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.GetCorrelationId(),
            });
}
