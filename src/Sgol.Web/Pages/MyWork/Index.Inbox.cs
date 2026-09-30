using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Execution.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Components;
using Sgol.Web.Presentation.MyWork;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Pages.MyWork;

public sealed partial class IndexModel
{
    public MyWorkQuery Query { get; private set; } = new();
    public InboxData? Inbox { get; private set; }
    public IReadOnlyList<ObligationListItem> Obligations { get; private set; } = [];
    public ProblemDetailsPresentation? InboxError { get; private set; }
    public ProblemDetailsPresentation? QueryError { get; private set; }
    public ProblemDetailsPresentation? NoticeError { get; private set; }
    public CursorPaginationViewModel? TaskPagination { get; private set; }
    public CursorPaginationViewModel? NoticePagination { get; private set; }
    public CursorPaginationViewModel? QueryPagination { get; private set; }
    public bool CanViewInbox { get; private set; }
    public bool CanViewTasks { get; private set; }
    public string? NoticeSuccess { get; private set; }
    public Guid? ReadNoticeId { get; private set; }
    public DateTimeOffset? ReadNoticeAt { get; private set; }
    public string? FocusId { get; private set; }
    public string NoticePostHref => QueryHelpers.AddQueryString(Query.Href("avisos"), "handler", "ReadNotice");
    private string? returnToken;
    public string DetailHref(Guid id) => QueryHelpers.AddQueryString("/mi-trabajo/tareas/" + id.ToString("D"),
        new Dictionary<string, string?> { ["returnToken"] = returnToken });

