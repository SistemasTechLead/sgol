using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Xunit;

namespace Sgol.UnitTests;

// Pure Razor rendering: synthetic view states, no server, persistence or TLS assertions.
public sealed class RenewedDesignPreviewTests
{
    [Theory]
    [InlineData("Access/Index", "/acceso")]
    [InlineData("Entry", "/")]
    [InlineData("Branches/Details", "/branches/LOR-001")]
    [InlineData("People/Index", "/personas-y-accesos")]
    [InlineData("People/Details", "/personas-y-accesos/personas/synthetic")]
    [InlineData("Configuration/Index", "/configuracion")]
    [InlineData("Planning/Index", "/planificacion")]
    [InlineData("MyWork/Index", "/mi-trabajo")]
    [InlineData("MyWork/Details", "/mi-trabajo/tareas/synthetic")]
    [InlineData("People/Details", "/personas-y-accesos/personas/synthetic", "normal")]
    [InlineData("Branches/Details", "/branches/LOR-001", "loading")]
    [InlineData("Access/Index", "/acceso", "error")]
    [InlineData("MyWork/Index", "/mi-trabajo", "normal")]
    [InlineData("MyWork/Details", "/mi-trabajo/tareas/synthetic", "normal")]
    [InlineData("Configuration/Index", "/configuracion", "normal")]
    [InlineData("Planning/Index", "/planificacion", "normal")]
    public async Task ExistingPageAndSharedLayoutRenderSyntheticStates(string page, string path, string state = "empty")
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IRazorSessionState, SyntheticSession>()));
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https"; context.Request.Path = path;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "synthetic-render"));
        var modelType = typeof(Program).Assembly.GetType("Sgol.Web.Pages." + page.Replace('/', '.') + "Model")!;
        var model = page == "Branches/Details" ? new Sgol.Web.Pages.Branches.DetailsModel(null!) : (PageModel)ActivatorUtilities.CreateInstance(services, modelType);
        var route = new RouteData(); route.Values["page"] = "/" + page;
        model.PageContext = new PageContext { HttpContext = context, RouteData = route };
        void Set(string name, object value) => modelType.GetProperty(name)?.SetValue(model, value);
        foreach (var name in new[] { "CanShowPeople", "CanManageAccounts", "CanManage", "CanShow", "CanViewWeek", "CanEditCalendar", "CanViewTasks", "CanViewInbox" }) Set(name, true);
        foreach (var property in modelType.GetProperties().Where(p => p.Name.StartsWith("Can", StringComparison.Ordinal) && p.PropertyType == typeof(bool) && p.SetMethod is not null)) property.SetValue(model, true);
        var unavailable = new ProblemDetailsPresentation("No existe o no está disponible en tu alcance", page == "People/Details" ? "Vuelve a la lista de personas." : "Vuelve a la consulta.", null);
        if (page.EndsWith("Details", StringComparison.Ordinal)) Set("Error", unavailable);
        if (page == "Branches/Details")
        {
            Set("IsLoading", false); Set("Error", null!);
            Set("EmptyState", new Sgol.Web.Presentation.Components.EmptyStateViewModel("No se encontró la sucursal Loretta", "La semilla canónica LOR-001 no está disponible.", "Volver a consultar", "/branches/LOR-001"));
        }
        if (page == "MyWork/Index") Set("Inbox", new Sgol.Web.Presentation.MyWork.InboxData(null, new([], null, 0, true), new([], null, 0, true), true));
        if (page == "Configuration/Index") Set("Branch", new Sgol.Organization.Contracts.BranchCatalogItem(Guid.Parse("019d9300-0000-7000-8000-000000000001"), "LOR-001", "Loretta", "ACTIVA", "America/Mexico_City"));
        if (page == "People/Details" && state == "normal")
        {
            Set("Error", null!); Set("ETag", "\"1\"");
            Set("Person", new Sgol.Organization.Contracts.PersonDetails(Guid.Parse("019d9300-0000-7000-8000-000000000002"), "SIN-001", "Persona sintética", new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero),
                [new(Guid.Parse("019d9300-0000-7000-8000-000000000003"), "ACTIVA", new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero), null, null, 1, "Piso de ventas", "Matutino")]));
        }
        if (page == "Branches/Details" && state == "loading") { Set("Error", null!); Set("IsLoading", true); }
        if (page == "Access/Index" && state == "error") { Set("ErrorTitle", "No se pudo verificar la solicitud"); Set("ErrorMessage", "Recarga la página antes de volver a enviarla."); }
        if (page == "MyWork/Index" && state == "normal")
        {
            var task = new Sgol.Notifications.Contracts.InboxTask(Guid.Parse("019d9300-0000-7000-8000-000000000020"), new(Guid.Parse("019d9300-0000-7000-8000-000000000021"), "TAR-0008", "Tarea sintética"),
                new(Guid.Parse("019d9300-0000-7000-8000-000000000022"), 2026, 40, new(2026, 9, 28), new(2026, 10, 4), "America/Mexico_City"), "MANUAL", false, new(null, null, null), "PENDIENTE", "DISPONIBLE", new("INCOMPLETA", []), ["VIEW_TASK"]);
            Set("Inbox", new Sgol.Web.Presentation.MyWork.InboxData(task.Period, new([task], null, 1, false), new([], null, 0, true), false));
        }
        var syntheticId = Guid.Parse("019d9300-0000-7000-8000-000000000030");
        var now = new DateTimeOffset(2026, 9, 30, 18, 0, 0, TimeSpan.Zero);
        if (page == "MyWork/Details" && state == "normal")
        {
            Set("Error", null!);
            Set("Detail", new Sgol.Execution.Contracts.ObligationDetail(syntheticId,
                new(syntheticId, "TAR-0008", "Tarea sintética", new(syntheticId, 1, "VIGENTE", now, null, 1)),
                new("MANUAL", "CAT", "Referencia sintética", syntheticId, syntheticId, now), new(syntheticId, 2026, 40, new(2026, 9, 28), new(2026, 10, 4), "America/Mexico_City"),
                new(null, null, null), "PENDIENTE", "DISPONIBLE", null, new Dictionary<string, string>(), new(syntheticId, "COMPLETADA", now, syntheticId), []));
        }
        if (page == "Configuration/Index" && state == "normal")
        {
            var task = new Sgol.Configuration.Contracts.TaskDefinitionDetails(syntheticId, "TAR-0008", "Tarea sintética", null, null, null, null, [], []);
            Set("SelectedTask", task); Set("TaskDefinitions", new[] { task }); Set("SelectedTaskCode", "TAR-0008");
        }
        if (page == "Planning/Index" && state == "normal")
        {
            Set("ManualOptions", new[] { new Sgol.Generation.Contracts.ManualGenerationOption("TAR-0008", "Tarea sintética", syntheticId, syntheticId, syntheticId, "TAR0008") }); Set("ManualTaskCode", "TAR-0008");
        }
        var descriptor = new Microsoft.AspNetCore.Mvc.RazorPages.PageActionDescriptor { RelativePath = "/Pages/" + page + ".cshtml", ViewEnginePath = "/" + page };
        descriptor.RouteValues["page"] = "/" + page;
        var action = new ActionContext(context, route, descriptor);
        var view = services.GetRequiredService<IRazorViewEngine>().GetView(null, "/Pages/" + page + ".cshtml", isMainPage: true);
        Assert.True(view.Success);
        var data = (ViewDataDictionary)Activator.CreateInstance(typeof(ViewDataDictionary<>).MakeGenericType(modelType), services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary())!;
        data.Model = model;
        model.PageContext.ViewData = data;
        if (view.View is RazorView razor && razor.RazorPage is Page razorPage)
            razorPage.PageContext = model.PageContext;
        var temp = new TempDataDictionary(context, services.GetRequiredService<ITempDataProvider>());
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        await view.View.RenderAsync(new ViewContext(action, view.View, data, temp, writer, new HtmlHelperOptions()));
        var html = writer.ToString();
        Assert.Contains("contenido-principal", html);
        Assert.Contains("<h1", html);
        var output = Environment.GetEnvironmentVariable("SGOL_RENEWED_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        output = Path.GetFullPath(output);
        Assert.DoesNotContain("Fuentes", output, StringComparison.OrdinalIgnoreCase);
        Directory.CreateDirectory(output);
        // Strip hidden credentials/intent tokens before any artifact leaves the test.
        html = Regex.Replace(html, "(<input[^>]*type=\"hidden\"[^>]*value=\")[^\"]*(\")", "$1$2", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "(href|src)=\"/?(?:~/)?(css|js)/([^\"?]+)(?:\\?[^\"]*)?\"", "$1=\"assets/$2/$3\"");
        await File.WriteAllTextAsync(Path.Combine(output, page.Replace('/', '-') + "-" + state + ".html"), html);
    }

    private sealed class SyntheticSession : IRazorSessionState
    {
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(new(
            Guid.Parse("019d9300-0000-7000-8000-000000000001"), Guid.Parse("019d9300-0000-7000-8000-000000000002"),
            "synthetic", "Persona sintética", "LOR-001", "DIRECCION", ["PER-BANDEJA-PROPIA", "PER-PERSONA-ADMIN"],
            new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero), new(2026, 9, 30, 18, 30, 0, TimeSpan.Zero), new(2026, 10, 1, 2, 0, 0, TimeSpan.Zero)));
    }
}
