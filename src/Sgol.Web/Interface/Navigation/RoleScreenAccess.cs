using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sgol.Identity.Contracts;

namespace Sgol.Web.Presentation.Navigation;

public static class RoleScreenAccess
{
    public static IReadOnlyList<NavigationItem> Items { get; } =
    [
        new("Mi trabajo", "/mi-trabajo", Roles(CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor), AuthenticatedOnly: true),
        new("Tablero", "/tablero", Roles(CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor), AuthenticatedOnly: true),
        new("Supervisión", "/validaciones", Roles(CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination), AuthenticatedOnly: true),
        new("Asignar tarea", "/asignar-tarea", Roles(CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination), AuthenticatedOnly: true),
        new("Personas y accesos", "/personas-y-accesos", Roles(CanonicalRole.Direction), AuthenticatedOnly: true),
        new("Configuración", "/configuracion", Roles(CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination, CanonicalRole.SalesFloor), AuthenticatedOnly: true),
        new("Tareas", "/tareas", Roles(CanonicalRole.Direction, CanonicalRole.Administration), AuthenticatedOnly: true),
        new("Planificación", "/planificacion", Roles(CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination), AuthenticatedOnly: true),
        new("Reportes", "/reportes", Roles(CanonicalRole.Direction, CanonicalRole.Administration, CanonicalRole.Subcoordination), AuthenticatedOnly: true)
    ];

    private static IReadOnlySet<string> Roles(params string[] roles) => new HashSet<string>(roles, StringComparer.Ordinal);
}

// Run before every page handler, including POSTs and nested screens.
public sealed class RoleScreenAccessFilter(IRazorSessionState sessions) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (Sgol.Web.Infrastructure.Authentication.ScreenAuthentication.IsScreen(context.HttpContext.User))
        {
            if ((!Sgol.Web.Infrastructure.Authentication.ScreenAuthentication.IsBoard(context.HttpContext.Request.Path) &&
                 !context.HttpContext.Request.Path.Equals("/pantalla", StringComparison.OrdinalIgnoreCase)) ||
                context.HttpContext.Request.Method is not ("GET" or "HEAD"))
            { context.Result = new StatusCodeResult(403); return; }
            await next();
            return;
        }
        var screen = RoleScreenAccess.Items.FirstOrDefault(item => context.HttpContext.Request.Path.StartsWithSegments(item.Href));
        // The legacy branch details screen belongs to organization administration.
        if (context.HttpContext.Request.Path.StartsWithSegments("/branches"))
            screen = RoleScreenAccess.Items.Single(item => item.Href == "/personas-y-accesos");
        if (screen is not null)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            var session = await sessions.GetAsync(context.HttpContext.RequestAborted);
            if (session is null) { context.Result = new RedirectResult("/acceso"); return; }
            if (!screen.AllowedRoles.Contains(session.RoleCode)) { context.Result = new StatusCodeResult(403); return; }
        }
        await next();
    }
}
