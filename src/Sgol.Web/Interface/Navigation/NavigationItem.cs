using Sgol.Identity.Contracts;

namespace Sgol.Web.Presentation.Navigation;

public sealed record NavigationItem(
    string Label,
    string Href,
    IReadOnlySet<string> AllowedRoles,
    IReadOnlySet<string>? RequiredPermissions = null,
    bool AuthenticatedOnly = false);

public static class RoleAwareNavigation
{
    public static IReadOnlyList<NavigationItem> VisibleTo(
        SessionSnapshot? session,
        IEnumerable<NavigationItem> items,
        IReadOnlySet<string>? implementedRoutes = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (session is null)
        {
            return [];
        }

        implementedRoutes ??= ImplementedNavigationRoutes;
        return items
            .Where(item => implementedRoutes.Contains(item.Href) &&
                item.AllowedRoles.Contains(session.RoleCode) &&
                (item.AuthenticatedOnly ||
                    item.RequiredPermissions is { Count: > 0 } &&
                    item.RequiredPermissions.All(session.Permissions.Contains)))
            .ToArray();
    }

    // This set grows only when a NAV route is actually implemented by its own story.
    private static readonly HashSet<string> ImplementedNavigationRoutes = new(
        ["/personas-y-accesos", "/configuracion", "/planificacion"], StringComparer.Ordinal);
}
