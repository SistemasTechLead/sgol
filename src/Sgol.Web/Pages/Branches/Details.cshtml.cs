using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Presentation.Components;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.Branches;

public sealed class DetailsModel(IBranchCatalogReader reader, IRazorSessionState sessionState) : PageModel
{
    public BranchCatalogItem? Branch { get; private set; }

    public ProblemDetailsPresentation? Error { get; private set; }

    public EmptyStateViewModel? EmptyState { get; private set; }

    public bool IsLoading { get; private set; } = true;

    public StatusBadgeViewModel ActiveBadge { get; } = new("Activa", "✓", "exito");

    public async Task OnGetAsync(string branchCode, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Sucursal Loretta";
        ViewData["NavigationItems"] = new[]
        {
            new NavigationItem(
                "Sucursal",
                $"/branches/{BranchScope.LorettaCode}",
                new HashSet<string>(["DIRECCION"], StringComparer.Ordinal)),
        };

        if (await sessionState.GetAsync(cancellationToken) is null)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Error = new ProblemDetailsPresentation(
                "Se requiere una sesión activa",
                "Inicia sesión para consultar la sucursal Loretta.",
                HttpContext.GetCorrelationId());
            IsLoading = false;
            return;
        }

        try
        {
            var canonicalCode = BranchScope.RequireLoretta(branchCode);
            Branch = await reader.FindAsync(canonicalCode, cancellationToken);
            if (Branch is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                EmptyState = new EmptyStateViewModel(
                    "No se encontró la sucursal Loretta",
                    "La semilla canónica LOR-001 no está disponible.",
                    "Volver a consultar",
                    $"/branches/{BranchScope.LorettaCode}");
            }
        }
        catch (UnsupportedBranchCodeException)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            EmptyState = new EmptyStateViewModel(
                "No se encontró la sucursal Loretta",
                "La semilla canónica LOR-001 no está disponible.",
                "Volver a consultar",
                $"/branches/{BranchScope.LorettaCode}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
