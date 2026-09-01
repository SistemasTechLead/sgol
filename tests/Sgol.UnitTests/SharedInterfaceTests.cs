using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Xunit;

namespace Sgol.UnitTests;

public sealed class SharedInterfaceTests
{
    [Fact]
    public void Navigation_ShowsOnlyItemsForTheActiveRole()
    {
        var direction = CreatePrincipal("DIRECCION");
        var items = new[]
        {
            CreateItem("Dirección", "/direccion", "DIRECCION"),
            CreateItem("Piso de ventas", "/piso", "PISO_VENTAS"),
            CreateItem("Sin contrato", "/sin-rol")
        };

        var visible = RoleAwareNavigation.VisibleTo(direction, items);

        Assert.Equal("Dirección", Assert.Single(visible).Label);
    }

    [Fact]
    public void Navigation_DeniesByDefaultForDifferentRoleAndUnauthenticatedUser()
    {
        var items = new[] { CreateItem("Dirección", "/direccion", "DIRECCION") };
        var differentRole = CreatePrincipal("SUBCOORDINACION");
        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Empty(RoleAwareNavigation.VisibleTo(differentRole, items));
        Assert.Empty(RoleAwareNavigation.VisibleTo(unauthenticated, items));
    }

    [Theory]
    [InlineData(400, "No se pudo guardar el registro")]
    [InlineData(403, "No tienes permiso para ver este contenido")]
    [InlineData(412, "Este registro cambió mientras lo editabas")]
    [InlineData(500, "No se pudo completar la operación")]
    public void ProblemDetails_MapsToSafeSpanishMessages(int status, string expectedTitle)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = "Internal service failure",
            Detail = "Sensitive internal failure detail"
        };
        problem.Extensions["correlationId"] = "01991f5f-ae33-7f5e-a3f8-7f02f67dcb1f";

        var presentation = ProblemDetailsPresenter.Present(problem);

        Assert.Equal(expectedTitle, presentation.Title);
        Assert.DoesNotContain("Internal", presentation.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sensitive", presentation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("01991f5f-ae33-7f5e-a3f8-7f02f67dcb1f", presentation.CorrelationId);
    }

    private static ClaimsPrincipal CreatePrincipal(params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, ClaimTypes.Role));
    }

    private static NavigationItem CreateItem(string label, string href, params string[] roles) =>
        new(label, href, roles.ToHashSet(StringComparer.Ordinal));
}
