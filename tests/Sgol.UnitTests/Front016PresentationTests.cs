using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Sgol.Execution.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Web.Pages.MyWork;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;
using Xunit;

namespace Sgol.UnitTests;

public sealed class Front016PresentationTests
{
    [Theory]
    [InlineData("DIRECCION")]
    [InlineData("ADMINISTRACION")]
    [InlineData("SUBCOORDINACION")]
    [InlineData("PISO_VENTAS")]
    public async Task FourRolesUseOwnInboxAndAuthorizedQueryWithoutAnyMutation(string role)
    {
        using var fixture = new Fixture(role);
        var model = fixture.Index();
        Assert.IsType<PageResult>(await model.OnGetAsync(default));
        Assert.True(model.CanViewInbox); Assert.True(model.CanViewTasks);
        Assert.Equal(2, fixture.Api.Requests.Count);
        Assert.All(fixture.Api.Requests, r => { Assert.Equal(HttpMethod.Get, r.Method); Assert.Null(r.Body); Assert.Null(r.Intent); Assert.Null(r.IfMatch); Assert.Null(r.CsrfToken); });
        Assert.Equal("/api/v1/me/inbox", fixture.Api.Requests[0].Path);
        Assert.Equal("/api/v1/obligations", fixture.Api.Requests[1].Path);
        Assert.True(model.Inbox!.IsEmpty); Assert.Empty(model.Obligations);
    }

    [Theory]
    [InlineData("taskState=FUTURA&noticeStatus=UNREAD", true)]
    [InlineData("taskState=&taskCode=&condition=", true)]
    [InlineData("taskState=FUTURA&taskState=VENCIDA", false)]
    [InlineData("taskState=inventado", false)]
    [InlineData("taskCode=TAR-9999", false)]
    [InlineData("periodId=123", false)]
    [InlineData("cursor=", false)]
    [InlineData("handler=ReadNotice", false)]
    [InlineData("returnUrl=https://example.org", false)]
    public async Task FiltersAreClosedAndInvalidQueriesNeverCallApi(string query, bool valid)
    {
        using var fixture = new Fixture();
        var model = fixture.Index(query);
        await model.OnGetAsync(default);
        Assert.Equal(valid ? 2 : 0, fixture.Api.Requests.Count);
        Assert.Equal(valid ? 200 : 400, model.Response.StatusCode);
        if (valid && query.Contains("taskState=&", StringComparison.Ordinal))
            Assert.DoesNotContain("taskState=", fixture.Api.Requests[0].Path);
    }

    [Fact]
    public async Task PeriodFiltersUseKnownIdAndNeverMaterializeWeek()
    {
        using var fixture = new Fixture();
        var id = Guid.CreateVersion7();
        var model = fixture.Index($"periodId={id:D}&queryPeriodId={id:D}&taskState=FUTURA");
        await model.OnGetAsync(default);
        Assert.Contains($"periodId={id:D}", fixture.Api.Requests[0].Path);
        Assert.Contains("taskState=FUTURA", fixture.Api.Requests[0].Path);
        Assert.Contains($"periodId={id:D}", fixture.Api.Requests[1].Path);
        Assert.DoesNotContain(fixture.Api.Requests, r => r.Path.Contains("/weeks", StringComparison.Ordinal) || r.Method != HttpMethod.Get);
    }

