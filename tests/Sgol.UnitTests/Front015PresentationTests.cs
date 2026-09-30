using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Execution.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Pages.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front015PresentationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid Actor = Guid.Parse("019d9400-0000-7000-8000-000000000010");
    private static readonly Guid Plan = Guid.Parse("019d9400-0000-7000-8000-000000000011");
    private static readonly Guid Period = Guid.Parse("019d9400-0000-7000-8000-000000000012");
    private static readonly Guid Publication = Guid.Parse("019d9400-0000-7000-8000-000000000013");
    private static readonly Guid FrozenAssignment = Guid.Parse("019d9400-0000-7000-8000-000000000014");
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CanonicalRole.Direction)]
    [InlineData(CanonicalRole.Administration)]
    [InlineData(CanonicalRole.Subcoordination)]
    [InlineData(CanonicalRole.SalesFloor)]
    public async Task EveryViewerSeesOnePlanAndSeparateCurrentAndFrozenContent(string role)
    {
        using var scope = factory.Services.CreateScope(); var api = new Api();
        var model = Model(scope.ServiceProvider, api, Context(scope.ServiceProvider, "?publicationId=" + Publication), role);
        await Get(model);
        var html = await Render(scope.ServiceProvider, model); var readable = WebUtility.HtmlDecode(html);
        Assert.Contains("Plan semanal", readable); Assert.Contains("TAR-0007", readable); Assert.Contains("TAR-0005", readable);
        Assert.Contains("Publicado", readable); Assert.Contains("Sólo se muestra el contenido permitido", readable);
        Assert.Contains(FrozenAssignment.ToString("D"), html); Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("<script>Sintética</script>", readable);
        Assert.DoesNotContain("<script>Sintética", html);
        Assert.Equal(role != CanonicalRole.SalesFloor, html.Contains("Preparar publicación", StringComparison.Ordinal));
        Assert.Contains("Crear o recuperar plan", html); Assert.All(api.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        Assert.DoesNotContain("/mi-trabajo", html); Assert.DoesNotContain("href=\"/api/v1/obligations", html);
        await Preview("normal-" + role, html);
    }

    [Fact]
    public async Task EmptyPlanAndEmptyHistoryHaveDifferentMessagesAndDoNotPublish()
    {
        using var scope = factory.Services.CreateScope(); var api = new Api { Empty = true };
        var model = Model(scope.ServiceProvider, api, Context(scope.ServiceProvider)); await Get(model);
        var html = WebUtility.HtmlDecode(await Render(scope.ServiceProvider, model));
        Assert.Contains("No hay obligaciones visibles en este plan", html);
        Assert.Contains("No hay publicaciones visibles en tu alcance", html);
        Assert.All(api.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        await Preview("empty", await Render(scope.ServiceProvider, model));
    }

    [Fact]
    public async Task MissingPlanOffersExplicitEnsureWithoutMutationOnGet()
    {
        using var scope = factory.Services.CreateScope(); var api = new Api { HasPlan = false };
        var model = Model(scope.ServiceProvider, api, Context(scope.ServiceProvider), CanonicalRole.SalesFloor); await Get(model);
        var html = WebUtility.HtmlDecode(await Render(scope.ServiceProvider, model));
        Assert.Contains("Aún no existe un plan para esta semana", html); Assert.NotNull(model.EnsurePlanToken);
        Assert.DoesNotContain(api.Requests, r => r.Path.EndsWith("/versions", StringComparison.Ordinal));
        Assert.All(api.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        await Preview("missing", await Render(scope.ServiceProvider, model));
    }

    [Fact]
    public async Task EnsurePreservesItsIntentionAndRecoversTheSamePlan()
    {
        using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider; var api = new Api { HasPlan = false };
        var initial = Model(services, api, Context(services), CanonicalRole.SalesFloor); await Get(initial);
        var fields = new Dictionary<string, string> { ["planIntent"] = initial.EnsurePlanToken! };
        var first = Model(services, api, await Form(services, fields), CanonicalRole.SalesFloor);
        await first.OnPostEnsurePlanAsync(default);
        var second = Model(services, api, await Form(services, fields), CanonicalRole.SalesFloor);
        await second.OnPostEnsurePlanAsync(default);
        Assert.Equal(Plan, first.EnsuredPlan!.PlanId); Assert.Equal(Plan, second.EnsuredPlan!.PlanId);
        Assert.Equal("Se recuperó el plan semanal existente", second.PlanSuccess);
        var posts = api.Requests.Where(r => r.Method == HttpMethod.Post).ToArray(); Assert.Equal(2, posts.Length);
        Assert.Equal(posts[0].Intent!.Key, posts[1].Intent!.Key); Assert.All(posts, r => Assert.Null(r.IfMatch));
        Assert.All(posts, r => Assert.EndsWith("/ensure", r.Path));
    }

    [Fact]
    public async Task PublicationPreparationIsReadOnlyAndConfirmationHasNoScopeOrReasonFields()
    {
        using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider; var api = new Api();
        var model = Model(services, api, await Form(services, Preparation()));
        await model.OnPostPreparePlanPublicationAsync(default);
        Assert.True(model.PlanConfirmation); Assert.NotNull(model.PublishPlanToken);
        Assert.All(api.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        var html = await Render(services, model);
        Assert.Contains("data-initial-focus", html); Assert.Contains("data-plan-confirm", html);
        Assert.DoesNotContain("<textarea", html); Assert.DoesNotContain("name=\"scopeRole\"", html);
        Assert.DoesNotContain("name=\"obligationIds\"", html); await Preview("confirmation", html);
    }

    [Fact]
    public async Task StalePreparationCannotSilentlyAdoptTheNewEtag()
    {
        using var scope = factory.Services.CreateScope(); var fields = Preparation(); fields["etag"] = "\"2\"";
        var api = new Api(); var model = Model(scope.ServiceProvider, api, await Form(scope.ServiceProvider, fields));
        await model.OnPostPreparePlanPublicationAsync(default);
        Assert.True(model.PlanConflict); Assert.Null(model.PublishPlanToken); Assert.Equal(412, model.Response.StatusCode);
        Assert.All(api.Requests, r => Assert.Equal(HttpMethod.Get, r.Method)); await Preview("conflict-preparation", await Render(scope.ServiceProvider, model));
    }

    [Fact]
    public async Task PublicationConflictDoesNotRetryAndRequiresExplicitReload()
    {
        using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider; var api = new Api { Conflict = true };
        var preparation = Model(services, api, await Form(services, Preparation())); await preparation.OnPostPreparePlanPublicationAsync(default);
        var model = Model(services, api, await Form(services, new() { ["planIntent"] = preparation.PublishPlanToken! }));
        await model.OnPostPublishPlanAsync(default);
        Assert.True(model.PlanConflict); Assert.Null(model.PublishedPlan); Assert.Null(model.PlanSuccess);
        Assert.Single(api.Requests, r => r.Method == HttpMethod.Post);
        var html = WebUtility.HtmlDecode(await Render(services, model)); Assert.Contains("Recargar plan", html);
        Assert.DoesNotContain("PreparePlanPublication", html); Assert.DoesNotContain("Reenviar la misma solicitud", html);
        await Preview("conflict", await Render(services, model));
    }

    [Fact]
    public async Task UncertainPublicationCanOnlyResendItsOriginalKeyAndEtag()
    {
        using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider; var api = new Api { UncertainFirst = true };
        var preparation = Model(services, api, await Form(services, Preparation())); await preparation.OnPostPreparePlanPublicationAsync(default);
        var fields = new Dictionary<string, string> { ["planIntent"] = preparation.PublishPlanToken! };
        var uncertain = Model(services, api, await Form(services, fields)); await uncertain.OnPostPublishPlanAsync(default);
        Assert.True(uncertain.PlanUncertain); Assert.Null(uncertain.PlanSuccess);
        Assert.Equal(preparation.PublishPlanToken, uncertain.PublishPlanToken);
        var html = WebUtility.HtmlDecode(await Render(services, uncertain)); Assert.Contains("Reenviar la misma solicitud", html);
        Assert.DoesNotContain("PreparePlanPublication", html); await Preview("uncertain", await Render(services, uncertain));
        var recovered = Model(services, api, await Form(services, fields)); await recovered.OnPostPublishPlanAsync(default);
        Assert.Equal("Se recuperó la publicación ya confirmada", recovered.PlanSuccess);
        var posts = api.Requests.Where(r => r.Method == HttpMethod.Post).ToArray(); Assert.Equal(2, posts.Length);
        Assert.Equal(posts[0].Intent!.Key, posts[1].Intent!.Key); Assert.Equal("\"3\"", posts[0].IfMatch); Assert.Equal(posts[0].IfMatch, posts[1].IfMatch);
        await Preview("recovered", await Render(services, recovered));
    }

    [Fact]
    public async Task CsrfTamperingAndAnotherActorCannotMutate()
    {
        using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider; var api = new Api();
        var initial = Model(services, api, Context(services)); await Get(initial);
        var invalidCsrf = await Form(services, new() { ["planIntent"] = initial.EnsurePlanToken! }); invalidCsrf.Request.Headers.Cookie = "";
        Assert.IsType<BadRequestResult>(await Model(services, api, invalidCsrf).OnPostEnsurePlanAsync(default));
        Assert.IsType<BadRequestResult>(await Model(services, api, await Form(services, new() { ["planIntent"] = "tampered" })).OnPostEnsurePlanAsync(default));
        var other = Model(services, api, await Form(services, new() { ["planIntent"] = initial.EnsurePlanToken! }), actor: Guid.NewGuid());
        Assert.IsType<BadRequestResult>(await other.OnPostEnsurePlanAsync(default));
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task FloorCannotPrepareOrSubmitPublicationEvenWithAValidProtectedToken()
    {
        using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider; var api = new Api();
        var preparation = Model(services, api, await Form(services, Preparation())); await preparation.OnPostPreparePlanPublicationAsync(default);
        var floor = Model(services, api, await Form(services, new() { ["planIntent"] = preparation.PublishPlanToken! }), CanonicalRole.SalesFloor);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await floor.OnPostPublishPlanAsync(default)).StatusCode);
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task DeniedPlanAndInvalidCursorDoNotExposeResidualRows()
    {
        using var scope = factory.Services.CreateScope(); var services = scope.ServiceProvider; var api = new Api { Denied = true };
        var denied = Model(services, api, Context(services)); await Get(denied);
        var html = WebUtility.HtmlDecode(await Render(services, denied)); Assert.Contains("No tienes permiso para consultar este plan", html);
        Assert.DoesNotContain(Plan.ToString("D"), html); Assert.DoesNotContain("Crear o recuperar plan", html);
        await Preview("denied", await Render(services, denied));
        var cursorApi = new Api(); var invalid = Model(services, cursorApi, Context(services, "?planItemsCursor=tampered")); await Get(invalid);
        Assert.Empty(invalid.PlanObligations); Assert.NotNull(invalid.PlanItemsError);
        Assert.DoesNotContain(cursorApi.Requests, r => r.Path.StartsWith("/api/v1/obligations?", StringComparison.Ordinal));
    }

    private static Dictionary<string, string> Preparation() => new() { ["isoYear"] = "2026", ["isoWeek"] = "40", ["planId"] = Plan.ToString("D"), ["etag"] = "\"3\"" };
    private static Task<IActionResult> Get(IndexModel model) => model.OnGetAsync("2026", "40", "2026-09-28", "2026-10-04", null, null, null, null, default);
    private static DefaultHttpContext Context(IServiceProvider services, string query = "")
    {
        var context = new DefaultHttpContext { RequestServices = services }; context.Request.Scheme = "https";
        context.Request.Path = "/planificacion"; context.Request.QueryString = new(query); return context;
    }
    private static IndexModel Model(IServiceProvider services, Api api, HttpContext context, string role = CanonicalRole.Subcoordination, Guid? actor = null)
    {
        var session = new Session(role, actor ?? Actor); var antiforgery = services.GetRequiredService<IAntiforgery>();
        return new(session, api, new RazorAntiforgeryBridge(antiforgery, api, session), services.GetRequiredService<IDataProtectionProvider>(), antiforgery)
        { PageContext = new PageContext { HttpContext = context } };
    }
    private static async Task<DefaultHttpContext> Form(IServiceProvider services, Dictionary<string, string> fields)
    {
        var issuance = Context(services); var tokens = services.GetRequiredService<IAntiforgery>().GetAndStoreTokens(issuance);
        var context = Context(services); context.Request.Method = "POST"; context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Headers.Cookie = issuance.Response.Headers.SetCookie.ToString().Split(';')[0]; fields["__RequestVerificationToken"] = tokens.RequestToken!;
        using var content = new FormUrlEncodedContent(fields); var bytes = await content.ReadAsByteArrayAsync();
        context.Request.ContentLength = bytes.Length; context.Request.Body = new MemoryStream(bytes); return context;
    }
    private static async Task<string> Render(IServiceProvider services, IndexModel model)
    {
        var context = model.HttpContext; context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "synthetic-render"));
        var route = new RouteData(); route.Values["page"] = "/Planning/Index";
        var action = new ActionContext(context, route, new ActionDescriptor()); var view = services.GetRequiredService<IRazorViewEngine>().GetView(null, "/Pages/Planning/_Plans.cshtml", false); Assert.True(view.Success);
        var data = new ViewDataDictionary<IndexModel>(services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
        var temp = new TempDataDictionary(context, services.GetRequiredService<ITempDataProvider>()); await using var writer = new StringWriter(CultureInfo.InvariantCulture);
        await view.View!.RenderAsync(new ViewContext(action, view.View, data, temp, writer, new HtmlHelperOptions())); return writer.ToString();
    }
    private static async Task Preview(string name, string html)
    {
        var output = Environment.GetEnvironmentVariable("SGOL_FRONT015_PREVIEW_DIR"); if (string.IsNullOrEmpty(output)) return;
        html = Regex.Replace(html, "(<input[^>]*type=\"hidden\"[^>]*value=\")[^\"]*", "$1", RegexOptions.CultureInvariant);
        Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, name + ".html"), html);
    }
    private sealed class Session(string role, Guid actor) : IRazorSessionState
    {
        public bool IsInvalid { get; private set; }
        public void Invalidate() => IsInvalid = true;
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(IsInvalid ? null : new(
            actor, actor, "synthetic", "Sintética", "LOR-001", role,
            role == CanonicalRole.SalesFloor ? ["PER-PLAN-VER", "PER-TAREA-VER"] : ["PER-PLAN-VER", "PER-TAREA-VER", "PER-PLAN-PUBLICAR"],
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow.AddHours(8)));
    }
    private sealed class Api : ISgolApiClient
    {
        public List<ApiRequest> Requests { get; } = [];
        public bool HasPlan { get; set; } = true;
        public bool Empty { get; init; }
        public bool Denied { get; init; }
        public bool Conflict { get; init; }
        public bool UncertainFirst { get; init; }
        public Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request); object? data = null; IReadOnlyList<T>? items = []; string? etag = null;
            if (request.Method == HttpMethod.Post)
            {
                if (UncertainFirst && Requests.Count(r => r.Method == HttpMethod.Post) == 1) throw new ApiProtocolException();
                if (Conflict) return Task.FromResult(Error<T>(412, "VERSION_CONFLICT"));
                if (request.Path.EndsWith("/ensure", StringComparison.Ordinal))
                { data = new WorkPlanEnsureResult(HasPlan ? "RECUPERADA" : "CREADA", Plan, Plan, "LOR-001", Period, 2026, 40, "PUBLICADO", 3); HasPlan = true; etag = "\"3\""; }
                else { data = new PlanPublicationResult("RECUPERADA", Plan, "PUBLICADO", Publication, 1, "VIGENTE", "SUBCOORDINACION", Actor, Now, [], [], 4); etag = "\"4\""; }
            }
            else if (typeof(T) == typeof(WeekPeriodDetails)) data = new WeekPeriodDetails(Period, "LOR-001", 2026, 40, new(2026, 9, 28), new(2026, 10, 4), "VIGENTE");
            else if (typeof(T) == typeof(WorkPlanDetails))
            {
                if (Denied) return Task.FromResult(Error<T>(403, "ACCESO_DENEGADO"));
                if (!HasPlan) return Task.FromResult(Error<T>(404, "PLAN_NO_ENCONTRADO"));
                data = new WorkPlanDetails(Plan, Plan, "LOR-001", Period, 2026, 40, "PUBLICADO", 3); etag = "\"3\"";
            }
            else if (typeof(T) == typeof(ObligationListItem) && !Empty) items = [(T)(object)Obligation("TAR-0007"), (T)(object)Obligation("TAR-0005")];
            else if (typeof(T) == typeof(PlanVersionDetails) && !Empty) items = [(T)(object)Version()];
            else if (typeof(T) == typeof(PlanSnapshotDetails)) data = new PlanSnapshotDetails(Version(), [new(Actor, FrozenAssignment)]);
            return Task.FromResult(new ApiResponse<T>(200, data is null ? default : (T)data, items, Actor.ToString("D"), null, items?.Count ?? 0, etag, false, null, null));
        }
        private static PlanVersionDetails Version() => new(Publication, Plan, 1, "SUSTITUIDA", "SUBCOORDINACION", Actor, Now, null, 2);
        private static ObligationListItem Obligation(string code) => new(Actor,
            new(Actor, code, "Tarea sintética", new(Actor, 1, "VIGENTE", Now, null, 1)),
            new("MANUAL", "MANUAL_REFERENCE_V1", "SYN", Actor, Actor, Now), new(Period, 2026, 40, new(2026, 9, 28), new(2026, 10, 4), "America/Mexico_City"),
            new(null, null, null), "PENDIENTE", "NO_VENCIDA", new(Actor, new(Actor, "SYN-001", "<script>Sintética</script>"), "AUTOMATICA", Now), new Dictionary<string, string>());
        private static ApiResponse<T> Error<T>(int status, string code) => new(status, default, null, Actor.ToString("D"), null, null, null, false, code, ProblemDetailsPresenter.Present(status, code, Actor.ToString("D")));
    }
}
