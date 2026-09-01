using System.Security.Claims;

namespace Sgol.Web.Presentation.Navigation;

public sealed record NavigationItem(
    string Label,
    string Href,
    IReadOnlySet<string> AllowedRoles);

public static class RoleAwareNavigation
{
    public static IReadOnlyList<NavigationItem> VisibleTo(
        ClaimsPrincipal principal,
        IEnumerable<NavigationItem> items)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(items);

        if (principal.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        return items
            .Where(item => item.AllowedRoles.Count > 0 && item.AllowedRoles.Any(principal.IsInRole))
            .ToArray();
    }
}
