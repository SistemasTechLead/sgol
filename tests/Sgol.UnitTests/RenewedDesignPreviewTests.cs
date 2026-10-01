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
    private static readonly string[] SyntheticRecoveryCodes = ["CODIGO-SINTETICO-01", "CODIGO-SINTETICO-02"];
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
    [InlineData("Access/Index", "/acceso", "password")]
    [InlineData("Access/Index", "/acceso", "enroll")]
    [InlineData("Access/Index", "/acceso", "enroll-key")]
    [InlineData("Access/Index", "/acceso", "verify")]
    [InlineData("Access/Index", "/acceso", "regenerate")]
    [InlineData("Access/Index", "/acceso", "codes")]
    [InlineData("Access/Index", "/acceso", "complete")]
    [InlineData("Access/Index", "/acceso", "expired")]
    [InlineData("Configuration/Index", "/configuracion", "TAR-0005")]
    [InlineData("Configuration/Index", "/configuracion", "TAR-0007")]
    [InlineData("Configuration/Index", "/configuracion", "TAR-0008")]
    [InlineData("Configuration/Index", "/configuracion", "TAR-0011")]
    [InlineData("Configuration/Index", "/configuracion", "TAR-0018")]
    [InlineData("Configuration/Index", "/configuracion", "TAR-0026")]
    [InlineData("Configuration/Index", "/configuracion", "TAR-0092")]
    [InlineData("Configuration/Index", "/configuracion", "TAR-0093")]
    [InlineData("Planning/Index", "/planificacion", "TAR-0007")]
    [InlineData("Planning/Index", "/planificacion", "TAR-0008")]
    [InlineData("Planning/Index", "/planificacion", "TAR-0011")]
    [InlineData("Planning/Index", "/planificacion", "TAR-0018")]
    [InlineData("Planning/Index", "/planificacion", "TAR-0092")]
    [InlineData("Planning/Index", "/planificacion", "TAR-0093")]
    [InlineData("People/Index", "/personas-y-accesos", "normal")]
    [InlineData("MyWork/Index", "/mi-trabajo", "normal-ADMINISTRACION")]
    [InlineData("MyWork/Index", "/mi-trabajo", "normal-SUBCOORDINACION")]
    [InlineData("MyWork/Index", "/mi-trabajo", "normal-PISO_VENTAS")]
    public async Task ExistingPageAndSharedLayoutRenderSyntheticStates(string page, string path, string state = "empty")
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IRazorSessionState>(_ => new SyntheticSession(state.StartsWith("normal-", StringComparison.Ordinal) ? state[7..] : "DIRECCION"))));
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
        if (page == "MyWork/Index" && state.StartsWith("normal", StringComparison.Ordinal))
        {
            var task = new Sgol.Notifications.Contracts.InboxTask(Guid.Parse("019d9300-0000-7000-8000-000000000020"), new(Guid.Parse("019d9300-0000-7000-8000-000000000021"), "TAR-0008", Sgol.Configuration.Contracts.TaskDefinitionCatalog.Require("TAR-0008").Name),
                new(Guid.Parse("019d9300-0000-7000-8000-000000000022"), 2026, 40, new(2026, 9, 28), new(2026, 10, 4), "America/Mexico_City"), "MANUAL", false, new(null, null, null), "PENDIENTE", "DISPONIBLE", new("INCOMPLETA", []), ["VIEW_TASK"]);
            Set("Inbox", new Sgol.Web.Presentation.MyWork.InboxData(task.Period, new([task, task with { ObligationId = Guid.Parse("019d9300-0000-7000-8000-000000000023"), TaskState = "VENCIDA", Dates = new(new(2026, 9, 30, 15, 55, 0, TimeSpan.Zero), null, null) }, task with { ObligationId = Guid.Parse("019d9300-0000-7000-8000-000000000024"), ExecutionStatus = "CONCLUIDA", TaskState = "CONCLUIDA", Evidence = new("COMPLETA", []) }], null, 3, false), new([new(Guid.Parse("019d9300-0000-7000-8000-000000000025"), "OBLIGATION_ASSIGNED", "UNREAD", new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero), null, new("ASSIGNMENT_VERSION", task.ObligationId, true, task.ObligationId, "TAR-0008", task.Task.Name), ["MARK_NOTICE_READ"])], null, 1, false), false));
        }
        var syntheticId = Guid.Parse("019d9300-0000-7000-8000-000000000030");
        var now = new DateTimeOffset(2026, 9, 30, 18, 0, 0, TimeSpan.Zero);
        if (page == "MyWork/Details" && state == "normal")
        {
            Set("Error", null!);
            Set("Detail", new Sgol.Execution.Contracts.ObligationDetail(syntheticId,
                new(syntheticId, "TAR-0008", Sgol.Configuration.Contracts.TaskDefinitionCatalog.Require("TAR-0008").Name, new(syntheticId, 1, "VIGENTE", now, null, 1)),
                new("MANUAL", "CAT", "Referencia sintética", syntheticId, syntheticId, now), new(syntheticId, 2026, 40, new(2026, 9, 28), new(2026, 10, 4), "America/Mexico_City"),
                new(null, null, null), "PENDIENTE", "DISPONIBLE", null, new Dictionary<string, string>(), new(syntheticId, "COMPLETADA", now, syntheticId), []));
        }
        if (page == "Configuration/Index" && state == "normal")
        {
            var task = new Sgol.Configuration.Contracts.TaskDefinitionDetails(syntheticId, "TAR-0008", Sgol.Configuration.Contracts.TaskDefinitionCatalog.Require("TAR-0008").Name, null, null, null, null, [], []);
            Set("SelectedTask", task); Set("TaskDefinitions", new[] { task }); Set("SelectedTaskCode", "TAR-0008");
        }
        if (page == "Planning/Index" && state == "normal")
        {
            Set("ManualOptions", new[] { new Sgol.Generation.Contracts.ManualGenerationOption("TAR-0008", Sgol.Configuration.Contracts.TaskDefinitionCatalog.Require("TAR-0008").Name, syntheticId, syntheticId, syntheticId, "TAR0008") }); Set("ManualTaskCode", "TAR-0008");
        }
        if (page == "Access/Index" && state is "password" or "enroll" or "enroll-key" or "verify" or "regenerate" or "codes" or "complete" or "expired")
        {
            Set("Stage", state);
            Set("ManualKey", "CLAVE SINTETICA NO UTILIZABLE");
            Set("RecoveryCodes", SyntheticRecoveryCodes);
        }
        if (page == "People/Details" && state == "normal")
        {
            Set("FromDate", "2026-09-28"); Set("ToDate", "2026-10-04"); Set("SelectedDay", "2026-09-30");
            Set("AvailabilityDays", new Sgol.Web.Pages.People.DetailsModel.AvailabilityDay[] { new(new(2026, 9, 28), true), new(new(2026, 9, 29), false), new(new(2026, 9, 30), null) });
        }
        if (page == "People/Index" && state == "normal")
        {
            Set("People", new Sgol.Organization.Contracts.PersonSummary[] { new(syntheticId, "SIN-001", "Persona sintética A"), new(Guid.Parse("019d9300-0000-7000-8000-000000000031"), "SIN-002", "Persona sintética B") });
            Set("Accounts", new Sgol.Identity.Contracts.AccountSummary[] { new(syntheticId, syntheticId, "persona.sintetica.a", "ACTIVA", false, now), new(Guid.Parse("019d9300-0000-7000-8000-000000000031"), syntheticId, "persona.sintetica.b", "INACTIVA", false, now) });
        }
        if (page == "Configuration/Index" && (state.StartsWith("TAR-", StringComparison.Ordinal) || state == "normal"))
        {
            var code = state.StartsWith("TAR-", StringComparison.Ordinal) ? state : "TAR-0008";
            var catalog = Sgol.Configuration.Contracts.TaskDefinitionCatalog.All.Select(seed => new Sgol.Configuration.Contracts.TaskDefinitionDetails(seed.Id, seed.TaskCode, seed.Name, null, null, null, null, [], [])).ToArray();
            Set("SelectedTaskCode", code); Set("SelectedTask", catalog.Single(item => item.TaskCode == code)); Set("TaskDefinitions", catalog);
            Set("Releases", new Sgol.Configuration.Contracts.ConfigurationReleaseDetails[] { new(syntheticId, null, "BORRADOR", null, null, null, null, null, null, 1) });
        }
        if (page == "Planning/Index" && state.StartsWith("TAR-", StringComparison.Ordinal))
        {
            var code = state; var origin = code.Replace("-", "", StringComparison.Ordinal);
            Set("ManualOptions", new Sgol.Generation.Contracts.ManualGenerationOption[] { new(code, Sgol.Configuration.Contracts.TaskDefinitionCatalog.Require(code).Name, syntheticId, syntheticId, syntheticId, origin) });
            Set("ManualTaskCode", code);
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
        if (page == "Access/Index")
        {
            Assert.Contains("Delicias, Chihuahua", System.Net.WebUtility.HtmlDecode(html));
            Assert.Contains("version-sistema", html);
            Assert.DoesNotContain("Gestionar fotografía", html);
        }
        if (page == "People/Details" && state == "normal")
        {
            Assert.Contains("name=\"etag\"", html);
            Assert.Contains("name=\"intentKey\"", html);
            Assert.Contains("name=\"isAvailable\"", html);
        }
        if (page == "Configuration/Index" && state.StartsWith("TAR-", StringComparison.Ordinal))
        {
            Assert.Contains(state, html);
            Assert.Contains("name=\"taskCode\"", html);
            Assert.DoesNotContain("Crear nueva TAR", html);
        }
        var output = Environment.GetEnvironmentVariable("SGOL_RENEWED_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        output = Path.GetFullPath(output);
        Assert.DoesNotContain("Fuentes", output, StringComparison.OrdinalIgnoreCase);
        Directory.CreateDirectory(output);
        // Strip hidden credentials/intent tokens before any artifact leaves the test.
        html = Regex.Replace(html, "(<input[^>]*type=\"hidden\"[^>]*value=\")[^\"]*(\")", "$1$2", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "(href|src)=\"/?(?:~/)?(css|js|images|fonts)/([^\"?]+)(?:\\?[^\"]*)?\"", "$1=\"assets/$2/$3\"");
        await File.WriteAllTextAsync(Path.Combine(output, page.Replace('/', '-') + "-" + state + ".html"), html);
    }

    private sealed class SyntheticSession(string role) : IRazorSessionState
    {
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(new(
            Guid.Parse("019d9300-0000-7000-8000-000000000001"), Guid.Parse("019d9300-0000-7000-8000-000000000002"),
            "synthetic", "Persona sintética", "LOR-001", role, ["PER-BANDEJA-PROPIA", "PER-PERSONA-ADMIN"],
            new(2026, 9, 30, 18, 0, 0, TimeSpan.Zero), new(2026, 9, 30, 18, 30, 0, TimeSpan.Zero), new(2026, 10, 1, 2, 0, 0, TimeSpan.Zero)));
    }
}
