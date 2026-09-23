using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Web.Presentation.Components;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ComponentRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ComponentRenderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Credential_DoesNotRenderASecretAndExposesAccessibleRevealState()
    {
        var html = await RenderAsync(
            "/Pages/Shared/_CredentialField.cshtml",
            new CredentialFieldViewModel(
                "totp",
                "Código de autenticación",
                CredentialKind.OneTimeCode,
                ErrorMessage: "Revisa el código.",
                CanReveal: true));

        Assert.Contains("type=\"password\"", html, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"one-time-code\"", html, StringComparison.Ordinal);
        Assert.Contains("inputmode=\"numeric\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"totp\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"false\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MotivatedConfirmation_RendersNativeModalWithCancelFirstAndAssociatedReason()
    {
        var html = await RenderAsync(
            "/Pages/Shared/_MotivatedConfirmation.cshtml",
            new MotivatedConfirmationViewModel(
                "confirm-role",
                "Cambiar rol",
                "Persona sintética — LOR-001",
                "reason",
                "Motivo",
                "Cambiar rol"));

        Assert.Contains("<dialog", html, StringComparison.Ordinal);
        Assert.Contains("aria-modal=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("autofocus", html, StringComparison.Ordinal);
        Assert.Contains("data-dialog-cancel", html, StringComparison.Ordinal);
        Assert.Contains("<textarea", html, StringComparison.Ordinal);
        Assert.Contains("required", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Upload_EncodesFileNameAndAnnouncesPendingScanWithoutSensitiveTransportData()
    {
        var html = await RenderAsync(
            "/Pages/Shared/_UploadPresentation.cshtml",
            new UploadPresentationViewModel(
                "evidence",
                "Evidencia",
                UploadPresentationState.PendingScan,
                "<script>evidence.pdf</script>"));

        Assert.Contains(
            "Esperando análisis antimalware",
            WebUtility.HtmlDecode(html),
            StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;evidence.pdf&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>evidence.pdf</script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("signed", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DataTable_RendersOpaqueCursorLinksAndHeaderLabelsWithoutPageNumbers()
    {
        var html = await RenderAsync(
            "/Pages/Shared/_DataTable.cshtml",
            new DataTableViewModel(
                "Personas",
                ["Nombre", "Estado"],
                [["Persona sintética", "Activa"]],
                Pagination: new CursorPaginationViewModel(
                    PreviousHref: null,
                    NextHref: "/people?cursor=opaque-token"),
                FilterAction: "/people",
                Filters: [new TableFilterViewModel("query", "Buscar", "persona")],
                ClearFiltersHref: "/people",
                RowActions: [[new DataTableActionViewModel("Ver detalle", "/people/synthetic")]]));

        Assert.Contains("data-label=\"Nombre\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-disabled=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("rel=\"next\"", html, StringComparison.Ordinal);
        Assert.Contains("cursor=opaque-token", html, StringComparison.Ordinal);
        Assert.Contains("Aplicar filtros", html, StringComparison.Ordinal);
        Assert.Contains("Quitar filtros", html, StringComparison.Ordinal);
        Assert.Contains("data-label=\"Acciones\"", html, StringComparison.Ordinal);
        Assert.Contains("Ver detalle", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-current=\"page\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FormPrimitives_RenderGroupSemanticsZoneAndNonColorErrorText()
    {
        var radioHtml = await RenderAsync(
            "/Pages/Shared/_RadioGroup.cshtml",
            new RadioGroupViewModel(
                "result",
                "Resultado",
                [new RadioOption("complete", "Cumplida")],
                ErrorMessage: "Selecciona un resultado."));
        var dateHtml = await RenderAsync(
            "/Pages/Shared/_LocalDateField.cshtml",
            new LocalDateFieldViewModel(
                "from",
                "Fecha inicial",
                LocalDateFieldKind.Date));
        var summaryHtml = await RenderAsync(
            "/Pages/Shared/_ValidationSummary.cshtml",
            new ValidationSummaryViewModel(
                "Revisa el formulario",
                ["El motivo es obligatorio."]));

        Assert.Contains("<fieldset", radioHtml, StringComparison.Ordinal);
        Assert.Contains("<legend", radioHtml, StringComparison.Ordinal);
        Assert.Contains("Selecciona un resultado.", radioHtml, StringComparison.Ordinal);
        Assert.Contains("type=\"date\"", dateHtml, StringComparison.Ordinal);
        Assert.Contains("America/Mexico_City", dateHtml, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", summaryHtml, StringComparison.Ordinal);
        Assert.Contains("El motivo es obligatorio.", summaryHtml, StringComparison.Ordinal);
    }

    private async Task<string> RenderAsync<TModel>(string viewPath, TModel model)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
        };
        httpContext.Request.Scheme = "https";
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
        var viewEngine = services.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.GetView(null, viewPath, isMainPage: false);

        Assert.True(
            viewResult.Success,
            $"No se encontró {viewPath}: {string.Join(", ", viewResult.SearchedLocations ?? [])}");

        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        var viewData = new ViewDataDictionary<TModel>(metadataProvider, new ModelStateDictionary())
        {
            Model = model,
        };
        var tempData = new TempDataDictionary(
            httpContext,
            services.GetRequiredService<ITempDataProvider>());
        await using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewData,
            tempData,
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
