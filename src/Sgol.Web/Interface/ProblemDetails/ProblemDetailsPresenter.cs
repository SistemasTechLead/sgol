using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Sgol.Web.Presentation.ProblemDetails;

public sealed record ProblemDetailsPresentation(
    string Title,
    string Message,
    string? CorrelationId);

public static class ProblemDetailsPresenter
{
    public static ProblemDetailsPresentation Present(Microsoft.AspNetCore.Mvc.ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var (title, message) = problem.Status switch
        {
            StatusCodes.Status400BadRequest => (
                "No se pudo guardar el registro",
                "Revisa los campos marcados y vuelve a intentar."),
            StatusCodes.Status403Forbidden => (
                "No tienes permiso para ver este contenido",
                "La operación no está disponible para tu rol actual."),
            StatusCodes.Status412PreconditionFailed => (
                "Este registro cambió mientras lo editabas",
                "Recarga para ver la versión más reciente antes de volver a guardar."),
            _ => (
                "No se pudo completar la operación",
                "Vuelve a intentar. Si el problema continúa, comparte el identificador técnico con soporte.")
        };

        return new ProblemDetailsPresentation(title, message, ReadCorrelationId(problem));
    }

    private static string? ReadCorrelationId(Microsoft.AspNetCore.Mvc.ProblemDetails problem)
    {
        if (!problem.Extensions.TryGetValue("correlationId", out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            Guid identifier => identifier.ToString("D", CultureInfo.InvariantCulture),
            _ => null
        };
    }
}
