using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Navigation;

namespace Sgol.Web.Infrastructure.Http;

public sealed record SystemErrorPage(int Status, string Label, string Title, string Description,
    string Guidance, string Icon, string Action, string Destination, string Reference)
{
    public static SystemErrorPage For(HttpContext context, bool csrf = false, bool replayUnavailable = false)
    {
        var status = context.Response.StatusCode;
        var reference = context.GetCorrelationId();
        if (replayUnavailable)
            return new(status, "Resultado no disponible", "No pudimos recuperar el resultado de la operación",
                "Existe una respuesta confirmada para esta solicitud, pero no podemos mostrarla en este momento.",
                "Comprueba el resultado en el sistema antes de volver a enviar cambios. Si necesitas ayuda, comparte la referencia con soporte.",
                "alert", "Ir a Mi trabajo", "/mi-trabajo", reference);
        if (csrf)
            return new(status, "Verificación del formulario", "Este formulario necesita actualizarse",
                "No pudimos validar la seguridad del envío. Puede ocurrir si cambió tu sesión o utilizaste una pestaña anterior.",
                "Este intento fue rechazado antes de guardar. Conserva el texto que siga visible y abre nuevamente el formulario.",
                "shield", "Iniciar sesión o continuar", "/acceso", reference);
        return status switch
        {
            401 => new(status, "Sesión terminada", "Inicia sesión para continuar",
                "Tu sesión ya no está activa. Necesitas identificarte nuevamente para acceder al sistema.",
                "Si estabas llenando un formulario, conserva el texto que siga visible antes de salir. No se enviará automáticamente al volver.",
                "clock", "Iniciar sesión", "/acceso", reference),
            403 => new(status, "Acceso restringido", "Esta pantalla requiere otro permiso",
                "Tu rol actual no tiene acceso a esta sección. Puedes continuar con las opciones disponibles para tu cuenta.",
                "Si necesitas entrar para realizar tu trabajo, solicita a Dirección que revise tus permisos.",
                "shield", "Ir a Mi trabajo", "/mi-trabajo", reference),
            404 => new(status, "Página no encontrada", "No encontramos esta página",
                "El enlace puede haber cambiado o la dirección puede estar incompleta.",
                "Vuelve a Mi trabajo para continuar desde una sección disponible.",
                "info", "Ir a Mi trabajo", "/mi-trabajo", reference),
            409 or 412 => new(status, "Cambios en conflicto", "La información cambió mientras trabajabas",
                "La operación no pudo completarse con la versión de los datos que enviaste.",
                "Conserva tus cambios y consulta la versión actual antes de volver a enviarlos.",
                "alert", "Ir a Mi trabajo", "/mi-trabajo", reference),
            429 => new(status, "Demasiadas solicitudes", "Espera un momento antes de continuar",
                "Se alcanzó el límite de solicitudes para esta operación.",
                "Inténtalo más tarde. Si estabas guardando cambios, comprueba el resultado antes de repetir el envío.",
                "clock", "Ir a Mi trabajo", "/mi-trabajo", reference),
            503 => new(status, "Servicio no disponible", "El sistema no está disponible por ahora",
                "No podemos atender esta solicitud en este momento. Intenta acceder de nuevo en unos minutos.",
                "Si el problema ocurrió al guardar, verifica el resultado cuando se restablezca el servicio antes de repetir la operación.",
                "alert", "Volver al acceso", "/acceso", reference),
            >= 500 => new(status, "Error inesperado", "No pudimos completar la operación",
                "Ocurrió un problema al procesar la solicitud.",
                "Si estabas guardando cambios, comprueba primero si aparecen en el sistema antes de repetir el envío. Si el problema continúa, comparte la referencia con soporte.",
                "alert", "Ir a Mi trabajo", "/mi-trabajo", reference),
            _ => new(status, "Solicitud no válida", "No pudimos procesar la solicitud",
                "La información enviada no pudo procesarse para esta operación.",
                "Conserva tus datos y vuelve a abrir el formulario. Si el problema continúa, comparte la referencia con soporte.",
                "alert", "Ir a Mi trabajo", "/mi-trabajo", reference)
        };
    }
}

public sealed class SystemErrorPageRenderer(IRazorViewEngine views, ITempDataProvider tempData,
    IModelMetadataProvider metadata, IRazorSessionState sessions)
{
    public const string CsrfFailure = "SGOL.Error.CsrfFailure";

    public static bool WantsHtml(HttpContext context) =>
        !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
        !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) &&
        context.Request.GetTypedHeaders().Accept?.Any(a =>
            a.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) && (a.Quality ?? 1) > 0) == true;

    public async Task RenderAsync(HttpContext context, bool replayUnavailable = false)
    {
        SessionSnapshot? session = null;
        // Error rendering must remain usable when authentication or its dependencies are unavailable.
        if (context.Response.StatusCode is 403 or 404 && context.User.Identity?.IsAuthenticated == true)
        {
            try { session = await sessions.GetAsync(context.RequestAborted); }
            catch (Exception) when (!context.RequestAborted.IsCancellationRequested) { session = null; }
        }
        var model = SystemErrorPage.For(context, context.Items.ContainsKey(CsrfFailure), replayUnavailable);
        var action = new ActionContext(context, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var view = views.GetView(null, "/Pages/Shared/_SystemError.cshtml", isMainPage: true);
        if (!view.Success) throw new InvalidOperationException("System error view is missing.");
        var data = new ViewDataDictionary<SystemErrorPage>(metadata, new ModelStateDictionary())
        {
            Model = model,
            ["Title"] = model.Label,
            ["SystemError"] = true,
            ["SystemErrorSession"] = session
        };
        using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var viewContext = new ViewContext(action, view.View, data, new TempDataDictionary(context, tempData), writer, new HtmlHelperOptions());
        await view.View.RenderAsync(viewContext);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        await context.Response.WriteAsync(writer.ToString(), context.RequestAborted);
    }
}

// The default antiforgery filter runs before page handlers. Mark its result without bypassing validation.
public sealed class SystemErrorAntiforgeryFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is AntiforgeryValidationFailedResult)
            context.HttpContext.Items[SystemErrorPageRenderer.CsrfFailure] = true;
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
