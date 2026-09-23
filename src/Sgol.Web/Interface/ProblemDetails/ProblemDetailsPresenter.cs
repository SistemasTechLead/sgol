using System.Text.Json;

namespace Sgol.Web.Presentation.ProblemDetails;

public sealed record ProblemDetailsPresentation(string Title, string Message, string? CorrelationId);

public static class ProblemDetailsPresenter
{
    public static ProblemDetailsPresentation Present(Microsoft.AspNetCore.Mvc.ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return Present(problem.Status ?? 500, Extension(problem, "code"), Extension(problem, "correlationId"));
    }

    public static ProblemDetailsPresentation Present(int status, string? code, string? correlationId)
    {
        // The server's title and detail are never rendered. Code and status must agree.
        var (title, message) = (status, code) switch
        {
            (400, "CSRF_INVALID" or "CSRF_INVALIDO") => ("No se pudo verificar la solicitud", "Recarga la página antes de volver a enviarla."),
            (400, "IF_MATCH_REQUERIDO") => ("Falta la versión del registro", "Recarga el registro antes de guardar los cambios."),
            (400, "IF_MATCH_INVALIDO") => ("La versión del registro no es válida", "Recárgalo antes de guardar."),
            (428, "IF_MATCH_REQUERIDO") => ("Falta la versión de la reconciliación", "Recarga la reconciliación antes de aprobarla."),
            (412, "VERSION_CONFLICT") => ("Este registro cambió mientras lo editabas", "Probablemente alguien más lo actualizó. Recarga para ver la versión más reciente antes de guardar, o tus cambios podrían sobrescribir los de la otra persona."),
            (409, "IDEMPOTENCY_CONFLICT") => ("La solicitud ya se usó con otros datos", "Revisa los cambios antes de crear una solicitud nueva."),
            (413, "FILE_TOO_LARGE") => ("El archivo supera el tamaño permitido", "Selecciona un archivo de hasta 15 MiB."),
            (415, "FILE_TYPE_NOT_ALLOWED") => ("El tipo de archivo no está permitido", "Selecciona un archivo del tipo solicitado."),
            (422, "EVIDENCIA_FALTANTE") => ("Falta evidencia obligatoria", "Completa los requisitos antes de continuar."),
            (423, "ACCOUNT_LOCKED") => ("La cuenta está bloqueada temporalmente", "Espera antes de volver a iniciar sesión."),
            (429, "RATE_LIMITED") => ("Se alcanzó el límite de solicitudes", "Espera antes de volver a enviar la solicitud."),
            (503, "DEPENDENCY_UNAVAILABLE" or "FILE_SCAN_PENDING") => ("El servicio no está disponible", "La operación no se completó. Consulta su estado más tarde."),
            (401, _) => ("Se requiere una sesión activa", "Inicia sesión para continuar."),
            (403, _) => ("No tienes permiso para ver este contenido", "La operación no está disponible para tu rol actual."),
            (404, _) => ("No se encontró el recurso", "El recurso no existe o no está disponible en tu alcance."),
            (400, _) => ("No se pudo guardar el registro", "Revisa los campos marcados y vuelve a intentar."),
            (412, _) => ("Este registro cambió mientras lo editabas", "La respuesta de concurrencia no se reconoció. Recarga antes de continuar."),
            (500, _) => ("No se pudo completar la operación", "Comparte el identificador técnico con soporte si el problema continúa."),
            _ => ("No se pudo completar la operación", "La respuesta no se reconoció. Consulta el estado antes de volver a enviarla.")
        };
        return new(title, message, ValidCorrelation(correlationId));
    }

    private static string? Extension(Microsoft.AspNetCore.Mvc.ProblemDetails problem, string name) =>
        problem.Extensions.TryGetValue(name, out var value) ? value switch
        {
            string text => text,
            Guid guid => guid.ToString("D"),
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            _ => null
        } : null;

    private static string? ValidCorrelation(string? value) =>
        Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty && id.Version == 7 &&
        string.Equals(value, id.ToString("D"), StringComparison.Ordinal) ? value : null;
}