    private bool SetQuery(bool post = false)
    {
        var raw = new QueryCollection(Request.Query.Where(p => p.Key != "handler" || !post)
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        if (!MyWorkQuery.TryRead(raw, out var query))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            ErrorTitle = "Revisa los filtros de esta consulta";
            ErrorMessage = "Limpia los filtros y vuelve a consultar.";
            return false;
        }
        Query = query;
        return true;
    }
    private async Task<IActionResult> LoadWorkAsync(SessionSnapshot session, CancellationToken token, bool post = false)
    {
        if (!SetQuery(post)) return Page();
        CanViewInbox = session.Permissions.Contains(InboxAuthorization.ViewOwn);
        CanViewTasks = session.Permissions.Contains(ObligationQueryAuthorization.View);
        returnToken = new MyWorkReturnContext(protection, session.UserId).Protect(Query);
        if (CanViewInbox) await LoadInboxAsync(session.UserId, token);
        if (sessionState.IsInvalid) return Ended();
        if (CanViewTasks) await LoadObligationsAsync(session.UserId, token);
        if (sessionState.IsInvalid) return Ended();
        if (NoticeSuccess is not null)
            FocusId = Inbox?.Notices.Items.Any(n => n.NoticeId == ReadNoticeId) == true
                ? "notice-state-" + ReadNoticeId!.Value.ToString("N") : "avisos-title";
        return Page();
    }
    private RedirectResult Ended()
    {
        Inbox = null; Obligations = []; NoticeSuccess = null; ReadNoticeAt = null;
        ApiCookieBridge.Clear(HttpContext); sessionState.Invalidate();
        return Access(AccessNoticeKind.Ended);
    }
    private async Task LoadInboxAsync(Guid actor, CancellationToken token)
    {
        try
        {
            var tasks = new MyWorkCursor(protection, actor, "tasks", Query.Filter("tasks"));
            var notices = new MyWorkCursor(protection, actor, "notices", Query.Filter("notices"));
            if (!tasks.Read(Query["taskCursor"]) || !notices.Read(Query["noticeCursor"]))
            { InboxError = MyWorkPresentation.Message(400, "FILTRO_BANDEJA_INVALIDO", null, true); return; }
            var path = QueryHelpers.AddQueryString("/api/v1/me/inbox", new Dictionary<string, string?>
            {
                ["periodId"] = Query["periodId"],
                ["taskState"] = Query["taskState"],
                ["noticeStatus"] = Query["noticeStatus"],
                ["taskCursor"] = tasks.Cursor,
                ["noticeCursor"] = notices.Cursor
            });
            var response = await apiClient.SendAsync<InboxData>(new(HttpMethod.Get, path, ApiResponseShape.Item), token);
            if (response.Status == 401) { sessionState.Invalidate(); return; }
            if (!response.IsSuccess) { InboxError = MyWorkPresentation.Message(response, true); return; }
            if (response.Data is null) throw new ApiProtocolException();
            MyWorkPresentation.Validate(response.Data);
            TaskPagination = tasks.Links(response.Data.Tasks.NextCursor, cursor => Query.Href("mis-tareas", ("taskCursor", cursor)));
            NoticePagination = notices.Links(response.Data.Notices.NextCursor, cursor => Query.Href("avisos", ("noticeCursor", cursor)));
            Inbox = response.Data;
        }
        catch (ApiProtocolException) { Inbox = null; InboxError = MyWorkPresentation.Message(503, null, null, true); }
    }
    private async Task LoadObligationsAsync(Guid actor, CancellationToken token)
    {
        try
        {
            var cursor = new MyWorkCursor(protection, actor, "obligations", Query.Filter("obligations"));
            if (!cursor.Read(Query["cursor"]))
            { QueryError = MyWorkPresentation.Message(400, "FILTRO_OBLIGACIONES_INVALIDO", null, false); return; }
            var path = QueryHelpers.AddQueryString("/api/v1/obligations", new Dictionary<string, string?>
            {
                ["periodId"] = Query["queryPeriodId"],
                ["taskCode"] = Query["taskCode"],
                ["executionStatus"] = Query["executionStatus"],
                ["condition"] = Query["condition"],
                ["responsiblePersonId"] = Query["responsiblePersonId"],
                ["cursor"] = cursor.Cursor
            });
            var response = await apiClient.SendAsync<ObligationListItem>(new(HttpMethod.Get, path, ApiResponseShape.Collection), token);
            if (response.Status == 401) { sessionState.Invalidate(); return; }
            if (!response.IsSuccess) { QueryError = MyWorkPresentation.Message(response, false); return; }
            if (response.Items is null) throw new ApiProtocolException();
            foreach (var item in response.Items) MyWorkPresentation.Validate(item);
            QueryPagination = cursor.Links(response.NextCursor, value => Query.Href("consulta-tareas", ("cursor", value)));
            Obligations = response.Items;
        }
        catch (ApiProtocolException) { Obligations = []; QueryError = MyWorkPresentation.Message(503, null, null, false); }
    }
    public async Task<IActionResult> OnPostReadNoticeAsync(CancellationToken cancellationToken)
    {
        NoStore();
        var session = await sessionState.GetAsync(cancellationToken);
        if (session is null) return sessionState.IsInvalid ? Ended() : Redirect("/acceso");
        if (!session.Permissions.Contains(InboxAuthorization.ViewOwn))
        { Response.StatusCode = 403; NoticeError = MyWorkPresentation.Message(403, "ACCESO_DENEGADO", null, true); return Page(); }
        if (!SetQuery(true)) return Page();
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(cancellationToken);
        if (form.Keys.Any(k => k is not ("noticeId" or "__RequestVerificationToken")) ||
            form["noticeId"].Count != 1 || !MyWorkQuery.CanonicalId(form["noticeId"], out var id))
        {
            Response.StatusCode = 400; NoticeError = MyWorkPresentation.Message(400, "AVISO_ID_INVALIDO", null, true);
            return await LoadWorkAsync(session, cancellationToken, true);
        }
        try
        {
            var response = await antiforgery.SendValidatedAsync<ReadInternalNoticeResult>(HttpContext,
                new(HttpMethod.Post, $"/api/v1/me/notices/{id:D}/read", ApiResponseShape.Item), cancellationToken);
            if (response is null)
            { Response.StatusCode = 400; NoticeError = MyWorkPresentation.Message(400, "CSRF_INVALIDO", null, true); }
            else if (response.Status == 401) return Ended();
            else if (!response.IsSuccess)
            { Response.StatusCode = response.Status; NoticeError = MyWorkPresentation.Message(response, true); }
            else if (response.Data is { Status: "READ", Result: "MARKED_READ" or "ALREADY_READ" } result && result.NoticeId == id)
            {
                NoticeSuccess = result.Result == "MARKED_READ" ? "Aviso marcado como leído. La tarea no cambió" : "Este aviso ya estaba leído. La tarea no cambió";
                ReadNoticeId = id; ReadNoticeAt = result.ReadAt;
            }
            else throw new ApiProtocolException();
        }
        catch (ApiProtocolException)
        {
            Response.StatusCode = 503;
            NoticeError = MyWorkPresentation.Message(409, "LECTURA_AVISO_CONCURRENCIA_CONFLICTO", null, true);
        }
        // Read changes notice ordering; never carry its old cursor into the next GET.
        Query.Values.Remove("noticeCursor");
        // LoadWork reads query again, so remove the stale notice cursor in its source too.
        var values = Request.Query.Where(p => p.Key != "noticeCursor").ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        Request.Query = new QueryCollection(values);
        var page = await LoadWorkAsync(session, cancellationToken, true);
        if (NoticeError is not null && Response.StatusCode == 403)
        {
            Inbox = null; TaskPagination = null; NoticePagination = null;
            InboxError = NoticeError;
            NoticeError = null;
        }
        return page;
    }
}
