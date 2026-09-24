using Sgol.Identity.Contracts;

namespace Sgol.Web.Presentation.Navigation;

public sealed record NavigationItem(
    string Label,
    string Href,
    IReadOnlySet<string> AllowedRoles,
    IReadOnlySet<string>? RequiredPermissions = null);

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
                item.RequiredPermissions is not null && item.RequiredPermissions.Count > 0 &&
                item.RequiredPermissions.All(session.Permissions.Contains))
            .ToArray();
    }

    // This set grows only when a NAV route is actually implemented by its own story.
    private static readonly HashSet<string> ImplementedNavigationRoutes = new(["/personas-y-accesos"], StringComparer.Ordinal);
}
