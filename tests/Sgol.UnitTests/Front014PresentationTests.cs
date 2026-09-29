using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
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
using Sgol.Assignment.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Web.Pages.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front014PresentationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public Front014PresentationTests(WebApplicationFactory<Program> factory) => this.factory = factory;
    private static readonly Guid Actor = Guid.Parse("019d9300-0000-7000-8000-000000000010");
    private static readonly Guid Obligation = Guid.Parse("019d9300-0000-7000-8000-000000000011");
    private static readonly Guid Evaluation = Guid.Parse("019d9300-0000-7000-8000-000000000012");
    private static readonly Guid Candidate = Guid.Parse("019d9300-0000-7000-8000-000000000013");
    private static readonly Guid Previous = Guid.Parse("019d9300-0000-7000-8000-000000000014");
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
    private static readonly string[] Permissions = ["PER-TAREA-VER", "PER-ASIGNACION-EXPLICAR", "PER-CARGA-VER", "PER-ASIGNACION-CORREGIR"];

    [Fact]
    public async Task ReadOnlyPresentationPreservesReasonsOrderServerCountsNullAndEncodedNames()
    {
        using var scope = factory.Services.CreateScope();
        var api = new Api();
        var model = Model(scope.ServiceProvider, api, Context(scope.ServiceProvider, $"?obligationId={Obligation:D}"));
        await Get(model);
        var html = await Render(scope.ServiceProvider, model);
        var readable = WebUtility.HtmlDecode(html);
        Assert.Contains("Hay candidatos elegibles", readable);
        Assert.Contains("No informado", readable);
        Assert.Contains("CO-002", readable);
        Assert.True(readable.IndexOf("No disponible para la fecha evaluada", StringComparison.Ordinal) <
            readable.IndexOf("El turno no coincide", StringComparison.Ordinal));
        Assert.Contains("&lt;script&gt;Sint", html);
        Assert.Contains("&lt;/script&gt;", html);
        Assert.Contains("<script>Sintética</script>", readable);
        Assert.DoesNotContain("<script>Sintética</script>", html);
        Assert.Contains("<td>3</td>", html);
        Assert.Contains("<td>0</td>", html);
        Assert.DoesNotContain("value=\"" + Previous, html);
        Assert.DoesNotContain("value=\"019d9300-0000-7000-8000-000000000015", html);
        Assert.All(api.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        Assert.Equal("opaque-history", model.NextAssignmentHistoryCursor);
        await Preview("normal", html);
    }

    [Fact]
    public async Task EmptyStateDoesNotInventZeroLoadOrAnAssignment()
    {
        using var scope = factory.Services.CreateScope();
        var api = new Api { EmptyLoads = true };
        var model = Model(scope.ServiceProvider, api, Context(scope.ServiceProvider));
        await Get(model);
        var html = await Render(scope.ServiceProvider, model);
        Assert.Contains("Selecciona una obligación", WebUtility.HtmlDecode(html));
        Assert.Contains("No hay personas visibles", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("PrepareCorrection", html);
        Assert.DoesNotContain(api.Requests, r => r.Path.StartsWith("/api/v1/obligations", StringComparison.Ordinal));
        await Preview("empty", html);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidSelectionDoesNotProbeTheApi(string id)
    {
        using var scope = factory.Services.CreateScope();
        var api = new Api();
        var model = Model(scope.ServiceProvider, api, Context(scope.ServiceProvider, "?obligationId=" + id));
        await Get(model);
        Assert.Equal(404, model.Response.StatusCode);
        Assert.Null(model.AssignmentObligation);
        Assert.DoesNotContain(api.Requests, r => r.Path.StartsWith("/api/v1/obligations", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("DIRECCION", true)]
    [InlineData("ADMINISTRACION", true)]
    [InlineData("SUBCOORDINACION", true)]
    [InlineData("PISO_VENTAS", false)]
    public void PermissionProjectionOnlyExposesAssignmentActionsToApprovedRoles(string role, bool expected)
    {
        var projected = RolePermissionProjection.ForRole(role);
        Assert.Contains("PER-TAREA-VER", projected);
        foreach (var permission in Permissions.Skip(1)) Assert.Equal(expected, projected.Contains(permission));
    }

    [Fact]
    public async Task MissingPermissionDoesNotReadAssignmentDataOrRenderActions()
    {
        using var scope = factory.Services.CreateScope();
        var api = new Api();
        var context = Context(scope.ServiceProvider, $"?obligationId={Obligation:D}");
        var model = Model(scope.ServiceProvider, api, context, permissions: []);
        await Get(model);
        Assert.DoesNotContain(api.Requests, r => r.Path is not "/api/v1/calendar?from=2026-09-28&to=2026-10-04");
        Assert.Null(model.AssignmentObligation);
        var html = await Render(scope.ServiceProvider, model);
        Assert.DoesNotContain("CO-002", html);
        Assert.DoesNotContain("PrepareCorrection", html);
    }

    [Fact]
    public async Task ServerDenialsRemoveOnlyTheirSectionAndDoNotSimulateEmptySuccess()
    {
        using var scope = factory.Services.CreateScope();
        var api = new Api { DenyEligibility = true, DenyLoads = true };
        var model = Model(scope.ServiceProvider, api, Context(scope.ServiceProvider, $"?obligationId={Obligation:D}"));
        await Get(model);
        Assert.False(model.CanPrepareCorrection);
        Assert.Empty(model.ActiveLoads);
        var html = WebUtility.HtmlDecode(await Render(scope.ServiceProvider, model));
        Assert.Contains("No tienes permiso para consultar esta elegibilidad", html);
        Assert.Contains("No tienes permiso para consultar carga activa", html);
        Assert.DoesNotContain("PrepareCorrection", html);
        Assert.DoesNotContain("No hay personas visibles", html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corto")]
    public async Task InvalidReasonNeverWrites(string reason)
    {
        using var scope = factory.Services.CreateScope();
        var api = new Api();
        var model = Model(scope.ServiceProvider, api, await Form(scope.ServiceProvider, PrepareFields(reason)));
        await model.OnPostPrepareCorrectionAsync(CancellationToken.None);
        Assert.Equal(400, model.Response.StatusCode);
        Assert.Null(model.CorrectionIntentToken);
        Assert.NotNull(model.CorrectionFieldError);
        Assert.All(api.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparationDoesNotAdoptANewerEtagOrEvaluation(bool staleEvaluation)
    {
        using var scope = factory.Services.CreateScope();
        var api = new Api();
        var fields = PrepareFields("Motivo sintético de corrección");
        fields[staleEvaluation ? "evaluationId" : "etag"] = staleEvaluation ? Guid.NewGuid().ToString("D") : "\"6\"";
        var model = Model(scope.ServiceProvider, api, await Form(scope.ServiceProvider, fields));
        await model.OnPostPrepareCorrectionAsync(CancellationToken.None);
        Assert.Equal(412, model.Response.StatusCode);
        Assert.True(model.CorrectionConflict);
        Assert.Null(model.CorrectionIntentToken);
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task PreparationAndRecoveryPreserveExactBodyKeyAndOriginalVersion()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var api = new Api { UncertainFirstWrite = true };
        var prepared = Model(services, api, await Form(services, PrepareFields("  Motivo   sintético de corrección  ")));
        await prepared.OnPostPrepareCorrectionAsync(CancellationToken.None);
        Assert.True(prepared.OpenCorrectionConfirmation);
        Assert.NotNull(prepared.CorrectionIntentToken);
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
        var confirmation = await Render(services, prepared);
        Assert.Contains("<dialog", confirmation);
        Assert.Contains("data-initial-focus", confirmation);
        await Preview("confirmation", confirmation);
        var fields = new Dictionary<string, string> { ["correctionIntent"] = prepared.CorrectionIntentToken! };
        var first = Model(services, api, await Form(services, fields));
        await first.OnPostCorrectAssignmentAsync(CancellationToken.None);
        Assert.True(first.CorrectionUncertain);
        Assert.Null(first.CorrectionResult);
        Assert.Single(api.Requests, r => r.Method == HttpMethod.Post);
        var second = Model(services, api, await Form(services, fields));
        await second.OnPostCorrectAssignmentAsync(CancellationToken.None);
        var writes = api.Requests.Where(r => r.Method == HttpMethod.Post).ToArray();
        Assert.Equal(2, writes.Length);
        Assert.Equal(writes[0].Intent!.Key, writes[1].Intent!.Key);
        Assert.Equal("\"7\"", writes[1].IfMatch);
        Assert.Equal(JsonSerializer.Serialize(writes[0].Body), JsonSerializer.Serialize(writes[1].Body));
        Assert.Equal("Motivo sintético de corrección", Assert.IsType<IndexModel.CorrectionBody>(writes[1].Body).Reason);
        Assert.NotNull(writes[1].CsrfToken);
        Assert.Equal("RECUPERADA", second.CorrectionResult?.Result);
        await Preview("recovered", await Render(services, second));
    }

    [Fact]
    public async Task ConflictNeverRetriesAndRendererBlocksTheOriginalIntent()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var api = new Api { Conflict = true };
        var model = Model(services, api, await Form(services, new() { ["correctionIntent"] = Intent(services) }));
        await model.OnPostCorrectAssignmentAsync(CancellationToken.None);
        Assert.Equal(412, model.Response.StatusCode);
        Assert.True(model.CorrectionConflict);
        Assert.Single(api.Requests, r => r.Method == HttpMethod.Post);
        Assert.False(model.CanPrepareCorrection);
        var html = await Render(services, model);
        Assert.Contains("Esta obligaci", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("name=\"correctionIntent\"", html);
        Assert.DoesNotContain("PrepareCorrection", html);
        Assert.Contains("Recargar asignaci", WebUtility.HtmlDecode(html));
        await Preview("conflict", html);
    }

    [Fact]
    public async Task CreatedResultSurvivesAFailedFollowingReadWithoutAnotherMutation()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var api = new Api { CreateWrite = true, FailReadAfterWrite = true };
        var model = Model(services, api, await Form(services, new() { ["correctionIntent"] = Intent(services) }));
        await model.OnPostCorrectAssignmentAsync(CancellationToken.None);
        Assert.Equal("CREADA", model.CorrectionResult?.Result);
        Assert.Null(model.AssignmentObligation);
        Assert.NotNull(model.AssignmentError);
        Assert.Single(api.Requests, r => r.Method == HttpMethod.Post);
        var html = WebUtility.HtmlDecode(await Render(services, model));
        Assert.Contains("Asignación corregida; la anterior permanece en historia", html);
        Assert.Contains("No se pudo completar la operación", html);
    }

    [Theory]
    [InlineData("019d9300-0000-7000-8000-000000000014")]
    [InlineData("019d9300-0000-7000-8000-000000000015")]
    public async Task CurrentOrExcludedPersonCannotPrepareAnIntent(string id)
    {
        using var scope = factory.Services.CreateScope();
        var api = new Api();
        var fields = PrepareFields("Motivo sintético de corrección");
        fields["newResponsiblePersonId"] = id;
        var model = Model(scope.ServiceProvider, api, await Form(scope.ServiceProvider, fields));
        await model.OnPostPrepareCorrectionAsync(CancellationToken.None);
        Assert.Equal(400, model.Response.StatusCode);
        Assert.Null(model.CorrectionIntentToken);
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task CursorNavigationIsBoundToTheResourceAndPreservesOpaqueServerCursor()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var api = new Api();
        var initial = Model(services, api, Context(services, $"?obligationId={Obligation:D}"));
        await Get(initial);
        var nextQuery = new Uri("https://synthetic.local" + initial.NextHistoryHref).Query;
        Assert.DoesNotContain("opaque-history", nextQuery);
        var next = Model(services, api, Context(services, nextQuery));
        await Get(next);
        Assert.Contains(api.Requests, r => r.Path == $"/api/v1/obligations/{Obligation:D}?historyCursor=opaque-history");
        Assert.Equal(initial.AssignmentHref(), next.PreviousHistoryHref);
        var calls = api.Requests.Count(r => r.Path.StartsWith("/api/v1/obligations", StringComparison.Ordinal));
        var changedQuery = nextQuery.Replace(Obligation.ToString("D"), Candidate.ToString("D"), StringComparison.Ordinal);
        var changed = Model(services, api, Context(services, changedQuery));
        await Get(changed);
        Assert.Equal(404, changed.Response.StatusCode);
        Assert.Equal(calls, api.Requests.Count(r => r.Path.StartsWith("/api/v1/obligations", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("actor")]
    [InlineData("tampered")]
    public async Task InvalidIntentCannotWriteOrRotateItsKey(string kind)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var api = new Api();
        var token = kind == "tampered" ? "invalid" : Intent(services, kind == "actor" ? Guid.NewGuid() : Actor,
            kind == "expired" ? DateTimeOffset.UtcNow.AddSeconds(-1) : DateTimeOffset.UtcNow.AddHours(1));
        var model = Model(services, api, await Form(services, new() { ["correctionIntent"] = token }));
        await model.OnPostCorrectAssignmentAsync(CancellationToken.None);
        Assert.Equal(400, model.Response.StatusCode);
        Assert.Null(model.CorrectionIntentToken);
        Assert.DoesNotContain(api.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task MissingCsrfAndForgedRoleHaveNoApiEffect()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var api = new Api();
        var noCsrf = Context(services);
        noCsrf.Request.Method = "POST"; noCsrf.Request.ContentType = "application/x-www-form-urlencoded";
        var model = Model(services, api, noCsrf);
        Assert.IsType<BadRequestResult>(await model.OnPostCorrectAssignmentAsync(CancellationToken.None));
        Assert.Empty(api.Requests);
        var denied = Model(services, api, await Form(services, new() { ["correctionIntent"] = Intent(services) }), permissions: []);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await denied.OnPostCorrectAssignmentAsync(CancellationToken.None)).StatusCode);
        Assert.Empty(api.Requests);
    }

    private static Dictionary<string, string> PrepareFields(string reason) => new()
    { ["obligationId"] = Obligation.ToString("D"), ["evaluationId"] = Evaluation.ToString("D"), ["etag"] = "\"7\"", ["newResponsiblePersonId"] = Candidate.ToString("D"), ["reason"] = reason };
    private static string Intent(IServiceProvider services, Guid? actor = null, DateTimeOffset? expires = null) =>
        services.GetRequiredService<IDataProtectionProvider>().CreateProtector("SGOL.FRONT-014.correction-intention.v1").Protect(
            JsonSerializer.Serialize(new
            {
                Actor = actor ?? Actor,
                ObligationId = Obligation,
                Key = Guid.CreateVersion7(),
                ExpiresAt = expires ?? DateTimeOffset.UtcNow.AddHours(1),
                Etag = "\"7\"",
                Body = new IndexModel.CorrectionBody(Candidate, Evaluation, "Motivo sintético de corrección")
            }));
    private static DefaultHttpContext Context(IServiceProvider services, string query = "")
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https"; context.Request.Path = "/planificacion";
        context.Request.QueryString = new QueryString(query);
        return context;
    }
    private static IndexModel Model(IServiceProvider services, Api api, HttpContext context, string[]? permissions = null)
    {
        var session = new Session(permissions ?? Permissions);
        var antiforgery = services.GetRequiredService<IAntiforgery>();
        return new(session, api, new RazorAntiforgeryBridge(antiforgery, api, session),
            services.GetRequiredService<IDataProtectionProvider>(), antiforgery)
        { PageContext = new PageContext { HttpContext = context } };
    }
    private static Task<IActionResult> Get(IndexModel model) => model.OnGetAsync("2026", "40", "2026-09-28", "2026-10-04", null, null, null, null, CancellationToken.None);
    private static async Task<DefaultHttpContext> Form(IServiceProvider services, Dictionary<string, string> fields)
    {
        var issuance = Context(services);
        var tokens = services.GetRequiredService<IAntiforgery>().GetAndStoreTokens(issuance);
        var context = Context(services);
        context.Request.Method = "POST"; context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Headers.Cookie = issuance.Response.Headers.SetCookie.ToString().Split(';')[0];
        fields["__RequestVerificationToken"] = tokens.RequestToken!;
        using var content = new FormUrlEncodedContent(fields);
        var bytes = await content.ReadAsByteArrayAsync();
        context.Request.ContentLength = bytes.Length; context.Request.Body = new MemoryStream(bytes);
        return context;
    }
    private static async Task<string> Render(IServiceProvider services, IndexModel model)
    {
        var context = model.HttpContext;
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "synthetic-render"));
        var route = new RouteData(); route.Values["page"] = "/Planning/Index";
        var action = new ActionContext(context, route, new ActionDescriptor());
        var engine = services.GetRequiredService<IRazorViewEngine>();
        var view = engine.GetView(null, "/Pages/Planning/_Assignments.cshtml", false);
        Assert.True(view.Success);
        var data = new ViewDataDictionary<IndexModel>(services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
        var temp = new TempDataDictionary(context, services.GetRequiredService<ITempDataProvider>());
        await using var writer = new StringWriter(CultureInfo.InvariantCulture);
        await view.View!.RenderAsync(new ViewContext(action, view.View, data, temp, writer, new HtmlHelperOptions()));
        return writer.ToString();
    }
    private static async Task Preview(string name, string html)
    {
        var output = Environment.GetEnvironmentVariable("SGOL_FRONT014_PREVIEW_DIR");
        if (string.IsNullOrEmpty(output)) return;
        // Optional review artifacts contain synthetic data only and no CSRF/intention values.
        html = Regex.Replace(html, "(<input[^>]*type=\"hidden\"[^>]*value=\")[^\"]*", "$1", RegexOptions.CultureInvariant);
        var root = FindRoot();
        var css = await File.ReadAllTextAsync(Path.Combine(root, "src/Sgol.Web/wwwroot/css/tokens.css")) +
            await File.ReadAllTextAsync(Path.Combine(root, "src/Sgol.Web/wwwroot/css/components.css"));
        var script = await File.ReadAllTextAsync(Path.Combine(root, "src/Sgol.Web/wwwroot/js/components.js")) +
            await File.ReadAllTextAsync(Path.Combine(root, "src/Sgol.Web/wwwroot/js/assignments.js"));
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, name + ".html"),
            "<!doctype html><html lang=\"es-MX\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>FRONT-014 — revisión sintética</title><style>" + css +
            "</style><body><main class=\"contenido-principal\"><h1>Asignaciones — revisión sintética FRONT-014</h1><p>Vista Razor real con respuestas sintéticas; no acredita integración ni TLS.</p>" + html + "</main><script>" + script + "</script></body></html>");
    }
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SGOL.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository not found.");
    }
    private sealed class Session(string[] permissions) : IRazorSessionState
    {
        public bool IsInvalid => false;
        public void Invalidate() { }
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(new(
            Actor, Actor, "synthetic", "Sintética", "LOR-001", "ADMINISTRACION", permissions,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow.AddHours(8)));
    }
    private sealed class Api : ISgolApiClient
    {
        public List<ApiRequest> Requests { get; } = [];
        public bool EmptyLoads { get; init; }
        public bool DenyEligibility { get; init; }
        public bool DenyLoads { get; init; }
        public bool UncertainFirstWrite { get; init; }
        public bool Conflict { get; init; }
        public bool CreateWrite { get; init; }
        public bool FailReadAfterWrite { get; init; }
        public Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.Method == HttpMethod.Post)
            {
                if (Conflict) return Task.FromResult(Error<T>(412, "VERSION_CONFLICT"));
                if (UncertainFirstWrite && Requests.Count(r => r.Method == HttpMethod.Post) == 1) throw new ApiProtocolException();
                var value = new AssignmentCorrectionResult(CreateWrite ? "CREADA" : "RECUPERADA", Candidate, Obligation, Previous, Candidate, "VIGENTE", "CORRECCION", "Motivo sintético de corrección", Actor, Now, Previous, 8);
                return Task.FromResult(new ApiResponse<T>(CreateWrite ? 201 : 200, (T)(object)value, null, Actor.ToString("D"), null, null, "\"8\"", !CreateWrite, null, null));
            }
            if (typeof(T) == typeof(EligibilityEvaluationDetails))
            {
                if (DenyEligibility) return Task.FromResult(Error<T>(403, "ACCESO_DENEGADO"));
                var excluded = Guid.Parse("019d9300-0000-7000-8000-000000000015");
                var candidates = new[] { Explanation(Previous, "CO-001", true, []), Explanation(Candidate, "CO-002", true, []),
                    Explanation(excluded, "CO-003", false, ["DISPONIBILIDAD_NO_POSITIVA", "TURNO_NO_COINCIDE"]) };
                return Task.FromResult(Ok<T>((T)(object)new EligibilityEvaluationDetails(Evaluation, Evaluation, Obligation, Now, new(2026, 9, 29), "MANUAL_REQUEST", Evaluation, "SUBCOORDINACION", null, "CANDIDATOS_ELEGIBLES", null, candidates, false)));
            }
            if (typeof(T) == typeof(ObligationDetail))
            {
                if (FailReadAfterWrite && Requests.Any(r => r.Method == HttpMethod.Post)) throw new ApiProtocolException();
                return Task.FromResult(Ok<T>((T)(object)Detail(), etag: "\"7\"", history: "opaque-history"));
            }
            if (typeof(T) == typeof(ActiveLoadItem))
            {
                if (DenyLoads) return Task.FromResult(Error<T>(403, "ACCESO_DENEGADO"));
                IReadOnlyList<T> items = EmptyLoads ? [] : [(T)(object)new ActiveLoadItem(new(Previous, "CO-001", "<script>Sintética</script>"), 3, Now), (T)(object)new ActiveLoadItem(new(Candidate, "CO-002", "Persona sintética B"), 0, Now)];
                return Task.FromResult(new ApiResponse<T>(200, default, items, Actor.ToString("D"), null, items.Count, null, false, null, null));
            }
            return Task.FromResult(Ok<T>(default));
        }
        private static ApiResponse<T> Ok<T>(T? data, string? etag = null, string? history = null) => new(200, data, [], Actor.ToString("D"), null, 0, etag, false, null, null, HistoryNextCursor: history);
        private static ApiResponse<T> Error<T>(int status, string code) => new(status, default, null, Actor.ToString("D"), null, null, null, false, code, ProblemDetailsPresenter.Present(status, code, Actor.ToString("D")));
        private static EligibilityCandidateExplanation Explanation(Guid id, string code, bool eligible, string[] reasons) => new(id, code, eligible, reasons, null, "ACTIVA", null, "Puesto sintético", "Día", null, "SUBCOORDINACION", null, eligible, null, null, null);
        private static ObligationDetail Detail()
        {
            var person = new ObligationPerson(Previous, "CO-001", "Persona sintética A");
            return new(Obligation, new(Evaluation, "TAR-0018", "Cambio de exhibición", new(Evaluation, 1, "VIGENTE", Now, null, 1)),
                new("MANUAL", "MANUAL_REFERENCE_V1", "SYN", Evaluation, Evaluation, Now), new(Evaluation, 2026, 40, new(2026, 9, 28), new(2026, 10, 4), "America/Mexico_City"),
                new(null, null, null), "PENDIENTE", "NO_VENCIDA", new(Previous, person, "AUTOMATICA", Now), new Dictionary<string, string>(), new(Evaluation, "ACEPTADA", Now, Actor),
                [new(Previous, "ASIGNACION_AUTOMATICA", Now, "SYSTEM", null, null, new(Previous, "VIGENTE", "AUTOMATICA", person, null), null)]);
        }
    }
}
