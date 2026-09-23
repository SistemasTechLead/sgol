using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Sgol.Identity.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class SharedInterfaceTests
{
    [Fact]
    public void Navigation_ShowsOnlyItemsForTheActiveRole()
    {
        var direction = CreateSession("DIRECCION", "PER-BRANCH-VIEW");
        var items = new[]
        {
            CreateItem("Dirección", "/direccion", "DIRECCION"),
            CreateItem("Piso de ventas", "/piso", "PISO_VENTAS"),
            CreateItem("Sin contrato", "/sin-rol")
        };

        var visible = RoleAwareNavigation.VisibleTo(direction, items, new HashSet<string> { "/direccion", "/piso" });

        Assert.Equal("Dirección", Assert.Single(visible).Label);
    }

    [Fact]
    public void Navigation_DeniesByDefaultForDifferentRoleAndUnauthenticatedUser()
    {
        var items = new[] { CreateItem("Dirección", "/direccion", "DIRECCION") };
        var differentRole = CreateSession("SUBCOORDINACION", "PER-BRANCH-VIEW");

        Assert.Empty(RoleAwareNavigation.VisibleTo(differentRole, items, new HashSet<string> { "/direccion" }));
        Assert.Empty(RoleAwareNavigation.VisibleTo(null, items));
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

    private static SessionSnapshot CreateSession(string role, params string[] permissions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "synthetic", "Synthetic", "LOR-001", role, permissions,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow.AddHours(8));

    private static NavigationItem CreateItem(string label, string href, params string[] roles) =>
        new(label, href, roles.ToHashSet(StringComparer.Ordinal), new HashSet<string> { "PER-BRANCH-VIEW" });
}