    [Theory]
    [InlineData("MARKED_READ")]
    [InlineData("ALREADY_READ")]
    public async Task MarkReadForwardsOnlyCsrfAndConfirmsOriginalTimeWithoutTaskCommand(string result)
    {
        using var fixture = new Fixture();
        var id = Guid.CreateVersion7();
        var readAt = new DateTimeOffset(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);
        fixture.Api.ReadResult = new(id, "READ", readAt, result);
        var model = fixture.Post(id.ToString("D"));
        Assert.IsType<PageResult>(await model.OnPostReadNoticeAsync(default));
        var request = Assert.Single(fixture.Api.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal($"/api/v1/me/notices/{id:D}/read", request.Path);
        Assert.Null(request.Body); Assert.Null(request.Intent); Assert.Null(request.IfMatch);
        Assert.False(string.IsNullOrEmpty(request.CsrfToken));
        Assert.Equal(readAt, model.ReadNoticeAt); Assert.Contains("La tarea no cambió", model.NoticeSuccess);
        Assert.Equal("avisos-title", model.FocusId);
        Assert.DoesNotContain(fixture.Api.Requests, r => r.Path.Contains("conclusion", StringComparison.Ordinal) || r.Path.Contains("evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidCsrfNeverMarksOrConfirmsRead()
    {
        using var fixture = new Fixture();
        var model = fixture.Post(Guid.CreateVersion7().ToString("D"), validCsrf: false);
        await model.OnPostReadNoticeAsync(default);
        Assert.DoesNotContain(fixture.Api.Requests, r => r.Method == HttpMethod.Post);
        Assert.Null(model.NoticeSuccess); Assert.Null(model.ReadNoticeAt);
        Assert.Equal(400, model.Response.StatusCode);
        Assert.Equal("No se pudo verificar la solicitud", model.NoticeError!.Title);
    }

    [Theory]
    [InlineData(404, "AVISO_NO_ENCONTRADO")]
    [InlineData(409, "LECTURA_AVISO_CONCURRENCIA_CONFLICTO")]
    [InlineData(403, "ACCESO_DENEGADO")]
    [InlineData(503, "SYNTHETIC")]
    public async Task RejectedMarkDoesNotAnnounceReadOrRetry(int status, string code)
    {
        using var fixture = new Fixture();
        fixture.Api.MutationFailure = (status, code);
        var model = fixture.Post(Guid.CreateVersion7().ToString("D"));
        await model.OnPostReadNoticeAsync(default);
        Assert.Single(fixture.Api.Requests, r => r.Method == HttpMethod.Post);
        Assert.Null(model.NoticeSuccess);
        var error = status == 403 ? model.InboxError : model.NoticeError;
        Assert.NotNull(error);
        Assert.DoesNotContain("synthetic private detail", error.Message);
        if (status == 403) Assert.Null(model.Inbox);
    }

    [Fact]
    public async Task MissingPermissionDoesNotCallBackendAndGetCannotMarkNotice()
    {
        using var fixture = new Fixture();
        fixture.Session.Permissions = [];
        var model = fixture.Index(); await model.OnGetAsync(default);
        Assert.Empty(fixture.Api.Requests); Assert.False(model.CanViewInbox); Assert.False(model.CanViewTasks);
        var post = fixture.Post(Guid.CreateVersion7().ToString("D")); await post.OnPostReadNoticeAsync(default);
        Assert.Equal(403, post.Response.StatusCode); Assert.Empty(fixture.Api.Requests);
    }

    [Fact]
    public async Task UnauthorizedDetailConvergesAndReturnsNoResourceOrHistory()
    {
        using var fixture = new Fixture();
        fixture.Api.DetailFailure = (404, "OBLIGACION_NO_ENCONTRADA");
        var model = fixture.Details();
        await model.OnGetAsync(Guid.CreateVersion7().ToString("D"), default);
        Assert.Null(model.Detail); Assert.Equal(404, model.Response.StatusCode);
        Assert.Equal("No existe o no está disponible en tu alcance", model.Error!.Title);
        Assert.Single(fixture.Api.Requests); Assert.Equal(HttpMethod.Get, fixture.Api.Requests[0].Method);
    }

    [Fact]
    public async Task InconsistentInboxNeverPublishesPartialRows()
    {
        using var fixture = new Fixture();
        fixture.Api.Inbox = new(null, new([], null, 1, false), new([], null, 0, true), false);
        var model = fixture.Index(); await model.OnGetAsync(default);
        Assert.Null(model.Inbox); Assert.NotNull(model.InboxError);
    }

    [Theory]
    [InlineData("/api/v1/me/inbox")]
    [InlineData("/api/v1/obligations")]
    public async Task ExpiredReadClearsFunctionalDataAndEndsSession(string path)
    {
        using var fixture = new Fixture();
        fixture.Api.ReadFailurePath = path;
        var model = fixture.Index();
        var redirect = Assert.IsType<RedirectResult>(await model.OnGetAsync(default));
        Assert.StartsWith("/acceso?notice=", redirect.Url);
        Assert.True(fixture.Session.IsInvalid);
        Assert.Null(model.Inbox); Assert.Empty(model.Obligations);
        Assert.Null(model.NoticeSuccess);
    }

    [Theory]
    [InlineData("FUTURA")]
    [InlineData("DISPONIBLE")]
    [InlineData("VENCIDA")]
    [InlineData("CONCLUIDA")]
    public void TaskStatesPreserveReceivedDatesAndRequireEvidenceCatalogue(string state)
    {
        var task = new InboxTask(Guid.CreateVersion7(), new(Guid.CreateVersion7(), "TAR-0008", "Tarea sintética"),
            new(Guid.CreateVersion7(), 2026, 40, new(2026, 9, 28), new(2026, 10, 4), "America/Mexico_City"),
            "MANUAL", false, new(null, null, null), state == "CONCLUIDA" ? "CONCLUIDA" : "PENDIENTE",
            state, new("INCOMPLETA", []), ["VIEW_TASK"]);
        var data = new InboxData(task.Period, new([task], null, 1, false), new([], null, 0, true), false);
        MyWorkPresentation.Validate(data);
        Assert.Equal("Sin fecha registrada", MyWorkPresentation.Time(task.Dates.DueAt));
        Assert.Throws<ApiProtocolException>(() => MyWorkPresentation.Validate(data with
        { Tasks = data.Tasks with { Items = [task with { Evidence = new("FUTURA", []) }] } }));
    }

    [Theory]
    [InlineData("FUTURA", "Futura", "info")]
    [InlineData("DISPONIBLE", "Disponible", "info")]
    [InlineData("VENCIDA", "Vencida", "peligro")]
    [InlineData("CONCLUIDA", "Concluida", "exito")]
    [InlineData("UNREAD", "Sin leer", "info")]
    [InlineData("READ", "Leído", "neutro")]
    public void InformativeStatesUseApprovedTextAndIcon(string state, string text, string style)
    {
        var badge = MyWorkPresentation.Badge(state);
        Assert.Equal(text, badge.Text); Assert.Equal(style, badge.Style); Assert.NotEmpty(badge.Icon);
    }

    [Fact]
    public void UnknownStatesAndUnavailableNoticeWithTaskDataFailClosed()
    {
        Assert.Throws<ApiProtocolException>(() => MyWorkPresentation.Badge("VALIDADA"));
        Assert.Throws<ApiProtocolException>(() => MyWorkPresentation.HistoryName("EVIDENCIA_CARGADA"));
        var notice = new InboxNotice(Guid.CreateVersion7(), "OBLIGATION_ASSIGNED", "UNREAD", DateTimeOffset.UtcNow, null,
            new("ASSIGNMENT_VERSION", Guid.CreateVersion7(), false, Guid.CreateVersion7(), "TAR-0007", "Private"), ["MARK_NOTICE_READ"]);
        Assert.Throws<ApiProtocolException>(() => MyWorkPresentation.Validate(new InboxData(null, new([], null, 0, true), new([notice], null, 1, false), false)));
    }

    [Fact]
    public void CursorsBindActorSectionAndFiltersAndKeepPreviousNavigation()
    {
        using var fixture = new Fixture();
        var first = new MyWorkCursor(fixture.Protection, fixture.Session.Id, "tasks", "futura");
        var links = first.Links("opaque-api-cursor", value => value ?? "first");
        var second = new MyWorkCursor(fixture.Protection, fixture.Session.Id, "tasks", "futura");
        Assert.True(second.Read(links.NextHref)); Assert.Equal("opaque-api-cursor", second.Cursor);
        Assert.Equal("first", second.Links(null, v => v ?? "first").PreviousHref);
        Assert.False(new MyWorkCursor(fixture.Protection, Guid.CreateVersion7(), "tasks", "futura").Read(links.NextHref));
        Assert.False(new MyWorkCursor(fixture.Protection, fixture.Session.Id, "notices", "futura").Read(links.NextHref));
        Assert.False(new MyWorkCursor(fixture.Protection, fixture.Session.Id, "tasks", "vencida").Read(links.NextHref));
    }

    [Fact]
    public void ReturnContextPreservesOnlyLocalFiltersAndBindsActor()
    {
        using var fixture = new Fixture();
        Assert.True(MyWorkQuery.TryRead(new QueryCollection(new Dictionary<string, StringValues> { ["taskCode"] = "TAR-0007" }), out var query));
        var context = new MyWorkReturnContext(fixture.Protection, fixture.Session.Id);
        var token = context.Protect(query);
        Assert.Contains("taskCode=TAR-0007", context.Read(token));
        Assert.Equal("/mi-trabajo", context.Read("https://example.org"));
        Assert.Equal("/mi-trabajo", new MyWorkReturnContext(fixture.Protection, Guid.CreateVersion7()).Read(token));
    }

    private sealed class Session(string role) : IRazorSessionState
    {
        public Guid Id { get; } = Guid.CreateVersion7();
        public IReadOnlyList<string> Permissions { get; set; } = ["PER-BANDEJA-PROPIA", "PER-TAREA-VER"];
        public bool IsInvalid { get; private set; }
        public void Invalidate() => IsInvalid = true;
        public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<SessionSnapshot?>(IsInvalid ? null :
            new(Id, Guid.CreateVersion7(), "synthetic", "Persona sintética", "LOR-001", role, Permissions, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow.AddHours(8)));
    }
    private sealed class Api : ISgolApiClient
    {
        public List<ApiRequest> Requests { get; } = [];
        public InboxData Inbox { get; set; } = new(null, new([], null, 0, true), new([], null, 0, true), true);
        public ReadInternalNoticeResult? ReadResult { get; set; }
        public (int Status, string Code)? MutationFailure { get; set; }
        public (int Status, string Code)? DetailFailure { get; set; }
        public string? ReadFailurePath { get; set; }
        public Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var failure = request.Path == ReadFailurePath ? (401, "SESION_TERMINADA") : request.Method == HttpMethod.Post ? MutationFailure : request.Path.StartsWith("/api/v1/obligations/", StringComparison.Ordinal) ? DetailFailure : null;
            var data = request.Method == HttpMethod.Post ? (object?)ReadResult : request.Path.StartsWith("/api/v1/me/inbox", StringComparison.Ordinal) ? Inbox : null;
            return Task.FromResult(new ApiResponse<T>(failure?.Status ?? 200, data is T value ? value : default,
                request.Shape == ApiResponseShape.Collection ? [] : null, Guid.CreateVersion7().ToString("D"), null,
                request.Shape == ApiResponseShape.Collection ? 0 : null, null, false, failure?.Code,
                failure is null ? null : new ProblemDetailsPresentation("synthetic private title", "synthetic private detail", null)));
        }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider services;
        private readonly RazorAntiforgeryBridge bridge;
        private readonly AccessNotice notice;
        public Session Session { get; }
        public Api Api { get; } = new();
        public IDataProtectionProvider Protection { get; }
        public Fixture(string role = "DIRECCION")
        {
            Session = new(role);
            var collection = new ServiceCollection(); collection.AddLogging(); collection.AddDataProtection();
            collection.AddAntiforgery(options => { options.Cookie.Name = "__Host-SGOL-CSRF"; options.Cookie.SecurePolicy = CookieSecurePolicy.Always; });
            services = collection.BuildServiceProvider(); Protection = services.GetRequiredService<IDataProtectionProvider>();
            bridge = new(services.GetRequiredService<IAntiforgery>(), Api, Session); notice = new(Protection);
        }
        private DefaultHttpContext Context(string query = "")
        {
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Scheme = "https"; context.Request.Method = "GET";
            context.Request.QueryString = new(query.Length == 0 ? "" : "?" + query);
            return context;
        }
        public IndexModel Index(string query = "") => new(Session, bridge, notice, Api, Protection) { PageContext = new PageContext { HttpContext = Context(query) } };
        public DetailsModel Details() => new(Session, Api, Protection, notice) { PageContext = new PageContext { HttpContext = Context() } };
        public IndexModel Post(string id, bool validCsrf = true)
        {
            var get = Context(); var token = bridge.Issue(get);
            var post = Context("handler=ReadNotice"); post.Request.Method = "POST";
            post.Request.Headers.Cookie = get.Response.Headers.SetCookie.ToString().Split(';')[0];
            post.Request.ContentType = "application/x-www-form-urlencoded";
            var body = Encoding.UTF8.GetBytes("noticeId=" + Uri.EscapeDataString(id) + "&__RequestVerificationToken=" + Uri.EscapeDataString(validCsrf ? token : ""));
            post.Request.ContentLength = body.Length; post.Request.Body = new MemoryStream(body);
            return new(Session, bridge, notice, Api, Protection) { PageContext = new PageContext { HttpContext = post } };
        }
        public void Dispose() => services.Dispose();
    }
}
